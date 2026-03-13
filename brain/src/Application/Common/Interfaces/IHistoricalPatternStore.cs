using Brain.Application.Common.Models;
using Brain.Application.Common.Services;

namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Store for historical pattern matches (10+ year memory).
/// </summary>
public interface IHistoricalPatternStore
{
    /// <summary>
    /// Finds historical pattern matches for the given market conditions.
    /// </summary>
    Task<IReadOnlyCollection<HistoricalPatternMatch>> FindMatchesAsync(
        MarketSnapshotContract snapshot,
        string session,
        string phase,
        DayOfWeek dayOfWeek,
        CancellationToken cancellationToken = default);
}