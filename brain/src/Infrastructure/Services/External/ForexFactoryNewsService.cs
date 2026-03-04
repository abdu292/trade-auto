using System.Globalization;
using System.Xml.Linq;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class ForexFactoryNewsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ForexFactoryNewsService> logger) : IEconomicNewsService
{
    private static readonly Lock CacheGate = new();
    private static List<NewsEventContract> _cachedEvents = [];
    private static DateTimeOffset _lastRefreshUtc = DateTimeOffset.MinValue;
    private static bool _lastRefreshFailed;

    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    public async Task<NewsRiskAssessmentContract> AssessAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await EnsureFreshAsync(cancellationToken);

        List<NewsEventContract> snapshot;
        DateTimeOffset refreshedAt;
        bool stale;
        lock (CacheGate)
        {
            snapshot = [.. _cachedEvents];
            refreshedAt = _lastRefreshUtc;
            stale = _lastRefreshFailed;
        }

        if (snapshot.Count == 0)
        {
            return new NewsRiskAssessmentContract(
                IsBlocked: false,
                Reason: stale ? "News feed unavailable; fail-open to avoid false hard blocks." : "No nearby high-impact USD events.",
                NearbyEvents: [],
                RefreshedAtUtc: refreshedAt,
                IsStale: stale);
        }

        var preMinutes = Math.Clamp(configuration.GetValue("EconomicNews:PreRiskMinutes", 45), 1, 240);
        var postMinutes = Math.Clamp(configuration.GetValue("EconomicNews:PostRiskMinutes", 30), 0, 240);

        var windowStart = nowUtc.AddMinutes(-postMinutes);
        var windowEnd = nowUtc.AddMinutes(preMinutes);

        var nearby = snapshot
            .Where(x => x.EventTimeUtc >= windowStart && x.EventTimeUtc <= windowEnd)
            .OrderBy(x => x.EventTimeUtc)
            .ToList();

        if (nearby.Count == 0)
        {
            return new NewsRiskAssessmentContract(
                IsBlocked: false,
                Reason: "No nearby high-impact USD events.",
                NearbyEvents: [],
                RefreshedAtUtc: refreshedAt,
                IsStale: stale);
        }

        var nextEvent = nearby[0];
        var minutes = Math.Round((nextEvent.EventTimeUtc - nowUtc).TotalMinutes, 1);
        var reason = minutes >= 0
            ? $"High-impact USD event within +{preMinutes}m window: {nextEvent.Title} in {minutes}m."
            : $"High-impact USD event occurred within -{postMinutes}m window: {nextEvent.Title} ({Math.Abs(minutes)}m ago).";

        return new NewsRiskAssessmentContract(
            IsBlocked: true,
            Reason: reason,
            NearbyEvents: nearby,
            RefreshedAtUtc: refreshedAt,
            IsStale: stale);
    }

    private async Task EnsureFreshAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset lastRefresh;
        lock (CacheGate)
        {
            lastRefresh = _lastRefreshUtc;
        }

        if (DateTimeOffset.UtcNow - lastRefresh < _refreshInterval)
        {
            return;
        }

        var feedUrl = (configuration["EconomicNews:ForexFactoryXmlUrl"] ?? "https://nfs.faireconomy.media/ff_calendar_thisweek.xml").Trim();
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var xml = await client.GetStringAsync(feedUrl, cancellationToken);
            var events = ParseHighImpactUsd(xml);

            lock (CacheGate)
            {
                _cachedEvents = events;
                _lastRefreshUtc = DateTimeOffset.UtcNow;
                _lastRefreshFailed = false;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Economic news refresh failed from ForexFactory XML feed.");
            lock (CacheGate)
            {
                _lastRefreshUtc = DateTimeOffset.UtcNow;
                _lastRefreshFailed = true;
            }
        }
    }

    private static List<NewsEventContract> ParseHighImpactUsd(string xml)
    {
        var output = new List<NewsEventContract>();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return output;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch
        {
            return output;
        }

        var events = doc.Descendants("event");
        foreach (var evt in events)
        {
            var currency = (evt.Element("currency")?.Value ?? evt.Element("country")?.Value ?? string.Empty).Trim().ToUpperInvariant();
            if (!currency.Contains("USD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var impactRaw = (evt.Element("impact")?.Value ?? evt.Element("impact_title")?.Value ?? string.Empty).Trim();
            if (!IsHighImpact(impactRaw))
            {
                continue;
            }

            var title = (evt.Element("title")?.Value ?? "USD High Impact Event").Trim();
            var datePart = (evt.Element("date")?.Value ?? string.Empty).Trim();
            var timePart = (evt.Element("time")?.Value ?? string.Empty).Trim();

            if (!TryParseEventTimeUtc(datePart, timePart, out var eventTimeUtc))
            {
                continue;
            }

            output.Add(new NewsEventContract(
                Title: title,
                Currency: "USD",
                Impact: "HIGH",
                EventTimeUtc: eventTimeUtc));
        }

        return output.OrderBy(x => x.EventTimeUtc).ToList();
    }

    private static bool IsHighImpact(string impact)
    {
        if (string.IsNullOrWhiteSpace(impact))
        {
            return false;
        }

        return impact.Contains("high", StringComparison.OrdinalIgnoreCase)
            || impact.Contains("red", StringComparison.OrdinalIgnoreCase)
            || impact.Contains("3", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseEventTimeUtc(string datePart, string timePart, out DateTimeOffset eventTimeUtc)
    {
        eventTimeUtc = default;
        var composed = string.IsNullOrWhiteSpace(timePart)
            ? datePart
            : $"{datePart} {timePart}";

        var formats = new[]
        {
            "MM-dd-yyyy hh:mmtt",
            "MM-dd-yyyy h:mmtt",
            "MM/dd/yyyy hh:mmtt",
            "MM/dd/yyyy h:mmtt",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "dddMMM d h:mmtt",
            "ddd MMM d h:mmtt",
            "MMM d h:mmtt",
            "MMMM d h:mmtt"
        };

        if (DateTimeOffset.TryParseExact(
                composed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out eventTimeUtc))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
            composed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out eventTimeUtc);
    }
}
