بسم الله الرحمن الرحيم

Short implementation brief
(Use together with the full “Final Gold Engine Constitution” spec)

1) Please implement the attached constitution as the NEW gold-engine standard. The old logic:
   “M5 entry not confirmed → abort”
   must be removed as a universal rule.

2) Hard-locked non-negotiables (never tunable):
   - no market buys ever
   - pending-before-level only
   - WATERFALL / FAIL / hazard windows = hard blocks
   - spreadBlock / exposure laws / PRETABLE BLOCK = hard blocks
   - physical bullion ledger (cash + grams) = only source of truth
   - MT5 = execution account only, never ledger truth

3) Mandatory decision stack order:
   1. ledger / capital truth
   2. session + phase
   3. macro / risk
   4. regime
   5. structure
   6. overextension
   7. path routing
   8. PRETABLE + legality + confidence
   9. size + TP + expiry
   10. order generation
   11. UI + STUDY logs

4) Path model must always classify one of 3 states:
   - BUY_LIMIT
   - BUY_STOP
   - WAIT_PULLBACK / OVEREXTENDED / STAND_DOWN

5) BUY_LIMIT path:
   - for base / reclaim / sweep near price
   - M5 trigger optional
   - must use PENDING_LIMIT_PATH
   - derive S1 / S2 / S3 from real structure
   - attach TP + expiry
   - no generic abort just because M5 trigger is absent

6) BUY_STOP path:
   - for lid + compression + breakout continuation
   - M5 trigger mandatory
   - if not ready → reason must be:
     BUY_STOP_BREAKOUT_NOT_READY
   - not generic RULE_ENGINE_ABORT

7) WAIT / OVEREXTENDED path:
   - for stretched price / no clean base / no valid breakout / hazard states
   - explicit reasons only, e.g.:
     OVEREXTENDED_ABOVE_BASE
     WAIT_PULLBACK_BASE
     RANGE_NO_STRUCTURE
     HAZARD_WINDOW_BLOCK

8) Critical modules to build and wire:
   - OverextensionDetector
     • inputs: MA20 distance / ATR, RSI, candleRange / ATR, distance from base
     • outputs: NORMAL / STRETCHED / OVEREXTENDED
   - LiquiditySweep + ReclaimDetector
     • outputs: NONE / SWEEP_ONLY / SWEEP_RECLAIM / FAILED_RECLAIM
     • sweep + reclaim near base must strongly improve BUY_LIMIT quality

9) Factor engine must be explicit and aggregated:
   factor classes:
   - macro/risk
   - session/time
   - volatility/liquidity
   - structure
   - stretch/exhaustion
   - execution
   - capital/business

   aggregate into:
   - legalityState
   - biasState
   - pathState
   - sizeState
   - exitState

10) Key starter thresholds:
   - ma20DistATR:
     NORMAL ≤0.8
     STRETCHED 0.8–1.5
     EXTREME >1.5
   - RSI:
     LOW <35
     MID 35–65
     HIGH 65–75
     EXTREME >75
   - BUY_LIMIT valid while baseDistATR ≤1.0
   - BUY_LIMIT re-arm near base when baseDistATR ≤0.4
   - ADR used >1.0 → block most continuation BUY_STOPs
   - VCI:
     COMPRESSED ≤0.7
     NORMAL 0.7–1.3
     EXPANDED >1.3
   - spread:
     caution ≥0.5
     block ≥0.7
     reject if TP_distance / spread <3

11) Session starter multipliers / expiry:
   - Japan: size 0.5, expiry 90–120m
   - India: size 0.7, expiry 90–150m
   - London: size 1.0, expiry 60–90m
   - NY: size 0.6, expiry 45–60m

12) Confidence scoring:
   - 0–100 score using H1 context, M15 structure, sweep/reclaim, volatility fit,
     stretch state, session fit, ADR usage, spread, and safety flags
   - thresholds:
     <60 = WAIT
     60–74 = MICRO
     75–89 = normal
     ≥90 = high-confidence
   - still always subject to exposure laws

13) Auto-Tune Phase 1 = report only
   - output AUTO_TUNE_REPORT
   - no auto-apply yet
   - may only adjust within bounds:
     • ma20DistATR stretched/extreme thresholds
     • BUY_LIMIT baseDistATR cutoff
     • BUY_LIMIT compressionCount in ranges
     • optional M5 alignment for BUY_LIMIT
   - must NEVER touch:
     • WATERFALL
     • FAIL
     • hazard windows
     • no-market-buy
     • exposure / capital laws
     • spreadBlock
     • PRETABLE BLOCK
     • pending-before-level

