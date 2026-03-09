# Trade Auto End-to-End Architecture (Client View)

## Purpose of This Document
This document explains how the core Trade Auto system works from end to end, in business language, based on the actual code.

It is written to help confirm whether the real behavior matches your intended operating model:
- strict buy-only physical gold flow
- strong risk protection
- AI-assisted decisions with verification layers
- final client control before execution (approve/reject)
- Auto Trade toggle for fully automated execution when you are ready

Scope note:
- This covers core backend + MT5 + AI workflow.
- UI feature details are intentionally excluded.

---

## The System in One Plain-English Picture
The system works like a guarded decision factory:

1. MT5 continuously sends live market state to Brain.
2. Brain runs strict structural and risk filters first.
3. Pattern Detector identifies live structural patterns (sweep, waterfall, breakout, trap, etc.).
4. Only valid opportunities are sent to the AI worker.
5. AI is run in a single-lead live path (with deterministic gate + failover protections).
6. Brain applies trade score + pre-execution risk intelligence (impulse/sweep/session sizing), then final decision-engine rules.
7. Post-decision execution legality gates run (capital utilization + portfolio exposure).
8. Trade is either rejected, queued for client approval, or sent to MT5 queue (based on Auto Trade toggle and execution mode).
9. Client can approve or reject queued trades.
10. MT5 executes only after final release and sends status back.
11. Brain records outcomes, updates ledger/slips for fill events, and tags study candidates for later analysis.

The result: AI can propose, but cannot bypass the system rules or the client decision handoff path.

---

## Core Components and Their Roles

### 1) MT5 Expert Advisor (`mt5ea`)
What it does:
- watches live ticks for `XAUUSD`
- builds rich market snapshots (multi-timeframe + quality + context)
- sends snapshots to Brain
- polls Brain for pending trades
- executes broker orders (buy limit / buy stop)
- sends execution updates back to Brain
- consumes control signals (cancel pending / replay history fetch)

What it enforces locally before placing an order:
- valid price/TP/lifetime
- configurable minimum grams (runtime setting; default can be 0.1g for micro/live-experience testing)
- only BUY_LIMIT or BUY_STOP
- blocks execution if order is marked high-risk or capital-protected


### 2) Brain (`brain`)
What it does:
- central decision engine and orchestration layer
- receives market snapshots
- runs rule engine, pattern detector, regime checks, news checks, AI orchestration, scoring, and final decisioning
- routes orders either to MT5 queue or to manual approval queue based on Auto Trade toggle
- exposes endpoints for approval (`approve/reject`), monitoring, and control actions (panic interrupt)
- logs full timeline events for each cycle


### 3) AI Worker (`aiworker`)
What it does:
- builds AI context from market + telegram + macro context
- enforces an AI pre-flight gate before any LLM call (freshness/risk-state/session-budget)
- runs a single lead model for live decisions (required agreement = 1)
- can fail over once to the full analyzer set if lead path returns no usable signal
- keeps validation/study/self-crosscheck prompt stages outside the live path (reserved for async STUDY flows)
- returns structured signal + safety and trace metadata
- returns `NO_TRADE` when AI gate, model consensus, or safety conditions fail


### 4) Prompt Library (`prompts`)
What it does:
- stores the operating prompt framework used by AI providers/stages
- includes provider-specific master prompts and stage prompts

Used directly in code during AI pipeline.

---

## End-to-End Live Flow (From MT5 to Final Client Decision)

## Step 1: Market Data Is Captured in MT5
On each tick, the EA:
- tracks tick timing and feed quality
- detects session/time context
- computes indicators and structure signals from M5/M15/H1 and supporting data
- composes market quality data (spread windows, freeze gaps, slippage estimate, liquidity signs)
- includes account context (free margin, equity, balance)
- includes open pending orders and open positions snapshot

It posts this as a full `market-snapshot` to Brain.


## Step 2: Brain Stores Snapshot and Normalizes Context
Brain receives the snapshot and:
- standardizes times (MT5 server, KSA, India)
- resolves authoritative price source
- normalizes key tags (telegram impact, confirmation, alert type)
- stores latest snapshot in memory for decision cycles
- writes timeline telemetry event

At this point, no trade is placed yet.


