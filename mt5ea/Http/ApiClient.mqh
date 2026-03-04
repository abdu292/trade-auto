#ifndef __API_CLIENT_MQH__
#define __API_CLIENT_MQH__

#include "../Models/TradeCommand.mqh"

// Spread tracking buffer for 1m/5m statistics (sampled at snapshot push frequency)
#define SPREAD_BUFFER_SIZE 60

class ApiClient
{
private:
    string m_baseUrl;
    string m_apiKey;

    // Rolling spread buffer for stats (approx 5 minutes at default 5s push rate = 60 samples)
    double m_spreadBuffer[SPREAD_BUFFER_SIZE];
    int    m_spreadBufferCount;
    int    m_spreadBufferIdx;
    long   m_lastTickTimeMsc;

    void RecordSpread(double spread)
    {
        m_spreadBuffer[m_spreadBufferIdx] = spread;
        m_spreadBufferIdx = (m_spreadBufferIdx + 1) % SPREAD_BUFFER_SIZE;
        if (m_spreadBufferCount < SPREAD_BUFFER_SIZE)
            m_spreadBufferCount++;
    }

    void ComputeSpreadStats(int samples, double &outMin, double &outAvg, double &outMax)
    {
        outMin = 0.0;
        outAvg = 0.0;
        outMax = 0.0;

        int n = MathMin(samples, m_spreadBufferCount);
        if (n <= 0)
            return;

        double sum = 0.0;
        outMin = 1e18;
        outMax = 0.0;

        // Walk backwards through buffer to get the most recent n samples
        for (int i = 0; i < n; i++)
        {
            int idx = ((m_spreadBufferIdx - 1 - i) % SPREAD_BUFFER_SIZE + SPREAD_BUFFER_SIZE) % SPREAD_BUFFER_SIZE;
            double v = m_spreadBuffer[idx];
            if (v < outMin) outMin = v;
            if (v > outMax) outMax = v;
            sum += v;
        }

        outAvg = sum / n;
        if (outMin > outMax) outMin = outMax;
    }

    double ComputeSessionVwap(string symbol, int bars)
    {
        double sumPv = 0.0;
        double sumV = 0.0;
        for (int i = 1; i <= bars; i++)
        {
            double h = iHigh(symbol, PERIOD_M1, i);
            double l = iLow(symbol, PERIOD_M1, i);
            double c = iClose(symbol, PERIOD_M1, i);
            long v = iVolume(symbol, PERIOD_M1, i);
            if (h <= 0.0 || l <= 0.0 || c <= 0.0 || v <= 0)
                continue;

            double typical = (h + l + c) / 3.0;
            sumPv += typical * (double)v;
            sumV += (double)v;
        }

        if (sumV <= 0.0)
            return iClose(symbol, PERIOD_M1, 1);
        return sumPv / sumV;
    }

    string BuildCompressionRangesM15Json(string symbol, int count)
    {
        string json = "[";
        for (int i = 1; i <= count; i++)
        {
            double h = iHigh(symbol, PERIOD_M15, i);
            double l = iLow(symbol, PERIOD_M15, i);
            double range = (h > 0.0 && l > 0.0) ? (h - l) : 0.0;
            if (i > 1)
                json += ",";
            json += StringFormat("%.5f", range);
        }
        json += "]";
        return json;
    }

    string BuildPendingOrdersJson(string symbol)
    {
        string json = "[";
        int added = 0;
        int total = OrdersTotal();
        for (int i = 0; i < total; i++)
        {
            ulong ticket = OrderGetTicket(i);
            if (ticket == 0 || !OrderSelect(ticket))
                continue;

            if (OrderGetString(ORDER_SYMBOL) != symbol)
                continue;

            long orderType = OrderGetInteger(ORDER_TYPE);
            if (orderType != ORDER_TYPE_BUY_LIMIT && orderType != ORDER_TYPE_BUY_STOP)
                continue;

            string typeText = orderType == ORDER_TYPE_BUY_STOP ? "BUY_STOP" : "BUY_LIMIT";
            double price = OrderGetDouble(ORDER_PRICE_OPEN);
            double tp = OrderGetDouble(ORDER_TP);
            datetime expiry = (datetime)OrderGetInteger(ORDER_TIME_EXPIRATION);
            double volumeLots = OrderGetDouble(ORDER_VOLUME_CURRENT);
            double gramsEq = MathMax(0.0, volumeLots * 100.0);

            if (added > 0)
                json += ",";
            json += "{";
            json += "\"type\":\"" + typeText + "\",";
            json += StringFormat("\"price\":%.5f,", price);
            json += StringFormat("\"tp\":%.5f,", tp);
            if (expiry > 0)
                json += "\"expiry\":\"" + ToIsoUtc(expiry) + "\",";
            else
                json += "\"expiry\":null,";
            json += StringFormat("\"volumeGramsEquivalent\":%.2f", gramsEq);
            json += "}";
            added++;
        }

        json += "]";
        return json;
    }

