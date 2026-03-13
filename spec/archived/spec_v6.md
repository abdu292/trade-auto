Subject: FINAL BUILD SPEC v1.0 – Ultra-Safe High-Rotation Physical Gold Automation

بسم الله الرحمن الرحيم

This is the final, merged specification for the automation system.  
It combines:

- The master prompts (Perplexity/ChatGPT + Grok) as the **Constitution**,
- The hard‑locked rotation engine,
- The profit‑boost patch (Fast/Slow path, Constitution Library, 3‑layer auto‑cancel),
- My personal philosophy (no stop-loss selling; protection via entries only).

If you build exactly this, you have everything needed to implement.

==================================================
0) TRADING PHILOSOPHY & STYLE (CONTEXT)
==================================================

- Instrument: Physical XAUUSD.gram via MT5 (Dubai bullion shop model).
- Shariah: buy → own → then sell in profit only.
- No leverage, no shorts, no hedging, no loss‑selling, no time‑based forced exits.
- Typical trade:
  - Design target: hit TP in **<30 minutes**.
  - Can extend to **1–3 hours** if structure is clean.
  - If necessary, I am willing to hold a stuck but structurally valid trade until TP, even days/weeks. The system must **never** auto‑close at a loss.
- Style:
  - **Intraday scalping engine**:
    - H1 = context, sweep + reclaim, regime.
    - M15 = base + momentum.
    - M5 = compression + precise entries.
  - Target: **5–12 trades per clean, active day** (war‑premium / high ATR).
  - Zero “babysitting”; I only monitor high‑level health and P&L.

One‑line anchor:
“Build an H1/M15/M5 intraday scalping engine targeting <30 minutes per trade and 5–12 trades on clean days, with no time-based stop-loss; protection comes from entry filters and waterfall immunity, not from cutting losers.”

==================================================
1) CORE CONSTANTS & CAPACITY LAW
==================================================

Physical + Shop model:
- 1 troy ounce = 31.1035 g
- 1 USD = 3.674 AED
- Shop spread = ±0.80 USD/oz
  - ShopBuy  = MT5_entry + 0.80
  - ShopSell = MT5_TP − 0.80

Buckets:
- C1: active rotation bucket.
- C2: reserve positional bucket.
- Normal regime split:
  - C1 = 80% of capital,
  - C2 = 20%.
- High‑Caution regime (Fri London/NY, severe war shock, “TRUE CHAOS”):
  - C1 = 90%,
  - C2 = 10%.

Capacity law (hard lock, no exceptions):
- For any candidate order:
  - RequiredAED = grams × (ShopBuy / 31.1035) × 3.674
- RequiredAED must be ≤ remaining AED in the chosen bucket (C1 or C2), after:
  - subtracting AED locked in existing open positions (based on their ShopBuy),
  - subtracting AED reserved for still‑pending orders (if you implement reserved AED).
- If RequiredAED > remaining bucket AED → ABORT. No order.
- This is the **only execution permission** regarding capacity and ensures we never exceed shop capacity, including spread.

Time mapping:
- KSA time is authoritative (device/server).
- MT5 ServerTime = KSA − 50 minutes (fixed).
- For each order:
  - ExpiryServer must be consistent with ExpiryKSA:
    - ExpiryKSA ≈ ExpiryServer + 50 minutes (±1 minute allowed).

==================================================
2) AI ROLES & HIERARCHY
==================================================

Master prompts = Constitution. They define:
- Pattern bans,
- BottomPermission,
- News rules,
- Session rules,
- Entry engines (BuyLimit/BuyStop),
- STUDY/SELF‑CROSSCHECK.

Roles:

Grok (primary engine):
- NEWS: war/macro/X sentiment, hazard windows.
- ANALYZE: regime classification, pattern bans, BottomPermission, SELF_CROSSCHECK.
- TABLE: one-row order or ABORT under the Grok master prompt.

Perplexity (validator):
- Validates NEWS_HYPOTHESIS, hazard windows, macro bias.
- Outputs:
  - NEWS_VALIDATE = CONFIRMED / CONFLICT / INCONCLUSIVE + one short reason.
- Cannot create trades or modify sizing/expiry beyond validation feedback.

