using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IMarketSimulationService
{
    MarketSimulationStatusContract GetStatus();
    void Start(MarketSimulationStartContract start);
    void Stop();
    void StepOnce();
}
