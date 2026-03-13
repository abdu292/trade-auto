# بسم الله الرحمن الرحيم
# DECISION STATE MACHINE
Version: FINAL-v2.0

## Master Logic
SAFETY -> CAPITAL -> STRUCTURE -> REGIME/VOLATILITY -> SESSION -> HISTORY -> FACTORS -> CANDIDATE -> RAIL -> TABLE -> EXECUTION

## Layer Outputs
### Safety
- SAFETY_PASS
- SAFETY_BLOCK

### Capital
- CAPITAL_PASS
- CAPITAL_BLOCK

### Structure
- STRUCTURE_STRONG
- STRUCTURE_PROVISIONAL
- STRUCTURE_INVALID

### Regime
- RANGE
- RANGE_RELOAD
- CONTINUATION_REBUILD
- EXPANSION
- EXHAUSTION
- LIQUIDATION
- NEWS_SPIKE
- SHOCK

### Volatility
- COMPRESSED
- NORMAL
- EXPANSION
- EXHAUSTION

### Session
- JAPAN / INDIA / LONDON / NEW_YORK
- START / MID / END / TRANSITION

### Historical Pattern
- historicalContinuationScore
- historicalReversalRisk
- historicalExtensionBandUSD
- sessionHistoricalModifier

### Candidate
- NONE
- FORMING
- ZONE_WATCH_ACTIVE
- EARLY_FLUSH_CANDIDATE
- CANDIDATE
- ARMED
- PENDING_PLANTED
- FILLED
- PASSED
- OVEREXTENDED
- REQUALIFIED
- INVALIDATED

### Rail
- RailA_Legal = YES / ONLY_AFTER_STRUCTURE / NO
- RailB_Legal = YES / STRICT / NO

## Key State Transitions
### NONE -> FORMING
When structure begins forming and safety/capital pass.

### FORMING -> ZONE_WATCH_ACTIVE
When price approaches a valid S1/S2/S3 zone and projected move can still meet reward floor.

### ZONE_WATCH_ACTIVE -> EARLY_FLUSH_CANDIDATE
When price enters zone and flush into shelf is detected without FAIL break and without waterfall continuation.

### EARLY_FLUSH_CANDIDATE -> CANDIDATE
When next candles hold lows / rejection confirms and projected move >= 8 USD.

### CANDIDATE -> ARMED
Only when:
- rail permission allows it
- TABLE prechecks pass
- confidence threshold passes

### ARMED -> PENDING_PLANTED
When TABLE compiler emits exact legal order and MT5 accepts it.

### OVEREXTENDED -> REQUALIFIED
When price cools, shelf rebuilds, safety still passes, and reward remains >= 8 USD.

## Hard Law
No lower layer may override a block from a higher layer.
