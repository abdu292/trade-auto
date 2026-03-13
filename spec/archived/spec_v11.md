بسم الله الرحمن الرحيم

FINAL UNIFIED IMPLEMENTATION SPEC
PHYSICAL GOLD LIVE AUTOMATION
(MT5-LINKED • LEDGER-FIRST • BUY-ONLY • WATERFALL-SAFE • MULTI-AI DISCIPLINED)

السلام عليكم ورحمة الله وبركاته

Please treat this as the final unified implementation direction for the physical-gold automation.

This specification consolidates:
- the original manual workflow
- the short-prompt framework
- the later constitution / factor-engine refinements
- candidate lifecycle and re-qualification fixes
- MT5-linked live supervision advantages
- multi-AI role discipline
- Telegram/news/macro/cross-market/historical intelligence
- capital / ledger / slips laws
- smart pending placement, cancellation, TP, expiry, and study loops

Nothing here cancels earlier protections.
Everything here preserves the old strengths, removes the known gaps, and upgrades the system from safe-but-late into safe-and-early.

============================================================
0) FINAL OBJECTIVE
============================================================

Build a BUY-ONLY PHYSICAL XAUUSD automation that:

- plants BUY_LIMIT and BUY_STOP orders before the move happens
- supervises every pending order continuously until trigger, expiry, or cancellation
- cancels automatically if structure, macro, or safety premise breaks
- captures more safe monthly rotations
- avoids waterfall, panic, first-leg, fake-breakout, and mid-air traps
- remains Shariah-compliant and ledger-true
- uses MT5 live connectivity and AI APIs intelligently
- reduces manual babysitting to the minimum possible

The system must not become reckless.
It must become:
SAFE + EARLY + DISCIPLINED + HIGH-CAPTURE.

============================================================
1) ABSOLUTE NON-NEGOTIABLE LAWS
============================================================

These are hard laws. Nothing may override them.

A) Trading law
- buy-only physical gold
- no leverage
- no shorting
- no hedging
- no market buys ever

B) Order law
- pending-before-level law always active
- only BUY_LIMIT or BUY_STOP
- every order must have TP + expiry
- no zombie orders
- no hidden conversion from pending logic to market execution

C) Safety law
- WATERFALL_RISK hard block
- FAIL hard block
- hazard-window hard block
- MID-AIR ban hard block
- first-leg waterfall catch ban hard block
- spreadBlock hard block
- stale-context hard block
- structural-breakdown hard block

D) Capital / ledger law
- physical ledger (cash + grams via SLIPS) is the only source of truth
- MT5 account is execution infrastructure only
- two-bucket model only (C1 / C2)
- max two logical live slots
- real-cash capacity clamp
- no negative balances
- shop spread math always applied
- never size from MT5 balance when physical ledger differs

E) Governance law
- structure + safety + capital always override AI suggestions
- Telegram, macro, cross-market, and external AI inputs may modify context and scoring, but must never override hard blockers

============================================================
2) LIVE OPERATING MODEL — FINAL CONTINUOUS LOOP
============================================================

Because the EA is directly linked to MT5, the automation must run as a continuous supervised live loop.

Every cycle (tick batch / few seconds / new candle), run:

1. Pull MT5 + ledger state
   - bid, ask, spread
   - timeframe summaries
   - indicators / ATR / RSI / MA distance
   - open positions
   - pending orders
   - ledger cash, grams, deployable capital

2. Run deterministic engine
   - session / phase / day
   - market regime
   - structure
   - base / lid / sweep / reclaim / compression
   - ADR / ATR / VCI / volatility state
   - overextension
   - waterfallRisk
   - hazardWindow
   - FAIL proximity

3. Run Base-Opportunity Window Detector
   - detect early shelf/reclaim opportunity before expansion

4. Update Candidate Lifecycle
   - FORMING / CANDIDATE / ARMED / PENDING_PLANTED / etc.

5. Run Re-Qualification logic
   - for previously overextended or missed setups that cool off and rebuild safely

6. Build a structured TEXT CONTEXT packet

7. Send packet to Main AI
   - for path refinement, TP/expiry realism, confidence interpretation

8. Trigger bounded validation only if needed
   - Perplexity for macro/geo/rates contradiction checks
   - optional secondary AI only as advisory pattern checker

