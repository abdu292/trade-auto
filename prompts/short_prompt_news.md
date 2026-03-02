NEWS vULTRA-360+ (ACTION-CONVERTING | GROK-PRIMARY | TABLE-READY)

PRIMARY RULE:
Grok live rumor feed is source-of-truth for GEO MODE switching (rumors count).
Perplexity/web confirmation is optional and must NEVER block live action.

OUTPUT MUST BE STRUCTURED FIELDS ONLY (no stories).

1) TIME/SESSION
- MT5 server time
- KSA time
- Session: Japan/India/London/NY + phase (open/early/mid/late)
- Minutes to next session + liquidity amplifier (month-end/rollover/holiday?)

2) GROK GEO INTEL (LAST 2H FOCUS)
- war_status: escalate / stable / de-escalate / ceasefire / unknown
- escalation_rumor_strength: none/weak/med/strong
- deesc_rumor_strength: none/weak/med/strong
- KEYWORDS detected: ceasefire/talks/containment/no-further-escalation? (true/false)
- MODE = WAR_PREMIUM / DEESCALATION_RISK / UNKNOWN + confidence 0–100

3) MACRO CONFIRM (FAST)
- DXY direction, US10Y direction, real yields direction
- Oil direction, equity futures risk tone
- macro_bias = supportive/neutral/hostile

4) FLOW/PHYSICAL (ONLY IF FRESH)
- ETF flows headline? yes/no
- CB headline? yes/no
- physical premiums rising? yes/no

5) PRICE/VOLATILITY SNAPSHOT (FROM MT5)
- current price, today range, ADR_used%
- ATR M15 vs median (low/normal/high/extreme)
- RSI H1 & H4
- distance from MA20 (ATR units)
- phase = compression / early expansion / late expansion-exhaust / shock / mean-revert

6) WATERFALL RISK (NEWS+PRICE COMBINED)
- waterfall_risk = LOW/MED/HIGH
RULE:
HIGH if (deesc rumor strong) OR (wide red M15 breaks shelf) OR (ADR_used>150% + RSI>74 + stall)

7) ACTION STATE (MANDATORY)
- ATTACK: MODE=WAR_PREMIUM & waterfall LOW & phase=expansion
- CONTROLLED: WAR_PREMIUM but MED risk or late expansion
- DEFENSIVE: UNKNOWN or mixed + MED risk
- STAND-DOWN: DEESCALATION_RISK or HIGH risk

8) PERMISSION MATRIX (MANDATORY)
- Rail-A: allowed / only-after-structure / blocked
- Rail-B: allowed / blocked
- Pyramid adds: enabled/disabled
- Max deployment allowed (0/25/50/75/100%) based on affordability + risk

9) HANDOFF NOTE FOR ANALYZE/TABLE
- 2–4 “activation triggers” (IF/THEN) for next 2–4 hours
- 2–4 “veto triggers” (kill-switch)