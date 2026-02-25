#ifndef __TRADE_EXECUTOR_MQH__
#define __TRADE_EXECUTOR_MQH__

#include <Trade/Trade.mqh>
#include "../Models/TradeCommand.mqh"

class TradeExecutor
{
private:
    CTrade m_trade;

public:
    bool Execute(const TradeCommand &command)
    {
        if (command.type == "BUY_LIMIT")
        {
            return m_trade.BuyLimit(0.10, command.price, _Symbol, 0, command.tp, ORDER_TIME_SPECIFIED, command.expiry);
        }

        if (command.type == "BUY_STOP")
        {
            return m_trade.BuyStop(0.10, command.price, _Symbol, 0, command.tp, ORDER_TIME_SPECIFIED, command.expiry);
        }

        return false;
    }
};

#endif
