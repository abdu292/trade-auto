# Trader Mode Guide (Plain Language)

This guide explains how the system behaves in each trading mode, in simple trader terms.

## 1) Standard Mode

Think of this as the normal, balanced profile.

- The system trades more conservatively.
- It prefers safer entries and can reduce or block aggressive expansion entries when risk rises.
- It uses normal protections for spread, alignment, and volatility.
- Best when market is not in extreme war-headline expansion.

Typical behavior:
- More Buy Limit style behavior than aggressive pyramid continuation.
- Faster refusal of weak setups.
- Controlled sizing with capacity checks.

## 2) WarPremium Mode

Think of this as the emergency high-volatility profile for war-spike conditions.

- The system is allowed to harvest expansion more aggressively (Rail-B behavior).
- It still has strict firewalls to avoid waterfall traps.
- It can stage into strength and use mode/risk state to control adds.
- It can hard-kill pending orders if de-escalation or waterfall risk appears.

Typical behavior:
- Uses mode state (`WAR_PREMIUM`, `DEESCALATION_RISK`, `UNKNOWN`) from AI mode feed.
- Prioritizes continuation entries only when conditions stay clean.
- Blocks or pauses additions when risk escalates.
- First-leg ban can activate after kill-switch until base forms again.

## 3) What Changes Between Modes

- Entry aggressiveness: WarPremium > Standard
- Safety intervention speed: both are strict, WarPremium is stricter during de-escalation signals
- Continuation staging: emphasized in WarPremium
- Default profile at seed: Standard

## 4) How to Switch Modes

Use backend strategy endpoint:

1. Get profiles from `/api/strategies`
2. Activate selected profile with `/api/strategies/{id}/activate`

After activation, the decision engine uses that profile for live evaluations.

## 5) Simulator Behavior by Mode

Simulator can mimic each profile.

Start simulator with:
- `strategyProfile: "Standard"` for normal behavior
- `strategyProfile: "WarPremium"` for war-style behavior (higher shock probability, stronger impulse conditions)

Endpoint:
- `POST /api/monitoring/simulator/start`

Check status:
- `GET /api/monitoring/simulator/status`

## 6) Practical Trader Notes

- If headlines are chaotic and expansion is strong, use WarPremium only if you want war-style behavior.
- If conditions are mixed/unclear, keep Standard active.
- If you see de-escalation headlines, WarPremium protections are designed to cut new pending exposure quickly.
- Always monitor runtime status and pending queue depth in monitoring endpoints.