## Step 3: Brain Decision Cycle Starts (Candle-Aligned)
A background cycle runs only when a new M5 or M15 candle appears (noise reduction).

For each cycle:
- creates a unique cycle id
- loads latest snapshot
- checks session availability
- continues only if the market state is current and valid


## Step 4: Market Regime + Rule Engine Gate (Before AI)
This is critical: Brain runs deterministic rules first.

### 4.1 Regime pre-filter
System can block trading if market is structurally poor:
- DEAD
- CHOPPY
- strong bearish conditions for this buy-only approach

Each regime detection cycle emits a `MARKET_REGIME_DETECTED` timeline event that includes:
- `regime`, `isTradeable`, `reason`
- `ema50H1`, `ema200H1` — EMA values from the snapshot used for the golden/death-cross check
- `rsiH1` — H1 RSI value used in the regime classification

These fields are present in both the live and replay timeline logs for full observability.

### 4.2 Four-layer structural rule engine
Must pass all relevant layers:
- H1 context
- M15 setup
- M5 entry
- impulse confirmation

If rule engine fails:
- AI is skipped
- cycle ends `NO_TRADE`

This prevents AI from inventing trades without structure.


## Step 4b: Pattern Detector
After the rule engine, the Pattern Detector runs to identify live structural patterns.

**Purpose (current code):** active execution gate input for PRETABLE + structured timeline/logging output.
Pattern output is consumed before final decisioning and can hard-block trading.

**Mandatory pattern classes:**
- `LIQUIDITY_SWEEP` — sweep of intraday swing lows with or without reclaim
- `WATERFALL_RISK` — active panic drop or elevated volatility pattern
- `CONTINUATION_BREAKOUT` — lid break or compression release confirmation
- `FALSE_BREAKOUT` — spike that reversed back into range
- `RANGE_RELOAD` — tight consolidation with no expansion, liquidity coiling
- `SESSION_TRANSITION_TRAP` — false move at session open (stop hunt / fake spike)

**Detection mode:** Runtime currently uses `RULE_ONLY` (deterministic rules first).
Schema also supports `RULE_PLUS_AI` for a future extension.

**Output fields per pattern:**
- `PATTERN_ID`, `PATTERN_VERSION`, `DETECTION_MODE`
- `PATTERN_TYPE`, `SUBTYPE`, `CONFIDENCE`, `SESSION`, `TIMEFRAME_PRIMARY`
- `ENTRY_SAFETY`, `WATERFALL_RISK`, `FAIL_THREATENED`, `RECOMMENDED_ACTION`

**Recommended actions:** `ALLOW_RAIL_A_ONLY`, `ALLOW_RAIL_B`, `WAIT_RECLAIM`, `WAIT_RETEST`, `WAIT_COMPRESSION`, `NO_BREAKOUT_BUY`, `BLOCK_NEW_BUYS`, `CAPITAL_PROTECTED`

**Important:** AI ranking never overrides hard bans (FAIL threatened, waterfall, no base/compression).

### 4.3 Pattern -> PRETABLE active gate contract
Pattern Detector feeds PRETABLE directly:

- Hard block rule:
   - `PATTERN_TYPE=WATERFALL_RISK` with `WATERFALL_RISK=HIGH` or `FAIL_THREATENED=true`
   - Result: `PRETABLE=BLOCK` -> immediate `NO_TRADE`
- Breakout continuation pathway:
   - `PATTERN_TYPE=CONTINUATION_BREAKOUT` with safe entry and no FAIL threat
   - Result: may keep/upgrade legal continuation path to `BUY_STOP` (still subject to all legality gates)
- Range/reload pathway:
   - `RANGE_RELOAD` / `LIQUIDITY_SWEEP` / reclaim-retest-compression actions
   - Result: biases legal pending pullback path (`BUY_LIMIT`) at structural reclaim/base zones


## Step 5: Economic News Gate
If structural setup passes, Brain checks economic-news risk.

If news gate blocks:
- cycle ends `NO_TRADE`
- no AI order candidate is released


## Step 6: AI Analysis Is Requested
Only after rule + news gate pass.

Brain sends full market payload to AI worker and logs this event.

AI worker then runs this pipeline:

### 6.1 Context build
- market context
- telegram context
- macro context
- risk/regime hints