14) UI / dashboard requirements:
   - Physical Ledger card:
     cash 2237.42 AED
     gold 2292 g
     deployable AED
     buyable grams
   - separate MT5 Execution Account card
   - factor state panel:
     legalityState
     biasState
     pathState
     overextensionState
     WATERFALL_RISK
     session + phase
   - engine trade-map chart:
     bases, lids, sweeps, reclaims, TP magnets,
     pending BUY_LIMIT / BUY_STOP,
     expected / alternate / invalidation paths
   - executionMode must show real state:
     AUTO / HYBRID / MANUAL

15) Micro mode:
   - use only fresh cash 2237.42 AED
   - one micro pending order at a time initially
   - no ladder initially
   - BUY_LIMIT / BUY_STOP only
   - TP + expiry mandatory

16) Final implementation goal:
   - remove generic “no M5 trigger = no setup” behavior
   - replace it with path-aware, factor-aware, explicit reasoning
   - plant pending orders only when structurally correct
   - stand down intelligently when stretched / hazardous
   - keep all hard safety laws intact
   - reduce manual babysitting over time

Please treat this as the active implementation constitution together with the full attached spec.

الحمد لله

=====================================

بسم الله الرحمن الرحيم

FINAL GOLD ENGINE CONSTITUTION
(Complete implementation brief — path logic + factor engine + thresholds + UI)

Objective:
Build a BUY-ONLY physical-gold pending-order engine that:

- plants BUY_LIMIT / BUY_STOP orders intelligently
- avoids waterfall / panic / fake-break traps
- understands why gold should or should not be bought
- adapts safely through bounded Auto-Tune
- reduces manual babysitting
- keeps physical-ledger truth as the dashboard source of truth

This message is intended to close the missing gaps so nothing essential is left.

============================================================
0) HARD-LOCKED FOUNDATIONS
============================================================

These rules must never be weakened by Auto-Tune, AI, or later modules:

1. No market buys ever
2. Pending-before-level law always active
3. WATERFALL_RISK / FAIL / hazard windows remain hard blockers
4. Capital / exposure / deployable limits remain hard blockers
5. Spread protection remains hard blocker
6. PRETABLE BLOCK remains hard blocker
7. Physical ledger is the source of truth, not MT5 balance/equity

============================================================
1) FINAL DECISION STACK — MANDATORY ORDER
============================================================

The engine must process in this exact order:

1. Ledger / capital truth
2. Session + phase classification
3. Macro / risk context
4. Market regime classification
5. Structure detection
6. Overextension detection
7. Path routing
8. PRETABLE / legality / confidence
9. Size / expiry / TP assignment
10. Order generation
11. UI / logging / STUDY output

Do not let lower-level M5 trigger logic abort the cycle before the higher-level path is known.

============================================================
2) FINAL PATH MODEL — THREE EXPLICIT PATHS
============================================================

The rule engine must always classify one of these path states:

A) BUY_LIMIT
B) BUY_STOP
C) WAIT_PULLBACK / OVEREXTENDED / STAND_DOWN

No more generic:
- “M5 entry not confirmed = abort”

------------------------------------------------------------
A) BUY_LIMIT PATH
------------------------------------------------------------

Use when:

- regime is tradeable (not DEAD, not SHOCK)
- H1 context acceptable for buy-only
- M15 base / reclaim / sweep structure valid
- no FAIL
- no high WATERFALL_RISK
- no hazard conflict
- spread OK
- price is near base / reclaim, not already too far above it

BUY_LIMIT path behavior:

- M5 entry confirmation is OPTIONAL
- missing M5 breakout / momentum / retest must NOT cause abort
- route to PENDING_LIMIT_PATH
- derive structural pending levels:
  - S1 = base shelf
  - S2 = sweep-under-base pocket
  - S3 = deeper exhaustion pocket if valid
- assign TP + expiry
- continue to PRETABLE and decision engine

------------------------------------------------------------
B) BUY_STOP PATH
------------------------------------------------------------

Use when:

- valid structural lid
- compression below lid
- breakout / momentum confirmation exists
- ImpulseExhaustionGuard is not BLOCK
- no waterfall / hazard / FAIL conflict

BUY_STOP path behavior:

- M5 trigger remains mandatory
- if not ready:
  - reason = BUY_STOP_BREAKOUT_NOT_READY
- do not call this a generic rule-engine abort

------------------------------------------------------------
C) WAIT_PULLBACK / OVEREXTENDED / STAND_DOWN PATH
------------------------------------------------------------

Use when:

- price already expanded strongly away from base
- H1/M15 MA20 distance stretched
- RSI high or recently rolled over from high
- no clean pullback base just below
- no valid breakout structure either
- or regime / macro / hazard state says stand down

Behavior:

- NO_TRADE may be correct here
- but reason must be explicit:
  - OVEREXTENDED_ABOVE_BASE
  - WAIT_PULLBACK_BASE
  - RANGE_NO_STRUCTURE
  - HAZARD_WINDOW_BLOCK
  - HIGH_WATERFALL_BLOCK
  - FAIL_THREATENED

============================================================
3) FACTOR ENGINE — FULL CORRELATION LAYER
============================================================

The engine must maintain a factor-impact matrix.

Each factor should contain:

- factorName
- state / value
- impactDirection = bullish / bearish / neutral / hazard
- impactStrength = 0.0 to 1.0
- timeHorizon = immediate / intraday / session / multi-session
- affects:
  - legality
  - bias
  - size
  - tp
  - expiry
  - standDown

The engine must aggregate all factors into 5 engine states:

1. legalityState = LEGAL / CAUTION / BLOCK
2. biasState = BULLISH / NEUTRAL / BEARISH / SHOCK
3. pathState = BUY_LIMIT / BUY_STOP / WAIT_PULLBACK / STAND_DOWN
4. sizeState = FULL / REDUCED / MICRO / ZERO
5. exitState = MAGNET_TP / STANDARD_TP / TIGHT_EXPIRY / STAND_DOWN

============================================================
4) FACTOR CLASSES — NOTHING MISSING VERSION
============================================================

The following classes must all be explicit in code.

------------------------------------------------------------
A) MACRO / RISK FACTORS
------------------------------------------------------------

Required factors:

- DXY / USD strength
- real yields / UST yields
- risk-on / risk-off state
- geopolitical escalation / de-escalation
- high-impact scheduled news (CPI, NFP, FOMC, Powell, etc.)

Effects:
- mostly bias / caution / hazard / stand-down
- should not directly create entries, but should modify path confidence and legality

Examples:
- DXY strong up → bearish bias on gold
- real yields up → bearish bias
- geo escalation → bullish shock / high caution
- de-escalation → bearish unwind risk
- red news imminent → hazard block

------------------------------------------------------------
B) SESSION / TIME FACTORS
------------------------------------------------------------

Required:

- session identity = Japan / India / London / New York
- session phase = START / MID / END
- day-of-week (especially Friday)
- time-to-session-end

Effects:
- preferred path
- size multiplier
- expiry realism
- trap probability

------------------------------------------------------------
C) VOLATILITY / LIQUIDITY FACTORS
------------------------------------------------------------

Required:

- ATR by timeframe
- ADR usage
- volatility compression index
- liquidity environment = normal / thin / vacuum
- spread condition = normal / caution / block

Effects:
- path suitability
- overextension
- legality
- size
- expiry

------------------------------------------------------------
D) STRUCTURE FACTORS
------------------------------------------------------------

Required:

- base / shelf detection
- lid detection
- sweep detection
- reclaim detection
- range / trend / shock structure classification

These are the main entry-path drivers.

------------------------------------------------------------
E) STRETCH / EXHAUSTION FACTORS
------------------------------------------------------------

Required:

- MA20 distance (M15 + H1)
- RSI level + velocity (M15 + H1)
- candle range vs ATR
- distance from last valid base

These decide:
- late chase block
- WAIT state
- whether BUY_LIMIT is still valid
- whether BUY_STOP is blocked

------------------------------------------------------------
F) EXECUTION / MARKET-CONDITION FACTORS
------------------------------------------------------------

Required:

- pending-before-level law
- session-aware expiry
- execution mode selection
- no stale order logic
- order still valid after latency / spread changes

------------------------------------------------------------
G) CAPITAL / BUSINESS-MODEL FACTORS
------------------------------------------------------------

Required:

- same-session TP probability
- expected AED/minute
- capital efficiency
- deployable fresh cash
- micro-mode constraints
- exposure / bucket laws

============================================================
5) TWO CRITICAL MISSING MODULES TO HARD-WIRE
============================================================

