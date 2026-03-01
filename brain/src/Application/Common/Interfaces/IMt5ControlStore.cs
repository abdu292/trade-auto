namespace Brain.Application.Common.Interfaces;

public interface IMt5ControlStore
{
    void RequestCancelPending(string reason);
    bool TryConsumeCancelPending(out string reason);
}