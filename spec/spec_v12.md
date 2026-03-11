# بسم الله الرحمن الرحيم
# PHYSICAL GOLD AUTOMATION — MASTER SPEC FILE
## FULL REFINEMENTS CONSOLIDATION
### MT5-LINKED • LEDGER-FIRST • BUY-ONLY • WATERFALL-SAFE • MULTI-AI SUPERVISED

---

## 0) PURPOSE AND SCOPE

This file is the **single source of truth** for the physical-gold automation project.
It consolidates the full refinement path developed across the project and is meant to be pasted into a new thread or handed directly to the developer and his AI assistant.

It merges and preserves:

- original manual workflow and trading philosophy
- all core short-code modules and their protections
- the later constitution / factor-engine / decision-stack upgrades
- candidate lifecycle + re-qualification logic
- MT5-linked continuous automation advantages
- multi-AI role discipline
- Telegram/news/macro/cross-market/historical overlays
- structured text packet architecture replacing screenshot dependence
- capital, ledger, slips, exposure, session, and expiry laws
- anti-waterfall, anti-panic, anti-mid-air, anti-zombie protections
- current live bug fixes identified from recent logs

### Final objective

Build a **safe-and-early**, Shariah-compliant, MT5-linked, high-discipline gold rotation engine that:

- plants **BUY_LIMIT** and **BUY_STOP** orders before the move happens
- supervises each pending order continuously until fill, expiry, cancellation, or replacement
- captures more safe monthly rotations
- avoids waterfall traps, first-leg panic catches, mid-air entries, and stale zombie orders
- stays ledger-true and physically constrained
- reduces manual babysitting and question loops to the minimum possible

Nothing in this file may weaken safety, Shariah law, ledger truth, or capital discipline.

---

## 1) FOUNDATIONAL LAWS — NON-NEGOTIABLE

### 1.1 Trading Law

- BUY-ONLY physical XAUUSD bullion
- no leverage
- no short selling first
- no hedging
- Shariah model = **buy → own grams → sell owned grams at TP or later**
- existing gold inventory is not permission to short; it is inventory to close when TP logic is reached

### 1.2 Execution Law

- **No market buys ever**
- **Pending-before-level law always active**
- legal new order types only:
  - BUY_LIMIT
  - BUY_STOP
- no hidden conversion from pending logic into market execution

### 1.3 Safety Law

Hard blockers:

- WATERFALL_RISK = HIGH
- FAIL level broken or clearly threatened
- active hazard window where new buys are illegal
- MID_AIR entry zone
- first-leg waterfall catch
- spreadBlock
- clock/session authority unsafe
- unit-scale / ledger inconsistency

### 1.4 Capital / Ledger Law

- physical ledger via **SLIPS** is the only source of truth for capital
- MT5 account is execution infrastructure only
- two buckets only:
  - **C1** = primary rotation capital
  - **C2** = adaptive / recovery capital, locked by default
- max **two logical live slots** total
- real-cash capacity clamp must include shop spread
- no negative balances
- no over-purchase beyond real AED purchasing power

### 1.5 Order Law

Every order must contain:

- entry
- TP
- PE (pending expiry)
- ML (max life)
- grams
- legal rail classification

No zombie pendings. No indefinite pending orders.

### 1.6 Governance Law

- structure + safety + capital override all AI suggestions
- Telegram, macro, cross-market, and external AI inputs may modify **bias, size, TP, expiry, and path preference** only
- they may never override hard blockers

---

## 2) CONSTANTS AND TIME RULES

### 2.1 Pricing / shop constants

- `1 oz = 31.1035 g`
- `1 USD = 3.674 AED`
- Shop Buy = `MT5 + 0.80 USD/oz`
- Shop Sell = `MT5 - 0.80 USD/oz`

### 2.2 Canonical time law

All business logic must normalize to **UTC first**.

Then derive:

- KSA = `UTC + 3:00`
- Dubai = `UTC + 4:00`
- India = `UTC + 5:30`