### 6.2 Pre-AI hard block
If deterministic pre-classification marks risk as blocked:
- immediate `NO_TRADE`

### 6.3 AI Gate (hard pre-flight check)
Before any LLM call, a hard gate enforces:

1. **Data freshness** — snapshot must not be older than `AI_GATE_MAX_DATA_AGE_SECONDS` (default 180 s).
   - **In LIVE mode**: uses strict system-time comparison.
   - **In REPLAY mode**: freshness check is skipped because the snapshot timestamp is intentionally historical. Cycle-relative freshness applies (snapshot is fresh for its cycle as long as it was produced by the current replay cycle).
   - Replay mode is detected by checking if `cycleId` starts with `replay_`.
2. **Risk state** — AI is skipped when the deterministic risk classifier returns `BLOCK`.
3. **Session budget** — per-session call counter; resets automatically on session transition.

If any condition fails: returns `NO_TRADE` without invoking any LLM.

### 6.4 Single lead model decision
The live path uses one lead reasoning model.

- Pre-stage AI passes (`short_prompt_news.md`, `short_prompt_analyze.md`) have been removed from the live path. The market context already carries telegram/macro/news data.
- Validation stages (`short_prompt_validate.md`, `short_prompt_study.md`, `short_prompt_self_crosscheck.md`, `short_prompt_captial_utilization.md`) have been moved out of the live path. They are preserved for the async STUDY / SELF CROSSCHECK module.
- A single lead model is selected via `AI_SINGLE_LEAD_MODEL` flag (default: on). Other configured providers are reserved for STUDY / SELF CROSSCHECK only.

Consensus logic (single-lead path):
- requires exactly 1 agreement (lead model)
- if lead model fails or times out: returns `NO_TRADE` with explicit trace

### 6.5 Final AI response
Returns structured payload including:
- rail suggestion
- entry/tp/time windows
- confidence/alignment
- safety tags
- consensus stats
- prompt refs
- provider models used
- full trace json for auditability


## Step 7: Brain Applies Final Trade Scoring, Risk Intelligence, and Decision Engine
Even with AI approval, Brain still applies final business and risk logic.

### 7.1 Trade score gate
If total score below threshold:
- `NO_TRADE`

### 7.2 Pre-Execution Risk Intelligence (new live gate)
After score passes, Brain runs a risk-intelligence layer before the final decision engine:

- Impulse Exhaustion Guard (`SAFE` / `CAUTION` / `BLOCK`)
- Liquidity Sweep Detector
- Risk classifier (`SAFE` / `CAUTION` / `BLOCK`) with `riskScore`, `riskFlags`, `sizeModifier`
- Dynamic Session Risk modifier lookup (session-specific multiplier with safety bounds)
- Execution mode selector (`SINGLE_ENTRY`, `STAGGERED`, `BUY_STOP`, `STAND_DOWN`)

Timeline observability (live):
- risk-intelligence decision events are emitted for auditability

Hard behavior:
- `BLOCK` risk level -> immediate `NO_TRADE`
- `rotation mode = STAND_DOWN` -> immediate `NO_TRADE`
- effective sizing passed to decision engine uses: `pretableSizeModifier * dynamicSessionModifier`
- dynamic session modifier affects **size only**, never legality

Implementation note from current code:
- Dynamic Session Risk currently provides bootstrap/bounded modifiers in live flow via `GetModifier(...)`.
- Methods for recording waterfall/success outcomes and updating modifiers exist in service code, but those feedback writes are not yet wired in the live polling path.

Target bounds used by refinement requirements:
- default modifier envelope: `0.45 -> 0.80`
- temporary cap reduction: if waterfall-entry pressure is high, session cap can be reduced (for example max `0.60`)

### 7.3 Decision engine (final authority)
Decision engine applies many hard protections including:
- capital-protected blocking
- waterfall and panic veto
- first-leg-ban logic
- de-escalation-risk lock logic
- live-impulse ban
- top-liquidation ban
- structural-breakdown ban
- bottom-permission hard gate (dual-path — see below)
- session-specific TP and expiry constraints (adaptive by session)
- bucket sizing and minimum grams capacity checks

Output is either:
- `NO_TRADE`, or
- `ARMED` trade with full details (rail, entry, tp, grams, expiry, reason, tags)

