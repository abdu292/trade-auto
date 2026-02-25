#ifndef __TRADE_COMMAND_MQH__
#define __TRADE_COMMAND_MQH__

struct TradeCommand
{
    string id;
    string type;
    double price;
    double tp;
    datetime expiry;
    int ml;
};

#endif
