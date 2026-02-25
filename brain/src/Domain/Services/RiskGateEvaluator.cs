using Brain.Domain.Entities;

namespace Brain.Domain.Services;

public static class RiskGateEvaluator
{
    public static bool CanOpenTrade(RiskProfile riskProfile, decimal currentDrawdownPercent)
    {
        if (!riskProfile.IsActive)
        {
            return false;
        }

        return currentDrawdownPercent <= riskProfile.MaxDrawdownPercent;
    }
}
