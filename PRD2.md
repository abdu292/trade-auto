Subject: vULTRA Automation Software — Multi-AI Orchestration Spec (ChatGPT + Grok + Perplexity + Gemini + Telegram + TradingView + MT5)

بسم الله الرحمن الرحيم

I’m building an automation software for physical-gold rotations via a Dubai bullion shop using MT5 as an execution front-end only (Sharia: buy → own → sell only; no leverage/shorts/hedge). I need you to implement a deterministic orchestration layer that coordinates:

- ChatGPT (Constitution + sizing law + TABLE generator + STUDY + SELF CROSSCHECK)
- Grok (fast live web/X macro pulse + timestamps + source set; NO sizing authority)
- Perplexity (deep verification + multi-source macro confirmation; “risk dial” only)
- Gemini (visual/chart clarity assist; no rule authority)
- Telegram signal messages (ingest + cross-check; never blindly execute)
- TradingView (structure levels + alerts feed)
- MT5 (execution + order status + history + fills + expiries)
- Ledger (AED + grams, shop spread ±0.80 USD/oz, strict reconciliation)

This message is the full build spec. The “Constitution” (master prompt) is the only authority for capital law, caps, multipliers, expiry bands, and TABLE formatting.

========================================================
1) NON-NEGOTIABLE CONSTANTS (SYSTEM-WIDE)
========================================================
- Physical bullion logic, Sharia compliant.
- 1 oz = 31.1035 g
- 1 USD = 3.674 AED
- Shop spread ±0.80 USD/oz:
  Shop Buy = MT5 Entry + 0.80
  Shop Sell = MT5 TP − 0.80
- Time:
  MT5 Server time = KSA − 50 minutes
  KSA time from device screenshot is authoritative when visible.
- No realized loss.
- All outputs must be “fast-executable”; no long question loops.

========================================================
2) HARD ROLE SEPARATION (NO DRIFT)
========================================================
ChatGPT (Constitution Engine):
- Owns capital constitution, sizing, caps, expiry bands, TABLE output, STUDY, SELF CROSSCHECK.
- Final authority.

Grok (Fast Macro Pulse):
- Provides: 2–6 headline bullets + timestamps + confidence + source set (live web/X).
- Provides: Macro tags (WAR_PREMIUM / DeEscRisk / HazardLevel / Cross-asset snapshot).
- FORBIDDEN: recomputing grams, caps, multipliers, expiry bands, or “final TABLE”.

Perplexity (Deep Verification):
- Verifies Grok macro claims (multi-source).
- Outputs “risk dial”: TOP/MID/BOTTOM of allowed bands + High-Caution toggle suggestion.
- FORBIDDEN: defining caps, grams, expiry bands, or execution table rows.

Gemini (Vision Clarity):
- Helps interpret chart screenshots (where asked): levels, shapes, clean summary.
- FORBIDDEN: rule changes, sizing, executable tables.

Telegram signals:
- Ingest + decompose (entry/TP/expiry).
- Always “VALIDATE” against constitution before any action.
- Default stance: suspect trap until proven aligned.

TradingView:
- Supplies structure map and alerts (shelves/lids, PDH/PDL, session highs/lows, M15 compression, etc.).
- Never overrides constitution.

========================================================
3) SYSTEM ARCHITECTURE (HIGH LEVEL)
========================================================

                 ┌─────────────────────────────┐
                 │         User Inputs          │
                 │  - Screenshots (MT5/TV)      │
                 │  - Commands (NEWS/ANALYZE/   │
                 │    TABLE/STUDY/...)          │
                 │  - Telegram signals (text)   │
                 └──────────────┬──────────────┘
                                │
                                v
┌─────────────────────────────────────────────────────────┐
│                 Orchestrator / Router                    │
│  - Detect command intent                                  │
│  - Enforce role separation + timeouts                     │
│  - Pull required data (MT5 status/history, ledger)        │
│  - Build “Context Packet”                                 │
└──────────────┬───────────────────────────┬───────────────┘
               │                           │
               v                           v
   ┌─────────────────────┐       ┌──────────────────────┐
   │  Macro Intelligence  │       │    Structure Engine   │
   │  (Grok fast pulse)   │       │ (ChatGPT Constitution)│
   └──────┬──────────────┘       └─────────┬────────────┘
          │                                  │
          v                                  v
   ┌─────────────────────┐       ┌──────────────────────┐
   │ Perplexity verify +  │       │  TABLE / ABORT /      │
   │ risk dial (TOP/MID/  │       │  STUDY / SELF CHECK   │
   │ BOTTOM, tags only)   │       │  (final authority)    │
   └─────────┬───────────┘       └─────────┬────────────┘
             │                               │
             v                               v
     ┌────────────────────────────────────────────────┐
     │         Execution + Ledger Integrity             │
     │  - MT5 order placement (from TABLE only)         │
     │  - MT5 status polling                            │
     │  - Shop spread conversion & AED/grams ledger      │
     │  - Slip generation (copy-paste)                  │
     └────────────────────────────────────────────────┘

