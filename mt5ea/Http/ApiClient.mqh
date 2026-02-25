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
        StringReplace(m_baseUrl, "http://localhost", "http://127.0.0.1");
        StringReplace(m_baseUrl, "https://localhost", "https://127.0.0.1");
        m_apiKey = apiKey;
        Print("ApiClient configured. BaseUrl=", m_baseUrl, ", ApiKeyLength=", StringLen(m_apiKey));
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

        ResetLastError();
        int code = WebRequest("GET", url, headers, 5000, postData, result, responseHeaders);
        string response = CharArrayToString(result);

        if (code != 200)
        {
            int lastError = GetLastError();
            Print("GetPendingTrade failed. HTTP=", code,
                  ", LastError=", lastError,
                  ", Url=", url,
                  ", ApiKeyLength=", StringLen(m_apiKey),
                  ", Response=", response,
                  ", ResponseHeaders=", responseHeaders);
            return false;
        }

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
        StringToCharArray(payload, postData, 0, StringLen(payload));
        char result[];
        string responseHeaders;

        ResetLastError();
        int code = WebRequest("POST", url, headers, 5000, postData, result, responseHeaders);
        if (!(code >= 200 && code < 300))
        {
            int lastError = GetLastError();
            string response = CharArrayToString(result);
            Print("PostTradeStatus failed. HTTP=", code,
                  ", LastError=", lastError,
                  ", Url=", url,
                  ", TradeId=", tradeId,
                  ", Status=", status,
                  ", Response=", response,
                  ", ResponseHeaders=", responseHeaders);
        }
        return code >= 200 && code < 300;
    }
};

#endif
