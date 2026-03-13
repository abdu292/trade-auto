بسم الله الرحمن الرحيم

TO: Automation Developer
SUBJECT: “MONDAY WAR-SPIKE HARVEST” — Full-Stack Implementation Spec (Tonight-Ready)
GOAL: Monetize ≥75% of any +700 USD expansion using FULL CASH (within shop cap/spread), while preventing a first-leg waterfall entry (NO 5195-month trap repeat).
ACCOUNT STATE (LOCKED INPUT):
- Cash = 1,445,819.42 AED
- Gold = 0.00 g
- Physical rules: BUY then SELL only. No shorts. No market orders. No realized loss. Single TP per MT5 entry.
- Shop spread: ±0.80 USD/oz
  - Shop Buy = MT5 + 0.80
  - Shop Sell = MT5 − 0.80
- Constants: 1 oz = 31.1035 g ; 1 USD = 3.674 AED

────────────────────────────────────────────────────────────
0) WHAT “75% OF HIKE” MEANS OPERATIONALLY (NO CONFUSION)
────────────────────────────────────────────────────────────
User target example:
- If gold hikes +700 USD/oz tomorrow:
- With full deployable grams G, theoretical gross move value ≈ G * (700 USD/oz) * (1 oz / 31.1035g) * 3.674 AED/USD
- Target = monetize ≥75% of the move, i.e. capture ≥525 USD/oz equivalent across the deployed grams (NOT necessarily one single trade).
THIS IS ONLY POSSIBLE BY:
(A) Scaling INTO strength during WAR_PREMIUM expansion (pyramiding using Buy Stops on micro-lids),
(B) Taking multiple realized profits at higher tiers during the same expansion (profit-lock ladder),
(C) HARD-KILL on any de-escalation/waterfall regime shift BEFORE entries fill.

No “wait for perfect base” only. We must have an “expansion harvest rail”.

────────────────────────────────────────────────────────────
1) CORE ARCHITECTURE (AI + DATA + EXECUTION)
────────────────────────────────────────────────────────────
We will run a deterministic state machine in the Executor (EA/Service).
AIs feed signals; ONLY the executor can place/cancel orders.

COMPONENTS:
A) Grok (PRIMARY LIVE NEWS + RUMOR FEED)
   - Output: MODE + CONFIDENCE + KEYWORDS + TTL
   - Must treat rumors as valid (gold moves on rumors).
B) Telegram monitor (SECONDARY CONFIRMATION + “BUZZ SCORE”)
   - Output: DEESC_BUZZ score + ESCALATION_BUZZ score (fast keyword clustering).
C) TradingView alerts (STRUCTURE TRIGGERS)
   - Output: “LID_BREAK”, “SHELF_BREAK”, “RSI_DIVERGENCE”, “ADR_EXTREME”
D) MT5 monitoring (PRICE + INDICATORS)
   - Candles: M1/M5/M15/H1; ATR(M15/H1); RSI(H1); ADR_used; Spread proxy (tick spread or bid/ask distance).
E) Executor (EA or external service controlling MT5 via bridge)
   - Enforces: WaterfallRisk gates, Mid-Air bans, expiry auto-cancel, single-TP policy, capacity math, and order staging.

CRITICAL DESIGN RULE:
- Grok sets MODE immediately (no Perplexity blocking).
- Perplexity is OPTIONAL offline/historical mapping; not in live execution path.

────────────────────────────────────────────────────────────
2) CAPITAL / CAPACITY MATH (MUST BE EXACT & DYNAMIC)
────────────────────────────────────────────────────────────
We DO NOT assume “2400 g”. We compute max grams from CASH and live MT5 price.

FUNCTIONS (use everywhere):
1) cost_per_gram_aed(mt5_price):
   shop_buy = mt5_price + 0.80
   aed_per_oz = shop_buy * 3.674
   cost_per_g = aed_per_oz / 31.1035

