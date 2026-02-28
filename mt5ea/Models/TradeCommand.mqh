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
    double grams;
    double alignmentScore;
    string regime;
    string riskTag;
    string engineState;
    string mode;
    string cause;
    string waterfallRisk;
    string bucket;
    string session;
    string sizeClass;
    string telegramState;
};

#endif
