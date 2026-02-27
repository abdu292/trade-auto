Below is the exact instruction for you to design the full automation workflow. Please write the solution as a structured document with clear sections/subsections, and implement in phases (MVP → v1 → v2). Goal: maximize clean rotations (2+ cycles/session when safe) while structurally avoiding waterfall/panic-sell traps. We only trade PHYSICAL-style: Buy first (Buy Limit / Buy Stop) → TP sell what we own in grams. No sell-first, no hedging, no SL, no market orders. Shop spread is ±0.80 USD/oz, conversion: 1 oz = 31.1035g, 1 USD = 3.674 AED. MT5 server time = KSA − 50 minutes. Every order must have explicit MT5 expiry.

=====================================================
SECTION 0 — SYSTEM PRINCIPLES (NON-NEGOTIABLE)
=====================================================
0.1 Instrument: XAUUSD only (MT5 price in USD/oz; internal conversions to grams/AED).
0.2 Execution style: Buy Limit / Buy Stop only → single TP → no market orders.
0.3 Risk rule: Never allow waterfall mid-air catches; automation must detect and BLOCK.
0.4 Outputs: Either ONE final TABLE (entry/grams/TP/expiry) or NO TRADE.
0.5 Ledger discipline: Auto BUY SLIP and SELL SLIP to WhatsApp + ledger update at trigger and TP.

=====================================================
SECTION 1 — ARCHITECTURE OVERVIEW
=====================================================
1.1 Modules
- Data Layer (MT5 + TradingView + Telegram)
- Indicator Engine (compute signals consistently)
- Regime & Risk Classifier (SAFE/CAUTION/BLOCK + regime tag)
- AI Orchestrator (ChatGPT core + Perplexity/Gemini fetch + Grok formatting)
- Decision Engine (entry/TP/expiry/sizing logic)
- Execution Controller (manual approve / semi-auto)
- Slip + Ledger System (WhatsApp + database)
- Audit & Monitoring (logs, alerts, failuresafe)

1.2 Deployment Phases
- MVP: Manual approval, AI-driven table, slip/ledger automation, strong blocking.
- v1: Telegram ingestion + structured news tags, better regime classifier.
- v2: Semi-auto only in SAFE windows (Japan/India), dynamic sizing + re-entry ladder.

=====================================================
SECTION 2 — DATA INTEGRATIONS
=====================================================
2.1 MT5 Integration (Required)
- Pull: H4/H1/M30/M15/M5 candles, tick data, spread, order status, account exposure.
- Push: place pending orders (Buy Limit/Buy Stop), modify/cancel, set TP + expiry.
- Time sync: compute MT5 server time and show both MT5 + KSA on every output.

2.2 TradingView Integration (2 Free Indicators Only)
- Pull from TradingView alerts/webhook OR scrape-free method: user-configured alerts.
- Use exactly 2 indicators from TV for confirmation only (not primary decision).
- TV signals must be normalized to tags: CONFIRM / NEUTRAL / CONTRADICT.

2.3 Telegram Integration
- Monitor selected channels.
- Extract message type:
  - “HIGH IMPACT NEWS / USD event / Fed / CPI / Geo-shock”
  - “Rumor / Low-impact / Noise”
- Convert to structured tags: HIGH / MODERATE / LOW.
- Store message + timestamp + channel + category for audit.

=====================================================
SECTION 3 — INDICATOR ENGINE (MT5 FULL SET)
=====================================================
3.1 Compute in your backend (do not rely on UI screenshots)
Minimum list to include (configurable):
- MA20 (H4/H1/M30) for trend & air-gap detection
- RSI (H1 + M15) for overheat/relief detection
- ATR (M15 + H1) for volatility regime (expanding vs contracting)
- Session range markers: Asia/London/NY high-low, previous day high/low
- Candle expansion detector: consecutive large-bodied candles
- Compression detector: overlapping candles + contracting ATR
- Liquidity sweep detector: wick beyond key level then close back inside

3.2 Output all signals as normalized booleans/tags, not raw numbers only.

=====================================================
SECTION 4 — REGIME & RISK CLASSIFIER (MOST IMPORTANT)
=====================================================
4.1 Regime Tags (one must be active)
- COMPRESSION (ATR contracting + overlap)
- EXPANSION (ATR rising + wide candles)
- NEWS-SPIKE (telegram/news HIGH or sudden impulse)
- POST-SPIKE PULLBACK (expansion ended, pullback phase)
- FRIDAY HIGH-RISK (day filter + session overlap)

