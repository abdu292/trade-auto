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

        if (command.grams <= 0)
            return false;

        if (!(command.type == "BUY_LIMIT" || command.type == "BUY_STOP"))
            return false;

        return true;
    }
};

#endif
