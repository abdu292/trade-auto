بسم الله الرحمن الرحيم

FINAL DEVELOPER SPEC — PHYSICAL GOLD BULLION ROTATION ENGINE
Version: vULTIMATE-SUPREME-AUTO (FINAL) + Patch-A (Telegram 60+ + TradingView Free) + Patch-B (Perplexity Tightening)
Status: BUILD THIS EXACTLY (No ambiguity)

====================================================================
0) EXECUTIVE GOAL (WHAT THIS MUST ACHIEVE)
====================================================================

A fully automated MT5 system that:
- Trades XAUUSD ONLY as a physical bullion rotation proxy (grams).
- Is Sharia-compliant: BUY first → SELL only what is owned (grams). No leverage, no hedging, no sell-first.
- Runs NEWS → ANALYZE → TABLE → MT5 execution with ZERO human confirmation loop.
- Targets same-session trigger + same-session TP (money back in hand before next session begins) whenever regime permits.
- Captures spikes/dips safely (Spike Catch) while blocking waterfall/panic-sell traps early.
- Eliminates “watch & waste”: system is always either ARMED or CAPITAL_PROTECTED with explicit reason (or RECOVERY_HOLD).

HARD CONSTRAINT:
- System NEVER over-purchases beyond real ledger capacity (AED cash + shop spread + buffers).
- Minimum trade size = 100g if capacity permits; if capacity <100g → NO TRADE.

====================================================================
1) STRICT PRECEDENCE (OVERRIDE ORDER — MUST BE ENCODED IN CODE)
====================================================================

This hierarchy prevents conflicts. Higher items override all lower items:

P1) SHARIA + LEDGER SAFETY (EA/DB enforced)
- BUY-only, no realized loss, single TP, no stacking.
- Capacity math (AED + spread + min100g) is enforced by EA.
- If any violation → NO TRADE (regardless of Telegram/TV/Grok).

P2) WATERFALL + HAZARD VETO (EA and Grok both gate)
- If waterfall_risk = HIGH → NO TRADE + cancel pendings.
- If expiry intersects blocked hazard windows → NO TRADE.
- Telegram consensus and TradingView can NEVER override.

P3) CAUSE ATTRIBUTION (controls whether Buy Stops exist)
- UNSCHEDULED_GEO_POLICY / LIQUIDITY_SHOCK / UNKNOWN → Buy Stops BLOCKED; Limits AFTER_STRUCTURE only.
- TECH_BREAKOUT / SCHEDULED_MACRO(post-print) → spike catch MAY be allowed, subject to P1+P2.

P4) SESSION RULES + HISTORICAL PATTERN OVERLAY
- Session TP/expiry bands, rotation caps, day/week/month bias, ADR bands guide aggression.

P5) TELEGRAM CONSENSUS + TRADINGVIEW ALERTS (soft nudges only)
- Can adjust rail choice, shelf depth, size_class, rotation cap.
- Cannot force trades and cannot override P1–P4.

====================================================================
2) HARD-LOCK CONSTANTS, TIME, BUCKETS
====================================================================

2.1 Constants
- Shop spread: ±0.80 USD/oz
  - Shop Buy = MT5 Entry + 0.80
  - Shop Sell = MT5 TP − 0.80
- 1 oz = 31.1035 g
- 1 USD = 3.674 AED

2.2 Time
- MT5 server time is read from terminal by EA.
- KSA time = server + 50 minutes (derived, never guessed).

2.3 Buckets (two only)
- C1 = 80% of total equity (primary rotation bucket)
- C2 = 20% (reserve/recovery; used only under RECOVERY_HOLD + “UPGRADED_SAFE” flag)
- No sub-splits.

2.4 Trade Constraints
- Pending orders only: BUY LIMIT / BUY STOP.
- Single TP per position; no partial exits; no trailing.
- One open position per bucket (no stacking/pyramiding).
- Minimum size: 100g if capacity permits.

====================================================================
3) ARCHITECTURE (WHO DOES WHAT — STRENGTH-BASED)
====================================================================

LIVE PATH (must be fast):
- Grok = sole live decision engine for NEWS + ANALYZE + TABLE.
- EA + DB = sole authority for ledger/capacity/position lifecycle.

ASYNC / OFFLINE (must never delay live trades):
- Perplexity = macro/institutional cache refreshed every 30–60 min (async). Grok reads cache but never waits.
- ChatGPT = documentation, COMPARE/STUDY, mistake audits, spec evolution (offline).
- Gemini = diagrams/visual explanation only.

