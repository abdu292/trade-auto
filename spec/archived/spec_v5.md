DEV NOTE (for implementation) — vULTIMATE-AUTO MASTER v7 alignment

1) Keep BOTH strategy profiles, but don’t hard-code “Standard vs WarPremium” as manual only. Make them AUTO-SWITCH from MODE_TAG computed by the engine:
- MODE_TAG = NORMAL / WAR_PREMIUM / DEESCALATION_RISK (from live news + geo headlines + cross-metals + DXY/yields).
- Strategy profile is just a UI label that changes caps/permissions:
  • NORMAL: full engine rules
  • WAR_PREMIUM: stricter kill-switch + Rail-B tighter + smaller size near tops
  • DEESCALATION_RISK: Rail-B OFF, only deep Rail-A after proof sequence, often NO-TRADE
So: keep the two profiles in UI (good), but the engine decides which is active, with manual override only as “Emergency Pause”.

2) Current MT5 ingest (ticks + volatilityExpansion) is NOT enough to avoid waterfalls. You need full “TABLE FEED PACK” every cycle (aligned to candle closes) + live intelligence stack. Grok is the central decision engine that outputs NEWS/ANALYZE/TABLE; Perplexity+Gemini are live “eyes/ears” that return structured tags; ChatGPT is validator/auditor (VALIDATE + reason checks). Telegram + TradingView are signals/alerts only; they must be normalized into “risk tags”, never copied as trades.

------------------------------------------------------------
A) DATA INGEST LAYER (MT5 + TV + Telegram) — MUST HAVE
------------------------------------------------------------
A1) MT5 (XAUUSD + optional XAG/XPT if available on broker)
Fetch per timeframe: H4, H1, M30, M15, M5 (and optionally M1 for micro checks)
- OHLC + candle timestamps (openTime/closeTime) + tick volume
- MA20 value + distance-to-MA20 (air-gap)
- RSI (H1 mandatory, M15 mandatory, M5 optional)
- ATR (H1 + M15 mandatory)
- Session high/low + previous session high/low + previous day high/low
- Compression inputs: last N candle ranges + overlap score (or just send last N ranges and compute server-side)
- Spread stats: min/avg/max over 1m & 5m (not only current)
- Order state: pending orders + open positions + fills/partials/expiry/cancel events with timestamps
- Account/exposure: free margin/equity/balance (even demo) so caps are enforced
Important: indicators must be computed consistently (either MT5 side and sent, OR raw OHLCV sent and we compute; but choose ONE and stick to it).

A2) TradingView (2 free indicators)
- Treat TV as ALERT SOURCE, not truth.
- Pull: alert type + symbol + timeframe + trigger time + message text.
- Normalize to one of: TV_CONFIRMING / TV_DIVERGING / TV_NONE.
(Do NOT place orders directly from TV alerts.)

A3) Telegram channels (signals + breaking news)
- Ingest posts + timestamps + channel name.
- Classify message type:
  • SIGNAL (entry/TP text) => “SignalOnly” (never execute)
  • NEWS (war/talks/sanctions/central banks/data) => “NewsCandidate”
- Convert “NewsCandidate” into tags:
  ESCALATION / DEESC / MIXED / NONE and PANIC_FLAG true/false.
- Your UI “Telegram: QUIET” should be driven by this classifier.

A4) Cross-market intelligence (external feeds)
Needed for the correlation stack:
- DXY or USD proxy, US10Y yield/real yields proxy, XAG, XPT (if not in MT5, fetch from a market data API)
- Central bank / institutional positioning cannot be real-time perfect; implement as “last-known state” with freshness age (your UI already shows Macro Age). If stale => downgrade confidence and tighten trading permissions.

------------------------------------------------------------
B) AI ORCHESTRATION LAYER — ROLE SPLIT (NO CONFUSION)
------------------------------------------------------------
B1) Grok = ONLY live decision engine (outputs: NEWS / ANALYZE / TABLE)
Inputs to Grok must be structured JSON:
- MT5_FEED_PACK (all fields above)
- TV_ALERT_TAG
- TELEGRAM_TAGS (geo + panic + headline snippets)
- MACRO_SNAPSHOT (DXY/yields/cross-metals + freshness)
Grok outputs STRICT JSON + human-readable table:
- NEWS_JSON: MODE_TAG, hazard windows, correlation tags, WaterfallRisk, rail permissions
- ANALYZE_JSON: regime tag (RANGE/EXPANSION/EXHAUSTION/LIQUIDATION), proof-sequence status, levels (A/B/C zones)
- TABLE_JSON: 1–2 rows max, each row with:
  orderType (BUY_LIMIT/BUY_STOP), grams, entry_mt5, shop_buy, tp_mt5, shop_sell, expiry_server, expiry_ksa, dealHealth, invalidation, slot(C1-A/C1-B)

B2) Perplexity = live macro/news fetcher (fast)
Grok must generate a single compact query for Perplexity and require JSON back:
- Upcoming high-impact events (next 2–6h), geo headlines, DXY/yields tone, risk-on/off
Return format: {hazard_windows:[...], geo_tag, dxy_tag, yields_tag, sentiment_tag, confidence}

B3) Gemini = second live check (cross-verify)
Same query (or slightly shorter) but must output the SAME JSON schema so we can compare.

B4) ChatGPT = VALIDATE / AUDIT (not decision)
- Validate Grok’s TABLE_JSON against rule cage: no mid-air, rail legality, expiry sanity, caps, hazard blocks, 2-slot max.
- Output: ACCEPT / IMPROVE / REJECT with corrected TABLE_JSON if needed.

