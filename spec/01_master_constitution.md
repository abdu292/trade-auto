# بسم الله الرحمن الرحيم
# PHYSICAL GOLD LIVE AUTOMATION — MASTER CONSTITUTION
Version: FINAL-CONSOLIDATED-v2.0

## Truthful Note
This pack consolidates the locked refinements preserved in this conversation into a stable implementation set.
Some earlier uploaded files have expired and could not be re-opened, so this regeneration is based on the active conversation record, preserved locked rules, and the latest cross-check refinements integrated here.

## Mission
Build a BUY-ONLY PHYSICAL XAUUSD bullion automation that:
- plants BUY_LIMIT and BUY_STOP orders before the move
- captures safe 8–12 USD rotations by default
- captures safe 20–50 USD impulse legs when certified
- avoids waterfall, panic, mid-air, and hazard traps
- remains ledger-first, Shariah-compliant, pending-first, and deterministic
- minimizes latency and API cost
- minimizes question loops and reasoning drift

## 11.a Consolidated Answer Rule (Anti-Loop)
Standing intent:
“Apply every refinement that increases safe profit and rotation without increasing waterfall, mid-air, panic, FAIL, spread, or hazard risk.”
Therefore:
- All relevant improvements must be integrated into the current answer/spec in one consolidated response.
- No drip-feeding of “one more improvement”, “hidden weakness”, or similar prompts.
- Resolve structure internally first, then output final stable position.
- Questions only when a true binary design fork exists that cannot be decided logically.
- Otherwise, improvements are applied automatically without asking again.

## Foundational Laws
### Trading Law
- Buy-only physical gold
- No leverage
- No shorting
- No hedging
- Buy -> own grams -> later hit TP / sell owned grams only

### Execution Law
- No market buys ever
- Pending-before-level law always active
- Allowed orders:
  - BUY_LIMIT
  - BUY_STOP
- Every order must have:
  - entry
  - TP
  - PE
  - ML
  - grams
  - reason code

### Safety Hard Blockers
Block if any true:
- WATERFALL_RISK = HIGH
- FAIL threatened or broken
- fatal hazard window active
- spreadBlock = TRUE
- midAirFail = TRUE
- capital/exposure clamp failed
- stale context beyond TTL

### Ledger / Capital Law
- Physical ledger is source of truth
- MT5 account is execution infrastructure only
- Two buckets only:
  - C1 primary
  - C2 adaptive/recovery
- Max two logical slots
- No negative balances
- Real-cash capacity clamp must pass before release
- Remove any hard-coded local 100g minimum; use configurable minimum grams precision

## Time Law
- MT5 Server Time = KSA − 50 minutes
- IST = KSA + 2h30m
- Therefore IST = Server Time + 3h20m
Use one canonical base clock internally, preferably Server Time.

## Explicit Session Arrays
### Japan
- IST: START 05:30–06:30 | MID 06:30–08:30 | END 08:30–09:30
- Server: START 02:10–03:10 | MID 03:10–05:10 | END 05:10–06:10

### India
- IST: START 09:30–10:30 | MID 10:30–12:30 | END 12:30–13:30
- Server: START 06:10–07:10 | MID 07:10–09:10 | END 09:10–10:10

### London
- IST: START 13:30–14:30 | MID 14:30–17:00 | END 17:00–18:30
- Server: START 10:10–11:10 | MID 11:10–13:40 | END 13:40–15:10

### New York
- IST: START 18:30–19:30 | MID 19:30–22:00 | END 22:00–23:30
- Server: START 15:10–16:10 | MID 16:10–18:40 | END 18:40–20:10

### Transition Windows
- Japan→India: IST 09:15–09:45 | Server 05:55–06:25
- India→London: IST 13:15–13:45 | Server 09:55–10:25
- London→New York: IST 18:15–18:45 | Server 14:55–15:25

## Session Principle
Session = context, never permission.

## Final Priority Stack
1. Hard safety blockers
2. Legality / capital blockers
3. Structure validity
4. Regime + volatility
5. Session + phase
6. Bias state
7. Factor modifiers
8. Candidate lifecycle
9. Rail permissions
10. TABLE compiler

## Volatility States
- COMPRESSED
- NORMAL
- EXPANSION
- EXHAUSTION

## Regime Tags
- RANGE
- RANGE_RELOAD
- CONTINUATION_REBUILD
- EXPANSION
- EXHAUSTION
- LIQUIDATION
- NEWS_SPIKE
- SHOCK

## Rail Outputs
- RailA_Legal = YES / ONLY_AFTER_STRUCTURE / NO
- RailB_Legal = YES / STRICT / NO

