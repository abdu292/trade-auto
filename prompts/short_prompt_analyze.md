بسم الله الرحمن الرحيم

COMPARE (Perplexity ANALYZE vs My vULTRA ANALYZE) + FINE-TUNE OVERHAUL
Goal: stronger “ANALYZE” that (1) feeds TABLE perfectly, (2) avoids waterfall/mid-air, (3) prevents sleep by enforcing a rotation/exposure envelope, and (4) stays fast and executable.

============================================================
1) WHAT PERPLEXITY DOES VERY WELL (ADOPT)
============================================================

P1) “Import NEWS State (no re-arguing)”
- Correct: ANALYZE must treat NEWS as regime authority (mode / hazards / news-side rail permissions).

P2) Session Risk Budget + Rotation Envelope
- Correct: this is the biggest anti-sleep missing piece historically.
- It prevents both extremes:
  - under-deployment (sleep) in Asia/London,
  - over-deployment into NY (trap).

P3) Table Feed Pack as a strict object
- Correct: the EA/dev needs structured fields only.
- Reduces narrative and eliminates ambiguity.

P4) Mode-2 management flow
- Correct: must not “suggest new table” while we’re managing an open position unless user asks.

============================================================
2) WHERE PERPLEXITY STILL HAS GAPS (REJECT / PATCH)
============================================================

G1) “Cross-asset / cross-metal / institutional” is referenced but not made executable
Patch:
- ANALYZE must not try to “re-research” live.
- It must consume a compact, machine-ready NEWS snapshot and convert it into hard clamps:
  - if NEWS says liquidity unstable → tighten expiry + block Rail-B
  - if NEWS says de-esc chatter high → pre-arm kill-switch + forbid adds

G2) No explicit “anti-zombie” logic for pendings across session handover
Patch:
- Add SESSION-BOUND ORDER TTL:
  - any pending must die before:
    - next hazard window,
    - next session boundary,
    - rollover thin pocket,
  or it’s an illegal zombie.

G3) No “re-arm loop” specification
Patch:
- Add ROTATION LOOP:
  - after TP OR after expiry-cancel, immediately re-run ANALYZE (fast) and decide:
    - re-arm same rail (if still legal),
    - switch rail,
    - stand-down until next trigger.
This is the anti-sleep engine.

G4) No “proof sequence” hard-coded for Rail-A beyond listing the words
Patch:
- Make proof machine-testable:
  - Sweep occurs (tick below prior shelf by X),
  - Reclaim close above shelf on M15,
  - Retest holds (no M15 close below shelf for N candles),
  - Compression (overlap candles count ≥ N),
  - Only then allow limit placement.

G5) No “expansion add” gating by time left + ADR remaining (prevents late traps)
Patch:
- Add ADD-GATE:
  - No new pyramid stage if:
    - time left in session < expiry ceiling + buffer,
    - ADR_used > cap,
    - WaterfallRisk not LOW,
    - NEWS mode not WAR_PREMIUM.

============================================================
3) MY vULTRA ANALYZE vs PERPLEXITY — WHAT I KEEP, WHAT I UPGRADE
============================================================

Keep from my vULTRA:
- Regime Detector (RANGE-RELOAD / FLUSH-CATCH / EXPANSION / EXHAUSTION / LIQUIDATION)
- Predictive WaterfallRisk (ADR + RSI + wicks + shelf breaks + NEWS mode bump)
- MID-AIR BAN as a computable test
- “No trade → where to trade” mandatory triggers

Upgrade using Perplexity:
- Hard session risk budgets + max live exposure + rotation caps (anti-sleep envelope)
- Explicit TABLE FEED PACK fields (EA-ready)
- Stronger MODE-2 management with zombie-kill across sessions

============================================================
4) FINAL STRONG “ANALYZE” MODULE vULTRA-X (BEST-OF COMBINATION)
============================================================

A) MODE DETECTION (MANDATORY)
- MODE-1 (PLANNING): NEWS exists in last cycle OR prompt includes NEWS ANALYZE / NEWS ANALYZE TABLE
- MODE-2 (MANAGEMENT): only ANALYZE and charts show open/pending