2) max_affordable_grams(cash_aed, mt5_price, buffer_g):
   g = floor(cash_aed / cost_per_gram_aed(mt5_price)) - buffer_g
   return max(g, 0)

REQUIRED:
- buffer_g default = 10 g (safety).
- Executor must recompute max grams every time before placing any order.
- Prevent “shop cap breach” by refusing any new order if projected cash usage > cash_aed.

NOTE:
- Each order reserves cash: reserved_aed = grams * cost_per_gram_aed(current_mt5_price_estimate)
- Maintain “reserved cash ledger” for pending orders too (to avoid over-allocation).

────────────────────────────────────────────────────────────
3) MODE STATE MACHINE (GROK-DRIVEN, RUMORS COUNT)
────────────────────────────────────────────────────────────
MODE ∈ { WAR_PREMIUM, DEESCALATION_RISK, UNKNOWN }

INPUTS:
- Grok text stream (X/rumors/headlines)
- Telegram buzz score
- Optional: TV “WAR_KEYWORD” alert channel

RULES (hard):
- If Grok detects any strong “ceasefire / talks / containment / limited response / no further escalation / mediation” keywords:
  MODE := DEESCALATION_RISK immediately (TTL 30–60 min; renewable).
- If Grok detects “new strikes / GCC hit / Hormuz risk / retaliation / escalation / missiles / drones / shipping / oil shock”:
  MODE := WAR_PREMIUM (TTL 15–30 min; renewable).
- If mixed or no strong signal:
  MODE := UNKNOWN

IMPLEMENTATION:
- Grok endpoint returns JSON:
  {
    "mode": "WAR_PREMIUM|DEESCALATION_RISK|UNKNOWN",
    "confidence": 0..1,
    "keywords": ["..."],
    "ttl_seconds": 900
  }

Executor behavior:
- If Grok payload expires (TTL) and no refresh -> MODE auto downgrades to UNKNOWN.

────────────────────────────────────────────────────────────
4) WATERFALL_RISK (MT5 PRICE-ACTION GATE, PREDICTIVE)
────────────────────────────────────────────────────────────
WATERFALL_RISK ∈ { LOW, MEDIUM, HIGH }

Compute continuously from MT5:
- ADR_used = today_range / ADR(20)
- RSI_H1
- ATR_H1, ATR_M15
- “stall”: higher highs stop while ATR remains elevated
- “first_leg_red”: big red M15 body from recent highs

SET TO MEDIUM if >=2 true:
- ADR_used >= 1.20
- RSI_H1 >= 70 AND upper wicks present (rejecting highs)
- Stall condition: no new high in last N=6 M15 bars while ATR_M15 elevated
- Telegram “de-esc buzz” rising fast (Δbuzz > threshold)

SET TO HIGH if any true:
- MODE == DEESCALATION_RISK AND (ADR_used >= 1.00 OR first_leg_red == true)
- first_leg_red == true AND breaks an intraday shelf (see Shelf logic below)
- Spread instability (if tick spread > 1.8 × median last 60m)

EFFECTS:
- LOW: Expansion pyramid allowed.
- MEDIUM: No NEW adds (no new pyramid stages). Only manage existing orders/TP.
- HIGH: CANCEL ALL PENDINGS immediately; BLOCK ALL NEW BUYS until base forms.

────────────────────────────────────────────────────────────
5) STRUCTURE LOGIC (LIDS, SHELVES, MID-AIR BAN)
────────────────────────────────────────────────────────────
A) “LID” (for Buy Stop expansion entries):
- A lid is a compression ceiling:
  - ≥3 overlapping M5 candles under a tight high, OR
  - ≥2 overlapping M15 candles under a tight high
- Break condition:
  - Candle closes above lid high, not just wick.

B) “SHELF” (for reload Buy Limits):
- A shelf is a defended support:
  - price sweeps below then RECLAIMS (close back above),
  - then RETEST holds (wick test fails to break),
  - plus M15 compression after reclaim.