Important: AI does not override this layer.

### 7.7 Hard execution legality laws (non-negotiable)
- No market buys:
   - MT5 execution path is restricted to `BUY_LIMIT` / `BUY_STOP`.
   - Any invalid market-buy suggestion must be converted to a valid pending plan or rejected as `NO_TRADE`.
- Pending-before-level law:
   - `BUY_STOP` must satisfy `entryPrice > currentAsk`.
   - `BUY_LIMIT` must satisfy `entryPrice < currentBid`.
   - Violations are rejected (`NO_TRADE`) and never auto-converted into market execution.

### 7.3 Bottom Permission — Dual Path
The bottom-permission gate now supports two legal paths instead of one:

**Path A — Reversal (existing):**
- H1 sweep + reclaim confirmed
- M15 base (overlapping candles, compression ≥ 2, or ≥ 4 for London)
- M5 compression (≥ 6 candles, contracting range)
- Momentum: RSI(M15) > 35

**Path B — Continuation (new):**
- H1 bullish context intact (H1 close above MA20)
- M15 compression base (≥ 3 candles, or ≥ 4 for London)
- M5 entry alignment (compression + ≥ 3 candles)
- No FAIL threat (ADR usage ≤ 85%)
- No waterfall signature (no panic drop, no concurrent expansion + ATR expansion)
- No hazard conflict (no high-impact US risk window)
- NY-specific: additional spread guard (strictest spike check)

This allows clean pullback continuations to trade without requiring a dramatic H1 sweep, while keeping all anti-waterfall protections intact.

### 7.4 Blocked Valid Setup Tagging
When a setup passes scoring but is blocked by the bottom-permission gate, the system:
- emits a blocked-valid-setup timeline event with full context
- tags the cause, score, session, waterfall risk, and bottom-permission reason
- marks it as a study candidate in timeline/log data

This creates a clean handoff for later STUDY analysis workflows.

### 7.5 Session-Adaptive Risk
The decision engine applies adaptive risk by session rather than binary block/allow:

**Japan / India:** highest automation freedom, standard thresholds
**London:** stronger structure confirmation (M15 compression threshold raised)
**New York:** strictest spike/liquidity checks (additional spread guard at bottom permission)
**Friday:** reduced size, tighter expiry, higher caution (existing behavior)

### 7.6 Decision Parameterization (session TP/expiry)
Decision engine applies session-aware targets:

- TP model remains session-capped (adaptive cap, max 18 USD distance)
- expiry bands are session-specific in live path:
   - Japan/India: 45-60m band (default implementation point around 52m)
   - London: 30-45m band (default implementation point around 37m)
   - New York: 20-30m band (default implementation point around 25m)


## Step 8: Post-Decision Execution Legality Gates
Before routing, live flow runs final execution legality gates:

### 8.1 Capital Utilization Gate
- validates affordability against cash and authoritative/MT5 buy price
- can resize grams downward (`RESIZE_REQUIRED`) or reject (`NO_TRADE`) when cash is insufficient
- emits a capital-utilization timeline event
- capital basis is free cash (AED) only for buy sizing; existing physical-gold inventory is treated as separate holdings, not new-buy cash

### 8.2 Portfolio Exposure Gate
- rejects when projected open exposure exceeds symbol cap (25% of equity, converted to grams)
- emits an exposure-rejection timeline event when blocked

Only orders that pass both gates can proceed to routing.

## Step 8b: Micro Rotation Mode (live-experience mode)
Special runtime mode for small-balance live validation with full safety stack retained.

Operational intent:
- uses free cash for new buys (example operating scenario: `2237.42 AED` free cash)
- keeps existing inventory (example: `2292g`) as separate holdings
- validates pending-order placement quality, TP/expiry enforcement, slips, and ledger flow

Mode rules:
- one active pending plan at a time
- `BUY_LIMIT` / `BUY_STOP` only
- TP mandatory
- expiry mandatory
- ladder disabled initially

Rollout phases:
- Phase 1: single pending, no ladder, verify slips/ledger/expiry behavior
- Phase 2: engine may select `BUY_LIMIT`, `BUY_STOP`, or `STAND_DOWN`
- Phase 3: laddering can be re-enabled only after confidence criteria


