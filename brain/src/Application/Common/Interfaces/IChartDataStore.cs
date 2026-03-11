using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IChartDataStore
{
    void SetM15Candles(IReadOnlyCollection<CandlePoint> candles);
    IReadOnlyList<CandlePoint> GetM15Candles();
}
