using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using System.Collections.Concurrent;

namespace Brain.Infrastructure.Services.External;

public sealed class ReplayLiveBridge : IReplayLiveBridge
{
    private readonly ConcurrentQueue<(MarketSnapshotContract Snapshot, TaskCompletionSource Tcs)> _queue = new();

    public Task SubmitReplaySnapshotAndWaitAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((snapshot, tcs));

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    public (bool HasReplay, MarketSnapshotContract? Snapshot, Action? SignalCycleProcessed) TryDequeueReplaySnapshot()
    {
        if (!_queue.TryDequeue(out var pair))
        {
            return (false, null, null);
        }

        var (snapshot, tcs) = pair;
        void Signal()
        {
            try
            {
                tcs.TrySetResult();
            }
            catch
            {
                // ignore
            }
        }

        return (true, snapshot, Signal);
    }
}