## Step 9: Routing Decision (Auto Trade Toggle + Execution Mode)
If final decision is allowed (`ARMED`) and post-decision gates pass, Brain routes based on two factors:

### 9.1 Auto Trade Toggle
The system has a client-controlled **Auto Trade toggle** (default: **OFF**):

- **OFF (default):** ALL ARMED trades go to the approval queue, requiring manual client approval. This is the safe default until the client is comfortable.
- **ON:** ARMED trades are routed directly to MT5 for execution (subject to execution mode below).

The toggle can be controlled from the Risk screen in the app.

### 9.2 Execution Mode
Even when Auto Trade is ON, the execution mode controls routing:
- `AUTO`: direct MT5 pending queue
- `HYBRID`: direct MT5 for configured sessions (default: Japan + India), approval queue for others
- `MANUAL`: always requires manual approval

In manual path:
- trade appears in approvals queue
- nothing executes until explicit approval


## Step 10: Client Final Control (Approve or Reject)
Brain exposes approval actions:
- `approve`: moves trade from approval queue into MT5 pending queue
- `reject`: removes trade from approval queue

So client has final say before execution when Auto Trade is OFF or manual/hybrid routing is active.

This is the explicit handover checkpoint you requested.


## Step 11: MT5 Pulls and Executes Released Trades
EA polls `/mt5/pending-trades`.

If trade exists:
- EA validates it again with local risk guards
- EA places BUY_LIMIT or BUY_STOP
- reports status back (`ORDER_PLACED`, then fill/TP/fail states)

Brain receives status callbacks and updates ledger/notifications/timeline.


## Step 12: Protection Loops During Runtime
System keeps protective controls active continuously:
- cancel-pending control can be consumed by EA and applied immediately
- high-risk waterfalls can clear pending queue and trigger cancel signals
- repeated waterfall failures trigger temporary study lock
- stale/no snapshot conditions stop cycle execution

### 12.1 Global Panic Interrupt
A client-triggered global panic interrupt is available from the Risk screen:

Trigger conditions (use when any of these are detected):
- FAIL threatened
- sudden liquidation pattern
- spread explosion
- macro shock confirmation
- multiple high-risk signals together

Actions:
- cancels ALL pending orders immediately
- sends cancel signal to MT5 EA
- logs `PANIC_INTERRUPT_TRIGGERED` to timeline

This applies in **all sessions**.

---

## Prompt and AI Governance (What Is Actually Used)

## Master prompts used by providers
- `prompts/master_prompt.md`
- `prompts/master_prompt_chat_gpt.md`
- `prompts/master_prompt_grok.md`
- `prompts/master_prompt_perplexity.md`

Provider selection logic chooses role-specific master prompts where relevant.

## Short-stage prompts used in AI pipeline
- `prompts/short_prompt_news.md`
- `prompts/short_prompt_analyze.md`
- `prompts/short_prompt_validate.md`
- `prompts/short_prompt_study.md`
- `prompts/short_prompt_self_crosscheck.md`
- `prompts/short_prompt_captial_utilization.md`

Current usage split:
- Live decision path: single-lead model; pre-stage (`news/analyze`) and validation (`validate/study/self_crosscheck/capital_utilization`) AI rounds are removed from live execution.
- Async study/governance path: these short prompts remain available for STUDY / SELF CROSSCHECK style flows.

---

## 24-Module Engine Map

The system roadmap defines a 24-module target architecture.
Current code implements a core subset in the live path, while other modules remain planned.

### Current Code Status (Implemented)
1. CAPITAL UTILIZATION — implemented via ledger/runtime sizing and guard checks
2. NEWS — implemented economic-news gate
3. PATTERN DETECTOR — implemented deterministic detector with required classes and schema; runs in both live and replay paths, emitting `PATTERN_DETECTOR_RESULTS` timeline events
4. VALIDATE — implemented scoring/validation gates in decision flow
5. SLIPS — implemented for BUY and TP sell fills, with ledger updates
6. Core decision/routing spine — implemented (`NO_TRADE`/`ARMED`, Auto Trade toggle, execution modes, approvals, panic interrupt)
7. BLOCKED_VALID_SETUP_CANDIDATE tagging — implemented in both live and replay paths; emits study-candidate timeline events when a setup passes scoring but is blocked by the bottom-permission gate
8. AI Gate — implemented in AI worker (`aiworker/app/services/ai_gate.py`):
   - data freshness gate (strict in LIVE mode; skipped for replay cycles — cycle-relative freshness only)
   - risk-state block gate
   - per-session call budget with automatic session-change reset