### 2.3 Clock authority rules

The engine must not classify sessions directly from odd broker offsets such as `+02:10` unless first normalized safely to UTC.

Clock source priority:

1. MT5 candle timestamp normalized to UTC correctly
2. broker tick / server epoch time
3. internal UTC fallback only

Add hard guard:

- if `abs(normalized_mt5_utc - internal_utc) > 90 seconds`
  - `clock_safe = false`
  - `session_authority = LOW`
  - new order placement blocked with `CLOCK_SKEW`

### 2.4 Session definitions

Sessions must be derived only from canonical UTC/KSA mapping and tagged with authority:

- JAPAN
- INDIA
- LONDON
- NEW_YORK

Each cycle must include:

- `session_detected`
- `session_source`
- `session_authority`
- `phase_detected`
- `phase_authority`

---

## 3) SHORT-CODE MODULES — MUST NOT BE LOST

The system must preserve and operationalize the full short-code stack as live modules or explicit engine components.

### 3.1 CAPITAL UTILIZATION

Purpose:

- capital structure
- deployment discipline
- exposure control
- anti-sleep logic
- hard purchase capacity clamp

Key rules:

- ledger-based cash and gold state
- total capital derived from cash + gold valuation
- two buckets only (80/20 default, 90/10 allowed if justified)
- max two logical slots
- session-specific risk budgets
- anti-sleep labels:
  - ACTIVE_ROTATION
  - SAFELY_PARKED
  - UNNECESSARILY_SLEEPING
- `approved_by_capacity_gate = true` must exist before MT5 execution

### 3.2 NEWS

Purpose:

- macro / geo / policy / volatility front gate
- hazard, shock, and permission mapping

Must output at minimum:

- VerificationDepth
- InsideHazardNow
- NextTier1UltraWithin90m
- LiquidityQuality
- GlobalRegime
- TradingRegime
- SpikeState
- WaterfallRisk
- OverallMODE
- Rail-A permission
- Rail-B permission

Rules:

- fatal uncertainty = CAPITAL_PROTECTED
- post-event harvest must not be blocked forever if structure later stabilizes
- rumors never flip regime by themselves

### 3.3 ANALYZE

Purpose:

- convert NEWS + chart structure into a TABLE-ready map

Must produce:

- S1 / S2 / R1 / R2 / FAIL
- regime tag
- waterfall warning
- MID_AIR status
- rail legality
- current session map
- next session map
- where-to-trade triggers

### 3.4 TABLE

Purpose:

- deterministic compiler only

Must enforce:

- TTL / freshness gates
- exposure clamp
- capacity clamp
- FAIL guard
- hazard timing
- mid-air filter
- legal rail enforcement
- PE / ML time generation
- exact grams and profit math

### 3.5 RE ANALYZE

Purpose:

- reassess existing open or pending positions only

Must handle:

- zombie pending detection
- TP realism
- add / pyramid freeze
- cancel / keep / tighten logic

### 3.6 STUDY

Purpose:

- post-mortem learning
- missed opportunity reconstruction
- waterfall forensics
- rule refinement within bounded limits

### 3.7 SELF CROSSCHECK

Purpose:

- system-level audit of where profits leak or rules over-block

### 3.8 SLIPS

Purpose:

- immutable physical ledger
- cash / grams truth
- shop-adjusted accounting
- before/after balances
- lot matching

### 3.9 VERIFY / VALIDATE / COMPARE / CROSS CHECK

These must remain as governance / audit / arbitration modules for research and refinement. Their useful logic should be converted into explicit engine states where possible.

---

## 4) LIVE ARCHITECTURE — MT5-LINKED EA + MULTI-AI STACK

### 4.1 Core components

- **EA / Brain**: connected directly to MT5; reads market data, positions, and pending orders
- **Physical Ledger / SLIPS**: source of truth for cash and gold grams
- **Rule Engine**: deterministic authority for legality and safety
- **Main AI**: primary reasoning layer for structure/path/TP/expiry refinement
- **Perplexity**: macro/news/geo contradiction and hazard validator
- **Grok / Gemini / others**: optional advisory pattern checkers only
- **Telegram monitor**: sentiment / crowdLateRisk only