4.2 Permission Map
- SAFE: allow TABLE
- CAUTION: allow TABLE with reduced size + tighter expiry
- BLOCK: NO TRADE only (no table)

4.3 Waterfall Guard (auto-block)
Trigger BLOCK when:
- ATR expansion + impulse candles + news HIGH
- Friday London/NY overlap + expansion
- Large sudden drop sequence (panic pattern)

=====================================================
SECTION 5 — AI ORCHESTRATION (DIFFERENT TACTICS PER AI)
=====================================================
5.1 ChatGPT (Brain / Orchestrator)
- Takes structured MT5 signals + regime tags.
- Generates one single structured query for live intelligence.
- Synthesizes final decision and outputs TABLE/NO TRADE.

5.2 Perplexity (Live News/Macro Fetcher)
- Takes ChatGPT query and returns structured JSON:
  - upcoming high-impact events
  - geo headlines
  - risk-on/off sentiment
  - DXY/real yields direction (tag only)

5.3 Gemini (Cross-Check Fetcher)
- Same query, second opinion.
- Returns structured JSON tags that confirm/contradict.

5.4 Grok (Formatting + fast sanity check)
- Converts final decision into clean TABLE format.
- Must never override safety gates.

=====================================================
SECTION 6 — DECISION ENGINE (ENTRY / TP / EXPIRY / SIZING)
=====================================================
6.1 Entry Rules
- COMPRESSION: Buy Limit near defended floor (tight, not mid-air).
- EXPANSION: Buy Stop only if breakout is confirmed (strict).
- POST-SPIKE: allow deeper pullback Buy Limit only if SAFE/CAUTION.
- NEWS-SPIKE/BLOCK: NO TRADE.

6.2 TP Rules
- Single TP only.
- TP distance scales with ATR and regime.
- No “hope TP” beyond realistic micro swing.

6.3 Expiry Rules
- Must be explicit in MT5 server time, also show KSA.
- Dynamic expiry:
  - SAFE sessions (Japan/India): longer
  - NY / Friday / CAUTION: shorter
- Auto-cancel orders after expiry.

6.4 Sizing Rules
- Volatility-aware sizing:
  - SAFE: normal
  - CAUTION: reduced
  - BLOCK: none
- Never full allocation at “top” conditions.
- Must respect available cash + exposure cap.

6.5 Re-Entry Ladder (controlled averaging only)
- Only if:
  - cash available
  - exposure cap not exceeded
  - regime not BLOCK
  - spacing rule satisfied
- Each re-entry is smaller or controlled (never blind doubling).

=====================================================
SECTION 7 — SLIPS + LEDGER AUTOMATION (MANDATORY)
=====================================================
7.1 BUY Trigger
- Generate BUY SLIP → WhatsApp → ledger update (cash down, gold up)

7.2 TP Hit (SELL close)
- Generate SELL SLIP → WhatsApp → ledger update (cash up, gold down)
- IMPORTANT: sell AED credited includes principal+profit; profit logged separately only.

7.3 Ledger must always show:
- Total cash AED
- Total gold grams
- Total open exposure
- Free deployable cash

=====================================================
SECTION 8 — USER INTERFACE (MINIMAL, SAFE)
=====================================================
8.1 Manual mode default:
- App shows only:
  - regime tag
  - risk state
  - final TABLE or NO TRADE
  - button: APPROVE / REJECT
- Optional semi-auto only for Japan/India SAFE windows after proven stability.

=====================================================
SECTION 9 — DELIVERABLES YOU MUST PRODUCE
=====================================================
9.1 A full written SOP document (sections above).
9.2 Data schema for ledger + slips + orders.
9.3 A list of MT5 indicators computed + formula definitions.
9.4 Regime classifier rules and thresholds (configurable).
9.5 AI prompt templates:
- ChatGPT query generator prompt
- Perplexity JSON response prompt
- Gemini JSON response prompt
- Grok table formatting prompt

Start with MVP: strong BLOCK logic + slip/ledger automation + manual approval.
Once stable, upgrade to re-entry ladder + semi-auto SAFE sessions.

That is the full direction. Implement it exactly and keep it deterministic.