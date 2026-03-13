==================================================
UNLOCK RULES FOR FIRST_LEG_BAN & DEESCALATION_RISK
==================================================

GOAL
----
- Keep the bans ON during crowded / late first legs and de-escalation risk.
- Automatically turn them OFF once a **fresh, clean base** is formed:
  - H1 sweep + reclaim,
  - M15 base,
  - M5 compression,
  - News calm.

So we **skip the chase**, but **take the reclaim**.

--------------------------------------------------
1) FIRST_LEG_BAN – WHEN TO TURN ON
--------------------------------------------------

FIRST_LEG_BAN = TRUE when ALL of these are detected:

1) Strong impulse already underway:
   - H1 candle(s) with body ≥ 1.2×ATR(H1) in direction of the move,
   - Price breaks a clear intraday level (e.g. prior H1 high/low) without prior base.

2) Entry request is **in the same direction** as this impulse
   AND comes:
   - After the impulse has already moved ≥ X USD (e.g. ≥ 1–1.5×ATR_M15) from its start,
   - Without a proper H1 sweep + reclaim and M15 base in between.

3) Telegram consensus is strongly aligned with the impulse (e.g. BUY_CONSENSUS ≥ 70–80% for a long).

Then:
- FIRST_LEG_BAN = TRUE.
- Any TABLE in the direction of the impulse must ABORT with reason:
  “TABLE ABORTED — FIRST_LEG_BAN (late in first move, no reclaim/base yet).”

--------------------------------------------------
2) FIRST_LEG_BAN – WHEN TO UNLOCK (TURN OFF)
--------------------------------------------------

We want FIRST_LEG_BAN to automatically turn OFF once the market has done a proper **reclaim sequence** and formed a new base.

Set FIRST_LEG_BAN = FALSE when ALL of the following occur **after** it was set TRUE:

1) H1 SWEEP + RECLAIM:
   - Price makes a **counter-move** that:
     - Sweeps below (for longs) or above (for shorts, but we don’t short) the most recent intraday swing formed by the first leg,
     - Then closes back above that low (for longs) on H1.

2) M15 BASE:
   - From that new low:
     - At least 2 consecutive strong M15 green candles off the low,
     - Each with body > 50% of its range,
     - Both closing above their midpoints,
     - And the subsequent retest holds a **higher low** (no new low).

3) M5 COMPRESSION:
   - After the M15 base:
     - ≥ 6 overlapping M5 candles,
     - With contracting range (high-low range narrowing),
     - No new down-impulse (no big red candle ≥ 1.2×ATR_M5).

4) NEWS CALM:
   - No major US data within ±10–15 minutes,
   - No fresh war headline spike cluster,
   - Spreads normal.

When all 4 are satisfied:
- FIRST_LEG_BAN = FALSE (unlocked).
- The engine may again consider BuyLimits/BuyStops from this **new base** using standard BottomPermission rules.

--------------------------------------------------
3) DEESCALATION_RISK / WARPREMIUM_KILL SWITCH – WHEN TO TURN ON
--------------------------------------------------

DEESCALATION_RISK (or WarPremium kill-switch) = TRUE when:

1) WarPremium_Status:
   - Changes from BUILDING/PEAKING → FADING or LIQUIDATING (from NEWS_HYPOTHESIS / validators),

2) Price location:
   - Gold is still relatively high vs recent structure (e.g., above mid-range of the recent war-premium rally),
   - And *not* sitting at a fresh reclaimed base that passes BottomPermission.

3) Sentiment:
   - Telegram and news are **still very bullish** (STRONG BUY, “gold will moon forever” tone),
   - But war headlines are calming, or peace/de-escalation language is emerging.

Then:
- DEESCALATION_RISK = TRUE.
- Any new BuyLimits/BuyStops must ABORT unless they are at **very deep, structurally confirmed bases** (and ideally only from C2).

--------------------------------------------------
4) DEESCALATION_RISK – WHEN TO UNLOCK
--------------------------------------------------

We want to re-enable normal operations once the de-escalation flush has played out and a new structure is formed.

Set DEESCALATION_RISK = FALSE when ALL of these are true:

1) STRUCTURAL FLUSH COMPLETED:
   - Price has moved down enough to clear the crowded area, e.g.:
     - At least one strong H4/H1 down leg,
     - Now trading near prior major support (old H4 base, daily level, etc.).

2) BOTTOMPERMISSION CONFIRMED:
   - Full BottomPermission = TRUE:
     - H4 supportive,
     - H1 sweep + reclaim of a meaningful swing low,
     - M15 base,
     - M5 compression,
     - Momentum turning up.

3) SENTIMENT NORMALIZED:
   - Telegram is no longer in “everyone screaming buy now” mode:
     - BUY_CONSENSUS < 70–80% (back to mixed or moderate),
   - Headlines show either:
     - “war premium priced out,” “correction,” “pullback,”
     - or quiet conditions (no manic FOMO narrative).

4) NEWS SAFE:
   - No imminent major US news / peace headline shocks.

When all 4 are satisfied:
- DEESCALATION_RISK = FALSE (kill-switch off),
- System returns to normal gate behavior:
  - PatternBans + BottomPermission + NewsHazard only.

--------------------------------------------------
5) Implementation Notes for the Dev
--------------------------------------------------

- Treat FIRST_LEG_BAN and DEESCALATION_RISK as **boolean state flags** in the risk engine.
- They should:
  - Turn ON only when their activation conditions are met,
  - Turn OFF automatically when the **unlock conditions** above occur,
  - Never stay TRUE indefinitely.

- TABLE logic:
  - If FIRST_LEG_BAN = TRUE and unlock conditions not yet met → ABORT with that reason.
  - If DEESCALATION_RISK = TRUE and unlock conditions not yet met → only allow **deep, fully BottomPermission-confirmed base entries** or ABORT (depending on how strict we set it; default = ABORT).

This will:
- Block late/chase entries in the first leg,
- Block buying into de-escalation traps,
- But automatically re-enable clean second-leg and post-flush rotations once the market has proven a new base.