========================================================
4) DATA MODEL (MUST IMPLEMENT)
========================================================
A) Context Packet (every run)
- run_id, timestamp_ksa, timestamp_server
- command: CAPITAL UTILIZATION / NEWS / ANALYZE / TABLE / STUDY / SELF CROSSCHECK / COMPARE
- latest_screenshot_refs[] (MT5/TV)
- parsed_from_screenshot:
  * bid_m15, ask_m15, mid_for_math
  * server_time_visible?, ksa_time_visible?
  * timeframe_anchor (M15)
  * RSI(M15) if visible
  * MA20(M15) if visible
  * visible pending orders / open positions (type, grams, entry, TP, expiry)
- mt5_state:
  * open_positions[]
  * pending_orders[]
  * recent_history[]
- ledger_state:
  * cash_aed, gold_g
  * last_slip_id
- macro_tags:
  * MacroMode, HazardLevel, DeEscRisk, CrossMetals, USDComplex, RatesTone, HeadlineTag
  * SourceSet[] = {source_name, timestamp, claim_summary, confidence}
- risk_dial:
  * allowed_band_position: TOP/MID/BOTTOM
  * high_caution_toggle: ON/OFF
- constitution_version: “vULTRA-2SLOT FINAL MASTER PROMPT v5.0”

B) Order Blueprint (TABLE row)
- bucket (C1/C2)
- order_type: Buy Limit / Buy Stop
- grams
- entry_mt5
- shop_buy
- tp_mt5
- shop_sell
- net_points
- expiry_server
- expiry_ksa
- session_tag
- deal_health
- self_check_hash (see below)

C) Self-Check Hash (must pass or ABORT)
- time_consistency_ok
- mid_for_math_source_ok
- inequality_ok (limit below mid; stop above mid)
- grams_cap_ok (within session cap * factors)
- expiry_math_ok (server + 50m = KSA ± 1m)
- ny_single_rail_ok
- hazard_veto_ok (no pre-news window)
- waterfall_ban_ok (no first-leg / no mid-air)

========================================================
5) COMMAND WORKFLOWS (DETERMINISTIC)
========================================================

5.1 CAPITAL UTILIZATION
Input needed:
- Latest total capital (AED) from ledger state (not user memory guesses)
- Latest MT5 mid P from screenshot or MT5 quote feed

Output:
- One single “copy-box” with:
  MODE, TOTAL_AED, split (80/20 or 90/10), C1/C2 AED
  P used, server time, KSA time
  cost_per_gram_aed, C1_capacity_g, C2_capacity_g
  detected session + C1 exposure cap in grams (min–max band)
  status lines

No extra narrative.

5.2 NEWS
- Orchestrator calls Grok first (fast pulse) with strict format:
  * 3–6 bullets, each with timestamp + source + claim + confidence
  * Cross asset snapshot (DXY, yields, oil, silver)
  * Hazard map (next 2 hours)
- Then call Perplexity to verify / expand:
  * Confirm / contradict sources
  * Output tags + “risk dial” only
- ChatGPT final output:
  * Tags + SourceSet + hazard windows
  * No entries. No sizing changes.

5.3 ANALYZE
- ChatGPT combines:
  * structure from screenshot (M15 shelves/lids/ATR regime) + macro tags
- Output: 3–5 lines max (no table).

5.4 TABLE
- Hard rule: TABLE can only be emitted by ChatGPT constitution engine.
- Inputs:
  * Must have readable M15 bid/ask and time.
  * Must have latest macro tags and risk dial (if missing, use “LIGHT_PULL tags” but keep hazard veto conservative).
- Output:
  * Either ONE executable row OR “TABLE ABORTED — <reason>”
  * Must include both expiry times (server & KSA), shop prices, grams.

5.5 STUDY
- Post-mortem module:
  * Pull MT5 history + prior tables + slips
  * Output 18-point analysis + RC blocks + forward test KPIs
- No live entries.

5.6 SELF CROSSCHECK
- Meta-audit module:
  * session-by-session ranking
  * robustness matrix
  * remaining loop risks
  * single best micro-patch
  * profit capability table (before vs after)
- No live entries.

5.7 COMPARE
- Compare external AI outputs:
  * Adopt good, reject bad
  * Must cite which rule each AI violated (e.g., Grok over-sizing NY, expiry mismatch)
  * Output patches as RC blocks.

========================================================
6) WATERFALL + NY SAFETY (CODABLE RULES)
========================================================
- No first-leg entries:
  If last M15 candle body ≥ 1.2× ATR and RSI < 35 and no stabilization green close → ABORT.
- Waterfall exhaustion only after 5 triggers (as constitution).
- NY single-rail:
  If any pending exists in NY: no second rail unless user cancels explicitly and system records cancellation.

