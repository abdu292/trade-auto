# بسم الله الرحمن الرحيم
# EXTERNAL SIGNAL INTEGRATION + ANTI-LOOP HARD-LOCK
Version: EXTERNAL-SIGNAL-v2.0

## 11.a Consolidated Answer Rule (Anti-Loop)
Standing intent:
“Apply every refinement that increases safe profit and rotation without increasing waterfall, mid-air, or hazard risk.”
Operational consequences:
- integrate all relevant safe-profit refinements in one consolidated response
- no drip-feeding of hidden improvements
- resolve full structure internally first
- questions only for true binary design forks
- otherwise apply improvements automatically

## External Signal Engine
External sources are advisory, not direct order triggers.

Allowed sources:
- whitelisted Telegram channels
- TradingView alerts / webhooks from approved systems
- internal analyst notes if added later

## Telegram Signal Parser
Normalize each signal into:
- source_tag
- timestamp
- symbol
- direction
- zone_low
- zone_high
- stop_loss
- tp_pips_list
- comment_tags

XAU pip convention:
- 1 pip = 0.10 USD

Convert:
tp_price_i = approx_entry_price + (tp_pips_i * 0.10)

## Telegram Alignment
If external zone overlaps S1/S2/S3 by price and timing:
- telegramZoneAligned = TRUE
- small confidence boost
Else:
- telegramZoneAligned = FALSE
- advisory only

Hard law:
Telegram signals never bypass:
- safety blockers
- candidate lifecycle
- TABLE
- capital laws

## Minimum Profit Filter with Telegram
If projectedMoveUSD from zone < 8 USD:
- ignore as micro-scalp
If >= 8 USD:
- may proceed through candidate lifecycle if all other gates pass

## TradingView Integration
TradingView alerts are structured advisory signals.
If TV zone overlaps S1/S2/S3:
- stronger alignment boost than Telegram
Still cannot bypass safety or TABLE.

## External Inputs Affect Only
- candidatePriority
- confidenceScore
- impulseHarvestScore
- zoneWatchActive
- expiryMode

They must not create ARMED candidates by themselves.