### 4.2 Final role hierarchy

1. Rule Engine
2. Main AI
3. Perplexity
4. Optional secondary AIs

Never allow a slow committee model.
Use bounded validation modes only:

- FAST_VALIDATE
- DEEP_VALIDATE
- SKIP_VALIDATE_SAFE

### 4.3 Continuous live loop

Each cycle must do:

1. pull MT5 + ledger state
2. normalize time and verify clock authority
3. run deterministic structure and safety engine
4. detect regime and base-opportunity window
5. update candidate lifecycle
6. run re-qualification if needed
7. build structured text packet
8. send packet to main AI
9. validate only if needed through Perplexity
10. combine into final engine states
11. compile through TABLE-style execution logic
12. place / modify / cancel / keep pending orders
13. supervise every pending continuously
14. after fill / expiry / cancel, run RE ANALYZE / STUDY / SLIPS logging

---

## 5) FINAL DECISION STACK — MANDATORY ORDER

Every cycle must follow this order exactly:

1. ledger / capital truth
2. clock normalization and session authority
3. session + phase + day
4. macro / geo / news / Telegram / cross-market context
5. regime detection
6. structure detection
7. base-opportunity window detection
8. overextension / waterfall / hazard checks
9. path routing
10. candidate lifecycle update
11. re-qualification logic
12. PRETABLE legality + confidence + size
13. TP + expiry computation
14. order generation / cancellation / replacement
15. MT5 execution routing
16. RE ANALYZE / STUDY / SLIPS logging

No low-level issue such as `M5Compression=False` may abort the whole process before the stack finishes.

---

## 6) PATH MODEL — HARD LOCK

Exactly one path per cycle:

- BUY_LIMIT
- BUY_STOP
- WAIT / OVEREXTENDED / STAND_DOWN

### 6.1 BUY_LIMIT

Use when:

- defended base / shelf / reclaim / sweep exists
- price still near actionable shelf
- overextension not extreme at candidate creation
- no FAIL / hazard / waterfall / spread block

For BUY_LIMIT, M5 is **optional for confidence**, not a hard legality veto.

### 6.2 BUY_STOP

Use when:

- real lid exists
- real compression exists below it
- breakout not already exhausted
- session and NEWS allow Rail-B
- M5 confirmation may remain mandatory here

### 6.3 WAIT / OVEREXTENDED / STAND_DOWN

Use when:

- price already expanded too far
- structure unsafe or stale
- hazard / FAIL / waterfall / spread / clock block exists
- macro stand-down regime active

WAIT must explain why not trading now but must not erase earlier valid setups.

---

## 7) CANDIDATE LIFECYCLE — CORE PROFIT MODULE

### 7.1 Required states

- NONE
- FORMING
- CANDIDATE
- ARMED
- PENDING_PLANTED
- FILLED
- PASSED
- OVEREXTENDED
- REQUALIFIED
- INVALIDATED

### 7.2 Required stored fields

- candidateCreatedAt
- candidateState
- candidatePath
- candidateBase
- candidateSweep
- candidateReclaim
- candidateLid
- intendedEntryLevels
- TPConcept
- expiryWindow
- FAILLevel
- whyCreated
- whyNotArmedEarlier
- whyInvalidated
- candidateQualityScore
- candidateFreshnessScore

### 7.3 BUY_LIMIT creation rules

Create / refresh candidate when:

- regime tradeable
- H1 context acceptable
- M15 has valid base / shelf / sweep-reclaim
- price within `baseDistATR <= 0.6`
- overextensionState in `{NORMAL, STRETCHED}`
- no FAIL / hazard / waterfall / spreadBlock

### 7.4 Promotion to ARMED

- candidate still valid
- legalityState not BLOCK
- confidenceScore >= threshold
- sizeState not ZERO

### 7.5 Key behavioral rule

