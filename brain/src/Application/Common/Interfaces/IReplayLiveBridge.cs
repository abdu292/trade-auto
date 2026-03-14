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
    /// </summary>
    Task SubmitReplaySnapshotAndWaitAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Polling service: try to take the next replay snapshot. Returns (true, snapshot, signal) when one is available.
    /// Call signal() when the cycle for this snapshot has finished (success or abort).
    /// </summary>
    (bool HasReplay, MarketSnapshotContract? Snapshot, Action? SignalCycleProcessed) TryDequeueReplaySnapshot();
}
