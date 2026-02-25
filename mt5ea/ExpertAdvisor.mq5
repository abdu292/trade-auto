#property strict

#include "Http/ApiClient.mqh"
#include "Services/TradeExecutor.mqh"
#include "Services/RiskGuards.mqh"
#include "Models/TradeCommand.mqh"

input string BrainBaseUrl = "http://127.0.0.1:5000";
input string BrainApiKey = "dev-local-change-me";

ApiClient g_api;
TradeExecutor g_executor;
RiskGuards g_riskGuards;

int OnInit()
{
    g_api.Configure(BrainBaseUrl, BrainApiKey);
    Print("Trade Auto EA initialized - Reacting to price ticks");
    return(INIT_SUCCEEDED);
}

void OnTick()
{
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

    bool placed = g_executor.Execute(command);
    string status = placed ? "EXECUTED" : "FAILED";
    Print("Trade execution: ", status);
    g_api.PostTradeStatus(command.id, status);
}