HARD RULE:
- Ledger is NOT in any AI. Ledger truth is DB + EA only.

====================================================================
4) COMPONENTS TO BUILD (DEVELOPER DELIVERABLES)
====================================================================

D1) MT5 EA (MQL5)
- Collects market snapshot (multi-TF OHLC + indicators).
- Computes session ranges + ADR bands.
- Calls Backend Orchestrator for TABLE_JSON.
- Enforces P1/P2 gates locally.
- Places/updates/cancels pending orders with TP + expiry.
- Generates slips and writes ledger updates to DB.

D2) Backend Orchestrator (REST)
- Aggregates:
  - EA snapshot
  - Telegram consensus state
  - TradingView alerts
  - macro cache (Perplexity async)
  - news/calendar feeds
- Calls Grok and returns TABLE_JSON to EA.

D3) News + Calendar + Breaking Headline Feeds
- Must support:
  - scheduled macro hazard windows
  - breaking geo/policy headlines
  - auctions/rollovers/liquidity holes
- Used for cause attribution + hazard veto.

D4) Cross-Asset Feed
- DXY (or proxy), US10Y yield (or proxy), real yields proxy if possible
- XAGUSD cross-metal confirmation
- Optional: SPX/Oil/VIX if easy

D5) Database (schema required)
- ledger_state (cash/grams per bucket, cost basis)
- pending_orders, open_positions
- slips (buy/sell)
- decision_log (snapshot hash + outputs + outcome)
- pattern_stats (historical overlay)
- mistake_db (do-not-repeat guards)
- telegram_channels registry + weights
- telegram_signals raw + parsed
- tv_alerts log

D6) Telegram Bot + Telegram Listener
- Bot: commands + alerts.
- Listener: watches 60+ channels, parses signals, computes consensus.

D7) Backtest/Replay Harness (deterministic mock)
- Replace Grok with deterministic rule mock that matches gates, spike catch, waterfall veto, session bands, telegram/tv influences.
- Must prove “earns profits” under safe regimes and avoids waterfall captures.

====================================================================
5) EA SNAPSHOT PAYLOAD (EVERY 10–15s + EVENT-TRIGGERS)
====================================================================

EA → Backend JSON must include:

5.1 Time & Price
- server_time, ksa_time
- bid, ask, spread
- spread_median_60m, spread_max_60m

5.2 OHLC + Indicators (arrays)
- OHLC last N bars: M1, M5, M15, M30, H1, H4
- tick_volume arrays: M1/M5/M15
- MA20 (H1/H4)
- EMA50/EMA200 (H1)
- ATR(14) (M15/H1)
- RSI(14) (M15/H1)
- ADR(20) + ADR_used%

5.3 Levels
- PDH/PDL
- weekly high/low
- day open / week open
- session high/low for Japan, India, London, NY (EA computed)

5.4 Refinement Counters
- compression_count_M15 (overlap count)
- expansion_count_M15 (wide-body count)
- impulse_strength_score (EA computed)

5.5 Ledger & Orders (DB truth)
- C1_cash_aed, C2_cash_aed
- C1_gold_g, C2_gold_g
- cost_basis per bucket (MT5 entry and shop buy)
- open positions list (bucket, grams, entry, TP, open_time, expiry)
- pending orders list (same)

5.6 Event-Triggers (must force immediate cycle)
Trigger immediate Grok cycle when:
- TradingView BREAKOUT/SESSION_BREAK/RETEST alert arrives
- Telegram consensus flips (e.g., MIXED→STRONG_BUY, or BUY→STRONG_SELL)
- Breaking headline arrives (geo/policy)
- No ARMED order for N minutes (Watch/Waste kill switch; see section 9)

====================================================================
6) TELEGRAM 60+ CHANNELS (REAL IMPLEMENTATION)
====================================================================

6.1 Channel Registry + Categorization
Each channel has:
- channel_id, name
- type: SCALP / INTRADAY / SWING / NEWS / MIXED
- reliability_flags: spam/edit/delete/unknown
- weight (dynamic)
- win_rate_rolling (last 200 signals)
- impact_score (alignment with our profitable rotations)
- conflict_score (alignment with bad holds/mistakes)
- last_active_time

Bootstrap:
- default weight=1.0
- optional “Trusted Core Channels” list: start weight=1.5–2.0

6.2 Message Parser → SignalEvent
SignalEvent:
- time server+KSA
- direction: BUY / SELL / FLAT / UNKNOWN
- entry zone, TP/SL if present (SL stored only)
- XAUUSD confidence
- raw message stored

