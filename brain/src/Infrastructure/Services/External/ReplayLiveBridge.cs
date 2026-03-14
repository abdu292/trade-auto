using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using System.Collections.Concurrent;

namespace Brain.Infrastructure.Services.External;

public sealed class ReplayLiveBridge : IReplayLiveBridge
{
    private readonly ConcurrentQueue<(MarketSnapshotContract Snapshot, TaskCompletionSource<bool> Tcs)> _queue = new();
    private decimal? _replayDeployableCashAed;
    private bool? _replayUseLiveNewsAndTelegram;

    public decimal? ReplayDeployableCashAed { get => _replayDeployableCashAed; set => _replayDeployableCashAed = value; }
    public bool? ReplayUseLiveNewsAndTelegram { get => _replayUseLiveNewsAndTelegram; set => _replayUseLiveNewsAndTelegram = value; }

    public Task<bool> SubmitReplaySnapshotAndWaitAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((snapshot, tcs));

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    public (bool HasReplay, MarketSnapshotContract? Snapshot, Action<bool>? SignalCycleProcessed) TryDequeueReplaySnapshot()
    {
        if (!_queue.TryDequeue(out var pair))
        {
            return (false, null, null);
        }

        var (snapshot, tcs) = pair;
        void Signal(bool tradeArmed)
        {
            try
            {
                tcs.TrySetResult(tradeArmed);
            }
            catch
            {
                // ignore
            }
        }

        return (true, snapshot, Signal);
    }
}