C) MID-AIR BAN (hard, prevents 5195-type traps):
- Reload (Buy Limit) is FORBIDDEN unless:
  - It is within <= 10 USD of a proven shelf, AND
  - reclaim + retest + compression sequence is satisfied.
- During parabolic expansion (WAR_PREMIUM + WaterfallRisk LOW), we do NOT place “mid-range” buy limits.
  We only:
  - use Buy Stops above lids (expansion pyramid), OR
  - use Buy Limits at shelves after proof.

────────────────────────────────────────────────────────────
6) EXECUTION RAILS FOR TOMORROW (FULL CASH, SAFE)
────────────────────────────────────────────────────────────
We run TWO rails simultaneously, but governed by MODE + WATERFALL_RISK.

RAIL-B: EXPANSION PYRAMID (BUY STOPS) — This is how we monetize 75% of the hike
RAIL-A: STRUCTURE RELOAD (BUY LIMITS) — Only if shelves form cleanly, never mid-air

IMPORTANT: Each entry is a separate MT5 order with a SINGLE TP and an EXPIRY.
No orders live beyond expiry. No zombie pendings.

────────────────────────────────────────────────────────────
7) RAIL-B — EXPANSION PYRAMID SPEC (TARGET: ≥525 USD/oz CAPTURE)
────────────────────────────────────────────────────────────
Activation Conditions:
- MODE == WAR_PREMIUM
- WATERFALL_RISK == LOW
- Lid exists + break close above lid
- No de-esc keywords detected in last X minutes (X default 10)

Sizing (FULL CASH CAPABILITY, dynamic by remaining capacity):
Let Gmax = max_affordable_grams(cash, mt5_price, buffer_g=10)
We will aim to deploy up to 95–100% of Gmax ONLY IF expansion stays clean.
Deployment is staged to avoid buying the very top in one shot.

STAGES (dynamic grams; computed as fractions of Gmax):
- Stage B1: 25% of Gmax
- Stage B2: 25% of Gmax
- Stage B3: 20% of Gmax
- Stage B4: 15% of Gmax
- Stage B5 (optional): 10% of Gmax ONLY if WaterfallRisk still LOW and RSI_H1 < 72

Total if B1..B5 fill ≈ 95% of Gmax (full deploy without breaching cash).

Entry rules:
- B1: Buy Stop = lid_high + buffer
- B2: Next lid_high + buffer (after new compression forms)
- B3: Next lid_high + buffer
- etc.
buffer default = +1.0 USD (configurable 0.5–2.0 based on volatility)

TP logic (profit-lock ladder):
We need to realize profits across the run. Since we can’t sell before we buy, we do:
- Each stage has its OWN TP placed at a higher tier, so partial profits realize as price continues.

Suggested TP distances (war extreme; configurable):
- B1 TP = entry + 200 USD
- B2 TP = entry + 200 USD
- B3 TP = entry + 150 USD
- B4 TP = entry + 100 USD
- B5 TP = entry + 80 USD

Why this hits the “75% of hike” goal:
- If total hike is +700 and we deploy most grams progressively,
- average realized capture can approach 500–550 USD/oz across deployed grams if the move sustains,
- because earlier stages are filled earlier and exit much higher.

Expiry (strict; avoid being filled after regime shift):
- Each Buy Stop expires in 20 minutes (Asia) / 15 minutes (London) / 10 minutes (NY)
- If not triggered by expiry -> cancel.
- If MODE flips or WaterfallRisk becomes MED/HIGH -> cancel immediately.

Re-arm policy:
- After any TP is hit:
  - cooldown 2–3 minutes
  - require a NEW lid + break to arm next stage
  - never stack multiple unproven Buy Stops at once unless you explicitly allow “multi-arming”.
  Default: ONLY 1 pending Buy Stop at a time to reduce stale fills.

