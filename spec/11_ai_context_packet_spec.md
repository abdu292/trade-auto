# بسم الله الرحمن الرحيم
# AI CONTEXT PACKET SPEC
Version: CONTEXT-PACKET-v2.0

The EA must send structured JSON/text instead of screenshots.

## Required Fields
- symbol
- bid
- ask
- spread
- serverTime
- ksaTime
- indiaTime
- session
- phase
- H4/H1/M30/M15/M5 summaries
- ATR
- RSI
- MA20 distance
- ADR used
- S1 / S2 / R1 / R2 / FAIL
- base / shelf / lid / sweep / reclaim / compression flags
- RegimeTag
- VolatilityState
- WaterfallRisk
- hazard state
- candidate state
- candidate freshness
- ledger cash AED
- ledger gold grams
- deployable AED
- open positions
- pending orders
- DXYState
- YieldPressureState
- geoRiskState
- oilState
- CBDemandState
- institutionalDemandState
- newsPersistenceScore
- crossMarketAlignment
- crowdLateRisk
- historicalPatternTag
- historicalContinuationScore
- historicalReversalRisk
- historicalExtensionBandUSD
- sessionHistoricalModifier
