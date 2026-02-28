using System.Text;
using System.Text.Json;
using Brain.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class TelegramNotificationService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<TelegramNotificationService> logger,
    INotificationFeedStore feedStore) : INotificationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _botToken = ResolveBotToken(configuration);
    private readonly string _baseUrl = (configuration["External:Telegram:BaseUrl"] ?? "https://api.telegram.org").TrimEnd('/');
    private readonly string[] _targets = ResolveTargets(configuration);

    public async Task NotifyAsync(string title, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken) || _targets.Length == 0)
        {
            logger.LogWarning("Telegram notification skipped because token or targets are not configured.");
            feedStore.Add("TELEGRAM_DISABLED", title, message);
            return;
        }

        var payloadText = BuildText(title, message);
        var delivered = 0;

        foreach (var target in _targets)
        {
            var requestBody = new
            {
                chat_id = target,
                text = payloadText,
                disable_web_page_preview = true,
            };

            var endpoint = $"{_baseUrl}/bot{_botToken}/sendMessage";
            using var request = new StringContent(JsonSerializer.Serialize(requestBody, SerializerOptions), Encoding.UTF8, "application/json");

            try
            {
                using var response = await httpClient.PostAsync(endpoint, request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    delivered++;
                    continue;
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Telegram notification failed for target {Target}. Status={StatusCode}. Body={Body}", target, (int)response.StatusCode, responseText);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram notification failed for target {Target}", target);
            }
        }

        if (delivered > 0)
        {
            feedStore.Add("TELEGRAM", title, message);
            return;
        }

        feedStore.Add("TELEGRAM_ERROR", title, message);
    }

    private static string BuildText(string title, string message)
    {
        return string.IsNullOrWhiteSpace(title)
            ? message
            : $"{title}\n{message}";
    }

    private static string ResolveBotToken(IConfiguration configuration)
    {
        return (configuration["External:Telegram:BotToken"] ?? configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty).Trim();
    }

    private static string[] ResolveTargets(IConfiguration configuration)
    {
        var raw =
            configuration["External:Telegram:NotifyChannels"]
            ?? configuration["TELEGRAM_NOTIFY_CHANNELS"]
            ?? string.Empty;

        var values = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTarget)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values;
    }

    private static string NormalizeTarget(string value)
    {
        var raw = value.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("@", StringComparison.Ordinal))
        {
            return raw;
        }

        if (raw.StartsWith("-100", StringComparison.Ordinal) || raw.StartsWith("-", StringComparison.Ordinal) || raw.All(char.IsDigit))
        {
            return raw;
        }

        return $"@{raw}";
    }
}