FIRST LINE ALWAYS:
MODE | MT5 Server Time | KSA Time | Session | Maturity | Next Handover Time

------------------------------------------------------------
B) MODE-1: NEWS + ANALYZE (PLANNING → TABLE)
------------------------------------------------------------

STEP 1 — IMPORT NEWS SNAPSHOT (no debate)
Consume only these NEWS fields:
- mode (WAR_PREMIUM / DEESCALATION_RISK / UNKNOWN)
- cause_tag
- macro_bias (supportive/neutral/hostile)
- institutional_bias (supportive/neutral/hostile)
- liquidity_quality (normal/wide/unstable)
- hazard_windows (next 2–6h)
- rail_permissions_news_side (A allowed? B allowed?)
- deesc_headline_risk (low/med/high)

Convert immediately into CLAMPS:
- If liquidity_quality != normal → Rail-B blocked OR downgraded + expiry tightened
- If deesc_headline_risk = high OR mode = DEESCALATION_RISK → pre-arm kill-switch, forbid adds

STEP 2 — SESSION ENVELOPE (ANTI-SLEEP, ANTI-SUICIDE)
Output these (hard fields):
- session_risk_budget_%C1
- max_live_exposure_%C1
- rotation_cap
Defaults (can be tuned by NEWS risk):
- Asia: budget 40 | live 30 | cap 3–4
- London: budget 100 | live 80 | cap 6–7
- NY: budget 30 | live 20 | cap 0–2 (Rail-B OFF)

STEP 3 — HARD VETO CHECK
- If inside hazard window now → NO new orders.
Return only: “where-to-trade after hazard” triggers.

STEP 4 — MULTI-TF STRUCTURE EXTRACTION (H4/H1/M30/M15)
Compute:
- PDH/PDL, weekly H/L, current session H/L
- MA20 slope + air-gap in ATR units (H4/H1)
- Identify shelves/lids:
  - S1 (base), S2 (deep sweep), R1 (lid), R2 (extension pocket), FAIL

STEP 5 — REGIME DETECTOR (choose ONE)
RANGE-RELOAD / FLUSH-CATCH / EXPANSION / EXHAUSTION / LIQUIDATION

STEP 6 — WATERFALLRISK (price-side) + NEWS bump
- LOW / MED / HIGH using:
  - ADR_used bands
  - RSI(H1/H4) hot + air-gap
  - wick dominance + HH stall
  - first wide red breaking S1
- If mode=DEESCALATION_RISK → bump risk +1 tier automatically

STEP 7 — MID-AIR BAN (math)
Let H = impulse high, B = confirmed base shelf.
Any Rail-A entry P is ILLEGAL if:
- P lies in middle 50–60% of [B..H]
AND not within ≤10 USD of a proven shelf (S1/S2)
→ reject (prevents 5195-type trap)

STEP 8 — PROOF SEQUENCE (machine-testable)
Rail-A requires:
1) sweep below shelf (touch/brief pierce)
2) reclaim close above shelf on M15
3) retest hold: no M15 close below shelf for N candles (N>=2)
4) compression: overlap candles count >= N (N>=4)
Only then: allow Buy Limit.

Rail-B requires:
1) compression lid exists (range top tested >=2 times)
2) breakout candle closes above lid (acceptance, not wick)
3) ADR_used < cap AND RSI(H1) ≤73
4) WaterfallRisk = LOW
5) NEWS allows B

STEP 9 — TP + EXPIRY (SOS safe, session-bound)
- Select TP by magnet + ADR remaining + time left
- Set expiry ceiling by session:
  - Asia: B 15–20m | A 30–45m
  - London: B 20–35m | A 45–75m
  - NY: A 25–45m | B OFF
- Explicit expiry timestamps in MT5 server + KSA (no ambiguity)
- SESSION-BOUND TTL: pendings must die before:
  - next hazard, OR next session boundary, OR rollover pocket