9. Single Lead Model — live path uses one lead reasoning model; others reserved for STUDY/SELF CROSSCHECK
10. MARKET_REGIME_DETECTED logging — `ema50H1`, `ema200H1`, `rsiH1` are emitted as explicit fields in the timeline log payload for both live and replay cycles
11. Capital Utilization Gate — live post-decision affordability gate (approve/resize/reject) with timeline logging
12. Pre-execution risk-intelligence stack — Impulse Exhaustion Guard + Liquidity Sweep Detector + risk classifier + execution mode selector + dynamic session risk modifier lookup, with timeline events
13. Portfolio Exposure Gate — live post-decision exposure cap check (25% equity in grams) with `SYMBOL_EXPOSURE_REJECTED` event on block
14. Hard pending-only execution law — live/EA path constrained to `BUY_LIMIT` and `BUY_STOP`; pending-before-level checks enforced in decision layer
15. Micro Rotation Mode controls — runtime toggle with single-pending cap behavior and no-ladder-first operating profile

### Planned / Partial
The following map items are target modules and not fully implemented as standalone production modules yet:
- VERIFY
- ANALYZE (as separate module)
- TABLE (as separate module)
- MANAGE
- RE ANALYZE
- SESSION SIMULATOR
- SESSION TRANSITION GUARD
- LIQUIDITY MAP ENGINE
- LIQUIDITY TRAP DETECTOR
- DATA LOGGER (as separate analytics module)
- TRADE JOURNAL ANALYZER
- STUDY automation consumer loop
- COMPARE / COMPARE-RESEARCH / CROSS CHECK
- SELF CROSSCHECK
- REGRESSION TEST
- ENGINE HEALTH CHECK
- GENERATE MASTER PROMPT

### A) Execution Spine (Live Path)
1. CAPITAL UTILIZATION — compute usable capital, split C1/C2, enforce slot/bucket discipline
2. VERIFY — verify Telegram/external items, classify credibility and pipeline impact
3. NEWS — classify macro/geo/hazard regime, decide tradable vs protected environment
4. PATTERN DETECTOR — live pattern recognition
5. ANALYZE — build current/next-session structure map, define S1/S2/R1/R2/FAIL
6. TABLE — compile legal executable order rows, size grams, TP, expiry, profit math
7. VALIDATE — audit table legality, same-session realism, expiry, sizing
8. MANAGE — monitor live/pending trades, tighten TP, cancel zombie orders
9. RE ANALYZE — reassess live structure after orders exist
10. SLIPS — generate buy/sell slips, update ledger, log cap breach/shop correction

### B) Forecasting / Guard Modules
11. SESSION SIMULATOR — pre-session forecast
12. SESSION TRANSITION GUARD — detect handover volatility/false transitions
13. LIQUIDITY MAP ENGINE — map magnets, sweep zones, liquidity pools
14. LIQUIDITY TRAP DETECTOR — detect fake breakouts/stop hunts/first-leg traps

### C) Learning / Refinement
15. DATA LOGGER — record structured engine outputs and execution history
16. TRADE JOURNAL ANALYZER — summarize trade performance by session/setup/result
17. STUDY — post-mortem learning and engine refinement (processes BLOCKED_VALID_SETUP_CANDIDATEs)

### D) Cross-AI / Governance
18. COMPARE — compare two AI answers on same topic
19. COMPARE-RESEARCH — targeted external research support
20. CROSS CHECK — cross-AI synthesis
21. SELF CROSSCHECK — highest-level self-audit, detect profit leaks/paranoia
22. REGRESSION TEST — test refined engine against historical scenarios
23. ENGINE HEALTH CHECK — overall readiness audit

### E) Backup / Integrity
24. GENERATE MASTER PROMPT — rebuild full engine spec, detect drift

---

## Decision Outcomes You Will See
At cycle end, system lands in one of these practical outcomes:

1. `NO_TRADE`
Reason examples:
- structure failed
- news blocked
- AI consensus failed
- score too low
- decision engine hard gates blocked