9. Combine outputs into final engine states
   - legalityState
   - biasState
   - pathState
   - sizeState
   - exitState
   - urgencyState
   - action = PLACE / MODIFY / KEEP / CANCEL / WAIT

10. Execute through TABLE-style compiler
   - exact entry, grams, TP, expiry

11. Continue supervising all pendings before trigger
   - cancel / keep / replace / invalidate as conditions change

12. After trigger or expiry
   - RE-ANALYZE / STUDY / SELF-CROSSCHECK / SLIPS update

============================================================
3) FINAL DECISION STACK — MANDATORY ORDER
============================================================

Every cycle must run in this exact decision order:

1. Ledger / capital truth
2. Session + phase + day
3. Macro / geo / news / Telegram / cross-market context
4. Market Regime Detection
5. Structure Detection
6. Base-Opportunity Window Detection
7. Overextension / waterfall / hazard / spread / stale-context checks
8. Path Routing
9. Candidate Lifecycle update
10. Re-Qualification after overextension
11. PRETABLE legality + confidence + size
12. TP + expiry computation
13. Order generation / cancellation / replacement
14. MT5 execution routing
15. Learning / ledger logging

Important:
No low-level trigger failure such as “M5 not confirmed” may abort the cycle before this stack finishes.

============================================================
4) MARKET REGIME DETECTION ENGINE — GOLD-SPECIFIC
============================================================

Implement a first-class Market Regime Detection Engine calibrated for gold.

Primary regime states:
- RANGE
- RANGE_RELOAD
- TREND_CONTINUATION
- FLUSH_CATCH
- EXPANSION
- EXHAUSTION
- LIQUIDATION
- NEWS_SPIKE
- SHOCK
- DEAD / NO_EDGE

Determine regime using:
- H4 / H1 directional context
- M15 structure
- ATR expansion / contraction
- ADR used %
- MA20 distance
- RSI level + slope
- sweep / reclaim presence
- session and phase
- geo/news/oil/rates context
- spread behavior

Interpretation examples:

RANGE
- defended shelves both sides
- lower ATR expansion
- rotation-friendly
- BUY_LIMIT favored near shelves

RANGE_RELOAD
- prior move happened, now reloading
- shallow pullback into defended floor
- one of the best physical-gold rotation regimes

TREND_CONTINUATION
- H1 aligned
- price respecting higher lows
- clean compression under lid possible
- BUY_STOP allowed only if not overextended

FLUSH_CATCH
- sharp dump into known liquidity
- reclaim and stabilization visible
- BUY_LIMIT possible only after reclaim / hold

EXPANSION
- strong move already underway
- continuation possible only before exhaustion
- danger of late chase

EXHAUSTION
- large prior move, weak follow-through
- long wicks, stretched MA distance
- late continuation blocked
- wait for pullback or re-qualification

LIQUIDATION
- breakdown, vertical selling, support loss
- no catching without rebuild

NEWS_SPIKE
- event-driven distortion
- breakout chasing blocked unless fully stabilized and explicitly legal

SHOCK
- unstable, conflicting, violent conditions
- stand down

This regime engine must drive:
- path preference
- confidenceScore
- sizeState
- TP realism
- expiry strictness
- whether BUY_LIMIT or BUY_STOP is even allowed

============================================================
5) FINAL PATH MODEL — HARD LOCK
============================================================

The engine must classify exactly one path per cycle:

A) BUY_LIMIT
B) BUY_STOP
C) WAIT / OVEREXTENDED / STAND_DOWN

A) BUY_LIMIT path
Use when:
- defended base / shelf / reclaim / sweep exists
- price is still near the actionable shelf
- pullback entry is legal
- no FAIL / hazard / waterfall / spread / stale-context block
- overextension not extreme at candidate creation

B) BUY_STOP path
Use when:
- real lid exists
- true compression exists beneath it
- breakout is not already exhausted
- continuation is legal in this session, regime, and macro state
- M5 confirmation may remain mandatory here

C) WAIT / OVEREXTENDED / STAND_DOWN
Use when:
- price already expanded too far
- no safe fresh structure exists
- hazard / FAIL / waterfall / spread / stale context / poor regime blocks
- macro/news creates stand-down condition

WAIT must explain why the system is not trading now.
It must never erase earlier valid setups.

============================================================
6) BIGGEST PROFIT MODULE #1 — CANDIDATE LIFECYCLE
============================================================

