using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradeApprovalStore : ITradeApprovalStore
{
    private readonly Lock _gate = new();
    private readonly List<PendingTradeContract> _pending = [];

    public void Enqueue(PendingTradeContract trade)
    {
        lock (_gate)
        {
            if (_pending.Any(x => x.Id == trade.Id))
            {
                return;
            }

            _pending.Add(trade);
        }
    }

    public IReadOnlyCollection<PendingTradeContract> GetPending(int take = 20)
    {
        lock (_gate)
        {
            var normalizedTake = Math.Clamp(take, 1, 200);
            return _pending
                .OrderBy(x => x.Expiry)
                .Take(normalizedTake)
                .ToArray();
        }
    }

    public bool TryApprove(Guid tradeId, out PendingTradeContract? trade)
    {
        lock (_gate)
        {
            var idx = _pending.FindIndex(x => x.Id == tradeId);
            if (idx < 0)
            {
                trade = null;
                return false;
            }

            trade = _pending[idx];
            _pending.RemoveAt(idx);
            return true;
        }
    }

    public bool Reject(Guid tradeId)
    {
        lock (_gate)
        {
            var idx = _pending.FindIndex(x => x.Id == tradeId);
            if (idx < 0)
            {
                return false;
            }

            _pending.RemoveAt(idx);
            return true;
        }
    }
}