------------------------------------------------------------
5.1 OverextensionDetector
------------------------------------------------------------

Inputs:

- MA20 distance on M15 + H1
- RSI M15 + H1
- candle range vs ATR
- distance from last valid base

Output:

- NORMAL
- STRETCHED
- OVEREXTENDED

Behavior:

- OVEREXTENDED:
  - BUY_STOP blocked
  - shallow BUY_LIMITs above base blocked
  - pathState = WAIT_PULLBACK / OVEREXTENDED
  - explicit reason code required

------------------------------------------------------------
5.2 LiquiditySweep + ReclaimDetector
------------------------------------------------------------

Inputs:

- equal highs / lows
- wick penetration
- reclaim close back inside range
- volume response

Outputs:

- NONE
- SWEEP_ONLY
- SWEEP_RECLAIM
- FAILED_RECLAIM

Behavior:

- SWEEP_RECLAIM near base:
  - strong BUY_LIMIT quality booster
- SWEEP above highs:
  - caution / fake breakout risk for BUY_STOP

============================================================
6) NUMERIC STARTER THRESHOLDS
============================================================

These should all be config values, not hardcoded blindly.

------------------------------------------------------------
6.1 MA20 DISTANCE BANDS (normalized by ATR)
------------------------------------------------------------

ma20DistATR = |close - MA20| / ATR_tf

Bands:

- NORMAL:    ≤ 0.8
- STRETCHED: 0.8 to 1.5
- EXTREME:   > 1.5

Rules:

- if H1 ma20DistATR > 1.5 → overextensionState = OVEREXTENDED
- if 0.8 < H1 ma20DistATR ≤ 1.5 → STRETCHED
- H1 has higher priority than M15

------------------------------------------------------------
6.2 RSI BANDS + VELOCITY
------------------------------------------------------------

RSI bands:

- LOW: < 35
- MID: 35 to 65
- HIGH: 65 to 75
- EXTREME: > 75

Rules:

- BUY_LIMIT compression / base band:
  - RSI_M15 acceptable = 35 to 72
  - 72 to 75 = CAUTION only if near strong base
  - > 75 = WAIT / no new entries until pullback + reclaim
- if RSI_M15 HIGH/EXTREME and H1 ma20DistATR ≥ 1.2:
  - classify as STRETCHED / OVEREXTENDED

RSI velocity:
- rsiSlope = RSI_now - RSI_prev
- large positive slope with high RSI = impulse / exhaustion risk
- large negative slope after extreme high = rollover / pullback state

------------------------------------------------------------
6.3 DISTANCE FROM LAST VALID BASE
------------------------------------------------------------

baseDistATR = |close - baseLevel| / ATR_M15

Rules:

- BUY_LIMIT valid only if price above base but:
  - baseDistATR ≤ 1.0
- if baseDistATR > 1.0 while still above base:
  - no fresh BUY_LIMIT
  - WAIT_PULLBACK / OVEREXTENDED
- BUY_LIMIT can re-arm when:
  - baseDistATR ≤ 0.4
  - and reclaim conditions valid

------------------------------------------------------------
6.4 ADR USAGE
------------------------------------------------------------

adrUsed = todayRange / ADR_20

Bands:

- LOW: < 0.5
- NORMAL: 0.5 to 0.9
- FULL: > 0.9

Rules:

- if adrUsed > 1.0:
  - continuation BUY_STOP usually blocked
- if adrUsed LOW in RANGE regime:
  - BUY_LIMIT at strong base preferred

------------------------------------------------------------
6.5 VOLATILITY COMPRESSION INDEX
------------------------------------------------------------

VCI = avgRange(last10) / avgRange(last50)

Bands:

- COMPRESSED: ≤ 0.7
- NORMAL: 0.7 to 1.3
- EXPANDED: > 1.3

Rules:

- BUY_STOP allowed only when VCI ≤ 1.0 and breakout structure valid
- BUY_LIMIT works well in NORMAL
- COMPRESSED RANGE near base = strong BUY_LIMIT quality
- EXPANDED = caution / WAIT unless deep reclaim structure exists

------------------------------------------------------------
6.6 SPREAD GUARDS
------------------------------------------------------------

Current normal spread ≈ 0.3

Starter thresholds:

- spreadCaution = 0.5
- spreadBlock = 0.7

Rules:

- spread ≥ 0.7 → BLOCK
- 0.5 to 0.7 → MICRO only or WAIT
- if TP_distance / spread < 3 → reject trade