If the setup is early but not fully ripe, the engine must produce:

- `candidateState = FORMING` or `CANDIDATE`

not:

- immediate `NO_TRADE`

This is the primary fix for safe-but-late behavior.

---

## 8) BASE-OPPORTUNITY WINDOW DETECTOR

### 8.1 Why it exists

The engine has been waiting too long, then labeling valid opportunities as overextended after the move is already gone.

### 8.2 Activation criteria

Activate base-opportunity window when:

- H1 context acceptable or bullish
- M15 shows defended shelf / base
- range contraction or controlled rejection visible
- price still near base
- no FAIL / hazard / waterfall / spread conflict

### 8.3 Behavior

- create early BUY_LIMIT candidate
- do not place mid-air buys
- do not wait for full expansion before remembering the setup

---

## 9) RE-QUALIFICATION AFTER OVEREXTENSION

When previous path was:

- OVEREXTENDED_ABOVE_BASE
- WAIT_PULLBACK_BASE

The engine must keep the last valid candidate in memory and test each cycle whether it can safely re-enter.

### 9.1 Re-qualification checks

- MA20 distance normalized back from EXTREME
- RSI cooled into safe band
- price returned near shelf / reclaim
- M15 structure rebuilt / held / compressed
- no FAIL / hazard / waterfall / spreadBlock
- macro not hostile
- session still acceptable

If true:

- `candidateState = REQUALIFIED`
- BUY_LIMIT may be re-armed
- second-leg continuation can be captured safely

---

## 10) SPLIT BOTTOM PERMISSION INTO 3 STAGES

Current `BOTTOMPERMISSION_FALSE` is too brittle.

Replace with three layers:

### 10.1 BOTTOM_CANDIDATE_OK

Used for early shelf detection.
Requirements:

- H1 bullish or acceptable
- M15 base OR defended shelf
- no FAIL
- no waterfall
- no hazard
- spread OK

### 10.2 BOTTOM_ARM_OK

Used to arm pending BUY_LIMIT.
Needs:

- stronger M15 base
- reclaim or hold evidence
- improving compression or shelf defense

### 10.3 BOTTOM_EXECUTION_OK

Used only for actual order placement.
Needs:

- full rail legality
- expiry safety
- capital safety
- exposure safety
- no clock/session authority issues

This prevents premature rejection of valid early candidates.

---

## 11) FACTOR-IMPACT / DEPTH ENGINE

For every important factor, store:

- factorName
- factorState / value
- impactDirection = BULLISH / BEARISH / NEUTRAL / HAZARD
- impactStrength = 0.0 to 1.0
- timeHorizon = IMMEDIATE / INTRADAY / SESSION / MULTI_SESSION
- affects = legality / bias / size / TP / expiry / standDown / pathPreference

### 11.1 Core live factors

- DXYState
- YieldPressureState
- geoRiskState
- oilState
- newsPersistenceScore
- CBDemandState
- institutionalDemandState
- crowdLateRisk
- crossMarketAlignment

### 11.2 Effect rules

These may adjust:

- biasState
- confidenceScore
- sizeState
- TP realism
- expiry strictness
- path preference

They must never override hard safety laws.

---

## 12) MACRO / GEO / OIL / RATES / CB DEMAND HOOKS

### 12.1 DXY / USD

- DXY rising strongly = pressure on gold
- DXY weak = support for gold

### 12.2 Real yields / US10Y

- yields up = pressure on gold
- yields down = support for gold

### 12.3 Geopolitical escalation

- bullish safe-haven support medium-term
- but dangerous first-spike volatility intraday

### 12.4 De-escalation / ceasefire / negotiation

- unwind risk near highs
- continuation BUY_STOP should be tightened or blocked

### 12.5 Oil surge

- can support inflation / war-risk narrative
- but also increase spike trap risk

### 12.6 Central-bank / institutional demand

- multi-session bullish background only
- not immediate breakout permission by itself

---

## 13) TELEGRAM LAYER — TREND / NOISE MONITOR ONLY