2. `BLOCKED_VALID_SETUP_CANDIDATE`
Special case: setup passed scoring but was blocked by bottom-permission gate.
System emits a timeline study-candidate event for downstream analysis.

3. `TRADE_APPROVED` routed to approval queue
- waiting for your approve/reject action
- always used when Auto Trade toggle is OFF

4. `TRADE_APPROVED` routed to MT5 queue
- eligible for immediate EA pull and placement
- only when Auto Trade toggle is ON and execution mode permits

5. Post-release execution states
- order placed
- buy triggered
- TP hit
- failed/rejected/canceled

---

## Non-UI Requirement Alignment Notes
The client refinement docs also define UI behavior in detail; those requirements are intentionally excluded from this architecture document by scope.

Non-UI alignment captured here includes:
- pending-only buy execution (`BUY_LIMIT` / `BUY_STOP`)
- no-market-buy enforcement
- pending-before-level legality checks
- configurable minimum grams (no forced 100g floor)
- micro live-experience mode behavior and phased rollout
- PRETABLE + pattern hard-gate interaction
- free-cash-only capital utilization basis for new buys
- unified timeline/logging fields used by execution + study flows

---

## What This Means for Client Intent Alignment
The implemented system is not a single AI auto-fire flow.
It is a layered control stack:

- deterministic structure rules first
- pattern detection
- news and risk gates
- single-lead AI live path (with deterministic AI Gate and one-shot failover)
- final non-AI decision engine with dual bottom-permission paths
- risk intelligence and rotation optimization before final decisioning
- post-decision capital + exposure legality gates before routing
- Auto Trade toggle — default OFF, client enables when ready
- optional manual human approval checkpoint
- local MT5 validation before execution
- global panic interrupt for emergency protection

So operationally, it is designed to prioritize protection and controlled execution over uncontrolled AI autonomy.

The key operating principle:
> **The system must automate the monitoring itself and automatically execute trades if the Auto Trade toggle is on and whenever all core laws pass, across all sessions, using adaptive safety rather than paranoia.**

---

## Optional Appendix: Replay/Backtest Path (Same Core Logic)
There is also a replay path where MT5 history is fetched/imported and run through the same decision pipeline.
This is used for testing behavior consistency without live order execution.

Replay mirrors the core live decision path and key gates, with important scope notes:
- Market regime detection — `MARKET_REGIME_DETECTED` timeline event includes `ema50H1`, `ema200H1`, `rsiH1`
- Rule engine (structural validity)
- **Pattern Detector** — runs after regime/rule-engine, emits `PATTERN_DETECTOR_RESULTS` timeline events
- News gate (or bypass with `ignoreNewsGate=true` for pure backtest runs)
- AI analysis (or mock AI) — AI Gate uses **cycle-relative freshness** in replay mode: the system-time comparison is skipped because snapshot timestamps are intentionally historical. The gate detects replay mode from the `cycleId` prefix (`replay_`).
- Trade scoring
- Decision engine (dual-path bottom permission, all hard gates)
- **BLOCKED_VALID_SETUP_CANDIDATE** tagging — emitted when a setup passes scoring but is blocked by the bottom-permission gate, same as the live path

Current replay/live difference in code:
- The full risk-intelligence timeline stack implemented in live polling is not currently mirrored as standalone events in replay.
- Live-only post-decision queue routing gates (capital + exposure + approval/MT5 routing mechanics) are not executed for real order flow in replay.

Real order execution is always disabled in replay mode. All timeline events are tagged with `replayMode: true`.

---

## Final Summary
From MT5 intake to final release, the live core follows this rule:

No single layer can force a trade by itself.
A trade must pass:
1) structural validity,
2) pattern safety check,
3) risk/news gates,
4) AI Gate (freshness, risk state, session budget),
5) AI single-lead model decision,
6) trade scoring + risk intelligence (risk class, rotation mode, session modifier),
7) decision engine protections (including dual-path bottom permission),
8) post-decision capital utilization + portfolio exposure legality gates,
9) routing policy and Auto Trade toggle check,
10) and (if Auto Trade OFF or manual path) your explicit approval.

That is the real, code-backed end-to-end architecture currently running in this project.

