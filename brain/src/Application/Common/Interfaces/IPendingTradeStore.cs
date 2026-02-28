using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IPendingTradeStore
{
    void Enqueue(PendingTradeContract trade);
    bool TryDequeue(out PendingTradeContract? trade);
    int Count();
    int Clear();
}
