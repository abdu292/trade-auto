#property strict

#include "Http/ApiClient.mqh"
#include "Services/TradeExecutor.mqh"
#include "Services/RiskGuards.mqh"
#include "Models/TradeCommand.mqh"

input string BrainBaseUrl = "http://127.0.0.1:5000";
input string BrainApiKey = "dev-local-change-me";
input int PollSeconds = 10;

ApiClient g_api;
TradeExecutor g_executor;
RiskGuards g_riskGuards;

int OnInit()
{
    g_api.Configure(BrainBaseUrl, BrainApiKey);
    EventSetTimer(PollSeconds);
    Print("Trade Auto EA initialized - Polling every ", PollSeconds, " seconds");
    return(INIT_SUCCEEDED);
}

void OnDeinit(const int reason)
{
    EventKillTimer();
}

void OnTimer()
{
    Print("Polling Brain API at ", BrainBaseUrl);
    
    TradeCommand command;
    bool hasCommand = g_api.GetPendingTrade(command);

    if (!hasCommand)
    {
        Print("No pending trades available");
        return;
    }

    Print("Received trade command: ", command.type, " at ", command.price);

    if (!g_riskGuards.Validate(command))
    {
        Print("Trade rejected by risk guards");
        g_api.PostTradeStatus(command.id, "REJECTED_RISK_GUARD");
        return;
    }

    bool placed = g_executor.Execute(command);
    string status = placed ? "EXECUTED" : "FAILED";
    Print("Trade execution: ", status);
    g_api.PostTradeStatus(command.id, status);
}