6.3 Weighted Consensus (rolling window 5 minutes)
Compute:
- buy_score = Σ(weight for BUY)
- sell_score = Σ(weight for SELL)
- dominance = max(buy_score, sell_score) / (buy_score + sell_score + ε)

States:
- STRONG_BUY  : dominance ≥0.85 and buy_score>sell_score
- BUY         : dominance ≥0.70 and buy_score>sell_score
- STRONG_SELL : dominance ≥0.85 and sell_score>buy_score
- SELL        : dominance ≥0.70 and sell_score>buy_score
- MIXED       : dominance <0.70 (with activity)
- QUIET       : low activity

6.4 Telegram Influence Rules (BUY-ONLY, never overrides P1–P4)
- STRONG_BUY/BUY:
  - Do not chase.
  - Prefer Rail-A LIMIT micro shelf.
  - Rail-B (Buy Stop) allowed only if Spike Catch Green-Light passes (section 8).
- STRONG_SELL/SELL:
  - Treat as “expect drop”.
  - Rail-B always BLOCKED.
  - Rail-A only AFTER_STRUCTURE (deeper shelves, proof-based).
  - If waterfall HIGH → CAPITAL_PROTECTED.
- MIXED:
  - Rail-B BLOCKED.
  - Rail-A AFTER_STRUCTURE only; size_class capped 25–50%.
- QUIET:
  - Telegram influence ignored.

6.5 Panic-Sell Detector from Telegram
panic_suspected=YES if:
- sudden surge in SELL signals + EA shows spreads widening and ATR rising
Action:
- cancel all Buy Stops immediately
- block new Rail-B
- Rail-A only deep/proof or stand-down

6.6 Weight Learning (categorize by success rate automatically)
When a rotation completes:
- GOOD: triggered and TP hit in same session
- HOLD: triggered but TP not hit within configured hold time
- BAD_CONTEXT: mistake types triggered (waterfall trap, exhaustion chase, etc.)

Update:
- Channels aligned with GOOD → weight up slightly
- Channels aligned with HOLD/BAD_CONTEXT → weight down
- Spam/edit/delete → penalty

====================================================================
7) TRADINGVIEW FREE (REFERENCE + ALERTS, NOT EXECUTION)
====================================================================

- TradingView is your free reference tool; system consumes alerts only.
- Keep 2 indicators max (you choose):
  - RSI(14) + EMA20
  OR
  - RSI(14) + ADR/session range indicator

TradingView alert types (webhook):
- BREAKOUT
- RETEST_HOLD
- ADR_EXHAUSTION
- RSI_OVERHEAT
- SESSION_BREAK (break of session high/low)

Influence:
- BREAKOUT/SESSION_BREAK can help arm Spike Catch (if all gates pass).
- ADR_EXHAUSTION/RSI_OVERHEAT must downgrade aggression:
  - block Rail-B
  - reduce size_class
  - prefer deeper Rail-A shelves

====================================================================
8) SPIKE CATCH MODULE (FAST PROFIT, SAFE)
====================================================================

Spike Catch may arm ONLY if ALL true (hard checklist):
1) mode = IMPULSE
2) cause ∈ {TECH_BREAKOUT, SCHEDULED_MACRO(post-print)}
3) waterfall_risk = LOW (NOT MED/HIGH)
4) spreads = NORMAL
5) not inside hazard_window (except “just-after-news” post-print for scheduled macro)
6) RSI(H1) ≤ 73 AND no M15 rejection cluster
7) telegram_state ∉ {SELL, STRONG_SELL} AND panic_suspected = NO
8) TV alert supports impulse: BREAKOUT (not ADR_EXHAUSTION/RSI_OVERHEAT)
9) compression lid + breakout + retest exists:
   - ≥3 overlapping M15 candles before break

If ANY fails:
- rail_permissions.B = BLOCKED
- system uses Rail-A shelves only (micro or deep depending on cause and telegram_state)

====================================================================
9) WATCH & WASTE KILL SWITCH (CODABLE)
====================================================================

Purpose: remove idle time while staying safe.

If ALL true:
- session ACTIVE (not rollover hole)
- waterfall_risk = LOW
- no blocking hazard_window within next 30 minutes
- rotation_cap not reached
- AND no ARMED order exists for ≥ N minutes (default N=25)
Then:
- Backend forces a Grok cycle with flag:
  force_where_to_trade = true