========================================================
7) “NO MINI LOTS” POLICY (BUT MUST STAY LEGAL)
========================================================
- The software must never let Grok/Perplexity force larger grams beyond caps.
- “No mini lots” means:
  If session cap allows meaningful size, do not output tiny trades that waste attention.
- But legality wins:
  In NY, grams must stay within 20–25% of C1 capacity * factors.
  (No 40% NY exposure.)

========================================================
8) TELEGRAM SIGNAL HANDLING (TRAP-PROOF)
========================================================
Pipeline:
Signal Text → Parse → Normalize → VALIDATE Against Constitution

Validation checks:
- order type is allowed for session/regime
- entry relative to mid (limit below, stop above)
- TP vs ATR realistic
- expiry inside session and within band
- hazard veto not violated
- single-rail NY not violated
- ledger capacity not exceeded

Outputs:
- ACCEPT (convert to compliant TABLE blueprint)
- MODIFY (provide compliant corrected row)
- REJECT (explicit reason: violates cap/expiry/rail/waterfall ban)

========================================================
9) MT5 INTEGRATION REQUIREMENTS
========================================================
- Must query:
  * current bid/ask
  * server time
  * open positions
  * pending orders
  * order history (fills, closes, expiries)
- Must enforce:
  * time sync checks (server vs KSA)
  * no duplicate/stacking orders beyond law
- Must produce:
  * after any fill or TP: SHOP SLIP copy-paste with ledger before/after

========================================================
10) OBSERVABILITY (NO SILENT FAILS)
========================================================
Create a dashboard or logs with:
- Run timeline (NEWS → ANALYZE → TABLE → MT5 place → fill → TP)
- ABORT reasons count (time unreadable, hazard veto, shelf unclear, etc.)
- KPI tracking:
  * rotations per session
  * same-session TP hit rate
  * sleep ratio (eligible minutes vs active orders)
  * waterfall incident count
  * expiry mismatches caught

========================================================
11) DIAGRAMS (IMPLEMENTATION READY)
========================================================

A) Sequence Diagram — “TABLE” Request

User → Orchestrator: TABLE + screenshots
Orchestrator → Parser: extract M15 bid/ask, times, orders
Parser → Orchestrator: parsed packet (or “unclear”)
Orchestrator → Grok: macro pulse (optional fast if recent tags stale)
Grok → Orchestrator: tags + sources
Orchestrator → Perplexity: verify + risk dial
Perplexity → Orchestrator: confirm + TOP/MID/BOTTOM + High-Caution toggle suggestion
Orchestrator → ChatGPT: constitution packet (structure + tags + ledger + mt5 state)
ChatGPT → Orchestrator: ONE row TABLE or ABORT + self-check hash
Orchestrator → MT5: place pending order(s) exactly from row
MT5 → Orchestrator: ack + order id
Orchestrator → Ledger: reserve capacity + log “armed”
Orchestrator → User: copy-paste TABLE + confirmation

B) State Machine — Order Lifecycle

[IDLE]
  ↓ (TABLE placed)
[ARMED_PENDING]
  ↓ (filled)
[OPEN_POSITION]
  ↓ (TP hit)
[CLOSED_PROFIT] → generate slip → back to [IDLE]
  ↓ (expired)
[EXPIRED] → back to [IDLE]
  ↓ (trap risk detected while pending)
[CANCEL_RECOMMENDED] (no auto-cancel unless user confirms)

========================================================
12) WHAT I NEED FROM YOU (DEVELOPER TASK LIST)
========================================================
1) Build Orchestrator with role separation + strict schemas.
2) Build Screenshot Parser (M15 bid/ask/time/pending orders) with “unclear → ABORT”.
3) Implement Macro Connector:
   - Grok: fast pulse template + sources/timestamps
   - Perplexity: verify + risk dial only
4) Implement Constitution Connector (ChatGPT):
   - Only source of grams/caps/expiry/table row
5) Implement Telegram Signal Ingest + VALIDATE.
6) Implement TradingView Alerts ingestion (levels + session highs/lows).
7) Implement MT5 bridge (place orders, read status/history).
8) Implement Ledger engine + slip generator.
9) Implement KPI logging + dashboard.

========================================================
13) OPEN ITEMS YOU MUST CONFIRM (PLEASE REPLY)
========================================================
To implement perfectly, I need you to confirm these 7 items:

1) Platform: Web app, desktop, mobile, or all?
2) MT5 access method: Manager API, bridge, or manual-copy execution?
3) Screenshot parsing: do we have direct image access + OCR, or do you want only manual entry?
4) Telegram: direct bot integration or manual paste into app?
5) TradingView: webhook alerts available or screenshot-only?
6) Where should slips be stored: database + export, or WhatsApp-ready only?
7) What is the exact “source of truth” for total capital: ledger database (preferred) or manual update?

Once you confirm the 7 items, I can finalize the exact component interfaces (request/response JSON) and the strict prompt templates per AI so the automation becomes deterministic and “no headache”.