    string BuildOpenPositionsJson(string symbol)
    {
        string json = "[";
        int added = 0;
        int total = PositionsTotal();
        for (int i = 0; i < total; i++)
        {
            ulong ticket = PositionGetTicket(i);
            if (ticket == 0 || !PositionSelectByTicket(ticket))
                continue;

            if (PositionGetString(POSITION_SYMBOL) != symbol)
                continue;

            double entry = PositionGetDouble(POSITION_PRICE_OPEN);
            double tp = PositionGetDouble(POSITION_TP);
            double volumeLots = PositionGetDouble(POSITION_VOLUME);
            double pnl = PositionGetDouble(POSITION_PROFIT);
            double current = SymbolInfoDouble(symbol, SYMBOL_BID);
            double point = SymbolInfoDouble(symbol, SYMBOL_POINT);
            double pnlPoints = 0.0;
            if (point > 0.0 && current > 0.0 && entry > 0.0)
                pnlPoints = (current - entry) / point;
            if (MathAbs(pnlPoints) <= 0.0001)
                pnlPoints = pnl;

            if (added > 0)
                json += ",";
            json += "{";
            json += StringFormat("\"entryPrice\":%.5f,", entry);
            json += StringFormat("\"currentPnlPoints\":%.2f,", pnlPoints);
            json += StringFormat("\"tp\":%.5f,", tp);
            json += StringFormat("\"volumeGramsEquivalent\":%.2f", MathMax(0.0, volumeLots * 100.0));
            json += "}";
            added++;
        }
        json += "]";
        return json;
    }

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

