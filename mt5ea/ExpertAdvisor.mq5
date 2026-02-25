#property strict

#include "Http/ApiClient.mqh"
#include "Services/TradeExecutor.mqh"
#include "Services/RiskGuards.mqh"
#include "Models/TradeCommand.mqh"

input string BrainBaseUrl = "http://localhost:5000";
input string BrainApiKey = "dev-local-change-me";
input int PollSeconds = 10;

datetime g_lastPoll = 0;
ApiClient g_api;
TradeExecutor g_executor;
RiskGuards g_riskGuards;

int OnInit()
{
    g_api.Configure(BrainBaseUrl, BrainApiKey);
    Print("Trade Auto EA initialized");
    return(INIT_SUCCEEDED);
}

void OnTick()
{
    if ((TimeCurrent() - g_lastPoll) < PollSeconds)
        return;

    g_lastPoll = TimeCurrent();

    TradeCommand command;
    bool hasCommand = g_api.GetPendingTrade(command);

    if (!hasCommand)
        return;

    if (!g_riskGuards.Validate(command))
    {
        g_api.PostTradeStatus(command.id, "REJECTED_RISK_GUARD");
        return;
    }

    bool placed = g_executor.Execute(command);
    g_api.PostTradeStatus(command.id, placed ? "EXECUTED" : "FAILED");
}