In that mode Grok must:
- search for a safe Rail-A shelf that satisfies all gates,
- return NO_TRADE only if:
  - ADR_used is extreme/exhausted, OR
  - structure_regime = SHOCK, OR
  - pattern_stats show very low followthrough probability in this context.

====================================================================
10) GROK MODULES (MUST INCLUDE HISTORICAL + CENTRAL BANK/INSTITUTIONAL)
====================================================================

10.1 Macro/Institutional Cache (Perplexity async; Grok reads but never waits)
Refresh every 30–60 minutes:
- DXY trend + level
- US10Y trend + level
- real yields proxy (if possible)
- ETF flow headlines (GLD etc.) if available
- CFTC positioning summary when updated
- central bank purchase headlines (if available)
Output cache fields:
- macro_bias: SUPPORTIVE / NEUTRAL / HOSTILE / UNKNOWN
- institutional_bias: SUPPORTIVE / NEUTRAL / HOSTILE / UNKNOWN
- cb_flow_flag: BUYING / SELLING / NONE / UNKNOWN
- positioning_flag: RISK_ON_GOLD / RISK_OFF_GOLD / UNKNOWN
- cache_age_minutes

Grok must incorporate these in NEWS decisions (never “forgetting” them) but must not block if stale.

10.2 NEWS Module (every TABLE)
Must output:
- session + maturity
- hazard_windows (server & KSA)
- liquidity_quality
- macro_bias + institutional_bias + cb_flow_flag + positioning_flag (from cache)
- cross_asset_flags: dxy_up, yields_up, xag_confirming, etc.
- telegram_state + panic_suspected
- regime_tag
- cause attribution:
  SCHEDULED_MACRO / UNSCHEDULED_GEO_POLICY / LIQUIDITY_SHOCK / TECH_BREAKOUT / UNKNOWN
- rail_permissions A/B
- eligible_now YES/NO

10.3 ANALYZE Module (distinct from NEWS)
Must output:
- structure_regime: RANGE / FLUSH / EXPANSION / SHOCK
- impulse_state: LEG1 / LEG2 / EXHAUSTION
- mode: IMPULSE / EXHAUSTION
- shelves:
  - supports S1/S2/S3
  - resistances R1/R2
- coming-session expected shelves (next session prints)
- TP ceiling (micro swing highs + session band)
- expiry range suggestion
- Historical pattern overlay from DB (section 11)

10.4 Waterfall/Panic Detector (hard gate)
As defined in section 12 (EA and Grok both gate).

====================================================================
11) HISTORICAL PATTERN OVERLAY (DB-DRIVEN, REQUIRED)
====================================================================

Pattern context keys:
- session, day-of-week, week-of-month, month-of-year
- cause tag
- structure_regime
- ADR_used band (<50, 50–80, 80–95, 95–110, >110)
- telegram_state

Store:
- followthrough probability (30–90m)
- reversal risk
- typical best TP
- typical best expiry window
- typical rotation count
- hold-risk frequency

Grok must read this and output:
- historical_bias: BULLISH / NEUTRAL / BEARISH / UNKNOWN
- expected_followthrough_prob
- rotation_cap_this_session (0–5)
- aggression suggestion (size_class ceiling)

====================================================================
12) WATERFALL / PANIC-SELL VETO (HARD)
====================================================================

waterfall HIGH if ≥2:
- 2 wide red M15 bodies through shelf
- ATR spike + body close below shelf
- RSI(H1)>72 or RSI(M15)>75 + rejection cluster
- DXY up AND US10Y up strongly
- spreads UNSTABLE
- Friday late London/NY + ADR_used high
- panic_suspected YES

Actions:
- TABLE.status = NO_TRADE
- EA cancels all pending orders immediately
- engine_state = CAPITAL_PROTECTED with explicit reason + cooldown

waterfall MEDIUM:
- Rail-B blocked
- Rail-A only deep shelves; size_class capped 25–50%

====================================================================
13) SESSION BANDS (TP / EXPIRY / ROTATION CAPS)
====================================================================

Japan:
- TP +6 to +9 USD
- Expiry 45–60m
- rotation cap default 2 (pattern overlay may reduce to 0/1)

India:
- TP +8 to +12 USD
- Expiry 45–75m
- rotation cap default 2

London:
- TP +8 to +12 USD
- Expiry 30–55m
- rotation cap default 2 (reduced late)

New York:
- TP +9 to +15 USD (only if non-exhaustion)
- Expiry 20–45m
- rotation cap default 2 (often 1 late)