    bool JsonGetBool(string json, string key)
    {
        string pattern = "\"" + key + "\":";
        int start = StringFind(json, pattern);
        if (start < 0)
            return false;

        start += StringLen(pattern);
        string tail = StringSubstr(json, start, MathMin(8, StringLen(json) - start));
        tail = StringToLower(tail);
        return StringFind(tail, "true") == 0;
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

        // Convert UTC to KSA (UTC+3) and map to operating sessions per PRD:
        // JAPAN: 03:00-12:00, INDIA: 07:00-16:00, LONDON: 10:00-19:00, NY: 15:00-00:00
        // Overlap precedence: NY > LONDON > INDIA > JAPAN
        int ksaMinutes = dt.hour * 60 + dt.min + 180;
        while (ksaMinutes >= 1440)
            ksaMinutes -= 1440;

        if (ksaMinutes >= 900 && ksaMinutes < 1440)
            return "NY";
        if (ksaMinutes >= 600 && ksaMinutes < 1140)
            return "LONDON";
        if (ksaMinutes >= 420 && ksaMinutes < 960)
            return "INDIA";
        if (ksaMinutes >= 180 && ksaMinutes < 720)
            return "JAPAN";

        return "OFFHOURS";
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

    double GetEmaApprox(string symbol, ENUM_TIMEFRAMES timeframe, int period, int bars)
    {
        if (period <= 1)
            return iClose(symbol, timeframe, 1);

        int count = MathMax(period + 1, bars);
        double k = 2.0 / (period + 1.0);
        double ema = iClose(symbol, timeframe, count);
        if (ema <= 0.0)
            ema = iClose(symbol, timeframe, 1);

        for (int i = count - 1; i >= 1; i--)
        {
            double closeValue = iClose(symbol, timeframe, i);
            if (closeValue <= 0.0)
                continue;
            ema = (closeValue * k) + (ema * (1.0 - k));
        }

        return ema;
    }

    double GetAtrApproxTf(string symbol, ENUM_TIMEFRAMES timeframe, int bars)
    {
        double sum = 0.0;
        int used = 0;
        for (int i = 1; i <= bars; i++)
        {
            double h = iHigh(symbol, timeframe, i);
            double l = iLow(symbol, timeframe, i);
            if (h <= 0.0 || l <= 0.0)
                continue;

            sum += (h - l);
            used++;
        }

        if (used <= 0)
            return SymbolInfoDouble(symbol, SYMBOL_POINT) * 20.0;

        return sum / used;
    }

    double GetRsiApprox(string symbol, ENUM_TIMEFRAMES timeframe, int period)
    {
        int bars = period + 3;
        double gain = 0.0;
        double loss = 0.0;

        for (int i = bars; i >= 2; i--)
        {
            double c1 = iClose(symbol, timeframe, i - 1);
            double c2 = iClose(symbol, timeframe, i);
            if (c1 <= 0.0 || c2 <= 0.0)
                continue;

            double diff = c1 - c2;
            if (diff > 0)
                gain += diff;
            else
                loss += -diff;
        }

        if (gain <= 0.0 && loss <= 0.0)
            return 50.0;
        if (loss <= 0.0)
            return 100.0;

        double rs = gain / loss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    int CountCompressionM15(string symbol, double atrM15)
    {
        int count = 0;
        double threshold = atrM15 * 0.65;
        for (int i = 1; i <= 8; i++)
        {
            double h = iHigh(symbol, PERIOD_M15, i);
            double l = iLow(symbol, PERIOD_M15, i);
            if (h <= 0.0 || l <= 0.0)
                continue;

            if ((h - l) <= threshold)
                count++;
        }
        return count;
    }

    int CountExpansionM15(string symbol, double atrM15)
    {
        int count = 0;
        double threshold = atrM15 * 1.20;
        for (int i = 1; i <= 8; i++)
        {
            double h = iHigh(symbol, PERIOD_M15, i);
            double l = iLow(symbol, PERIOD_M15, i);
            if (h <= 0.0 || l <= 0.0)
                continue;

            if ((h - l) >= threshold)
                count++;
        }
        return count;
    }

    bool DetectOverlapCandlesM15(string symbol)
    {
        int overlaps = 0;
        for (int i = 1; i <= 4; i++)
        {
            double h1 = iHigh(symbol, PERIOD_M15, i);
            double l1 = iLow(symbol, PERIOD_M15, i);
            double h2 = iHigh(symbol, PERIOD_M15, i + 1);
            double l2 = iLow(symbol, PERIOD_M15, i + 1);
            if (h1 <= 0.0 || l1 <= 0.0 || h2 <= 0.0 || l2 <= 0.0)
                continue;

            double overlapHigh = MathMin(h1, h2);
            double overlapLow = MathMax(l1, l2);
            if (overlapHigh > overlapLow)
                overlaps++;
        }

        return overlaps >= 3;
    }

    bool DetectBreakoutConfirmedM15(string symbol)
    {
        double close1 = iClose(symbol, PERIOD_M15, 1);
        if (close1 <= 0.0)
            return false;

        double recentHigh = 0.0;
        for (int i = 2; i <= 7; i++)
        {
            double h = iHigh(symbol, PERIOD_M15, i);
            if (h > recentHigh)
                recentHigh = h;
        }

        return recentHigh > 0.0 && close1 > recentHigh;
    }

    bool DetectLiquiditySweepM15(string symbol)
    {
        double low1 = iLow(symbol, PERIOD_M15, 1);
        double close1 = iClose(symbol, PERIOD_M15, 1);
        if (low1 <= 0.0 || close1 <= 0.0)
            return false;

        double priorLow = low1;
        for (int i = 2; i <= 8; i++)
        {
            double l = iLow(symbol, PERIOD_M15, i);
            if (l > 0.0 && l < priorLow)
                priorLow = l;
        }

        return low1 < priorLow && close1 > priorLow;
    }

    bool DetectPanicDropSequenceM15(string symbol, double atrM15)
    {
        int panicBars = 0;
        for (int i = 1; i <= 3; i++)
        {
            double o = iOpen(symbol, PERIOD_M15, i);
            double c = iClose(symbol, PERIOD_M15, i);
            double h = iHigh(symbol, PERIOD_M15, i);
            double l = iLow(symbol, PERIOD_M15, i);
            if (o <= 0.0 || c <= 0.0 || h <= 0.0 || l <= 0.0)
                continue;

            double body = o - c;
            double range = h - l;
            if (body > 0.0 && body >= atrM15 * 0.55 && range >= atrM15 * 0.95)
                panicBars++;
        }

        return panicBars >= 2;
    }

    double ComputeImpulseStrengthM15(string symbol, double atrM15)
    {
        double o = iOpen(symbol, PERIOD_M15, 1);
        double c = iClose(symbol, PERIOD_M15, 1);
        double h = iHigh(symbol, PERIOD_M15, 1);
        double l = iLow(symbol, PERIOD_M15, 1);
        if (o <= 0.0 || c <= 0.0 || h <= 0.0 || l <= 0.0 || atrM15 <= 0.0)
            return 0.0;

        double body = MathAbs(c - o);
        double range = h - l;
        if (range <= 0.0)
            return 0.0;

        double bodyRatio = body / atrM15;
        double rangeRatio = range / atrM15;
        double score = (bodyRatio * 0.7) + (rangeRatio * 0.3);
        if (score < 0.0) score = 0.0;
        if (score > 2.0) score = 2.0;
        return score;
    }

    void GetHighLow(string symbol, ENUM_TIMEFRAMES timeframe, int startShift, int barsCount, double &highest, double &lowest)
    {
        highest = 0.0;
        lowest = 0.0;

        for (int i = startShift; i < startShift + barsCount; i++)
        {
            double h = iHigh(symbol, timeframe, i);
            double l = iLow(symbol, timeframe, i);
            if (h <= 0.0 || l <= 0.0)
                continue;

            if (highest <= 0.0 || h > highest)
                highest = h;
            if (lowest <= 0.0 || l < lowest)
                lowest = l;
        }

        if (highest <= 0.0)
            highest = SymbolInfoDouble(symbol, SYMBOL_ASK);
        if (lowest <= 0.0)
            lowest = SymbolInfoDouble(symbol, SYMBOL_BID);
    }

public:
    void Configure(string baseUrl, string apiKey = "")
    {
        m_baseUrl = baseUrl;
        m_apiKey = apiKey;
        m_spreadBufferCount = 0;
        m_spreadBufferIdx = 0;
        m_lastTickTimeMsc = 0;
        ArrayInitialize(m_spreadBuffer, 0.0);
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

    bool ConsumeCancelPendingSignal(bool &cancelPending)
    {
        cancelPending = false;

        string url = m_baseUrl + "/mt5/control/cancel-pending/consume";
        string headers = BuildHeaders();
        char postData[];
        char result[];
        string responseHeaders;

        ResetLastError();
        int code = WebRequest("GET", url, headers, 5000, postData, result, responseHeaders);
        string response = CharArrayToString(result);

        if (code != 200)
        {
            return false;
        }

        cancelPending = JsonGetBool(response, "cancelPending");
        return true;
    }

    bool PostMarketSnapshot(string symbol)
    {
        string url = m_baseUrl + "/mt5/market-snapshot";
        string headers = BuildHeaders();

        double atr = GetAtrApprox(symbol);
        double atrH1 = GetAtrApproxTf(symbol, PERIOD_H1, 14);
        double atrM15 = GetAtrApproxTf(symbol, PERIOD_M15, 14);
        double ma20 = GetMa20Approx(symbol);
        double rsiH1 = GetRsiApprox(symbol, PERIOD_H1, 14);
        double rsiM15 = GetRsiApprox(symbol, PERIOD_M15, 14);
        double ema50H1 = GetEmaApprox(symbol, PERIOD_H1, 50, 120);
        double ema200H1 = GetEmaApprox(symbol, PERIOD_H1, 200, 260);
        double adr = GetAdr(symbol);
        double adrUsedPct = (adr > 0.0) ? ((atr / adr) * 100.0) : 0.0;
        double volatilityExpansion = (adr > 0.0) ? (atr / adr) : 0.0;
        double bid = SymbolInfoDouble(symbol, SYMBOL_BID);
        double ask = SymbolInfoDouble(symbol, SYMBOL_ASK);
        double spread = (ask > 0.0 && bid > 0.0) ? (ask - bid) : 0.0;

        // Record spread in rolling buffer for statistics
        RecordSpread(spread);

        // Compute spread stats: 1m ≈ 12 samples, 5m ≈ 60 samples at 5s push rate
        double spreadMin1m, spreadAvg1m, spreadMax1m;
        double spreadMin5m, spreadAvg5m, spreadMax5m;
        ComputeSpreadStats(12, spreadMin1m, spreadAvg1m, spreadMax1m);
        ComputeSpreadStats(60, spreadMin5m, spreadAvg5m, spreadMax5m);

        // Use 5m stats as median/max for backward compatibility
        double spreadMedian60m = spreadAvg5m > 0.0 ? spreadAvg5m : spread;
        double spreadMax60m = spreadMax5m > 0.0 ? spreadMax5m : spread;

        double previousDayHigh = iHigh(symbol, PERIOD_D1, 1);
        double previousDayLow = iLow(symbol, PERIOD_D1, 1);
        double weeklyHigh = iHigh(symbol, PERIOD_W1, 1);
        double weeklyLow = iLow(symbol, PERIOD_W1, 1);
        double dayOpen = iOpen(symbol, PERIOD_D1, 0);
        double weekOpen = iOpen(symbol, PERIOD_W1, 0);
        double sessionHigh = iHigh(symbol, PERIOD_H1, 0);
        double sessionLow = iLow(symbol, PERIOD_H1, 0);
        double sessionHighJapan = 0.0;
        double sessionLowJapan = 0.0;
        double sessionHighIndia = 0.0;
        double sessionLowIndia = 0.0;
        double sessionHighLondon = 0.0;
        double sessionLowLondon = 0.0;
        double sessionHighNy = 0.0;
        double sessionLowNy = 0.0;
        double previousSessionHigh = 0.0;
        double previousSessionLow = 0.0;
        GetHighLow(symbol, PERIOD_M15, 48, 32, sessionHighJapan, sessionLowJapan);
        GetHighLow(symbol, PERIOD_M15, 32, 16, sessionHighIndia, sessionLowIndia);
        GetHighLow(symbol, PERIOD_M15, 16, 16, sessionHighLondon, sessionLowLondon);
        GetHighLow(symbol, PERIOD_M15, 0, 16, sessionHighNy, sessionLowNy);
        datetime utcNow = TimeGMT();
        string currentSession = DetermineSession(utcNow);
        if (currentSession == "JAPAN")
        {
            previousSessionHigh = sessionHighNy;
            previousSessionLow = sessionLowNy;
        }
        else if (currentSession == "INDIA")
        {
            previousSessionHigh = sessionHighJapan;
            previousSessionLow = sessionLowJapan;
        }
        else if (currentSession == "LONDON")
        {
            previousSessionHigh = sessionHighIndia;
            previousSessionLow = sessionLowIndia;
        }
        else
        {
            previousSessionHigh = sessionHighLondon;
            previousSessionLow = sessionLowLondon;
        }
        datetime mt5ServerNow = TimeCurrent();
        MqlDateTime mt5Struct;
        TimeToStruct(mt5ServerNow, mt5Struct);
        bool isFriday = (mt5Struct.day_of_week == 5);
        bool isUsRiskWindow = (mt5Struct.hour >= 15 && mt5Struct.hour <= 19);
        int compressionCountM15 = CountCompressionM15(symbol, atrM15);
        int expansionCountM15 = CountExpansionM15(symbol, atrM15);
        bool hasOverlapCandles = DetectOverlapCandlesM15(symbol);
        bool isBreakoutConfirmed = DetectBreakoutConfirmedM15(symbol);
        bool hasLiquiditySweep = DetectLiquiditySweepM15(symbol);
        bool hasPanicDropSequence = DetectPanicDropSequenceM15(symbol, atrM15);
        bool isCompression = compressionCountM15 >= 3;
        bool isExpansion = expansionCountM15 >= 2;
        bool isAtrExpanding = (atrH1 > 0.0 && atrM15 > 0.0 && atrM15 >= atrH1 * 0.55);
        bool hasImpulseCandles = ComputeImpulseStrengthM15(symbol, atrM15) >= 1.0;
        double impulseStrengthScore = ComputeImpulseStrengthM15(symbol, atrM15);
        bool panicSuspected = hasPanicDropSequence;
        bool isPostSpikePullback = !isExpansion && compressionCountM15 >= 2 && expansionCountM15 >= 1;
        bool isLondonNyOverlap = (mt5Struct.hour >= 12 && mt5Struct.hour <= 16);

        // Tick quality metrics (PRD): tick-rate proxy + freeze/gap detector + slippage estimate
        MqlTick tick;
        SymbolInfoTick(symbol, tick);
        long tickTimeMsc = (long)tick.time_msc;
        long tickDeltaMsc = 0;
        if (m_lastTickTimeMsc > 0 && tickTimeMsc > m_lastTickTimeMsc)
            tickDeltaMsc = tickTimeMsc - m_lastTickTimeMsc;
        m_lastTickTimeMsc = tickTimeMsc;
        double tickRatePer30s = tickDeltaMsc > 0 ? MathMin(200.0, 30000.0 / (double)tickDeltaMsc) : 0.0;
        bool freezeGapDetected = tickDeltaMsc > 4000;
        double point = SymbolInfoDouble(symbol, SYMBOL_POINT);
        double slippageEstimatePoints = point > 0.0 ? ((spread / point) * 0.25) : 0.0;

        // Optional but helpful: session VWAP approximation from recent M1 bars
        double sessionVwap = ComputeSessionVwap(symbol, 120);

        // Account state (free margin / equity / balance) for exposure cap enforcement
        double freeMargin = AccountInfoDouble(ACCOUNT_FREEMARGIN);
        double equity     = AccountInfoDouble(ACCOUNT_EQUITY);
        double balance    = AccountInfoDouble(ACCOUNT_BALANCE);

        // Tick volumes per timeframe candle
        long volumeM5  = iVolume(symbol, PERIOD_M5,  1);
        long volumeM15 = iVolume(symbol, PERIOD_M15, 1);
        long volumeM30 = iVolume(symbol, PERIOD_M30, 1);
        long volumeH1  = iVolume(symbol, PERIOD_H1,  1);
        long volumeH4  = iVolume(symbol, PERIOD_H4,  1);

        // Candle microstructure / indicator state per timeframe (PRD)
        double ma20M5 = GetEmaApprox(symbol, PERIOD_M5, 20, 40);
        double ma20M15 = GetEmaApprox(symbol, PERIOD_M15, 20, 50);
        double ma20M30 = GetEmaApprox(symbol, PERIOD_M30, 20, 50);
        double ma20H1 = GetEmaApprox(symbol, PERIOD_H1, 20, 60);
        double ma20H4 = GetEmaApprox(symbol, PERIOD_H4, 20, 80);
        double rsiM5 = GetRsiApprox(symbol, PERIOD_M5, 14);
        double rsiM30 = GetRsiApprox(symbol, PERIOD_M30, 14);
        double rsiH4 = GetRsiApprox(symbol, PERIOD_H4, 14);
        double atrM5 = GetAtrApproxTf(symbol, PERIOD_M5, 14);
        double atrM30 = GetAtrApproxTf(symbol, PERIOD_M30, 14);
        double atrH4 = GetAtrApproxTf(symbol, PERIOD_H4, 14);

        string pendingOrdersJson = BuildPendingOrdersJson(symbol);
        string openPositionsJson = BuildOpenPositionsJson(symbol);
        string executionEventsJson = "[]";
        string compressionRangesJson = BuildCompressionRangesM15Json(symbol, 8);

        string payload = "{";
        payload += "\"symbol\":\"" + JsonEscape(symbol) + "\",";
        payload += "\"timeframeData\":[";
        payload += StringFormat("{\"timeframe\":\"M5\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f,\"volume\":%I64d,\"candleStartTime\":\"%s\",\"candleCloseTime\":\"%s\",\"candleBodySize\":%.5f,\"upperWickSize\":%.5f,\"lowerWickSize\":%.5f,\"candleRange\":%.5f,\"ma20Value\":%.5f,\"ma20Distance\":%.5f,\"rsi\":%.5f,\"atr\":%.5f}",
                    iOpen(symbol, PERIOD_M5, 1), iHigh(symbol, PERIOD_M5, 1), iLow(symbol, PERIOD_M5, 1), iClose(symbol, PERIOD_M5, 1), volumeM5,
                    ToIsoUtc(iTime(symbol, PERIOD_M5, 1)), ToIsoUtc(iTime(symbol, PERIOD_M5, 0)),
                    MathAbs(iClose(symbol, PERIOD_M5, 1) - iOpen(symbol, PERIOD_M5, 1)),
                    iHigh(symbol, PERIOD_M5, 1) - MathMax(iOpen(symbol, PERIOD_M5, 1), iClose(symbol, PERIOD_M5, 1)),
                    MathMin(iOpen(symbol, PERIOD_M5, 1), iClose(symbol, PERIOD_M5, 1)) - iLow(symbol, PERIOD_M5, 1),
                    iHigh(symbol, PERIOD_M5, 1) - iLow(symbol, PERIOD_M5, 1),
                    ma20M5,
                    iClose(symbol, PERIOD_M5, 1) - ma20M5,
                    rsiM5,
                    atrM5);
        payload += ",";
        payload += StringFormat("{\"timeframe\":\"M15\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f,\"volume\":%I64d,\"candleStartTime\":\"%s\",\"candleCloseTime\":\"%s\",\"candleBodySize\":%.5f,\"upperWickSize\":%.5f,\"lowerWickSize\":%.5f,\"candleRange\":%.5f,\"ma20Value\":%.5f,\"ma20Distance\":%.5f,\"rsi\":%.5f,\"atr\":%.5f}",
                    iOpen(symbol, PERIOD_M15, 1), iHigh(symbol, PERIOD_M15, 1), iLow(symbol, PERIOD_M15, 1), iClose(symbol, PERIOD_M15, 1), volumeM15,
                    ToIsoUtc(iTime(symbol, PERIOD_M15, 1)), ToIsoUtc(iTime(symbol, PERIOD_M15, 0)),
                    MathAbs(iClose(symbol, PERIOD_M15, 1) - iOpen(symbol, PERIOD_M15, 1)),
                    iHigh(symbol, PERIOD_M15, 1) - MathMax(iOpen(symbol, PERIOD_M15, 1), iClose(symbol, PERIOD_M15, 1)),
                    MathMin(iOpen(symbol, PERIOD_M15, 1), iClose(symbol, PERIOD_M15, 1)) - iLow(symbol, PERIOD_M15, 1),
                    iHigh(symbol, PERIOD_M15, 1) - iLow(symbol, PERIOD_M15, 1),
                    ma20M15,
                    iClose(symbol, PERIOD_M15, 1) - ma20M15,
                    rsiM15,
                    atrM15);
        payload += ",";
        payload += StringFormat("{\"timeframe\":\"H1\",\"open\":%.5f,\"high\":%.5f,\"low\":%.5f,\"close\":%.5f,\"volume\":%I64d,\"candleStartTime\":\"%s\",\"candleCloseTime\":\"%s\",\"candleBodySize\":%.5f,\"upperWickSize\":%.5f,\"lowerWickSize\":%.5f,\"candleRange\":%.5f,\"ma20Value\":%.5f,\"ma20Distance\":%.5f,\"rsi\":%.5f,\"atr\":%.5f}",
                    iOpen(symbol, PERIOD_H1, 1), iHigh(symbol, PERIOD_H1, 1), iLow(symbol, PERIOD_H1, 1), iClose(symbol, PERIOD_H1, 1), volumeH1,
                    ToIsoUtc(iTime(symbol, PERIOD_H1, 1)), ToIsoUtc(iTime(symbol, PERIOD_H1, 0)),
                    MathAbs(iClose(symbol, PERIOD_H1, 1) - iOpen(symbol, PERIOD_H1, 1)),
                    iHigh(symbol, PERIOD_H1, 1) - MathMax(iOpen(symbol, PERIOD_H1, 1), iClose(symbol, PERIOD_H1, 1)),
                    MathMin(iOpen(symbol, PERIOD_H1, 1), iClose(symbol, PERIOD_H1, 1)) - iLow(symbol, PERIOD_H1, 1),
                    iHigh(symbol, PERIOD_H1, 1) - iLow(symbol, PERIOD_H1, 1),
                    ma20H1,
                    iClose(symbol, PERIOD_H1, 1) - ma20H1,
                    rsiH1,
                    atrH1);
        payload += "],";

        payload += StringFormat("\"atr\":%.5f,", atr);
        payload += StringFormat("\"adr\":%.5f,", adr);
        payload += StringFormat("\"ma20\":%.5f,", ma20);
        payload += StringFormat("\"rsiH1\":%.5f,", rsiH1);
        payload += StringFormat("\"rsiM15\":%.5f,", rsiM15);
        payload += StringFormat("\"atrH1\":%.5f,", atrH1);
        payload += StringFormat("\"atrM15\":%.5f,", atrM15);
        payload += StringFormat("\"ema50H1\":%.5f,", ema50H1);
        payload += StringFormat("\"ema200H1\":%.5f,", ema200H1);
        payload += StringFormat("\"adrUsedPct\":%.5f,", adrUsedPct);
        payload += StringFormat("\"previousDayHigh\":%.5f,", previousDayHigh);
        payload += StringFormat("\"previousDayLow\":%.5f,", previousDayLow);
        payload += StringFormat("\"weeklyHigh\":%.5f,", weeklyHigh);
        payload += StringFormat("\"weeklyLow\":%.5f,", weeklyLow);
        payload += StringFormat("\"dayOpen\":%.5f,", dayOpen);
        payload += StringFormat("\"weekOpen\":%.5f,", weekOpen);
        payload += StringFormat("\"sessionHigh\":%.5f,", sessionHigh);
        payload += StringFormat("\"sessionLow\":%.5f,", sessionLow);
        payload += StringFormat("\"sessionHighJapan\":%.5f,", sessionHighJapan);
        payload += StringFormat("\"sessionLowJapan\":%.5f,", sessionLowJapan);
        payload += StringFormat("\"sessionHighIndia\":%.5f,", sessionHighIndia);
        payload += StringFormat("\"sessionLowIndia\":%.5f,", sessionLowIndia);
        payload += StringFormat("\"sessionHighLondon\":%.5f,", sessionHighLondon);
        payload += StringFormat("\"sessionLowLondon\":%.5f,", sessionLowLondon);
        payload += StringFormat("\"sessionHighNy\":%.5f,", sessionHighNy);
        payload += StringFormat("\"sessionLowNy\":%.5f,", sessionLowNy);
        payload += StringFormat("\"previousSessionHigh\":%.5f,", previousSessionHigh);
        payload += StringFormat("\"previousSessionLow\":%.5f,", previousSessionLow);
        payload += "\"session\":\"" + currentSession + "\",";
        payload += "\"timestamp\":\"" + ToIsoUtc(utcNow) + "\",";
        payload += StringFormat("\"volatilityExpansion\":%.5f,", volatilityExpansion);
        payload += "\"mt5ServerTime\":\"" + ToIsoUtc(mt5ServerNow) + "\",";
        payload += "\"mt5ToKsaOffsetMinutes\":50,";
        payload += "\"isCompression\":" + (isCompression ? "true" : "false") + ",";
        payload += "\"isExpansion\":" + (isExpansion ? "true" : "false") + ",";
        payload += "\"isAtrExpanding\":" + (isAtrExpanding ? "true" : "false") + ",";
        payload += "\"hasOverlapCandles\":" + (hasOverlapCandles ? "true" : "false") + ",";
        payload += "\"hasImpulseCandles\":" + (hasImpulseCandles ? "true" : "false") + ",";
        payload += "\"hasLiquiditySweep\":" + (hasLiquiditySweep ? "true" : "false") + ",";
        payload += "\"hasPanicDropSequence\":" + (hasPanicDropSequence ? "true" : "false") + ",";
        payload += "\"isPostSpikePullback\":" + (isPostSpikePullback ? "true" : "false") + ",";
        payload += "\"isLondonNyOverlap\":" + (isLondonNyOverlap ? "true" : "false") + ",";
        payload += "\"isBreakoutConfirmed\":" + (isBreakoutConfirmed ? "true" : "false") + ",";
        payload += "\"isUsRiskWindow\":" + (isUsRiskWindow ? "true" : "false") + ",";
        payload += "\"isFriday\":" + (isFriday ? "true" : "false") + ",";
        payload += StringFormat("\"bid\":%.5f,", bid);
        payload += StringFormat("\"ask\":%.5f,", ask);
        payload += StringFormat("\"spread\":%.5f,", spread);
        payload += StringFormat("\"spreadMin1m\":%.5f,", spreadMin1m);
        payload += StringFormat("\"spreadAvg1m\":%.5f,", spreadAvg1m);
        payload += StringFormat("\"spreadMax1m\":%.5f,", spreadMax1m);
        payload += StringFormat("\"spreadMin5m\":%.5f,", spreadMin5m);
        payload += StringFormat("\"spreadAvg5m\":%.5f,", spreadAvg5m);
        payload += StringFormat("\"spreadMax5m\":%.5f,", spreadMax5m);
        payload += StringFormat("\"spreadMedian60m\":%.5f,", spreadMedian60m);
        payload += StringFormat("\"spreadMax60m\":%.5f,", spreadMax60m);
        payload += StringFormat("\"compressionCountM15\":%d,", compressionCountM15);
        payload += StringFormat("\"expansionCountM15\":%d,", expansionCountM15);
        payload += StringFormat("\"impulseStrengthScore\":%.5f,", impulseStrengthScore);
        payload += "\"telegramState\":\"QUIET\",";
        payload += "\"panicSuspected\":" + (panicSuspected ? "true" : "false") + ",";
        payload += "\"tvAlertType\":\"NONE\",";
        payload += StringFormat("\"freeMargin\":%.2f,", freeMargin);
        payload += StringFormat("\"equity\":%.2f,", equity);
        payload += StringFormat("\"balance\":%.2f,", balance);
        payload += StringFormat("\"tickRatePer30s\":%.2f,", tickRatePer30s);
        payload += "\"freezeGapDetected\":" + (freezeGapDetected ? "true" : "false") + ",";
        payload += StringFormat("\"slippageEstimatePoints\":%.2f,", slippageEstimatePoints);
        payload += StringFormat("\"sessionVwap\":%.5f,", sessionVwap);
        payload += "\"compressionRangesM15\":" + compressionRangesJson + ",";
        payload += "\"pendingOrders\":" + pendingOrdersJson + ",";
        payload += "\"openPositions\":" + openPositionsJson + ",";
        payload += "\"orderExecutionEvents\":" + executionEventsJson;
        payload += "}";

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