This must become the live core of the engine.

Required states:
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

Each candidate must store:
- candidateCreatedAt
- candidateState
- candidatePath
- candidateBase
- candidateSweep
- candidateReclaim
- candidateLid
- intendedEntryLevels
- TP concept
- expiry window
- invalidation / FAIL
- whyCreated
- whyNotArmedEarlier
- whyInvalidated

Creation example for BUY_LIMIT:
Create / refresh candidate when:
- regime tradeable
- H1 context acceptable
- M15 shows base / shelf / sweep-reclaim
- price within ≤ 0.6 ATR(M15) of base
- overextensionState in {NORMAL, STRETCHED}
- no FAIL / hazard / waterfall / spreadBlock

Promotion to ARMED:
- candidate still valid
- confidenceScore ≥ threshold
- legalityState not BLOCK
- sizeState not ZERO

This is the primary fix for “safe but late” missed setups.

============================================================
7) BIGGEST PROFIT MODULE #2 — BASE-OPPORTUNITY WINDOW DETECTOR
============================================================

This is the missing module that causes many missed rotations.

Failure mode:
- valid shelf forms
- system waits too long for perfect confirmation
- move expands
- system says OVEREXTENDED

Required fix:
Implement a Base-Opportunity Window Detector that activates when:
- H1 context acceptable
- M15 has valid shelf / defended base
- range contraction or rejection visible
- price still near shelf
- no FAIL / hazard / waterfall conflict
- regime is RANGE / RANGE_RELOAD / FLUSH_CATCH recovery / cooled continuation

This should allow early BUY_LIMIT candidate creation before the rate runs away.

This module must not place mid-air buys.
It must only create early candidate windows at real structural shelves.

============================================================
8) BIGGEST PROFIT MODULE #3 — RE-QUALIFICATION AFTER OVEREXTENSION
============================================================

The engine must stop treating overextended states as permanently dead.

When pathState becomes:
- OVEREXTENDED_ABOVE_BASE
or
- WAIT_PULLBACK_BASE

the system must keep memory of the last valid candidate and continue checking whether price cools off into a fresh safe zone.

Re-qualification checks:
- MA20 distance normalizes
- RSI cools from HIGH / EXTREME
- price returns near valid shelf / reclaim
- M15 structure rebuilds / defends
- no FAIL / hazard / waterfall / spreadBlock
- macro state not hostile
- session still appropriate
- regime not degraded to LIQUIDATION / SHOCK

If true:
- candidateState = REQUALIFIED
- BUY_LIMIT may be re-armed
- second-leg continuation can be captured safely

============================================================
9) PENDING ORDER SUPERVISION — ACTIVE UNTIL TRIGGER
============================================================

This is a major MT5-linked advantage and must be fully used.

Every pending order must remain under supervision until:
- triggered
- expired
- cancelled
- invalidated
- safely replaced

Every cycle, check:
- FAIL threatened?
- structure broken?
- waterfallRisk increased?
- hazard window too close?
- macro / geo flip?
- spread blowout?
- crowdLateRisk extreme?
- pending now mid-air relative to updated shelf?
- session quality deteriorated?
- order now stale relative to candidate freshness?
- regime changed against the order?

If yes:
- CANCEL_PENDING with explicit reason

If structure improved and rules explicitly allow:
- REPLACE_PENDING with a better legal order

This cancellation engine is as important as the entry engine.

============================================================
10) AI ROLE DISCIPLINE — NO SLOW COMMITTEE
============================================================

Do not let multiple AI APIs become a slow voting system.

A) Rule Engine
Final authority for:
- legalityState
- waterfallRisk
- FAIL
- hazardWindow
- capital law
- pending-before-level law
- mid-air law
- stale-context law

B) Main AI
Primary reasoning for:
- structure interpretation
- path refinement
- candidate timing
- TP / expiry realism
- re-qualification interpretation

C) Perplexity
Validation / contradiction checker for:
- macro conflict
- geo / rates / DXY / yields contradiction
- major news regime conflict
- special event risk

D) Grok / Gemini / other AI
Optional secondary pattern / contradiction checkers only.
Advisory, not final authority.

Hierarchy:
Rule Engine > Main AI > Perplexity validation > optional secondary AI

Never wait for equal consensus from all AIs.
Use strict timeouts and bounded validation modes:
- FAST_VALIDATE
- DEEP_VALIDATE
- SKIP_VALIDATE_SAFE