## Candidate Lifecycle
- NONE
- FORMING
- ZONE_WATCH_ACTIVE
- EARLY_FLUSH_CANDIDATE
- CANDIDATE
- ARMED
- PENDING_PLANTED
- FILLED
- PASSED
- OVEREXTENDED
- REQUALIFIED
- INVALIDATED

Hard law:
Candidate lifecycle may begin early, but no candidate may be ARMED or placed until rail permissions and TABLE legality both pass.

## Bottom Grammar
- CLASSIC_RECLAIM_BOTTOM
- FLUSH_ABSORPTION_BOTTOM
- PANIC_TO_REBUILD_BOTTOM
Outputs:
- BOTTOM_STRONG
- BOTTOM_PROVISIONAL
- BOTTOM_INVALID

## Waterfall vs Flush
### WATERFALL_CONTINUATION
- repeated bearish closes near lows
- shelf destruction continues
- no real rebound

### FLUSH_REVERSAL_ATTEMPT
- sharp drop into known shelf
- rejection / reclaim
- no decisive lower extension
- zone holds

Only the second can feed provisional bottom / early flush candidate.

## M5 Rule
- For deep BUY_LIMIT flush captures: M5 compression is a booster, not a hard veto
- For BUY_STOP breakout continuation: M5 compression remains hard

## Profit Classes
### Standard Rotation Mode
- Default target: +8 to +12 USD

### Impulse Harvest Mode
- Certified target bands: +20 / +30 / +50 USD or more
- Only when structure, factor alignment, volatility, session behavior, and historical pattern memory support continuation

## Historical Pattern Engine (10+ Year Integration)
Add a live Historical Pattern Engine with outputs:
- historicalPatternTag
- historicalContinuationScore
- historicalReversalRisk
- historicalExtensionBandUSD
- historicalBestPath
- historicalTrapProbability
- sessionHistoricalModifier

This layer must influence:
- rail permissions
- TP sizing
- expiry mode
- confidence
- impulseHarvestScore
- whether IMPULSE_HARVEST_MODE may activate

## Impulse Harvest Mode
The engine must classify:
- SAFE IMPULSE TO CAPTURE
vs
- DANGEROUS SPIKE TO AVOID

Enable IMPULSE_HARVEST_MODE only when:
- structure gate passes
- volatility = expansion, not terminal exhaustion
- historical continuation score supports extension
- factor gate aligns enough
- hazard gate is safe enough
- candidate is fresh

## External Signal / Advisory Engine
Telegram / TradingView / external signals are advisory only.

### XAU Pip Convention for External Signals
- 1 pip = 0.10 USD
- 40 pips = 4 USD
- 80 pips = 8 USD
- 120 pips = 12 USD
- 300 pips = 30 USD

They may affect:
- candidatePriority
- confidenceScore
- impulseHarvestScore
- zoneWatchActive
- expiryMode

They may never:
- bypass hard blockers
- bypass candidate lifecycle
- bypass TABLE
- place orders directly

## Minimum Projected Move Rule
- Absolute floor: 8 USD
- Preferred: 8–12 USD
- Compute from candidate entry zone to TP, not from after-the-fact rebound price

## Execution Templates
### FLUSH_LIMIT_CAPTURE
- Entry: upper half of deep S2 pocket
- TP1: +8 USD
- TP2: +12 USD
- Optional TP3: structural / historical extension magnet
- Short expiry
- Cancel on FAIL break / fatal hazard / waterfall continuation

### IMPULSE_HARVEST_CAPTURE
- Allowed only when IMPULSE_HARVEST_MODE = TRUE
- Supports:
  - IMPULSE_BREAKOUT_CAPTURE
  - IMPULSE_CONTINUATION_REBUILD
- TP ladder:
  - TP1 = +8 to +12 USD
  - TP2 = +20 USD region
  - TP3 = 30–50 USD historical extension band when justified
- Dynamic expiry required

## Pending Supervision
Every pending order must be re-evaluated every cycle for:
- FAIL proximity / break
- waterfallRisk jump
- hazard timing
- macro / geo regime flip
- spread blowout
- crowdLateRisk spike
- session phase decay
- stale candidate freshness
- structure migration
- mid-air drift
Allowed actions:
- KEEP
- CANCEL_PENDING
- REPLACE_PENDING
- WAIT

## AI Hierarchy
1. Grok = primary brain (do most work, including online search)
2. Perplexity = validator + macro contradiction engine
3. ChatGPT = deep structural fallback / arbitration
4. Gemini = final consistency audit

Rule Engine > Main AI > Validator > Others

## Final Principle
The engine must be:
- safe-and-early, not safe-but-late
- structure-first, not session-euphoric
- ledger-first, not MT5-balance-led
- pending-first, not market-chasing
- profit-dense, not tiny-noise obsessed
- anti-loop, not follow-up baiting
- hard-locked, not drifting
