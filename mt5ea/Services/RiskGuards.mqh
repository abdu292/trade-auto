#ifndef __RISK_GUARDS_MQH__
#define __RISK_GUARDS_MQH__

#include "../Models/TradeCommand.mqh"

class RiskGuards
{
public:
    bool Validate(const TradeCommand &command)
    {
        if (command.price <= 0 || command.tp <= 0)
            return false;

        if (command.ml <= 0)
            return false;

        if (command.grams < 100.0)
            return false;

        if (!(command.type == "BUY_LIMIT" || command.type == "BUY_STOP"))
            return false;

        if (command.type == "BUY_STOP" && command.waterfallRisk == "HIGH")
            return false;

        if (command.engineState == "CAPITAL_PROTECTED")
            return false;

        return true;
    }
};

#endif
