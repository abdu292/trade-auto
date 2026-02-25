/**
 * SHARED MODEL REFERENCE GUIDE
 * Shows how JSON models flow between brain (C#) ↔ aiworker (Python) ↔ mt5ea (MQL5)
 */

// ============================================================================
// 1. C# SIDE (brain/src/Application/Common/Models/Contracts.cs)
// ============================================================================

/*
// TimeframeDataContract - OHLC candle data
public sealed record TimeframeDataContract(
    string Timeframe,           // "H1", "H4", "D1", etc.
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close);

// MarketSnapshotContract - Market state snapshot sent to AI Worker
public sealed record MarketSnapshotContract(
    string Symbol,              // "EURUSD", "GBPUSD", etc.
    IReadOnlyCollection<TimeframeDataContract> TimeframeData,
    decimal Atr,                // Average True Range
    decimal Adr,                // Average Daily Range
    decimal Ma20,               // 20-period Moving Average
    string Session,             // "ASIA", "EUROPE", "LONDON", "NEW_YORK"
    DateTimeOffset Timestamp);  // UTC timestamp

// TradeSignalContract - Response from AI Worker
public sealed record TradeSignalContract(
    string Rail,                // "BUY_LIMIT", "SELL_STOP", etc.
    decimal Entry,
    decimal Tp,                 // Take Profit
    DateTimeOffset Pe,          // Expiration time
    int Ml,                     // Money Loss per pip (risk in $)
    decimal Confidence);        // 0.0 - 1.0

// TradeCommandContract - Command sent to MT5
public sealed record TradeCommandContract(
    string Type,                // "BUY_LIMIT", "SELL_STOP"
    decimal Price,
    decimal Tp,
    DateTimeOffset Expiry,
    int Ml);                    // Money Loss
*/

// ============================================================================
// 2. PYTHON SIDE (aiworker/app/models/contracts.py)
// ============================================================================

/*
from pydantic import BaseModel, Field
from datetime import datetime

class TimeframeData(BaseModel):
    timeframe: str
    open: float
    high: float
    low: float
    close: float

class MarketSnapshot(BaseModel):
    symbol: str = Field(min_length=1, max_length=20)
    timeframeData: list[TimeframeData]  # Note: camelCase from C#
    atr: float
    adr: float
    ma20: float
    session: str
    timestamp: datetime

class TradeSignal(BaseModel):
    rail: str
    entry: float
    tp: float
    pe: datetime
    ml: int
    confidence: float
*/

// ============================================================================
// 3. ACTUAL JSON PAYLOADS (HTTP Request/Response)
// ============================================================================

// REQUEST: brain → aiworker (POST http://localhost:8001/analyze)
{
  "symbol": "EURUSD",
  "timeframeData": [
    {
      "timeframe": "H1",
      "open": 1.10200,
      "high": 1.10300,
      "low": 1.10150,
      "close": 1.10250
    },
    {
      "timeframe": "H4",
      "open": 1.10100,
      "high": 1.10350,
      "low": 1.10050,
      "close": 1.10250
    }
  ],
  "atr": 0.00095,
  "adr": 0.0012,
  "ma20": 1.10250,
  "session": "EUROPE",
  "timestamp": "2025-02-25T14:30:00Z"
}

// RESPONSE: aiworker → brain (200 OK)
{
  "rail": "BUY_LIMIT",
  "entry": 1.10250,
  "tp": 1.10400,
  "pe": "2025-02-25T15:00:00Z",
  "ml": 3600,
  "confidence": 0.85
}

// REQUEST: brain → mt5 (GET /mt5/pending-trades)
// Headers: X-API-Key: dev-local-change-me
// Response (200 OK):
{
  "id": "abc123def456",
  "type": "BUY_LIMIT",
  "symbol": "EURUSD",
  "price": 1.10250,
  "tp": 1.10400,
  "expiry": "2025-02-25T15:00:00Z",
  "ml": 3600
}

// REQUEST: mt5 → brain (POST /mt5/trade-status)
// Headers: X-API-Key: dev-local-change-me
{
  "tradeId": "abc123def456",
  "status": "EXECUTED"
}

// RESPONSE: (200 OK)
{
  "received": true
}

// ============================================================================
// 4. KEY CONVERSION DETAILS
// ============================================================================

// C# → Python (HttpAIWorkerClient converts automatically):
// - decimal → float (in JSON both are numbers)
// - DateTimeOffset → ISO 8601 string (UTC)
// - PascalCase properties → camelCase in JSON
// - All DateTime values are UTC

// Python → C# (deserialization in HttpAIWorkerClient):
// - float → decimal (automatic)
// - ISO 8601 string → DateTimeOffset.Parse
// - camelCase remains camelCase in JSON, C# accessor uses PascalCase

// MQL5 → C# (JSON POST):
// - MQL5 sends raw JSON strings
// - C# uses JsonSerializer to deserialize to records

// ============================================================================
// 5. CONFIGURATION CHECKLIST
// ============================================================================

✓ brain uses HttpAIWorkerClient to call http://localhost:8001/analyze
✓ aiworker listens on port 8001 (see aiworker/main.py uvicorn config)
✓ All timestamps are ISO 8601 UTC format
✓ All HTTP calls have content-type: application/json
✓ MT5 security: API key checked via X-API-Key header

// ============================================================================
// 6. TO VERIFY LOCAL INTEGRATION
// ============================================================================

# Terminal 1: Start aiworker
cd aiworker
pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001

# Terminal 2: Start brain
cd brain/src/Web
dotnet run

# Terminal 3: Test the flow
curl -X GET http://localhost:5000/health
curl -X POST http://localhost:5000/api/signals/analyze/EURUSD \
  -H "Content-Type: application/json"

# Watch logs for:
# [brain] → [AIWorker] POST /analyze for EURUSD
# [brain] ✓ Analysis received: BUY_LIMIT
# [aiworker] Received MarketSnapshot with EURUSD data
