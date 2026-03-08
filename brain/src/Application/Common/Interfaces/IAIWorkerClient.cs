using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IAIWorkerClient
{
    Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, string? cycleId, CancellationToken cancellationToken);
    Task<ModeSignalContract?> GetModeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Calls the aiworker /study-analyze endpoint to perform an autonomous study/self-crosscheck
    /// refinement cycle. Uses ALL configured analyzers (not just the lead) for a deeper review of
    /// recent waterfall failures and blocked trade candidates.
    /// Returns null if the study call fails or times out (non-fatal).
    /// </summary>
    Task<StudyRefinementSuggestionContract?> StudyAnalyzeAsync(
        MarketSnapshotContract snapshot,
        StudyContextContract context,
        CancellationToken cancellationToken);
}