────────────────────────────────────────────────────────────
8) RAIL-A — STRUCTURE RELOAD SPEC (BUY LIMITS) — OPTIONAL SUPPORT
────────────────────────────────────────────────────────────
Activation Conditions:
- MODE != DEESCALATION_RISK
- WATERFALL_RISK != HIGH
- Shelf proof must be satisfied (sweep→reclaim→retest→compression)

Sizing:
- Reload uses remaining cash capacity AFTER any active pyramid stages.
- Recommended: single reload at a time, size 20–40% of remaining Gmax (not fixed).

TP:
- 12–25 USD depending on volatility (configurable)
Expiry:
- 30–60 minutes, shorter in late sessions

Hard blocks:
- No reloads in UNKNOWN mode unless shelf proof is exceptionally clean and size is small.
- No reloads at all when DEESCALATION_RISK or WaterfallRisk HIGH.

────────────────────────────────────────────────────────────
9) DE-ESCALATION / WATERFALL KILL SWITCH (NON-NEGOTIABLE)
────────────────────────────────────────────────────────────
Trigger:
- MODE == DEESCALATION_RISK  OR  WATERFALL_RISK == HIGH

Immediate Actions (within <1 second):
1) Cancel ALL pending orders (all rails, all stages).
2) FIRST-LEG BAN:
   - Do not place ANY new buys until:
     a) a clear flush low prints, AND
     b) ≥3 overlapping M15 candles form a base at lows, AND
     c) reclaim + retest holds a new shelf.
3) If already holding grams (filled earlier):
   - Do NOT add.
   - Keep TP only. No loss exits.
   - If you want “neutralization”, only allow “hold until recovery” (no realized loss).

This is the firewall that prevents the 5195 trap.

────────────────────────────────────────────────────────────
10) TELEGRAM INTEGRATION (FAST, SIMPLE, EFFECTIVE)
────────────────────────────────────────────────────────────
We do NOT need full NLP tonight. Implement keyword + burst detection.

Pipeline:
- Subscribe to selected channels
- For every message, compute:
  - deesc_score += keywords hits: ceasefire, talks, mediation, contained, no further escalation, deal, truce, backchannel
  - esc_score += keywords hits: strikes, missiles, drones, base hit, casualties, Hormuz, shipping, oil shock, retaliation, escalation

Burst rule:
- If deesc_score in last 3 minutes >= threshold OR message_count spikes >= threshold with de-esc keywords:
  -> raise “DEESC_BUZZ” event
  -> executor bumps WaterfallRisk up by 1 level (LOW->MED or MED->HIGH)
  -> if already MED and MODE flips to DEESC => immediate HIGH

Use Telegram as:
- Accelerator for risk escalation, not as entry trigger.

────────────────────────────────────────────────────────────
11) TRADINGVIEW INTEGRATION (ONLY 3 ALERTS NEEDED TONIGHT)
────────────────────────────────────────────────────────────
Create alerts:
A1) LID_BREAK:
- Condition: price breaks above Asia_high or last 15m high (developer chooses)
- Payload includes price + timeframe + timestamp

A2) SHELF_RECLAIM:
- Condition: price reclaims a marked shelf after sweep (developer chooses method)
- Payload includes level reclaimed

A3) EXHAUSTION:
- Condition: RSI(H1) > 74 OR divergence OR ADR_used > 1.50
- Payload sets WaterfallRisk MEDIUM and disables new pyramid stages

These alerts improve speed; executor still validates via MT5.

────────────────────────────────────────────────────────────
12) MT5 MONITORING + TRADE PROCEDURE (ORDER LIFECYCLE)
────────────────────────────────────────────────────────────
Order types allowed:
- Buy Stop (Rail-B pyramid)
- Buy Limit (Rail-A reload)
Every order must include:
- grams -> converted to MT5 lots by your broker mapping
- TP (single take profit)
- expiry timestamp (hard cancel at expiry)
- comment tag: MODE + RAIL + STAGE + timestamp

Lifecycle:
1) Pre-check:
   - recompute Gmax
   - ensure reserved cash + new order cash <= cash