ChatGPT (constitution auditor + ledger/STUDY):
- Validates Grok TABLE against Constitution and local laws.
- Can downgrade TABLE → ABORT if any rule is violated.
- Generates slips, updates ledger.
- Runs STUDY & SELF‑CROSSCHECK and produces rule-change blocks (RC‑###).

Gemini (optional):
- Chart visual summaries only (structure drawing if needed).
- No decision-making authority.

Hierarchy (must be enforced in code):
- Constitution (master prompts + local Constitution Library + capacity law)
  ↓
- ChatGPT validator
  ↓
- Local rule engine (hard-coded)
  ↓
- Grok engine (NEWS/ANALYZE/TABLE)
  ↓
- Perplexity / Gemini / Telegram / TradingView
  (context and validation only)

Only the Constitution and local code can authorize trades.

==================================================
3) LOCAL CONSTITUTION LIBRARY (CODE)
==================================================

Implement a central module (“Constitution Library”) that is the **single source of truth** for:

1) Session and day:
   - Session (Japan/India/London/NY) from MT5 ServerTime:
     - Japan:   00:00–06:59
     - India:   07:00–11:59
     - London:  12:00–16:59
     - New York:17:00–23:59
   - Identify Friday vs non‑Friday.

2) Allowed grams per bucket per session:
   - Max grams for C1/C2 per session and regime, according to master prompts.
   - Max % of C1 allowed for BuyStops per session.
   - Optional: min grams to avoid “baby lots” if specified.

3) Expiry bands:
   - Session-specific:
     - Japan: 30m
     - India: 30m
     - London: 25m
     - NY: 20m
     - Friday London/NY: 15m
   - Check that expiry is before:
     - next hazard window,
     - session change,
     - rollover.

4) TP caps:
   - Per session:
     - Japan: max +8 USD,
     - India: +10,
     - London/NY: +12,
     - Friday London/NY: +8.

5) Pattern bans & BottomPermission:
   - Boolean flags:
     - PatternBan.TOP_LIQUIDATION,
     - PatternBan.BREAKDOWN,
     - PatternBan.LIVE_IMPULSE,
     - BottomPermission.
   - These are computed by applying the definitions in the master prompts to live MT5 data.

6) Capacity law:
   - Implements RequiredAED and bucket constraints as in section 1.

Execution rule:
- Grok can propose:
  - EntryMT5,
  - TP_MT5,
  - candidate ExpiryServer.
- The Constitution Library:
  - Checks legality of grams, expiry, TP,
  - Adjusts only **downward** if necessary for:
    - TP cap trimming (to legal max),
    - Expiry rounding within band.
  - **Never** upscales grams or risk.
- Default behavior:
  - If any AI-proposed field violates legality in a way that cannot be trivially trimmed downward → ABORT, not auto‑correct.
  - This keeps behavior predictable and safe.

==================================================
4) TRIPLE GATE WATERFALL IMMUNITY
==================================================

No order can exist (pending or market) unless all three gates pass.

Gate A – Pattern Ban Filter:
- If any of:
  - TOP_LIQUIDATION = TRUE
  - BREAKDOWN = TRUE
  - LIVE_IMPULSE = TRUE
- Then: ABORT (no new orders).  

Gate B – BottomPermission:
- Buy allowed only if BottomPermission = TRUE, based on:
  - H1 sweep below prior swing + reclaim close above,
  - H4 context acceptable,
  - M15 2 strong green candles off the low, above their midpoints,
  - M5 compression (≥6 overlapping candles, narrowing range),
  - RSI(M15) > 35 and rising,
  - Momentum indicators turning up / flattening.
- If BottomPermission = FALSE: ABORT.

Gate C – News Hazard Filter:
- No new orders if:
  - Major US red news within ±10–15 minutes,
  - Fresh war/geopolitical spike headlines,
  - Unusually wide spreads.
- If any hazard condition: ABORT.

These three gates are evaluated **before** any TABLE is accepted.

==================================================
5) ENTRY ENGINES (MONEY MAKERS)
==================================================

Two engines only: BuyLimit and BuyStop, as defined in the master prompts.

Engine A – BUY LIMIT HARVEST (primary):
- Used to buy structured dips at bases.

Entry:
- Use structural support (H1/H4 swing low or public level band).
- Buffer = max(1.5 USD, 0.20 × ATR_M15).
- EntryMT5 = support − Buffer.
- ShopBuy  = EntryMT5 + 0.80.

TP:
- BaseTP = 0.8 × ATR_M15 × 3.
- TP_MT5 = EntryMT5 + min(BaseTP, SessionCap).
- SessionCap:
  - Japan: 8,
  - India: 10,
  - London: 12,
  - NY: 12,
  - Fri London/NY: 8.
- ShopSell = TP_MT5 − 0.80.

Expiry:
- Session-based band (as in section 3).
- Must end before:
  - hazard window,
  - session change,
  - rollover.

Engine B – BUY STOP BREAKOUT (secondary):
- Used only for continuation breakouts after a base.

