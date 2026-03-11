using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryChartDataStore : IChartDataStore
{
    private readonly Lock _gate = new();
    private List<CandlePoint> _m15Candles = [];

    public void SetM15Candles(IReadOnlyCollection<CandlePoint> candles)
    {
        lock (_gate)
        {
            _m15Candles = candles?.ToList() ?? [];
        }
    }

    public IReadOnlyList<CandlePoint> GetM15Candles()
    {
        lock (_gate)
        {
            return _m15Candles.ToList();
        }
    }
}
