using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// In-memory implementation of ITelegramSignalStore for development/testing.
/// In production, this would query a database or external Telegram API.
/// </summary>
public sealed class InMemoryTelegramSignalStore(
#pragma warning disable CS9113
    ILogger<InMemoryTelegramSignalStore> logger) : ITelegramSignalStore
#pragma warning restore CS9113
{
    private static readonly List<TelegramSignalContract> _signals = [];
    private static readonly Lock _gate = new();

    public Task<IReadOnlyCollection<TelegramSignalContract>> GetRecentSignalsAsync(
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        
        lock (_gate)
        {
            var recent = _signals
                .Where(s => s.Timestamp >= cutoff)
                .OrderByDescending(s => s.Timestamp)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<TelegramSignalContract>>(recent);
        }
    }

    /// <summary>
    /// Adds a signal to the store (for testing/development).
    /// </summary>
    public static void AddSignal(TelegramSignalContract signal)
    {
        lock (_gate)
        {
            _signals.Add(signal);
            // Keep only last 100 signals
            if (_signals.Count > 100)
            {
                _signals.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Clears all signals (for testing/development).
    /// </summary>
    public static void Clear()
    {
        lock (_gate)
        {
            _signals.Clear();
        }
    }
}