Friday late London/NY:
- automatic aggression reduction and rotation cap shrink via pattern overlay.

====================================================================
14) TABLE JSON CONTRACT (GROK → EA)
====================================================================

{
  "status": "ARMED" | "NO_TRADE",
  "engine_state": "ARMED" | "CAPITAL_PROTECTED" | "RECOVERY_HOLD",
  "mode": "IMPULSE" | "EXHAUSTION",
  "cause": "SCHEDULED_MACRO" | "UNSCHEDULED_GEO_POLICY" | "LIQUIDITY_SHOCK" | "TECH_BREAKOUT" | "UNKNOWN",
  "waterfall_risk": "LOW" | "MEDIUM" | "HIGH",
  "macro_bias": "SUPPORTIVE" | "NEUTRAL" | "HOSTILE" | "UNKNOWN",
  "institutional_bias": "SUPPORTIVE" | "NEUTRAL" | "HOSTILE" | "UNKNOWN",
  "cb_flow_flag": "BUYING" | "SELLING" | "NONE" | "UNKNOWN",
  "positioning_flag": "RISK_ON_GOLD" | "RISK_OFF_GOLD" | "UNKNOWN",
  "historical_bias": "BULLISH" | "NEUTRAL" | "BEARISH" | "UNKNOWN",
  "telegram_state": "STRONG_BUY|BUY|MIXED|SELL|STRONG_SELL|QUIET",
  "rail_permissions": { "A": "ALLOWED|AFTER_STRUCTURE|BLOCKED", "B": "ALLOWED|BLOCKED" },
  "rotation_cap_this_session": 0 | 1 | 2 | 3 | 4 | 5,
  "orders": [
    {
      "bucket": "C1" | "C2",
      "rail": "LIMIT" | "STOP",
      "session": "JAPAN" | "INDIA" | "LONDON" | "NY",
      "entry_mt5": float,
      "tp_mt5": float,
      "expiry_server": "YYYY-MM-DD HH:MM:SS",
      "expiry_ksa": "YYYY-MM-DD HH:MM:SS",
      "size_class": "25%" | "50%" | "75%" | "100%",
      "reason_entry": "string",
      "reason_tp": "string",
      "pattern_context_tag": "string"
    }
  ],
  "no_trade_reason": "string"
}

EA must reject if:
- status != ARMED
- waterfall_risk == HIGH
- rail violates rail_permissions
- expiry intersects hazard windows or violates session band
- capacity < 100g
- violates P1 (sharia/ledger rules)

====================================================================
15) EA CAPACITY + ANTI OVER-PURCHASE (YOU SPECIFICALLY REQUIRED THIS)
====================================================================

Per order:
- shop_buy = entry_mt5 + 0.80
- cost_per_gram_aed = (shop_buy / 31.1035) * 3.674
- bucket_cash = DB truth
- max_grams = floor(bucket_cash / cost_per_gram_aed) − 10g buffer
- if max_grams < 100g → NO TRADE
- grams_from_size_class = floor((size_class% * bucket_cash) / cost_per_gram_aed)
- grams = min(max_grams, grams_from_size_class)

This enforces:
- never exceed ledger capacity
- never breach shop trust line
- never trade below 100g

====================================================================
16) RECOVERY_HOLD + C2 ACTIVATION (CLARIFIED)
====================================================================

RECOVERY_HOLD:
- C1 has an open position beyond normal session horizons.
- New C1 entries disabled until recovery TP or ruleset change.

C2 may activate ONLY if ALL:
1) config flag UPGRADED_SAFE = true
2) waterfall_risk ≠ HIGH and macro_bias ≠ HOSTILE
3) pattern overlay not showing high reversal risk in current context

C2 defaults:
- size_class 25–50%
- tighter TP within band
- stricter expiry

====================================================================
17) BACKTEST / REPLAY (MUST PROVE IT EARNS)
====================================================================

Developer must backtest with deterministic rule mock:
- scheduled macro spikes vs unscheduled geo/policy spikes
- Friday late sessions
- strong telegram consensus days
- quiet Asia sessions
- liquidity shock (rollover)

Success criteria to proceed:
- High in-session TP hit-rate in Japan/India
- Spike Catch triggers only on clean TECH_BREAKOUT or SCHEDULED_MACRO post-print (never on unscheduled headline without structure)
- Low RECOVERY_HOLD frequency
- Capital sleep % reduced by Watch/Waste kill switch without increasing waterfall incidents

END OF FINAL SPEC.
الحمد لله