There are many noisy channels. Telegram must be treated only as:

- crowd temperature monitor
- trend/noise monitor
- rumor amplifier detector
- crowdLateRisk detector

### 13.1 Per-source memory

Track:

- source name
- trust score
- style tag
- hit quality
- crowd-late tendency
- current bias

### 13.2 Derived fields

- sentimentBias
- sentimentIntensity
- crowdLateRisk
- trustedSourceAlignment

Telegram may influence confidence and urgency only. It may never directly trigger a trade.

---

## 14) CROSS-MARKET AND HISTORICAL PATTERN MEMORY

### 14.1 Cross-market

Monitor:

- silver
- DXY
- real yields / US10Y
- indices / risk assets when relevant
- oil during geo tension

### 14.2 Historical patterns

Maintain live-recognized pattern library:

- first impulse then pullback continuation
- sweep + reclaim continuation
- range reload
- London fake break then reversal
- NY spike then unwind
- late-session exhaustion
- post-news mean reversion
- second-chance continuation after cooled overextension

For each pattern store:

- best rail type
- best session
- TP realism band
- expiry realism band
- trap tendency

---

## 15) PENDING ORDER SUPERVISION

Every pending must be supervised until:

- triggered
- expired
- cancelled
- replaced
- invalidated

Every cycle, check:

- FAIL threatened?
- structure broken?
- waterfallRisk increased?
- hazard too close?
- macro / geo flipped?
- spread blown out?
- crowdLateRisk extreme?
- pending now mid-air?
- candidate stale?
- clock authority unsafe?

If yes:

- `CANCEL_PENDING` with exact reason

If structure improved and explicit safe rule allows:

- `REPLACE_PENDING`

---

## 16) TEXT CONTEXT PACKET — REQUIRED LIVE FIELDS

Each cycle should send a structured packet containing at minimum:

### 16.1 Identity / clock

- symbol
- bid
- ask
- spread
- mt5_raw_time
- mt5_time_normalized_utc
- internal_utc
- clock_skew_ms
- clock_safe
- ksa_time
- dubai_time
- india_time
- session_detected
- session_source
- session_authority
- phase_detected
- phase_authority
- day_of_week

### 16.2 Timeframe summaries

For H4 / H1 / M30 / M15 / M5:

- close
- ATR
- RSI
- MA20
- MA20DistATR
- slope
- candleRange
- candleBodySize
- upper/lower wick
- compression_count
- overlap_count
- expansion_count

### 16.3 Structure

- regime
- overextensionState
- waterfallRisk
- hazardWindowActive
- S1
- S2
- R1
- R2
- FAIL
- base_detected
- lid_detected
- sweep_detected
- reclaim_detected
- compression_detected
- base_quality_score
- lid_quality_score

### 16.4 Candidate / bottom logic

- candidateState
- candidatePath
- candidateCreatedAt
- candidateBase
- candidateLid
- candidateQualityScore
- candidateFreshnessScore
- candidateRequalifying
- whyNotArmedEarlier
- bottom_candidate_ok
- bottom_arm_ok
- bottom_execution_ok
- bottom_block_reason

### 16.5 Capital / ledger / unit safety

- physical_ledger_cash_aed
- physical_ledger_gold_grams
- deployable_cash_aed
- deployable_new_buy_grams
- reserved_pending_grams
- mt5_position_grams_equivalent
- unit_scale_ok

### 16.6 Macro / crowd / cross-market

- DXYState
- YieldPressureState
- geoRiskState
- oilState
- CBDemandState
- institutionalDemandState
- macroBias
- macroConfidence
- telegram_state
- sentimentBias
- crowdLateRisk
- crossMarketAlignment

### 16.7 Final engine states

- legalityState
- biasState
- pathState
- sizeState
- exitState
- urgencyState
- confidenceScore
- rail_a_legal
- rail_b_legal
- action

---

## 17) TP + EXPIRY INTELLIGENCE

### 17.1 TP logic

TP must respect:

