# Trade Auto EA — UI Module Spec

## Objective
The UI must be a **live visual window into the engine**.  
It must not calculate trading logic on its own.

## Core Rule
The UI only visualizes:
- Brain structure levels
- Pattern Detector output
- PRETABLE / risk-intelligence decisions
- Pending orders, TP, expiry
- Execution state from Brain / EA
- SLIPS / ledger summaries

## Main Screen: XAUUSD Graphical Trade Map
Show:
- Current price
- Recent candles (M5 and M15 toggle)
- Engine structure levels:
  - bases
  - lids
  - sweep levels
  - compression zones
  - FAIL level
  - liquidity magnets / TP zones

## Chart Overlays
### Pending Orders
- BUY_LIMIT levels
- BUY_STOP levels
- TP levels
- Expiry marker or expiry band

### Engine Signals
- WATERFALL_RISK zones = red
- CONTINUATION_BREAKOUT = green
- RANGE_RELOAD / LIQUIDITY_SWEEP = blue
- FALSE_BREAKOUT / SESSION_TRANSITION_TRAP = amber

### PRETABLE Status
- SAFE = green badge
- CAUTION = yellow badge + effective reduced size
- BLOCK = red badge + reason

## Tap / Tooltip Details
For each planted order:
- orderType
- grams
- entryPrice
- tpPrice
- expiry time
- session
- PRETABLE riskLevel
- riskFlags

For each blocked candidate:
- riskLevel = BLOCK
- main reason
- key riskFlags
- regime
- session

## Required Panels
### A. Live Candidate Panel
- structureValid
- regime
- PRETABLE riskLevel
- executionMode
- orderType
- entryPrice
- tpPrice
- expiry

### B. Risk Panel
- WATERFALL_RISK
- FAIL_THREATENED
- ImpulseExhaustionLevel
- DynamicSessionModifier
- main riskFlags[]

### C. Capital Panel
- free AED available
- existing gold held separately
- max grams currently buyable
- current exposure grams
- micro mode active or not

### D. Ledger / SLIPS Panel
- latest slip summary
- realized AED profit today
- open orders count
- open exposure grams

## Update Model
The UI should refresh from the same event/log stream used by:
- STUDY
- execution monitoring
- ledger monitoring

This guarantees:
**UI view = Engine state = STUDY data**

## Prohibited
The UI must not:
- calculate new signals
- calculate new support/resistance
- override engine decisions
- place market buys
- reinterpret pending plans

## Recommended Implementation
Backend publishes a structured state object such as:

```json
{
  "symbol": "XAUUSD",
  "price": 5126.5,
  "session": "LONDON",
  "regime": "RANGE",
  "structure": {
    "bases": [5108.0, 5102.5],
    "lids": [5130.2],
    "fail": 5098.8,
    "magnets": [5134.2, 5140.0]
  },
  "patternDetector": {
    "patternType": "LIQUIDITY_SWEEP",
    "waterfallRisk": "LOW"
  },
  "pretable": {
    "riskLevel": "CAUTION",
    "riskFlags": ["MA_STRETCH_CAUTION"],
    "sizeModifier": 0.65
  },
  "orders": [
    {
      "orderType": "BUY_LIMIT",
      "entryPrice": 5108.0,
      "tpPrice": 5116.8,
      "expiryMinutes": 45,
      "grams": 3.2
    }
  ]
}
```

## Success Criteria
The user should be able to look at the chart and instantly understand:
1. where price is
2. where the engine sees support / resistance / sweep / FAIL
3. what order is planted
4. what TP and expiry are
5. why the trade is SAFE / CAUTION / BLOCK
