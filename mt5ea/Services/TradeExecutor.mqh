#ifndef __TRADE_EXECUTOR_MQH__
#define __TRADE_EXECUTOR_MQH__

#include <Trade/Trade.mqh>
#include "../Models/TradeCommand.mqh"

class TradeExecutor
{
private:
    CTrade m_trade;

public:
    double VolumeFromGrams(double grams)
    {
        double lots = grams / 100.0;
        if (lots < 0.01)
            lots = 0.01;
        if (lots > 5.0)
            lots = 5.0;

        return NormalizeDouble(lots, 2);
    }

    bool Execute(const TradeCommand &command, ulong &orderTicket)
    {
        double lots = VolumeFromGrams(command.grams);

        if (command.type == "BUY_LIMIT")
        {
            bool ok = m_trade.BuyLimit(lots, command.price, _Symbol, 0, command.tp, ORDER_TIME_SPECIFIED, command.expiry);
            orderTicket = ok ? m_trade.ResultOrder() : 0;
            return ok;
        }

        if (command.type == "BUY_STOP")
        {
            bool ok = m_trade.BuyStop(lots, command.price, _Symbol, 0, command.tp, ORDER_TIME_SPECIFIED, command.expiry);
            orderTicket = ok ? m_trade.ResultOrder() : 0;
            return ok;
        }

        orderTicket = 0;
        return false;
    }
};

#endif
