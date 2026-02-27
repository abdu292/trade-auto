using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface ITradingViewSignalStore
{
    void Upsert(TradingViewSignalContract signal);
    bool TryGetLatest(out TradingViewSignalContract? signal);
}
