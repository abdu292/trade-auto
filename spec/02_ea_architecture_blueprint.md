# بسم الله الرحمن الرحيم
# EA ARCHITECTURE BLUEPRINT
Version: FINAL-v2.0

## Objective
Implement an MT5-linked BUY-ONLY physical gold automation using deterministic modules first and AI assistance second.

## Main Components
### A. MT5 Bridge
- pull bid / ask / spread
- OHLC for M5 / M15 / M30 / H1 / H4
- open positions
- pending orders
- place / modify / cancel orders

### B. Ledger Service
- maintain cash_AED_total
- gold_grams_total
- deployable AED
- capacity clamp
- two-bucket state (C1 / C2)

### C. Rule Engine
- hard safety blockers
- hazard windows
- FAIL checks
- spread checks
- exposure clamp
- mid-air filter

### D. Structure Engine
- S1 / S2 / R1 / R2 / FAIL
- base / shelf / lid
- sweep / reclaim
- compression
- zone freshness

### E. Regime / Volatility Engine
- regime tag
- volatility state
- overextension state
- waterfall vs flush classification

### F. Session Engine
- map server time to session + phase
- transition windows
- session context modifiers only

### G. Historical Pattern Engine
- 10+ year pattern memory
- continuation probability
- reversal risk
- extension band
- sessionHistoricalModifier

### H. Factor Engine
- DXY
- yields
- geoRiskState
- oilState
- CBDemandState
- institutionalDemandState
- crossMarketAlignment
- crowdLateRisk
- newsPersistenceScore

### I. External Signal Engine
- Telegram parser
- TradingView webhook parser
- advisory alignment only

### J. Candidate Lifecycle Engine
- manage states from NONE to INVALIDATED
- zone watch
- early flush candidate
- re-qualification

### K. AI Orchestrator
- build context packet
- call Grok first
- call Perplexity validator
- call ChatGPT only when needed
- call Gemini last if enabled

### L. TABLE Compiler
- exact pending order build
- grams sizing
- TP ladder
- PE / ML expiry
- final vetoes

### M. Pending Supervision Engine
- keep / cancel / replace
- stale candidate handling
- structure migration handling

### N. Shared Logging / Study Stream
- candidate transitions
- factor snapshots
- reason codes
- missed opportunities
- realized outcomes

## Runtime Loop
1. Pull MT5 + ledger state
2. Normalize time
3. Safety blockers
4. Capital blockers
5. Structure map
6. Regime + volatility
7. Session + phase
8. Historical pattern outputs
9. Factor engine outputs
10. External signal alignment update
11. Candidate lifecycle update
12. Build context packet
13. AI orchestration
14. TABLE compiler
15. Execute / supervise / cancel / replace
16. Log everything

## Engineering Principle
Deterministic first, AI-assisted second, compiler-gated last.
