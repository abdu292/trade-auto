using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface ITradeApprovalStore
{
    void Enqueue(PendingTradeContract trade);
    IReadOnlyCollection<PendingTradeContract> GetPending(int take = 20);
    bool TryApprove(Guid tradeId, out PendingTradeContract? trade);
    bool Reject(Guid tradeId);
}