------------------------------------------------------------
6.7 SESSION SIZE MULTIPLIERS
------------------------------------------------------------

Initial defaults:

- Japan = 0.5
- India = 0.7
- London = 1.0
- New York = 0.6

Use only as modifiers after legality/path already determined.

------------------------------------------------------------
6.8 EXPIRY STARTER BANDS
------------------------------------------------------------

- Japan: 90–120 min
- India: 90–150 min
- London: 60–90 min
- NY: 45–60 min

Adjust by:
- session phase
- VCI
- distance to entry
- time to session end

============================================================
7) DECISION CONFIDENCE SCORE (0–100)
============================================================

This score ranks candidates inside legal space.

Suggested weights:

- H1 context aligned................... +15
- M15 base/lid valid................... +15
- sweep + reclaim...................... +15
- volatility fit for chosen path....... +10
- RSI + MA distance not stretched...... +10
- session fit.......................... +10
- ADR not overused..................... +10
- spread normal........................ +5
- no FAIL / no hazard / no waterfall... +10

Thresholds:

- < 60  → WAIT / STAND_DOWN
- 60–74 → MICRO
- 75–89 → normal size
- ≥ 90  → high-confidence candidate
(still subject to exposure/capital laws)

============================================================
8) PATH ROUTING DECISION TREE
============================================================

1. If legalityState = BLOCK:
   - STAND_DOWN

2. Else run OverextensionDetector:
   - if OVEREXTENDED:
     - pathState = WAIT_PULLBACK / OVEREXTENDED

3. Else evaluate structure:
   - valid base / reclaim / sweep near price?
     → BUY_LIMIT candidate
   - valid lid + compression?
     → BUY_STOP candidate
   - neither?
     → WAIT_PULLBACK / RANGE_NO_STRUCTURE

4. For chosen candidate:
   - run confidence score
   - assign sizeState
   - assign expiry
   - assign TP

5. Only then generate orders.

============================================================
9) AUTO-TUNE — BOUNDED SELF-IMPROVEMENT
============================================================

Phase 1:
- recommendation only
- AUTO_TUNE_REPORT
- no auto-apply

Auto-Tune may adjust only:

- BUY_LIMIT compression thresholds
- optional M5 alignment for BUY_LIMIT
- routing between BUY_LIMIT vs WAIT in valid base regimes
- overextension thresholds within min/max

Suggested tuning bounds:

- stretched threshold: 0.7 to 1.1 ATR
- extreme threshold: 1.3 to 1.8 ATR
- BUY_LIMIT baseDistATR cutoff: 0.8 to 1.2
- BUY_LIMIT compressionCount in ranges: 0 to 2

Auto-Tune must never touch:

- WATERFALL_RISK logic
- FAIL laws
- hazard windows
- no-market-buy law
- exposure / capital gates
- spread block rules
- pending-before-level law
- PRETABLE BLOCK

============================================================
10) UI / DASHBOARD — MUST REFLECT THE FACTOR ENGINE
============================================================

The dashboard must show:

A) Ledger Truth
- Cash = 2237.42 AED
- Gold = 2292.00 g
- Deployable AED
- Buyable grams now

B) Factor States
- legalityState
- biasState
- pathState
- overextensionState
- waterfallRisk
- session + phase

C) Trade Map Chart
- bases
- lids
- sweep zones
- reclaim zones
- TP magnets
- pending BUY_LIMIT / BUY_STOP levels
- expected / alternate / invalidation paths

D) Execution State
- AUTO / HYBRID / MANUAL
- must match actual system behavior

MT5 values must sit in a separate:
- Execution Account card
not in the physical ledger card.

============================================================
11) MICRO MODE
============================================================

For current fresh cash = 2237.42 AED:

- one pending order at a time initially
- BUY_LIMIT / BUY_STOP only
- no ladder initially
- TP + expiry mandatory
- existing 2292 g inventory must remain separate from new-buy cash

============================================================
12) FINAL OBJECTIVE
============================================================

After this implementation, the engine should:

- stop using “no M5 trigger” as a universal abort reason
- classify correctly between:
  - BUY_LIMIT
  - BUY_STOP
  - WAIT_PULLBACK / OVEREXTENDED
- understand factor correlations affecting gold
- adjust legality, bias, size, TP, expiry, and stand-down logically
- keep all hard safety laws intact
- become stable, profitable, and far less dependent on manual babysitting

الحمد لله