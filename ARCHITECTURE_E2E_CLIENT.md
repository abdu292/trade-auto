# Trade Auto End-to-End Architecture (Client View)

## Purpose of This Document
This document explains how the core Trade Auto system works from end to end, in business language, based on the actual code.

It is written to help confirm whether the real behavior matches your intended operating model:
- strict buy-only physical gold flow
- strong risk protection
- AI-assisted decisions with verification layers
- final client control before execution (approve/reject)

Scope note:
- This covers core backend + MT5 + AI workflow.
- UI feature details are intentionally excluded.

---

## The System in One Plain-English Picture
The system works like a guarded decision factory:

1. MT5 continuously sends live market state to Brain.
2. Brain runs strict structural and risk filters first.
3. Only valid opportunities are sent to the AI worker.
4. AI is run with committee + validation steps and prompt stages.
5. Brain applies final capital/risk/order rules.
6. Trade is either rejected, queued for client approval, or sent to MT5 queue (based on execution mode).
7. Client can approve or reject queued trades.
8. MT5 executes only after final release and sends status back.
9. Brain records outcomes and adjusts operational state/learning data.

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
- minimum size (100g)
- only BUY_LIMIT or BUY_STOP
- blocks execution if order is marked high-risk or capital-protected


### 2) Brain (`brain`)
What it does:
- central decision engine and orchestration layer
- receives market snapshots
- runs rule engine, regime checks, news checks, AI orchestration, scoring, and final decisioning
- routes orders either to MT5 queue or to manual approval queue
- exposes endpoints for approval (`approve/reject`) and monitoring
- logs full timeline events for each cycle


### 3) AI Worker (`aiworker`)
What it does:
- builds AI context from market + telegram + macro context
- runs multi-provider AI committee
- applies extra validation stages
- returns structured signal + safety and trace metadata
- returns `NO_TRADE` when confidence/consensus/validation conditions fail


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


## Step 5: Economic News Gate
If structural setup passes, Brain checks economic-news risk.

If news gate blocks:
- cycle ends `NO_TRADE`
- no AI order candidate is released


## Step 6: AI Analysis Is Requested (Multi-Stage, Multi-Provider)
Only after rule + news gate pass.

Brain sends full market payload to AI worker and logs this event.

AI worker then runs this pipeline:

### 6.1 Context build
- market context
- telegram context
- macro context
- risk/regime hints

### 6.2 Pre-table hard block
If pre-classification marks risk as blocked:
- immediate `NO_TRADE`

### 6.3 Pre-stage AI passes
Runs staged checks with dedicated short prompts:
- `short_prompt_news.md`
- `short_prompt_analyze.md`

### 6.4 Committee decision
Runs configured analyzers/providers (OpenRouter/OpenAI/Grok/Perplexity/Gemini as configured).

Consensus logic:
- requires minimum agreement count
- requires entry-price agreement tolerance

If committee fails:
- returns `NO_TRADE`
- if no usable output at all, falls back to deterministic fallback signal logic with explicit trace

### 6.5 Validation stages (cross-check)
Runs additional validation prompts:
- `short_prompt_validate.md`
- `short_prompt_study.md`
- `short_prompt_self_crosscheck.md`
- `short_prompt_captial_utilization.md`

Rule in code:
- minimum validation passes required (3)
- else trade is blocked (`NO_TRADE`)

### 6.6 Final AI response
Returns structured payload including:
- rail suggestion
- entry/tp/time windows
- confidence/alignment
- safety tags
- consensus stats
- prompt refs
- provider models used
- full trace json for auditability


## Step 7: Brain Applies Final Trade Scoring and Decision Engine
Even with AI approval, Brain still applies final business and risk logic.

### 7.1 Trade score gate
If total score below threshold:
- `NO_TRADE`

### 7.2 Decision engine (final authority)
Decision engine applies many hard protections including:
- capital-protected blocking
- waterfall and panic veto
- first-leg-ban logic
- de-escalation-risk lock logic
- live-impulse ban
- top-liquidation ban
- structural-breakdown ban
- bottom-permission hard gate
- session-specific TP and expiry constraints
- bucket sizing and minimum grams capacity checks

Output is either:
- `NO_TRADE`, or
- `ARMED` trade with full details (rail, entry, tp, grams, expiry, reason, tags)

Important: AI does not override this layer.


## Step 8: Routing Decision (Auto vs Manual Approval)
If final decision is allowed (`ARMED`), Brain routes by execution mode:
- direct MT5 pending queue, or
- manual approval queue

In manual path:
- trade appears in approvals queue
- nothing executes until explicit approval


## Step 9: Client Final Control (Approve or Reject)
Brain exposes approval actions:
- `approve`: moves trade from approval queue into MT5 pending queue
- `reject`: removes trade from approval queue

So client has final say before execution when manual/hybrid routing is active.

This is the explicit handover checkpoint you requested.


## Step 10: MT5 Pulls and Executes Released Trades
EA polls `/mt5/pending-trades`.

If trade exists:
- EA validates it again with local risk guards
- EA places BUY_LIMIT or BUY_STOP
- reports status back (`ORDER_PLACED`, then fill/TP/fail states)

Brain receives status callbacks and updates ledger/notifications/timeline.


## Step 11: Protection Loops During Runtime
System keeps protective controls active continuously:
- cancel-pending control can be consumed by EA and applied immediately
- high-risk waterfalls can clear pending queue and trigger cancel signals
- repeated waterfall failures trigger temporary study lock
- stale/no snapshot conditions stop cycle execution

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

These are used as staged policy layers in the AI worker flow, not just documentation.

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

2. `TRADE_APPROVED` routed to approval queue
- waiting for your approve/reject action

3. `TRADE_APPROVED` routed to MT5 queue
- eligible for immediate EA pull and placement

4. Post-release execution states
- order placed
- buy triggered
- TP hit
- failed/rejected/canceled

---

## What This Means for Client Intent Alignment
The implemented system is not a single AI auto-fire flow.
It is a layered control stack:

- deterministic structure rules first
- news and risk gates
- multi-provider AI with consensus and validation
- final non-AI decision engine
- optional manual human approval checkpoint
- local MT5 validation before execution

So operationally, it is designed to prioritize protection and controlled execution over uncontrolled AI autonomy.

---

## Optional Appendix: Replay/Backtest Path (Same Core Logic)
There is also a replay path where MT5 history is fetched/imported and run through the same decision pipeline.
This is used for testing behavior consistency without live order execution.

---

## Final Summary
From MT5 intake to final release, the live core follows this rule:

No single layer can force a trade by itself.
A trade must pass:
1) structural validity,
2) risk/news gates,
3) AI consensus + validation,
4) decision engine protections,
5) routing policy,
6) and (if manual path) your explicit approval.

That is the real, code-backed end-to-end architecture currently running in this project.
