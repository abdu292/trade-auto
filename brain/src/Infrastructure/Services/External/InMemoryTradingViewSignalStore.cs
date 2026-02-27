using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradingViewSignalStore : ITradingViewSignalStore
{
    private readonly Lock _gate = new();
    private TradingViewSignalContract? _latest;

    public void Upsert(TradingViewSignalContract signal)
    {
        lock (_gate)
        {
            _latest = signal;
        }
    }

    public bool TryGetLatest(out TradingViewSignalContract? signal)
    {
        lock (_gate)
        {
            signal = _latest;
            return signal is not null;
        }
    }
}
