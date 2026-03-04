PHYSICAL XAUUSD.GRAM EXECUTION SYSTEM

Full Developer Blueprint (MT5 EA + Backend + Dashboard)

بسم الله الرحمن الرحيم


---

1) Overview & Objectives

1.1 System Description

This system is a fully automated physical-gold rotation engine that trades XAUUSD in grams using MT5 as the execution interface, while settling as physical bullion through a Dubai shop pricing model.

The system consists of:

MT5 Expert Advisor (EA): executes pending orders, monitors states, enforces expiry/auto-cancel, sends logs.

Backend Orchestrator: collects market data, runs rule evaluation, calls AI proposals, validates with Constitution Library, emits TABLE or ABORT, and commands the EA.

Constitution Library (Code): the final authority that enforces all hard rules and vetoes illegal proposals.

Dashboard: shows state, open/pending, ledger, slip history, bans, sessions, hazards, and audit logs.


1.2 Core Objectives (Hard)

1. Shariah-compliant physical gold: buy → own → sell in profit only.


2. High rotation velocity: maximize safe rotations per session and monthly profit velocity.


3. Waterfall immunity: structurally avoid mid-air entries, panic legs, first-leg chases, liquidity traps.


4. No babysitting: automation is end-to-end with strict TABLE/ABORT outputs and auto-cancel.


5. Minimal dev questions: document is implementation-ready; where exact thresholds are not fully restated, dev must pull from Constitution / master prompts as canonical reference.




---

2) Shariah & Physical Trading Model

2.1 Allowed Operations (Strict)

Buy physical grams (via MT5 pending buy orders mapped to shop buy pricing).

Own gold post-fill (gold inventory increases).

Sell owned gold only when profitable (profit condition based on shop sell vs shop buy basis).


2.2 Forbidden Operations (Strict)

No shorts.

No leverage / margin.

No hedging.

No selling gold not owned.

No automated loss-selling.


2.3 Manual Override

“Sell at loss” is manual human override only and must never be part of automated logic, never suggested by default, and never executed automatically.



---

3) Shop & Pricing Model

3.1 Constants (Hard)

1 oz = 31.1035 g

1 USD = 3.674 AED

Shop spread = ±0.80 USD/oz

ShopBuy = Entry_MT5 + 0.80

ShopSell = Exit_MT5 − 0.80



3.2 Price Mapping

For any order proposal:

Entry_MT5 is the MT5 pending order entry level.

ShopBuy = Entry_MT5 + 0.80

TP_MT5 is the MT5 take-profit level.

ShopSell = TP_MT5 − 0.80 (for the intended exit)


3.3 Profit Calculation (When Sold)

Profit should be calculated at settlement close:

NetUSDPerOz = ShopSell − ShopBuyBasis

ProfitAED = grams × (NetUSDPerOz / 31.1035) × 3.674


Important: ledger cash credit on sell is the full sell AED amount (principal + profit); profit must be logged separately and never double-counted.


---

4) Capital Structure & Capacity Law

4.1 Buckets (Hard)

C1: primary rotation bucket.

C2: deep structural / rescue bucket.


Default split:

C1 = 80–90%

C2 = 10–20%


4.2 C2 Rules (Hard)

Only after C1 deployed in the incident.

Only at deep structural bases (BottomPermission + deep zone).

Only one C2 deployment per incident.

C2 position cannot be sold until profitable.


4.3 Capacity Formula (Hard)

Before placing any buy order, compute AED required using ShopBuy:

RequiredAED = grams × (ShopBuy / 31.1035) × 3.674

4.4 Remaining Bucket AED

Backend must maintain:

BucketAED[C1], BucketAED[C2]

ReservedAED for pending orders

AvailableAED = BucketAED − ReservedAED − SafetyReserve (if configured)


Rule: RequiredAED ≤ AvailableAED of the selected bucket must be TRUE before order placement.

4.5 Inventory Separation

Strategic inventory (e.g., 850 g) may be held as “untouched inventory” depending on current shop ledger status and the engine’s rotation plan.

Deployable free cash is distinct from existing inventory; sizing must not implicitly assume inventory liquidation.



---

5) Time Handling & Sessions

5.1 Time Conversion (Hard)

MT5 ServerTime = KSA − 50 minutes

Therefore:

KSA = ServerTime + 50 minutes

ServerTime = KSA − 50 minutes



All outputs must show:

ExpiryServer (MT5 time)

ExpiryKSA


5.2 Session Identification (Use KSA as master)

Sessions (KSA ranges):

Asia: 01:00–05:00

India: 05:00–08:00

London: 10:00–14:00

New York: 15:30–18:30
Avoid “NY open spike zone” as defined in Constitution.


Backend tags session based on KSA time (then converts to server as needed).

5.3 Session Liquidity Map (Priority)

Priority for safe rotations:

1. India


2. Asia


3. London mid


4. Late NY
Avoid:



NY open and known trap windows

Rollover spread widening windows per broker



---

6) Engine State Machine

6.1 States (Hard)

WAIT

SETUP_DETECTED

CONFIRM_STRUCTURE

EXECUTE_TRADE

CAPITAL_PROTECTED


6.2 Transition Rules

WAIT → SETUP_DETECTED

New market snapshot received + session allowed + no global hazard.


SETUP_DETECTED → CONFIRM_STRUCTURE

Candidate setup passes preliminary gates (no immediate bans) and requires confirmation.


CONFIRM_STRUCTURE → EXECUTE_TRADE

Constitution Library validates:

capacity legality

expiry legality

PatternBans FALSE

BottomPermission TRUE

session + TP/expiry caps met


Output is TABLE row(s).


Any State → CAPITAL_PROTECTED

If any of:

NEWS shock or hazard window

conflicting regime signals

PatternBan becomes TRUE

sticky bans active without unlock

uncertainty/ambiguity in inputs that affects legality



6.3 Cycle Narrative (End-to-End)

1. EA sends market snapshot (multi-timeframe data).


2. Backend runs news/hazard scan request (if required).


3. AI layer proposes candidate setup(s).


4. Constitution Library validates and finalizes:

TABLE or ABORT.



5. EA executes only if TABLE.


6. EA monitors pending/filled orders.


7. Auto-cancel triggers if required.


8. On fill/close, backend generates slip and updates ledger.




---

7) AI / Backend Architecture & Constitution Library

7.1 Components

MT5 EA

Collects ticks + OHLC across TFs

Places pending orders with expiry

Applies TP

Cancels orders per backend commands and per local safety triggers

Streams logs to backend


Backend Service

Receives EA telemetry

Maintains state machine

Calls AI proposal providers

Runs Constitution Library validation

Emits TABLE/ABORT

Updates ledger and slip store


AI Decision Layer

Provides proposals: regime tag, levels, entry/TP candidates, narrative.

Cannot override Constitution.


Dashboard

State, bans, sessions, hazards

Pending/open positions

Ledger & slips

Audit timeline


7.2 Authority Hierarchy (Hard)

Constitution prompts + Final Build Spec v1.0 define meaning.

Constitution Library (code) enforces:

capacity legality

expiry legality

PatternBans & BottomPermission booleans

session-specific expiry/TP/size caps


AI outputs are advisory only and must be validated.


7.3 Interaction Pattern

1. Backend requests AI proposal (can be multi-provider).


2. Backend passes proposal + live market metrics into Constitution Library.


3. Constitution Library returns:

ACCEPT with finalized TABLE row(s)

TRIM (adjusted to comply)

ABORT with reason




EA must never trade without finalized TABLE.


---

8) TABLE / ABORT Contract

8.1 Mandatory Output

Every decision cycle ends with either:

TABLE (one or more rows)
or

ABORT (single reason line)


8.2 TABLE Format (Hard)

Columns (exact): Bucket(C1/C2) | OrderType(BuyLimit/BuyStop) | Grams | EntryMT5 | ShopBuy | TP_MT5 | ShopSell | ExpiryServer | ExpiryKSA | Session | RegimeTag

Rules:

No ranges inside table fields.

Every row must have explicit grams and explicit expiry times.


8.3 ABORT Format (Hard)

TABLE ABORTED — <Reason>

Common reasons:

FIRST_LEG_BAN

DEESCALATION_RISK

LIVE_IMPULSE

TOP_LIQUIDATION

STRUCTURAL_BREAKDOWN

NEWS_HAZARD

BOTTOMPERMISSION_FALSE

CAPACITY_EXCEEDED

STRUCTURE_UNCLEAR

SESSION_BLOCK

ROLLOVER_RISK



---

9) Pattern Bans & BottomPermission (Detailed)

