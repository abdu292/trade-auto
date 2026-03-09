
# FINAL REFINEMENT + UI REQUIREMENT FOR TRADE AUTO EA

**Purpose:**  
Use current free balance **2237.42 AED** for live experience while keeping existing **2292g gold inventory separate**.  
Goal: increase **safe monthly rotations** using **BUY_LIMIT / BUY_STOP only**, avoid waterfalls, reduce manual questioning, and provide **clear visual confidence via UI**.

---

# A) UI REQUIREMENT — GRAPHICAL TRADE MAP

The application must include a **live graphical display** showing exactly what the engine is seeing and doing.

### Core rule
The UI must **not compute trading logic**.  
It must only **visualize engine output**.

### Chart view requirements

Display for XAUUSD:

- Current price
- Recent candles (M5 or M15)
- Engine structure levels:
  - Bases
  - Lids
  - Sweep levels
  - Compression zones
  - FAIL level
  - Liquidity magnets / TP zones

### Engine overlay

Overlay the following directly on chart:

**Pending orders**
- BUY_LIMIT
- BUY_STOP
- TP level
- Expiry window

**Pattern Detector signals**
- WATERFALL_RISK (red)
- CONTINUATION_BREAKOUT (green)
- RANGE_RELOAD / LIQUIDITY_SWEEP (blue)
- FALSE_BREAKOUT / SESSION_TRANSITION_TRAP (amber)

**PRETABLE result**
- SAFE → green
- CAUTION → yellow
- BLOCK → red with reason

### Tooltip details

On tap show:

- orderType
- grams
- entryPrice
- TP
- expiry
- session
- PRETABLE risk level
- riskFlags:
  - MA_STRETCH
  - MOMENTUM_WEAK
  - IMPULSE_EXHAUSTION
  - SESSION_TRANSITION
  - WATERFALL_RISK

### Data source

UI must visualize:

- Brain structure output
- Pattern Detector output
- PRETABLE decisions
- Pending orders
- TP levels
- Expiry timers
- Execution status

No additional trading logic in UI.

---

# B) HARD RULE — NO MARKET BUYS

MT5 must **only receive:**

- BUY_LIMIT
- BUY_STOP

Market BUY orders are forbidden.

If any internal path suggests market buy:

- convert to valid pending plan
- OR return `NO_TRADE`

---

# C) REMOVE 100g MINIMUM

Current EA enforces **100g minimum**.

Shop has **no minimum grams**.

Replace with:

```
min_grams = configurable (0.01g or 0.1g)
```

Order limits must depend only on:

- available AED
- exposure rules
- safety rules
- order validity

---

# D) MICRO LIVE EXPERIENCE MODE

Create a special execution mode:

```
MICRO_ROTATION_MODE
```

Purpose:

- Use **2237.42 AED free balance**
- Keep **2292g inventory separate**
- Allow safe live testing of full system

### Rules

- One active micro trade plan
- BUY_LIMIT / BUY_STOP only
- TP mandatory
- expiry mandatory
- same safety rules as normal engine
- ladder mode disabled initially

### Rollout phases

**Phase 1**
- Single pending trade
- No ladder
- Validate slips and ledger

**Phase 2**
- Allow engine to choose:
  - BUY_LIMIT
  - BUY_STOP
  - STAND_DOWN

**Phase 3**
- Allow ladder only after system confidence

---

# E) CAPITAL UTILIZATION

Use only free cash:

```
2237.42 AED
```

Do not treat **2292g inventory** as buy capital.

Capital engine must calculate grams from free cash only.

Purpose:

- test pending order logic
- test TP logic
- test expiry cancellation
- test ledger updates
- test UI display

---

# F) PATTERN DETECTOR AS ACTIVE GATE

Pattern Detector must feed directly into PRETABLE.

### Hard block

If:

```
PATTERN_TYPE = WATERFALL_RISK
AND
(WATERFALL_RISK = HIGH OR FAIL_THREATENED = TRUE)
```

→ PRETABLE = BLOCK  
→ NO_TRADE

### Breakout continuation

If:

```
PATTERN_TYPE = CONTINUATION_BREAKOUT
ENTRY_SAFETY = HIGH
WATERFALL_RISK != HIGH
FAIL_THREATENED = FALSE
```

→ SAFE or CAUTION  
→ generate BUY_STOP above structural lid

### Range / reload

For:

- RANGE_RELOAD
- LIQUIDITY_SWEEP
- WAIT_RECLAIM
- WAIT_RETEST
- WAIT_COMPRESSION

Generate BUY_LIMIT at:

- reclaim base
- compression base
- sweep reclaim zone

Never place orders in mid‑air.

---

# G) PENDING‑BEFORE‑LEVEL LAW

Each approved trade must include:

- orderType
- entryPrice
- tpPrice
- expiryMinutes

### BUY_STOP

```
entryPrice > currentAsk
```

Placed above structural lid.

### BUY_LIMIT

```
entryPrice < currentBid
```

Placed at base / reclaim / sweep.

If violated:

```
NO_TRADE
reason = violates_pending_before_level_law
```

EA must never convert pending orders into market orders.

---

# H) PRETABLE + IMPULSE EXHAUSTION GUARD

PRETABLE levels:

- SAFE
- CAUTION
- BLOCK

Behavior:

SAFE → normal size  
CAUTION → reduced size  
BLOCK → NO_TRADE

### Risk flags

- MA_STRETCH
- MOMENTUM_WEAK
- SESSION_TRANSITION
- IMPULSE_EXHAUSTION
- WATERFALL_RISK

### Impulse guard

If BLOCK:

- no breakout BUY_STOP

If CAUTION:

- prefer BUY_LIMIT pullbacks
- BUY_STOP only if not extended

---

# I) DYNAMIC SESSION RISK

Sessions:

- Japan
- India
- London
- New York

Maintain rolling stats:

- tradesExecuted
- sameSessionTPRate
- waterfallEntryCount
- AED_per_minute
- avg_PRETABLE_riskScore

Rolling window:

```
50–200 setups
```

Modifiers:

```
minModifier = 0.45
maxModifier = 0.80
```

If waterfallEntryCount ≥ 3:

```
maxModifier = 0.60
```

Dynamic Session Risk only adjusts **size**.

Never legality.

---

# J) LIQUIDITY MAGNET TP

TP must align with liquidity magnets:

Examples:

- consolidation shelves
- intraday swing highs
- reclaimed resistance
- session magnet levels

```
TP = magnet − buffer
```

Buffer depends on session volatility.

### Near‑magnet exit

If momentum weakens near TP:

- tighten TP
- allow early exit

Purpose:

- increase TP hit rate
- increase same-session completion
- improve AED/minute.

---

# K) LOGGING FOR STUDY + UI

Single unified log stream must feed:

- STUDY analytics
- UI overlays

Each cycle log:

- structureValid
- Pattern Detector result
- PRETABLE level
- riskScore
- riskFlags
- DynamicSessionModifier
- ImpulseExhaustionLevel
- executionMode
- orderType
- entryPrice
- TP
- expiry
- final decision
- execution result
- sameSessionTP
- AED profit

This ensures:

UI view = engine decisions = STUDY analysis.

---

# FINAL OBJECTIVE

With these refinements:

- system can live test using **2237.42 AED**
- existing **2292g gold remains separate**
- all trades are **BUY_LIMIT or BUY_STOP**
- orders placed **before price reaches level**
- waterfall traps remain blocked
- UI clearly shows structure, orders, magnets and risk zones
- safe monthly profit rotations increase

