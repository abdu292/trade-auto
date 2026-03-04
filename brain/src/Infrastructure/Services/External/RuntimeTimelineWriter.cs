using System.Text.Json;
using Brain.Application.Common.Interfaces;
using Brain.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class RuntimeTimelineWriter(
    IApplicationDbContext db,
    ILogger<RuntimeTimelineWriter> logger) : IRuntimeTimelineWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private const int MaxPayloadChars = 24000;

    public async Task WriteAsync(
        string eventType,
        string stage,
        string source,
        string symbol,
        string? cycleId,
        string? tradeId,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            payloadJson = Redact(payloadJson);
            if (payloadJson.Length > MaxPayloadChars)
            {
                payloadJson = JsonSerializer.Serialize(new
                {
                    truncated = true,
                    preview = payloadJson[..MaxPayloadChars],
                }, JsonOptions);
            }

            db.RuntimeTimelineEvents.Add(RuntimeTimelineEvent.Create(
                eventType: eventType,
                stage: stage,
                source: source,
                symbol: symbol,
                cycleId: cycleId,
                tradeId: tradeId,
                payloadJson: payloadJson));

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist runtime timeline event {EventType}/{Stage}", eventType, stage);
        }
    }

    private static string Redact(string json)
    {
        return json
            .Replace("api_key", "redacted_key", StringComparison.OrdinalIgnoreCase)
            .Replace("authorization", "redacted_authorization", StringComparison.OrdinalIgnoreCase)
            .Replace("token", "redacted_token", StringComparison.OrdinalIgnoreCase)
            .Replace("session_string", "redacted_session", StringComparison.OrdinalIgnoreCase);
    }
}