> Where exact numeric thresholds (ATR multipliers, wick ratios, RSI gates) are not restated here, dev must implement them from Constitution / master prompts as canonical truth.



9.1 TOP_LIQUIDATION BAN

Developer implementation must detect:

Parabolic rally behavior in H4/H1

Expansion beyond normal ATR

Crowded sentiment + late-stage push

Failure signatures (break of parabolic base / inability to reclaim)


If TRUE → ABORT new entries.

9.2 STRUCTURAL_BREAKDOWN BAN

Detect:

Break of key H4 support

H1 closes below and cannot reclaim

No M15/M5 base supporting a long


If TRUE → ABORT.

9.3 LIVE_IMPULSE BAN

Detect:

H1 or M30 candle ≥ ATR threshold and no reclaim structure

“Impulse in motion” state


If TRUE → ABORT new entries until cooldown.

9.4 BottomPermission (Hard Gate)

BottomPermission TRUE requires:

1. H4 context supportive (not breakdown).


2. H1 sweep + reclaim of meaningful swing low.


3. M15 base structure (base candles, higher low behavior per Constitution).


4. M5 compression:

≥ N overlapping candles (N per Constitution; minimum baseline is 6)

contracting range

no fresh impulse candle



5. Momentum confirmation as per Constitution (RSI/MACD gates if specified).



If BottomPermission FALSE → ABORT.


---

10) Behavioral Risk Modules

10.1 FIRST_LEG_BAN

Trigger

Strong impulse already underway

Entry request is late (distance moved ≥ threshold)

No reclaim/base exists

Optional: Telegram consensus aligned


Unlock

H1 sweep + reclaim after ban activation

M15 base

M5 compression ≥ 6

News safe


Time-decay Unlock

After N H1 candles without a new impulse (N defined in Constitution; baseline 3).


10.2 DEESCALATION_RISK (War-premium unwind)

Trigger

WarPremium status shifts to fading/liquidating (from news validator)

Price still elevated vs structure

Crowd sentiment still bullish


Behavior:

Force CAPITAL_PROTECTED unless deep confirmed base.


Unlock

Structural flush completed

BottomPermission TRUE

Sentiment normalized

News safe


10.3 SECOND-LEG DETECTOR

Detect:

First impulse completed

30–60% pullback

H1 reclaim

M15 base + M5 compression Tag: SecondLeg Priority: high.


10.4 LIQUIDITY VACUUM DETECTOR

Detect:

ATR(M15) contraction + ATR(M5) contraction

Coil/compression

Spread stable Differentiate “vacuum” vs “live impulse”.


10.5 MICRO-LIQUIDITY TRAP DETECTOR

Detect:

Level sweep

Wick % criteria

Reclaim

Compression base Tag and tighten expiry/cancel logic.


10.6 LIQUIDITY HEATMAP

Sources:

H1 swing highs/lows

Session highs/lows

Round numbers (.00/.50) Score: LOW/MED/HIGH
Prefer HIGH-quality zones.


10.7 SESSION LIQUIDITY MAP

Session-specific aggression guidance enforced by Constitution caps.


---

11) Entry Engines – BuyLimit & BuyStop (Exact Rules)

11.1 BuyLimit Engine

Inputs required

BottomPermission TRUE

All bans FALSE

No news hazard

Capacity legal


Entry selection

Choose support zones from H1/H4 swings and heatmap levels.


Buffer

Buffer = max(1.5 USD, 0.2 × ATR_M15)


TP logic

BaseTP = 0.8 × ATR_M15 × 3

Apply session caps (from Constitution; e.g., India 10, London/NY 12, etc.)


Expiry

Japan/India: 30m

London: 25m

NY: 20m

Friday: 15m

Late compression: 15m max

Must end before hazard windows, rollover risk, and session transitions as defined.


11.2 BuyStop Engine

Allowed only when

H1 above MA20

RSI window as defined in Constitution

M15 compression under lid

Prior reclaim confirmed

BottomPermission TRUE

No LIVE_IMPULSE


Entry offset

Offset = max(2 USD, 0.25 × ATR_M15)


TP

TP distance = EntryOffset × 3.5 capped by session rules.


Size bands

Must follow C1 size bands by session (exact % in Constitution).

Only one active BuyStop at a time; no stacking.


Dev instruction

Do not invent thresholds; implement exact numeric bands and RSI windows from Constitution prompts.



---

12) Expiry, Monitoring & Auto-Cancel