============================================================
11) TELEGRAM CHANNELS — TREND / NOISE MONITOR ONLY
============================================================

Because there are 60+ random/noisy channels, Telegram must be downgraded to:
- crowd trend monitor
- sentiment temperature monitor
- rumor amplifier detector
- crowd-late trap detector

Maintain per source:
- source name
- trust score
- style tag
- hit quality
- crowd-late tendency
- bias

Derive only:
- sentimentBias
- sentimentIntensity
- crowdLateRisk
- trustedSourceAlignment

Rules:
- many BUY calls after expansion → crowdLateRisk HIGH
- reduce BUY_STOP confidence
- prefer pullback BUY_LIMIT or WAIT
- panic selling near defended shelf may slightly support contrarian BUY_LIMIT confidence only if structure already valid

Telegram must never directly trigger a trade.

============================================================
12) FACTOR-IMPACT / DEPTH ENGINE — HARD LOCK
============================================================

For every major factor, store:
- factorName
- factorState / value
- impactDirection = BULLISH / BEARISH / NEUTRAL / HAZARD
- impactStrength = 0.0 to 1.0
- timeHorizon = IMMEDIATE / INTRADAY / SESSION / MULTI_SESSION
- affects = legality / bias / size / TP / expiry / standDown / pathPreference

This is necessary so the EA knows not just that “news exists,” but exactly how much it matters and what it is allowed to change.

============================================================
13) MACRO / GEO / OIL / RATES / CB DEMAND FACTORS
============================================================

These must be explicitly encoded.

A) DXY / USD strength
Effect:
- DXY up → bearish pressure on gold
- DXY down → bullish support

May affect:
- biasState
- confidenceScore
- BUY_STOP quality
- TP realism

B) Real yields / US10Y
Effect:
- real yields up → bearish pressure
- real yields down → bullish support

May affect:
- biasState
- breakout confidence
- sizeState
- expiry strictness

C) Geopolitical escalation / war risk
Effect:
- bullish safe-haven support
- but dangerous first-spike volatility

May affect:
- biasState
- standDown logic
- BUY_LIMIT preference
- Rail-B restrictions

D) De-escalation / ceasefire / negotiation
Effect:
- bearish unwind risk
- especially dangerous near highs

May affect:
- standDown
- Rail-B block
- tighter TP
- continuation caution

E) Oil surge
Effect:
- inflation + geo stress may support gold medium-term
- but can produce short-term spike traps

May affect:
- macroBias
- volatility state
- waterfall / trap caution

F) Central-bank / institutional demand
Effect:
- bullish multi-session background
- not immediate trigger by itself

May affect:
- biasState
- deeper BUY_LIMIT tolerance
- not breakout permission by itself

============================================================
14) REQUIRED LIVE FACTOR HOOKS
============================================================

Implement these live variables:
- geoRiskState = CALM / ELEVATED / ESCALATING / SHOCK
- oilState = NORMAL / STRONG / SHOCK
- dxyState
- yieldPressureState
- newsPersistenceScore
- crowdLateRisk
- CBDemandState / institutionalDemandState

These should influence:
- biasState
- confidenceScore
- sizeState
- path preference
- TP realism
- expiry tolerance

They must never override hard safety laws.

============================================================
15) HISTORICAL GOLD MEMORY — LIVE, NOT ONLY STUDY
============================================================

Historical pattern memory must be operational.

Recognize and score:
- first impulse then pullback continuation
- sweep + reclaim continuation
- range reload
- London fake break then reversal
- NY spike then unwind
- late-session exhaustion
- post-news mean reversion
- second-chance continuation after overextension cool-off

For each pattern, store:
- best rail type
- best session
- best trigger style
- TP realism band
- expiry realism band
- trap tendency

Use this to improve:
- path selection
- confidenceScore
- TP / expiry realism
- stand-down decisions

============================================================
16) CROSS-MARKET / CROSS-METAL CONFIRMATION
============================================================

Monitor:
- silver
- DXY
- real yields / US10Y
- indices / risk assets if relevant
- oil when geopolitics are active

Examples:
- gold up + silver up + DXY weak = stronger continuation quality
- gold up but DXY also strong = caution
- gold spike while silver weak = lower quality breakout
- gold pressured with DXY strong and yields rising = macro headwind

