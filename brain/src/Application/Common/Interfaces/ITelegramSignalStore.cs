using Brain.Application.Common.Services;

namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Store for Telegram signals (advisory only, never direct execution triggers).
/// </summary>
public interface ITelegramSignalStore
{
    /// <summary>
    /// Gets recent Telegram signals within the specified time window.
    /// </summary>
    Task<IReadOnlyCollection<TelegramSignalContract>> GetRecentSignalsAsync(
        TimeSpan window,
        CancellationToken cancellationToken = default);
}