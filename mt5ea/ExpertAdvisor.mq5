#property strict

#include "Http/ApiClient.mqh"
#include "Services/TradeExecutor.mqh"
#include "Services/RiskGuards.mqh"
#include "Models/TradeCommand.mqh"

input string BrainBaseUrl = "http://127.0.0.1:5000";
input string BrainApiKey = "dev-local-change-me";
input int SnapshotPushSeconds = 5;
input int PollTradeSeconds = 2;

ApiClient g_api;
TradeExecutor g_executor;
RiskGuards g_riskGuards;

struct TrackedTrade
{
    string tradeId;
    ulong orderTicket;
    ulong positionTicket;
    double grams;
    bool closed;
};

TrackedTrade g_trades[];

int FindTrackedByOrder(ulong orderTicket)
{
    for (int i = 0; i < ArraySize(g_trades); i++)
    {
        if (g_trades[i].orderTicket == orderTicket)
            return i;
    }

    return -1;
}

int FindTrackedByPosition(ulong positionTicket)
{
    for (int i = 0; i < ArraySize(g_trades); i++)
    {
        if (g_trades[i].positionTicket == positionTicket)
            return i;
    }

    return -1;
}

void AddTrackedTrade(string tradeId, ulong orderTicket, double grams)
{
    TrackedTrade item;
    item.tradeId = tradeId;
    item.orderTicket = orderTicket;
    item.positionTicket = 0;
    item.grams = grams;
    item.closed = false;

    int size = ArraySize(g_trades);
    ArrayResize(g_trades, size + 1);
    g_trades[size] = item;
}

void CancelAllPendingOrders()
{
    CTrade trade;
    int total = OrdersTotal();
    int canceled = 0;

    for (int i = total - 1; i >= 0; i--)
    {
        ulong ticket = OrderGetTicket(i);
        if (ticket == 0)
            continue;

        if (!OrderSelect(ticket))
            continue;

        string symbol = OrderGetString(ORDER_SYMBOL);
        long orderType = OrderGetInteger(ORDER_TYPE);
        if (symbol != _Symbol)
            continue;

        if (orderType == ORDER_TYPE_BUY_LIMIT || orderType == ORDER_TYPE_BUY_STOP)
        {
            if (trade.OrderDelete(ticket))
                canceled++;
        }
    }

    if (canceled > 0)
        Print("Kill-switch canceled pending orders: ", canceled);
}

int OnInit()
{
    g_api.Configure(BrainBaseUrl, BrainApiKey);
    Print("Trade Auto EA initialized - Reacting to price ticks");
    return(INIT_SUCCEEDED);
}

void OnTick()
{
    if (StringFind(_Symbol, "XAUUSD", 0) != 0)
    {
        return;
    }

    static datetime lastSnapshotPush = 0;
    static datetime lastTradePoll = 0;

    datetime now = TimeCurrent();

    if (now - lastSnapshotPush >= SnapshotPushSeconds)
    {
        g_api.PostMarketSnapshot(_Symbol);
        lastSnapshotPush = now;
    }

    if (now - lastTradePoll < PollTradeSeconds)
    {
        return;
    }

    lastTradePoll = now;

    bool cancelPending = false;
    if (g_api.ConsumeCancelPendingSignal(cancelPending) && cancelPending)
    {
        CancelAllPendingOrders();
    }

    TradeCommand command;
    bool hasCommand = g_api.GetPendingTrade(command);

    if (!hasCommand)
    {
        return;
    }

    Print("Received trade command: ", command.type, " at ", command.price);

    if (!g_riskGuards.Validate(command))
    {
        Print("Trade rejected by risk guards");
        g_api.PostTradeStatus(command.id, "REJECTED_RISK_GUARD");
        return;
    }

    ulong orderTicket = 0;
    bool placed = g_executor.Execute(command, orderTicket);
    string status = placed ? "EXECUTED" : "FAILED";
    Print("Trade execution: ", status);
    if (placed)
    {
        AddTrackedTrade(command.id, orderTicket, command.grams);
        g_api.PostTradeStatus(command.id, "ORDER_PLACED", command.price, command.grams, orderTicket, TimeCurrent());
    }
    else
    {
        g_api.PostTradeStatus(command.id, "FAILED", command.price, command.grams, orderTicket, TimeCurrent());
    }
}

void OnTradeTransaction(const MqlTradeTransaction &trans, const MqlTradeRequest &request, const MqlTradeResult &result)
{
    if (StringFind(_Symbol, "XAUUSD", 0) != 0)
    {
        return;
    }

    if (trans.type != TRADE_TRANSACTION_DEAL_ADD)
    {
        return;
    }

    ulong dealTicket = trans.deal;
    if (dealTicket == 0)
    {
        return;
    }

    long dealEntry = (long)HistoryDealGetInteger(dealTicket, DEAL_ENTRY);
    long dealReason = (long)HistoryDealGetInteger(dealTicket, DEAL_REASON);
    ulong orderTicket = (ulong)HistoryDealGetInteger(dealTicket, DEAL_ORDER);
    ulong positionTicket = (ulong)HistoryDealGetInteger(dealTicket, DEAL_POSITION_ID);
    double dealPrice = HistoryDealGetDouble(dealTicket, DEAL_PRICE);

    if (dealEntry == DEAL_ENTRY_IN)
    {
        int orderIndex = FindTrackedByOrder(orderTicket);
        if (orderIndex >= 0)
        {
            g_trades[orderIndex].positionTicket = positionTicket;
            g_api.PostTradeStatus(
                g_trades[orderIndex].tradeId,
                "BUY_TRIGGERED",
                dealPrice,
                g_trades[orderIndex].grams,
                orderTicket,
                TimeCurrent());
        }
    }

    if (dealEntry == DEAL_ENTRY_OUT && dealReason == DEAL_REASON_TP)
    {
        int positionIndex = FindTrackedByPosition(positionTicket);
        if (positionIndex >= 0 && !g_trades[positionIndex].closed)
        {
            g_api.PostTradeStatus(
                g_trades[positionIndex].tradeId,
                "TP_HIT",
                dealPrice,
                g_trades[positionIndex].grams,
                orderTicket,
                TimeCurrent());
            g_trades[positionIndex].closed = true;
        }
    }
}