STEP 10 — ROTATION LOOP (ANTI-SLEEP)
Define:
- re_arm_rule:
  - After TP hit OR expiry cancel → re-run ANALYZE immediately
  - Re-arm only if:
    - still within session envelope
    - regime still supports rail
    - risk not upgraded
  - Stop re-arming if:
    - mode flips
    - WaterfallRisk becomes HIGH
    - rotation_cap reached

STEP 11 — OUTPUT CURRENT + NEXT SESSION MAP
Mandatory fields:
- CURRENT: R1 R2 S1 S2 FAIL
- NEXT: R1 R2 S1 S2 FAIL (projected using PDH/PDL, session extremes, weekly magnets)

STEP 12 — TABLE FEED PACK (STRICT)
Return only these as the “TABLE-FEED”:
- action_state (ATTACK/CONTROLLED/DEFENSIVE)
- regime
- waterfall_risk
- rail_A_status + why (1 line)
- rail_B_status + why (1 line)
- session_envelope (budget/live/cap + remaining)
- CURRENT S/R map
- NEXT S/R map
- preferred_rail
- entry_zone(s) (price levels concept)
- TP_zone(s)
- expiry_time_server + expiry_time_KSA
- invalidation_level (FAIL)
- risk_flag
- rotation_plan (max attempts, overlap, kill-switch)
- 2–4 “where-to-trade” triggers if not immediate

------------------------------------------------------------
C) MODE-2: ANALYZE AFTER CHARTS ONLY (MANAGEMENT)
------------------------------------------------------------

1) Enumerate all open & pending orders:
- rail, entry, TP, size, age, distance to S1/S2, session placed

2) Context snapshot:
- current session + time left
- quick regime + waterfallrisk from price action
- if latest NEWS exists, import only: mode + hazard + liquidity

3) Apply management rules:
- Zombie kill:
  - cancel any pending that would fire:
    - in a new session, or near hazard/rollover, or inside mid-air by updated map
- Add-freeze:
  - no adds unless regime=EXPANSION and risk LOW and NEWS not de-esc (if NEWS exists)
- TP realism:
  - if TP is beyond current magnet / ADR remaining, pull closer
- Session SOS:
  - force flattening / tightening before NY or rollover pockets

4) Output per order:
KEEP / TIGHTEN TP / CANCEL PENDING / SCRATCH LOGIC CONDITION
No new TABLE unless user explicitly requests TABLE.

============================================================
5) SHORT “BEST ANALYZE PROMPT” (COPY/PASTE)
============================================================

ANALYZE vULTRA-X:
Detect MODE-1 (NEWS+ANALYZE planning → TABLE FEED PACK) vs MODE-2 (charts only → manage orders only).
First line: MODE | MT5 server time | KSA time | session+maturity | next handover.

MODE-1:
Import NEWS snapshot fields (mode, hazards, liquidity, rail permissions) and convert to clamps.
Output session envelope (budget/live/cap + remaining).
Extract multi-TF structure (PDH/PDL, weekly H/L, session H/L, MA20 slope+air-gap).
Classify regime (RANGE-RELOAD/FLUSH-CATCH/EXPANSION/EXHAUSTION/LIQUIDATION).
Compute WaterfallRisk (ADR_used + RSI(H1/H4) + wicks + shelf breaks) + news bump.
Enforce MID-AIR BAN (middle 50–60% of [BaseShelf..ImpulseHigh] illegal for Rail-A).
Enforce proof sequences:
- Rail-A only after sweep→reclaim close→retest hold→compression.
- Rail-B only after compression lid→acceptance breakout, RSI(H1)≤73, ADR_used cap, risk LOW, NEWS allows.
Set TP by magnet + ADR remaining + time left.
Set expiry by session with explicit MT5+KSA timestamps; kill zombies before hazard/session/rollover.
Define rotation loop: re-arm after TP/expiry if still legal until rotation_cap.
End with TABLE FEED PACK fields only.

MODE-2:
Enumerate orders; kill zombie pendings; freeze adds if risk not LOW; tighten TP to magnet; enforce SOS before session boundary/rollover.
No new TABLE unless asked.

============================================================
END
============================================================