12.1 Expiry Rules (Hard)

Must be explicit absolute time in server and KSA.

Must follow session expiry caps.

Must not overlap hazard windows.

Must shorten in strong expansion / trap states per Constitution.


12.2 Monitoring Loop

EA monitors:

every tick + every new candle for TFs used

periodic heartbeat to backend


12.3 Auto-Cancel Triggers

Cancel pending orders if:

Spread spikes beyond threshold

ATR collapses (dead volatility)

LIVE_IMPULSE flips TRUE

PatternBan flips TRUE

Base/compression invalidates

Hazard window begins

Session transition risk begins

Rollover risk window begins


Log cancel reason and set engine state accordingly (often back to WAIT or CAPITAL_PROTECTED).


---

13) Telegram Sentiment & External Signals

13.1 Telegram Processing

Read many channels

Score signals

Maintain Top-10 list

Compute:

BUY_CONSENSUS%

SELL_CONSENSUS%



13.2 Governance

Telegram:

never overrides bans

never overrides BottomPermission

never overrides capacity

cannot force a trade


If consensus >80%:

tighten filters

possibly trim size downward within allowed bands

never increase risk beyond Constitution.



---

14) Risk States & CAPITAL_PROTECTED

14.1 Triggers

Force CAPITAL_PROTECTED:

war shocks

macro shocks

news hazard proximity

contradictory signals

sticky bans without unlock

structure ambiguity affecting legality


14.2 Behavior in CAPITAL_PROTECTED

No new orders

Cancel unsafe pending orders

Maintain existing positions (no averaging)

Wait for unlock conditions and new base



---

15) Logging, Slips & Ledger

15.1 Required Logs (Per Cycle)

Log:

timestamps (server + KSA)

session tag

regime tag

all bans states

BottomPermission state

capacity calculation inputs/outputs

AI proposals received

Constitution Library decision (ACCEPT/TRIM/ABORT) + reasons


15.2 Slip Format (Per Fill / Close)

Include:

trade ID / ticket

EntryMT5, ShopBuy

TP_MT5, ShopSell

grams

timestamps (server + KSA)

before balances (cash AED, gold g)

after balances (cash AED, gold g)

realized profit AED (logged separately)

full sell AED credited (principal+profit)


15.3 Ledger Requirements

Maintain:

Cash AED

Gold grams

Realized P&L

Unrealized mark-to-market (optional informational)

Any agreed profit-share fields (e.g., developer 2% after expenses) as separate accounting fields, never mixing with trading ledger.



---

16) Implementation Notes & Phasing

16.1 Phase Plan

Phase 1

MT5 EA basic framework

Market data feed

Capacity law

TABLE/ABORT contract

Basic bans + BottomPermission (from Constitution definitions)

BuyLimit engine

Ledger + slip logging


Phase 2

Behavioral modules:

FIRST_LEG_BAN

DEESCALATION_RISK

SECOND-LEG

Vacuum

Micro-trap

Heatmap

Session map



Phase 3

Telegram integration

Dashboard

Study / audit tools (SELF-CROSSCHECK if required)

Multi-AI proposal orchestration (Grok/Perplexity/others)


16.2 Edge Cases (Defaults)

On ambiguity → ABORT.

If time mapping uncertain → ABORT.

If news hazard uncertain → ABORT.

If data missing for a required boolean → ABORT.

Never place multiple overlapping pending orders unless explicitly allowed by Constitution.



---

Final Developer Instruction (Non-negotiable)

Use this document as the top-level system blueprint.

Implement exact numeric gates and indicator conditions from the Constitution / master prompts as canonical truth.

Constitution Library must validate and veto everything.

EA executes only finalized TABLE rows and never trades on narrative.



---

Please ensure:

FIRST_LEG_BAN and DEESCALATION_RISK are two independent persistent boolean flags.

Each has explicit ON rules, explicit OFF (unlock) rules, and time-decay OFF (so they never stay sticky forever).

They must be evaluated before generating any TABLE row:

If either is TRUE and unlock not satisfied → ABORT with the exact reason.


Unlock must require the full reclaim/base proof (H1 sweep+reclaim + M15 base + M5 compression + NEWS safe), and de-escalation unlock also requires flush completed + sentiment normalized.

These flags must never override Shariah/capacity/bans/BottomPermission; they only gate trading permission.


So: treat it as “same feature, upgraded rules + correct persistence + clean unlocks.”