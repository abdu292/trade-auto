#ifndef __API_CLIENT_MQH__
#define __API_CLIENT_MQH__

#include "../Models/TradeCommand.mqh"

class ApiClient
{
private:
    string m_baseUrl;
    string m_apiKey;

    string JsonGetString(string json, string key)
    {
        string pattern = "\"" + key + "\":\"";
        int start = StringFind(json, pattern);
        if (start < 0)
            return "";

        start += StringLen(pattern);
        int end = StringFind(json, "\"", start);
        if (end < 0)
            return "";

        return StringSubstr(json, start, end - start);
    }

    double JsonGetNumber(string json, string key)
    {
        string pattern = "\"" + key + "\":";
        int start = StringFind(json, pattern);
        if (start < 0)
            return 0.0;

        start += StringLen(pattern);
        int endComma = StringFind(json, ",", start);
        int endBrace = StringFind(json, "}", start);

        int end = endComma;
        if (end < 0 || (endBrace >= 0 && endBrace < end))
            end = endBrace;
        if (end < 0)
            end = StringLen(json);

        string raw = StringSubstr(json, start, end - start);
        return StringToDouble(raw);
    }

    datetime ParseIsoDateTime(string iso)
    {
        if (StringLen(iso) < 19)
            return TimeCurrent() + 1200;

        string core = StringSubstr(iso, 0, 19);
        StringReplace(core, "T", " ");
        StringReplace(core, "-", ".");
        datetime parsed = StringToTime(core);
        if (parsed <= 0)
            return TimeCurrent() + 1200;
        return parsed;
    }

    string ToIsoUtc(datetime value)
    {
        MqlDateTime dt;
        TimeToStruct(value, dt);
        return StringFormat("%04d-%02d-%02dT%02d:%02d:%02dZ", dt.year, dt.mon, dt.day, dt.hour, dt.min, dt.sec);
    }

    string JsonEscape(string value)
    {
        string out = value;
        StringReplace(out, "\\", "\\\\");
        StringReplace(out, "\"", "\\\"");
        return out;
    }

    string DetermineSession(datetime utcNow)
    {
        MqlDateTime dt;
        TimeToStruct(utcNow, dt);

        if (dt.hour >= 0 && dt.hour < 8)
            return "ASIA";
        if (dt.hour >= 8 && dt.hour < 16)
            return "EUROPE";
        if (dt.hour >= 16 && dt.hour < 22)
            return "LONDON";
        return "NEW_YORK";
    }

    double GetAdr(string symbol)
    {
        double sum = 0.0;
        int bars = 5;
        for (int i = 1; i <= bars; i++)
        {
            double h = iHigh(symbol, PERIOD_D1, i);
            double l = iLow(symbol, PERIOD_D1, i);
            if (h > 0 && l > 0)
                sum += (h - l);
        }
        if (sum <= 0)
            return SymbolInfoDouble(symbol, SYMBOL_POINT) * 100.0;
        return sum / bars;
    }

    double GetAtrApprox(string symbol)
    {
        double sum = 0.0;
        int bars = 14;
        for (int i = 1; i <= bars; i++)
        {
            double h = iHigh(symbol, PERIOD_H1, i);
            double l = iLow(symbol, PERIOD_H1, i);
            if (h > 0 && l > 0)
                sum += (h - l);
        }
        if (sum <= 0.0)
            return SymbolInfoDouble(symbol, SYMBOL_POINT) * 10.0;
        return sum / bars;
    }

    double GetMa20Approx(string symbol)
    {
        double sum = 0.0;
        int bars = 20;
        for (int i = 1; i <= bars; i++)
        {
            double c = iClose(symbol, PERIOD_H1, i);
            if (c > 0)
                sum += c;
        }
        if (sum <= 0.0)
            return SymbolInfoDouble(symbol, SYMBOL_BID);
        return sum / bars;
    }

public:
    void Configure(string baseUrl, string apiKey = "")
    {
        m_baseUrl = baseUrl;
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

        if (code == 204)
        {
            return false;
        }

        if (code != 200)
        {
            int lastError = GetLastError();
            Print("GetPendingTrade failed. HTTP=", code,
                  ", LastError=", lastError,
                  ", Url=", url,
                  ", ApiKeyLength=", StringLen(m_apiKey),
                  ", Response=", response,
                  ", ResponseHeaders=", responseHeaders);

            if (lastError == 4014 || lastError == 5203 || lastError == 4006)
            {
                Print("WebRequest troubleshooting: In MT5 Tools->Options->Expert Advisors, enable 'Allow WebRequest for listed URL' and add exact URL: ", m_baseUrl);
            }
            return false;
        }

        command.id = JsonGetString(response, "id");
        command.type = JsonGetString(response, "type");
        command.price = JsonGetNumber(response, "price");
        command.tp = JsonGetNumber(response, "tp");
        command.ml = (int)MathRound(JsonGetNumber(response, "ml"));
        command.grams = JsonGetNumber(response, "grams");
        command.alignmentScore = JsonGetNumber(response, "alignmentScore");
        command.regime = JsonGetString(response, "regime");
        command.riskTag = JsonGetString(response, "riskTag");
        command.engineState = JsonGetString(response, "engineState");
        command.mode = JsonGetString(response, "mode");
        command.cause = JsonGetString(response, "cause");
        command.waterfallRisk = JsonGetString(response, "waterfallRisk");
        command.bucket = JsonGetString(response, "bucket");
        command.session = JsonGetString(response, "session");
        command.sizeClass = JsonGetString(response, "sizeClass");
        command.telegramState = JsonGetString(response, "telegramState");

        string expiryRaw = JsonGetString(response, "expiry");
        command.expiry = ParseIsoDateTime(expiryRaw);

        if (StringLen(command.id) == 0 || StringLen(command.type) == 0 || command.price <= 0.0 || command.tp <= 0.0)
        {
            Print("GetPendingTrade parse failed. RawResponse=", response);
            return false;
        }

        if (command.grams <= 0)
            command.grams = 100.0;

        Print("Pending trade payload: ", response);
        return true;
    }

