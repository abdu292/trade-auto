using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryMt5ControlStore : IMt5ControlStore
{
    private readonly Lock _gate = new();
    private bool _cancelPendingRequested;
    private string _reason = string.Empty;

    public void RequestCancelPending(string reason)
    {
        lock (_gate)
        {
            _cancelPendingRequested = true;
            _reason = string.IsNullOrWhiteSpace(reason) ? "kill_switch" : reason.Trim();
        }
    }

    public bool TryConsumeCancelPending(out string reason)
    {
        lock (_gate)
        {
            if (!_cancelPendingRequested)
            {
                reason = string.Empty;
                return false;
            }

            _cancelPendingRequested = false;
            reason = _reason;
            _reason = string.Empty;
            return true;
        }
    }
}