Cross-market inputs may modify:
- confidenceScore
- sizeState
- TP realism
- BUY_STOP quality

Never override safety laws.

============================================================
17) LIQUIDITY-SWEEP PROBABILITY ENGINE
============================================================

Implement a dedicated Liquidity-Sweep Probability Engine for gold.

Inputs:
- distance to liquidity pools
- equal highs / equal lows
- prior session high / low
- round number proximity
- wick penetration
- reclaim close
- volume/velocity expansion
- rejection quality
- session phase

Outputs:
- sweepProbability = LOW / MEDIUM / HIGH
- sweepType = downside / upside / both
- reclaimQuality = WEAK / FAIR / STRONG

Rules:
- HIGH downside sweepProbability + strong reclaim near S1/S2
  → boosts BUY_LIMIT confidence
- upside sweep into resistance with weak reclaim
  → caution on BUY_STOP
- sweep without reclaim
  → no reversal trade yet

This engine should strongly improve safe flush-catch and sweep-reclaim entries.

============================================================
18) DYNAMIC LIQUIDITY LADDER
============================================================

Implement a Dynamic Liquidity Ladder to avoid vague entries.

Map stacked liquidity levels:
- L1 = nearest shallow liquidity
- L2 = meaningful sweep pocket
- L3 = exhaustion pocket / deeper flush

Use ladder for:
- BUY_LIMIT staging logic
- TP magnet mapping
- determining whether price is between ladders (bad) or at ladder edge (better)

Hard rule:
Do not buy in between ladders without fresh shelf/reclaim structure.

Ladder use must remain bounded:
- no blind martingale behavior
- no over-stacking
- still obey max two logical slots and capital clamp

============================================================
19) STRUCTURED TEXT CONTEXT PACKET — REPLACE SCREENSHOT DEPENDENCE
============================================================

The EA should send structured text packets, not rely on manual screenshots.

Recommended packet fields:
- symbol, bid, ask, spread
- serverTime, KSATime, session, phase, day
- H4 / H1 / M15 / M5 summaries:
  close, MA20, MA20DistATR, RSI, ATR, candleRange
- regime, overextensionState, waterfallRisk, hazardWindowActive
- S1 / S2 / R1 / R2 / FAIL
- detected structures:
  base, shelf, lid, sweep, reclaim, compression
- ADR_used, VCI
- candidateState, candidateBase, candidateLid, candidatePath, candidateExpiry
- openPositions, pendingOrders summary
- ledgerCashAED, ledgerGoldGrams, deployableAED
- macro factors:
  DXYState, YieldState, GeoRiskState, OilState, CBDemandState
- Telegram:
  sentimentBias, crowdLateRisk
- crossMarketAlignment
- topActiveFactors with directions/strengths

This ensures all AIs see the same deterministic snapshot.

============================================================
20) TP + EXPIRY — PATTERN-AWARE AND SESSION-AWARE
============================================================

TP must consider:
- nearest liquidity magnet
- structure resistance / support
- historical reaction depth
- session profile
- macro / geo context
- volatility state
- crowd behavior
- ladder level relationship

Expiry must consider:
- session phase
- hazard timing
- candidate freshness
- distance to entry
- pattern type
- remaining clean window
- regime quality

Examples:
- sweep-reclaim → usually tighter TP and measured expiry
- range-reload → moderate TP and session-bound expiry
- continuation breakout → TP tied to next magnet and shorter expiry if late session
- re-qualified pullback → measured TP with fresh re-entry window

No zombie orders. No stale candidates.

============================================================
21) UI / DASHBOARD — VISUALIZE THE REAL ENGINE
============================================================

The UI must display the real engine state, not invent new trading logic.

Separate clearly:

A) Physical Ledger
- cash
- gold grams
- deployable capital

B) MT5 Execution Account
- balance / equity / margin / free margin / open MT5 positions

Trade map should visualize:
- current price
- bases / lids / sweeps / reclaim zones
- compression zones
- FAIL
- liquidity magnets / TP zones
- ladder levels
- BUY_LIMIT / BUY_STOP pending orders
- expiry markers
- PRETABLE result
- risk zones:
  waterfall / caution / block

============================================================
22) MICRO MODE / SMALL-CASH LIVE EXPERIENCE
============================================================

Keep micro live mode for smaller free cash balances.

