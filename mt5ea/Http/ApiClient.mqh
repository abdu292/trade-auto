#ifndef __API_CLIENT_MQH__
#define __API_CLIENT_MQH__

#include "../Models/TradeCommand.mqh"

class ApiClient
{
private:
    string m_baseUrl;
    string m_apiKey;

public:
    void Configure(string baseUrl, string apiKey = "")
    {
        m_baseUrl = baseUrl;
        m_apiKey = apiKey;
    }

    string BuildHeaders()
    {
        string headers = "Content-Type: application/json\r\n";
        if (StringLen(m_apiKey) > 0)
            headers += "X-API-Key: " + m_apiKey + "\r\n";

        return headers;
    }

    bool GetPendingTrade(TradeCommand &command)
    {
        string url = m_baseUrl + "/mt5/pending-trades";
        string headers = BuildHeaders();
        char postData[];
        char result[];
        string responseHeaders;

        int code = WebRequest("GET", url, headers, 5000, postData, result, responseHeaders);
        if (code != 200)
            return false;

        string response = CharArrayToString(result);

        command.id = "mock-id";
        command.type = "BUY_LIMIT";
        command.price = SymbolInfoDouble(_Symbol, SYMBOL_BID) - 0.0010;
        command.tp = SymbolInfoDouble(_Symbol, SYMBOL_BID) + 0.0020;
        command.expiry = TimeCurrent() + 1200;
        command.ml = 3600;

        Print("Pending trade payload: ", response);
        return true;
    }

    bool PostTradeStatus(string tradeId, string status)
    {
        string url = m_baseUrl + "/mt5/trade-status";
        string headers = BuildHeaders();
        string payload = "{\"tradeId\":\"" + tradeId + "\",\"status\":\"" + status + "\"}";

        char postData[];
        StringToCharArray(payload, postData);
        char result[];
        string responseHeaders;

        int code = WebRequest("POST", url, headers, 5000, postData, result, responseHeaders);
        return code >= 200 && code < 300;
    }
};

#endif