- nearest liquidity magnets
- structure resistance / shelf
- historical reaction distance
- session profile
- macro / geo context
- volatility state
- crowd behavior

### 17.2 Expiry logic

Must always print both:

- MT5 server time
- KSA time

Expiry must depend on:

- session type
- session phase
- hazard timing
- candidate freshness
- distance to entry
- pattern type

Start-point bands:

- Japan: 90–120 min
- India: 90–150 min
- London: 60–90 min
- New York: 45–60 min

No stale or zombie pendings.

---

## 18) UI / DASHBOARD REQUIREMENTS

The UI must visualize the real engine state and separate ledger truth from MT5 execution.

### 18.1 Physical ledger card

- cash
- gold grams
- deployable capital
- buyable grams at current shop-adjusted price

### 18.2 MT5 execution card

- balance
- equity
- margin
- free margin
- open MT5 positions

### 18.3 Trade map

Show:

- current price
- bases / lids / sweeps / reclaim zones
- S1 / S2 / R1 / R2 / FAIL
- pending BUY_LIMIT / BUY_STOP levels
- TP magnets
- expiry markers
- risk zones
- engine states

### 18.4 Unit-scale guard in UI

Display hard warning if gold grams displayed differ materially from ledger source-of-truth.

---

## 19) CURRENT LIVE BUGS — MUST BE FIXED ASAP

### 19.1 Clock / session bug

Recent logs show:

- abnormal server offset use
- ~3 hour clock skew
- session drift between INDIA and LONDON

Fix:

- normalize all times through UTC
- add `clock_safe` and `session_authority`
- block new orders if skew exceeds tolerance

### 19.2 Gold unit scaling bug

Recent logs/dashboard show `229200g` instead of real `2292g`.

Fix:

- hard separate ledger grams from any scaled internal units
- add `UNIT_SCALE_ERROR`
- exposure gate must not use inflated grams

### 19.3 BottomPermission rigidity

Recent logs show valid low-risk contexts being blocked because of brittle bottom checks.

Fix:

- split into candidate / arm / execution stages
- do not reject early shelf opportunities immediately

### 19.4 Missing macro/Telegram authority

Current logs show Telegram not configured and external news disabled.

Fix:

- label missing-context authority explicitly
- do not pretend full macro confidence when feeds are unavailable

---

## 20) IMPLEMENTATION PRIORITY ORDER

Developer should proceed in this order:

1. fix ledger truth fully and permanently
2. fix clock normalization and session authority
3. fix gold unit scaling and exposure contamination
4. preserve current safety stack exactly
5. implement full live candidate lifecycle
6. implement re-qualification after overextension
7. implement base-opportunity window detector
8. split BottomPermission into three stages
9. implement continuous pending supervision
10. operationalize Telegram as sentiment/crowdLateRisk only
11. operationalize full factor-impact / depth model
12. operationalize historical pattern memory live
13. improve smart BUY_LIMIT / BUY_STOP planting before expansion
14. improve TP + expiry with candidate freshness + pattern type
15. keep logs rich enough for STUDY and SELF CROSSCHECK
16. preserve all short-code protections as explicit live modules

---

## 21) FINAL EXPECTED BEHAVIOR

After full implementation, the automation should:

- detect valid buys earlier
- remember early shelves instead of forgetting them
- arm and plant pending orders before expansion
- supervise each pending continuously
- cancel bad pendings before they turn into losses
- refuse all mid-air and waterfall-trap entries
- re-qualify cooled pullbacks safely
- use macro / geo / crowd / cross-market / historical context intelligently
- understand how each factor affects gold and how deeply
- increase monthly profit through more valid safe rotations
- reduce manual intervention and question loops
- remain fully aligned with the physical bullion, Shariah, and ledger-first framework

The required result is:

**a strong, intelligent, safe, MT5-linked, rate-capturing gold automation that makes the right buys at the right time, increases profits through more safe rotations, and does not get caught in waterfall or panic traps.**

الحمد لله رب العالمين