Conditions:
- NEWS safe (no major event imminent; spreads normal).
- All PatternBans FALSE.
- H1 above MA20 (or reclaimed and held for 2 closes).
- RSI(H1) between 55 and 73.
- Clear resistance (LID) from prior H1 high / session high / M15 range top.
- M15 compression: ≥6 overlapping candles under LID, no vertical launch already.
- Base/reclaim already formed (BottomPermission TRUE or prior sweep+reclaim).

Entry:
- ATR_M15 = current ATR on M15.
- EntryOffset = max(2 USD, 0.25 × ATR_M15).
- EntryMT5 = LID + EntryOffset.
- ShopBuy = EntryMT5 + 0.80.

TP:
- BaseTP = EntryOffset × 3.5.
- TP_MT5 = EntryMT5 + min(BaseTP, session TP cap).
- ShopSell = TP_MT5 − 0.80.

Expiry:
- Same session bands as BuyLimit.

Size:
- As per master prompts:
  - Japan: 20–30% of C1,
  - India: 25–35% of C1,
  - London: 15–25% of C1,
  - NY: 10–20% of C1,
  - Friday London/NY: 10–15% of C1.
- Only **one** active BuyStop at a time. No stacking.

==================================================
6) TWO-SPEED EXECUTION (FAST / SLOW PATH)
==================================================

To avoid latency and missed pockets while preserving safety:

FAST PATH (default):
- Use when:
  - Session = Japan/India or clean London/NY pocket,
  - All three gates (PatternBans/BottomPermission/News) pass,
  - No major hazard within ~15 minutes,
  - No fresh large war spike.
- Behavior:
  - As soon as:
    - Grok TABLE row generated, and
    - Local Constitution Library + ChatGPT validator PASS,
    - Capacity law PASS,
  - → Immediately place the order.
  - Perplexity/Gemini can validate in parallel but do **not** block execution.
  - If a serious CONFLICT returns within a very small timeout (e.g. 5–10 seconds), you may cancel the pending order; otherwise the trade stands.

SLOW PATH (NY high-risk & war spike mode):
- Use when:
  - Session = NY around major US data/Fed,
  - Or large headline-driven war spikes,
  - Or system is in High‑Caution mode.
- Behavior:
  - Validators must respond inside a fixed timeout:
    - If validators CONFIRMED or acceptable → execute.
    - If validators CONFLICT → ABORT.
    - If timeout → ABORT (do not wait; no “analysis paralysis”).

==================================================
7) PENDING ORDER MANAGEMENT – 3 LAYER AUTO-CANCEL
==================================================

Pending orders must be re‑checked frequently (e.g. every 10–30 seconds).

Layer 1 – Time & Session:
- Cancel if:
  - Session is about to change and TP is not realistically reachable in time,
  - Approaching rollover / illiquid cluster,
  - High-impact hazard window is entering the ±10–15 minute zone and order wasn’t explicitly set as post-news.

Layer 2 – Structure Invalidation:
- Cancel if:
  - For BuyLimit:
    - Base/shelf low is broken with a new lower low and no reclaim → original base invalid.
  - For BuyStop:
    - Compression under LID breaks (price bleeds down), or
    - A vertical breakout already launched without us → risk/reward invalid.

Layer 3 – News/Regime Flip:
- Cancel if:
  - Major peace / de‑escalation headline appears (in war-premium regime),
  - Live volatility jumps and LIVE_IMPULSE flips TRUE after placing order,
  - New hazard window begins that invalidates the original thesis.

This makes pending orders regime-aware, not just time-aware.

==================================================
8) TELEGRAM INTELLIGENCE – 60+ TO TOP-10
==================================================

Goal:
Use Telegram as a sentiment/time filter, not as authority.

- Ingest ≈60 Telegram channels.
- For each:
  - Parse gold signals (bullish/bearish),
  - Track:
    - Hit rate / rotation rate,
    - Timing (early/late),
    - Noise density,
    - Alignment with traps (waterfall or impulse entries).

Scoring:
- Rolling window ≥30 days,
- Last 7 days overweighted.
- Penalize trap alignment and noise; reward early, clean calls.

Daily:
- Select **Top‑10** channels automatically.

Consensus:
- Over a near-term window (e.g., last 1–3 hours of active signals):
  - BUY_CONSENSUS% = % of Top‑10 with BUY/BULLISH stance.
  - SELL_CONSENSUS% = % of Top‑10 with SELL/BEARISH stance.

Rules:
- BUY_CONSENSUS% ≥ 80%:
  - Confidence boost for legal buys:
    - Faster acceptance of a valid TABLE,
    - Slight preference for deploying allowed C1 in clean pockets.
  - Never overrides bans, BottomPermission, capacity or hazard filters.