Rules:
- use only free cash allocated to that mode
- held gold inventory remains separate
- one pending order at a time initially
- BUY_LIMIT / BUY_STOP only
- TP mandatory
- expiry mandatory
- ladder disabled until explicitly and safely proven

This allows controlled live testing without violating capital laws.

============================================================
23) OLD PROMPT REFINEMENTS — PRESERVE THEM AS LIVE LOGIC
============================================================

These must remain alive inside the engine:

Capital Utilization:
- bucket law
- capacity clamp
- no negative balances
- slot discipline
- anti-sleep logic

NEWS:
- fatal vs non-fatal uncertainty
- hazard-window logic
- Rail permission matrix
- no rumor-driven regime flips

ANALYZE:
- numeric S1 / S2 / R1 / R2 / FAIL
- regime detector
- waterfall early warning
- MID-AIR ban
- rail legality
- current + next session mapping

TABLE:
- deterministic compiler only
- freshness gates
- safety vetoes
- exposure clamp
- sizing discipline
- PE / ML expiry logic
- profit math

RE ANALYZE:
- reassessment only
- zombie removal
- TP realism
- add / pyramid freeze logic

STUDY / SELF CROSSCHECK / SLIPS:
- missed opportunity reconstruction
- waterfall forensics
- post-mortem learning
- immutable ledger chain

These should be converted into explicit live modules / states where appropriate.

============================================================
24) RICH LOGGING — NO BLACK BOX
============================================================

The engine must log enough detail for future STUDY and debugging.

Required fields:
- candidateState
- candidateCreatedAt
- candidateBase
- candidateLid
- candidatePath
- candidateExpiry
- whyNotArmedEarlier
- requalificationState
- requalifiedFrom
- whyStillWaiting
- normalizedMaDistance
- normalizedRsiState
- telegramAlignment
- crossMarketAlignment
- newsRegime
- crowdLateRisk
- geoRiskState
- oilState
- newsPersistenceScore
- topActiveFactors
- factor directions
- factor strengths
- factor horizons
- what each factor changed in the decision
- AI vs rule-engine disagreement summary
- whyCancelledBeforeTrigger
- sweepProbability
- reclaimQuality
- ladder state

Without these, the system only explains late rejection, not why profitable setups were missed.

============================================================
25) FINAL IMPLEMENTATION PRIORITY
============================================================

Proceed in this order:

1. fix ledger truth fully and permanently
   - physical ledger separate
   - MT5 execution account separate
   - no scaling errors

2. preserve current safety stack exactly
   - do not weaken working protections

3. implement full live candidate lifecycle

4. implement re-qualification after overextension / pullback

5. implement base-opportunity window detector

6. implement continuous pending-order supervision and smart cancellation

7. operationalize Telegram as sentiment / crowdLateRisk layer only

8. operationalize full factor-impact / depth model
   including:
   - geoRiskState
   - oilState
   - newsPersistenceScore
   - crowdLateRisk
   - CBDemandState

9. operationalize historical pattern memory in live path selection

10. implement liquidity-sweep probability engine

11. implement dynamic liquidity ladder

12. improve smart BUY_LIMIT / BUY_STOP planting before expansion

13. improve TP + expiry using candidate freshness + pattern type + ladder context

14. keep logs rich enough for STUDY and SELF-CROSSCHECK

============================================================
26) FINAL EXPECTED BEHAVIOR
============================================================

After these upgrades, the automation should:

- detect valid buys earlier
- arm and plant pending orders before the move happens
- supervise every pending until trigger or cancellation
- cancel bad pendings before they become losses
- refuse all mid-air and waterfall-trap entries
- re-qualify cooled pullbacks safely after initial expansion
- use Telegram / news / geo / history / cross-market context intelligently
- understand how each factor affects gold and how deeply
- capture more safe monthly rotations
- reduce manual intervention and repeated question loops
- stay fully aligned with physical bullion, Shariah, ledger-first framework

The final required result is:

a strong, intelligent, safe, MT5-linked, rate-capturing gold automation
that makes the right buys at the right time,
increases monthly profit through more valid safe rotations,
and does not get caught in waterfall or panic-sell traps.

Please implement strictly according to this unified direction so that none of the last 6+ months of refinement is lost, and the engine evolves from safe-but-late into safe-and-early.

جزاك الله خيراً
والحمد لله رب العالمين