2) Validate gates:
   - MODE, WaterfallRisk, Lid/Shelf proof
3) Place order with TP + expiry
4) Monitor:
   - If MODE flips or WaterfallRisk rises -> cancel pending
   - If filled:
     - manage only via TP
     - after TP hit: log profit, update cash, reset reserved cash
5) Hard “quiet window” after closing trades (per your workflow): 5 minutes no new arming.

────────────────────────────────────────────────────────────
13) AI COORDINATION AUTOMATION (LOW LATENCY)
────────────────────────────────────────────────────────────
Decision priority (fastest wins):
1) Grok MODE (rumors count) => immediate state update
2) MT5 price-action => WaterfallRisk update
3) Telegram buzz => risk bump
4) TradingView => structural triggers

NEVER block on Perplexity.
Perplexity output can be stored for post-session STUDY only.

────────────────────────────────────────────────────────────
14) REQUIRED OUTPUTS / LOGS (FOR YOUR LEDGER + AUDIT)
────────────────────────────────────────────────────────────
- Every fill and TP must produce a “SHOP SLIP style” log record (copy-ready):
  - Before balances (cash, grams)
  - Order details (MT5 entry, shop buy, TP MT5, shop sell, grams)
  - Expiry times (server + KSA)
  - After balances (cash, grams)
  - Profit AED logged separately (do NOT double add)

- Also log:
  - MODE transitions (timestamp, keywords)
  - WaterfallRisk transitions (why)
  - Cancel reasons (expiry vs mode flip vs risk flip)

────────────────────────────────────────────────────────────
15) TONIGHT “MINIMUM SHIP LIST” (DO THIS, AND WE’RE LIVE-READY)
────────────────────────────────────────────────────────────
(1) Implement dynamic capacity math (Gmax) with shop spread + buffer_g.
(2) Implement MODE state machine fed by Grok endpoint (rumors count).
(3) Implement WaterfallRisk (LOW/MED/HIGH) from MT5 indicators.
(4) Implement Rail-B Expansion Pyramid:
    - staged grams fractions of Gmax
    - one pending Buy Stop at a time
    - TP ladder (200/200/150/100/80)
    - expiry auto-cancel
(5) Implement De-escalation Kill Switch:
    - cancel pendings instantly
    - first-leg ban until base forms
(6) Implement Mid-air ban for reload limits (shelf proof required).
(7) Telegram buzz: de-esc burst bumps risk up.
(8) TradingView: 3 alerts to speed triggers (optional but recommended).

────────────────────────────────────────────────────────────
16) ASCII DIAGRAM (END-TO-END FLOW)
────────────────────────────────────────────────────────────
            ┌──────────────┐
            │     Grok      │  (X + rumors)  ──► MODE (WAR / DEESC / UNKNOWN)
            └──────┬───────┘
                   │
                   ▼
┌──────────────┐   ┌─────────────────────────────┐   ┌──────────────┐
│   Telegram    │──►│   EXECUTOR (EA / Service)   │──►│      MT5      │
│  buzz scores  │   │  - Capacity math (Gmax)     │   │ place/cancel  │
└──────┬───────┘   │  - WaterfallRisk             │   │ orders+TP     │
       │           │  - Rail-B Pyramid (BuyStops) │   └──────┬───────┘
       │           │  - Rail-A Reload (BuyLimits) │          │
       │           │  - Mid-air ban               │          ▼
       │           │  - Kill switch + expiry      │   ┌──────────────┐
       │           └───────────┬─────────────────┘   │  Ledger/Slips  │
       │                       │                     │ (copy-ready)   │
       │                       ▼                     └──────────────┘
       │              ┌────────────────┐
       └─────────────►│ TradingView    │ (alerts: LID_BREAK / SHELF / EXHAUSTION)
                      └────────────────┘

END.

Developer: build exactly this. No extra features tonight. Focus on: MODE, WaterfallRisk, Gmax, Pyramid, KillSwitch, Expiry, Logs.