- SELL_CONSENSUS% ≥ 80%:
  - We never short or sell at a loss.
  - Interpretation:
    - Retail/signal crowd is in fear mode.
  - Effect:
    - Tighten entry criteria,
    - Prefer deeper BuyLimits only at strong bases,
    - Bias toward ABORT if structure is not super-clean.

- Mixed/low consensus:
  - Telegram = contextual info only, no direct execution effect.

UI:
- Show Top‑10 with scores/tags (early, noisy, trap-prone).
- Show BUY_CONSENSUS% and SELL_CONSENSUS% gauges.
- Show key recent signals and whether they were supportive/ignored/contra.

==================================================
9) AUTOMATION FLOW & EXECUTION PIPELINE
==================================================

State machine:

IDLE  
  ↓ (trigger: NEWS stale >90m, Telegram/TV alert, large MT5 move)  
NEWS SCAN (Grok + Perplexity)  
  ↓  
STRUCTURE ANALYSIS (MT5 data + Constitution Library, compute bans, BottomPermission)  
  ↓  
TABLE DRAFT (Grok TABLE)  
  ↓  
CONSTITUTION VALIDATION (Local Library + ChatGPT)  
  ↙ FAIL                  ↘ PASS  
TABLE ABORTED         MT5 EXECUTION (FAST/SLOW PATH)  
                            ↓  
                      MONITOR loop  
                            ↓  
                AUTO‑CANCEL if needed (3 layers)  
                            ↓  
                    TP HIT or manual close at profit  
                            ↓  
                SLIP + LEDGER UPDATE  
                            ↓  
                      DAILY STUDY LOOP

Key rules:
- System must always end each cycle in one of two states:
  - TABLE (single executable order),
  - ABORT (capital protected).
- No “scenario text,” no question loops.

==================================================
10) LEDGER, SLIPS & 2% DEVELOPER SHARE
==================================================

Ledger:
- Track:
  - Cash AED,
  - Gold grams,
  - Open positions (grams, ShopBuy, tickets),
  - Pending orders and optional reserved AED,
  - Realized profit per day/week/month.

Profit formula:
- profit_AED = grams × ((ShopSell − ShopBuy) / 31.1035) × 3.674

Slips:
- For each fill & closure:
  - Generate a “shop slip” with:
    - Ticket ID(s),
    - Entry/exit MT5 prices and ShopBuy/ShopSell,
    - grams,
    - timestamps (Server + KSA),
    - before/after balances,
    - realized profit AED.

Developer share (2%):
- NetProfit = RealizedProfit − Expenses.
- DeveloperShare = NetProfit × 0.02.
- UI shows:
  - Net profit,
  - 2% share,
  - Expense list,
  - Payout history (optional).

==================================================
11) STUDY & SELF‑CROSSCHECK
==================================================

Daily STUDY:
- After NY close:
  - Feed trade logs, slips, NEWS behavior, Telegram impact to ChatGPT with STUDY prompt.
  - Output:
    - Patterns that caused issues (e.g., EARLY_RELOAD, MISALIGNED_SESSION),
    - RC‑blocks (rule change proposals).

Weekly SELF‑CROSSCHECK:
- Evaluate:
  - Hit rates by session,
  - Times bans saved losses,
  - Effectiveness of consensus, Fast/Slow Path, auto-cancel.
- Propose at most **one** micro‑improvement per week.

Rule versioning:
- RC-blocks stored with statuses: Proposed / Tested / Active / Archived.
- Rollback ability must exist.

STUDY and SELF‑CROSSCHECK never place trades; they only edit configuration.

==================================================
12) IMPLEMENTATION PHASES
==================================================

Phase 1:
- MT5 integration (read/write),
- Constitution Library (session, caps, capacity, bans, BottomPermission),
- FAST/SLOW path executor,
- Basic UI for positions, orders, and P&L.

Phase 2:
- Telegram ingestion, scoring, Top‑10 consensus engine,
- Grok + Perplexity + ChatGPT orchestration,
- Full triple‑gate and 3‑layer auto‑cancel.

Phase 3:
- Ledger & slip engine,
- Profit & 2% developer share module,
- STUDY & SELF‑CROSSCHECK integration.

Phase 4 (optional):
- TradingView/webhook triggers wiring,
- UX polish and optimizations.

==================================================
END
==================================================

If any detail is unclear during implementation, please default to:
- Capital protection over extra trades,
- ABORT over auto-correct,
- Downward trimming over any increase in risk.