    bool PostTradeStatus(string tradeId, string status, double mt5Price = 0.0, double grams = 0.0, ulong ticket = 0, datetime mt5Time = 0)
    {
        string url = m_baseUrl + "/mt5/trade-status";
        string headers = BuildHeaders();
        if (mt5Time <= 0)
            mt5Time = TimeCurrent();

        string payload = StringFormat(
            "{\"tradeId\":\"%s\",\"status\":\"%s\",\"mt5Price\":%.2f,\"grams\":%.2f,\"ticket\":%I64u,\"mt5Time\":\"%s\"}",
            JsonEscape(tradeId),
            JsonEscape(status),
            mt5Price,
            grams,
            ticket,
            ToIsoUtc(mt5Time));

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

    bool PostMarketSnapshot(string symbol)
    {
        string url = m_baseUrl + "/mt5/market-snapshot";
        string headers = BuildHeaders();

        double atr = GetAtrApprox(symbol);
        double ma20 = GetMa20Approx(symbol);
        double adr = GetAdr(symbol);
        double volatilityExpansion = (adr > 0.0) ? (atr / adr) : 0.0;
        double bid = SymbolInfoDouble(symbol, SYMBOL_BID);
        double ask = SymbolInfoDouble(symbol, SYMBOL_ASK);
        double spread = (ask > 0.0 && bid > 0.0) ? (ask - bid) : 0.0;
        datetime utcNow = TimeGMT();
        datetime mt5ServerNow = TimeCurrent();
        MqlDateTime mt5Struct;
        TimeToStruct(mt5ServerNow, mt5Struct);
        bool isFriday = (mt5Struct.day_of_week == 5);
        bool isUsRiskWindow = (mt5Struct.hour >= 15 && mt5Struct.hour <= 19);

        string payload = StringFormat(
            "{\"symbol\":\"%s\",\"timeframeData\":[{\"timeframe\":\"M5\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f},{\"timeframe\":\"M15\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f},{\"timeframe\":\"M30\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f},{\"timeframe\":\"H1\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f},{\"timeframe\":\"H4\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f}],\"atr\":%.5f,\"adr\":%.5f,\"ma20\":%.5f,\"session\":\"%s\",\"timestamp\":\"%s\",\"volatilityExpansion\":%.5f,\"mt5ServerTime\":\"%s\",\"mt5ToKsaOffsetMinutes\":50,\"isUsRiskWindow\":%s,\"isFriday\":%s,\"bid\":%.5f,\"ask\":%.5f,\"spread\":%.5f,\"spreadMedian60m\":%.5f,\"spreadMax60m\":%.5f,\"compressionCountM15\":0,\"expansionCountM15\":0,\"impulseStrengthScore\":0.0,\"telegramState\":\"QUIET\",\"panicSuspected\":false,\"tvAlertType\":\"NONE\"}",
            symbol,
            iOpen(symbol, PERIOD_M5, 1), iHigh(symbol, PERIOD_M5, 1), iLow(symbol, PERIOD_M5, 1), iClose(symbol, PERIOD_M5, 1),
            iOpen(symbol, PERIOD_M15, 1), iHigh(symbol, PERIOD_M15, 1), iLow(symbol, PERIOD_M15, 1), iClose(symbol, PERIOD_M15, 1),
            iOpen(symbol, PERIOD_M30, 1), iHigh(symbol, PERIOD_M30, 1), iLow(symbol, PERIOD_M30, 1), iClose(symbol, PERIOD_M30, 1),
            iOpen(symbol, PERIOD_H1, 1), iHigh(symbol, PERIOD_H1, 1), iLow(symbol, PERIOD_H1, 1), iClose(symbol, PERIOD_H1, 1),
            iOpen(symbol, PERIOD_H4, 1), iHigh(symbol, PERIOD_H4, 1), iLow(symbol, PERIOD_H4, 1), iClose(symbol, PERIOD_H4, 1),
            atr,
            adr,
            ma20,
            DetermineSession(utcNow),
            ToIsoUtc(utcNow),
            volatilityExpansion,
            ToIsoUtc(mt5ServerNow),
            isUsRiskWindow ? "true" : "false",
            isFriday ? "true" : "false",
            bid,
            ask,
            spread,
            spread,
            spread
        );

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
            Print("PostMarketSnapshot failed. HTTP=", code,
                  ", LastError=", lastError,
                  ", Response=", response);

            if (lastError == 4014 || lastError == 5203 || lastError == 4006)
            {
                Print("WebRequest troubleshooting: In MT5 Tools->Options->Expert Advisors, enable 'Allow WebRequest for listed URL' and add exact URL: ", m_baseUrl);
            }
            return false;
        }

        return true;
    }
};

#endif