Consensus rule (simple):
- If Perplexity or Gemini says “HIGH HAZARD” or “DEESCALATION_RISK” => Grok must block or deep-only.
- If Perplexity and Gemini disagree => downgrade confidence and reduce size / Rail-B OFF / or NO TRADE.

------------------------------------------------------------
C) EXECUTION LAYER (MT5) — HALAL + WATERFALL IMMUNITY
------------------------------------------------------------
C1) Order rules (hard)
- Only BUY_LIMIT or BUY_STOP. Never sell-first. Never market.
- TP only (sell what is owned). (No forced SL logic.)
- Max 2 live slots total (C1-A and C1-B).
- MT5 server time handling: Server = KSA − 50 min (print both in outputs, use server for expiry).

C2) Waterfall trap prevention (must be coded, not “AI vibes”)
Before placing any order:
- Regime must be one of RANGE_RELOAD / EXPANSION (or deep FLUSH_CATCH with proof sequence).
- If EXHAUSTION or LIQUIDATION => Rail-B OFF; Rail-A only deep after proof sequence.
- Hazard windows active => block new orders.
- Friday + NY risk => tighter caps + often deep-only.

C3) Re-entry scaling logic (after first buy)
- If a buy is filled and price moves lower AND free cash exists:
  Re-check: hazard + regime + correlation score.
  If SAFE/controlled CAUTION => allow ONE additional scaled buy (slot C1-B) at a lower shelf (not mid-zone).
  If BLOCK/HIGH => no new order.

------------------------------------------------------------
D) SLIPS + LEDGER + WHATSAPP (POST-TRADE LOOP)
------------------------------------------------------------
D1) On BUY FILLED event
Generate BUY SLIP and WhatsApp it automatically:
- Trade ID, grams, entry_mt5, shop_buy (= mt5+0.80), time server+KSA
- Ledger before: Cash_AED, Gold_g
- AED debited, Ledger after: Cash_AED, Gold_g
- Slot label (C1-A or C1-B)
Also: start “quiet window” timer (5 minutes after any SELL close only; buy fill doesn’t block management).

D2) On TP HIT / SELL FILLED event
Generate SELL SLIP and WhatsApp it automatically:
- Trade ID, grams sold, tp_mt5, shop_sell (= mt5−0.80), time server+KSA
- AED credited (full amount), Profit logged separately
- Ledger before/after
Then enforce: 5-minute quiet window before arming next new entry.

D3) Ledger is the source of truth
- Never assume balances. Only update on confirmed MT5 fill events + slip generation.
- UI fields (Cash/Gold/Deployable/Exposure/Open Buys) must be derived from ledger + live orders.

------------------------------------------------------------
E) SESSIONS — EXACT WINDOWS (KSA / UAE / INDIA)
------------------------------------------------------------
Use these fixed windows for the “Session Controls” toggles and for expiry discipline:

KSA (UTC+3)
- JAPAN: 03:00–12:00  | peak: 05:00–09:00 | late: 09:00–12:00
- INDIA: 07:00–16:00  | peak: 09:00–13:00 | late: 13:00–16:00
- LONDON: 10:00–19:00 | peak: 12:00–17:00 | late: 17:00–19:00
- NEW YORK: 15:00–00:00 | peak: 16:00–20:00 | late: 20:00–00:00

UAE (UTC+4) = KSA + 1 hour
India (UTC+5:30) = KSA + 2 hours 30 minutes
(Use these offsets programmatically so one source-of-truth schedule drives all displays.)

------------------------------------------------------------
F) UI REQUIREMENTS (based on your current screens)
------------------------------------------------------------
- “Automation active” must show: current MODE_TAG, regime, WaterfallRisk, hazard countdown (if any), macro freshness, and which AI agreed/disagreed.
- “Hazard Windows” must be auto-created from Perplexity/Gemini event scan (not manual only).
- “Execution: MANUAL / HYBRID AUTO”:
  • MANUAL = show TABLE and wait approval
  • HYBRID AUTO = auto-place only during JAPAN/INDIA when SAFE and confidence high; otherwise revert to approval
- “Emergency Pause” must hard-stop: new orders + cancel pending orders (optional toggle) but still allow slip/ledger updates for existing fills.

------------------------------------------------------------
DIAGRAM (end-to-end)
------------------------------------------------------------
[MT5 OHLC/RSI/ATR/MA20 + Orders]   [TradingView Alerts]   [Telegram Feeds]   [DXY/Yields/XAG/XPT]
              \                         |                    |                    /
               \________________________|____________________|___________________/
                                        |
                                [DATA NORMALIZER]
                                        |
                                 [RISK/REGIME TAGGER]
                                        |
                              Grok (NEWS → ANALYZE → TABLE)
                                        |
                      +-----------------+------------------+
                      |                                    |
            Perplexity (live news JSON)            Gemini (confirm JSON)
                      \                                    /
                       \___________[CONSENSUS / CONFLICT]__/
                                        |
                               ChatGPT VALIDATE (audit)
                                        |
                           [PLACE ORDER MT5] or [BLOCK]
                                        |
                        [BUY FILLED] → BUY SLIP → WhatsApp → Ledger+
                                        |
                      [Monitor + Recheck + Optional Scale-in (slot2)]
                                        |
                        [TP HIT] → SELL SLIP → WhatsApp → Ledger+
                                        |
                               [5-min Quiet] → Next cycle