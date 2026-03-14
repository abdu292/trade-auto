using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Bridge for running replay snapshots through the same pipeline as live (SignalPollingBackgroundService).
/// When replay uses "live pipeline", it submits each candle snapshot here; the polling service dequeues
/// and runs the full state machine with execution blocked.
/// </summary>
public interface IReplayLiveBridge
{
    /// <summary>
    /// Replay service enqueues a snapshot and waits until the live pipeline has processed one cycle for it.
    /// Returns true if the cycle resulted in a trade armed (decision.IsTradeAllowed); false otherwise.
    /// </summary>
    Task<bool> SubmitReplaySnapshotAndWaitAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Polling service: try to take the next replay snapshot. Returns (true, snapshot, signal) when one is available.
    /// Call signal(tradeArmed) when the cycle has finished; tradeArmed = true when decision.IsTradeAllowed.
    /// </summary>
    (bool HasReplay, MarketSnapshotContract? Snapshot, Action<bool>? SignalCycleProcessed) TryDequeueReplaySnapshot();

    /// <summary>
    /// Replay service sets this at run start so the polling service uses it for capacity (replay virtual ledger).
    /// When null, polling uses live ledger. Clear when replay run ends.
    /// </summary>
    decimal? ReplayDeployableCashAed { get; set; }

    /// <summary>
    /// When true, replay uses live Telegram/news in the AI worker (for testing). Default false = neutral context.
    /// Set at replay start, clear when replay ends.
    /// </summary>
    bool? ReplayUseLiveNewsAndTelegram { get; set; }
}
