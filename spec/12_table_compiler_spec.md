# بسم الله الرحمن الرحيم
# TABLE COMPILER SPEC
Version: TABLE-COMPILER-v2.0

TABLE is the final execution gate.

## Inputs
- current legal context
- candidate state
- rail permissions
- structure map
- capital state
- expiry mode
- TP mode

## Must Re-check
- state freshness / TTL
- slot / exposure clamp
- capacity clamp
- FAIL distance
- hazard timing
- mid-air filter
- projected move >= 8 USD
- pending-before-level law

## Outputs
Either:
- exact BUY_LIMIT / BUY_STOP table
or:
- NO NEW ORDERS — <reason>

## Templates
### FLUSH_LIMIT_CAPTURE
- deep S2 pocket
- TP1 +8
- TP2 +12
- optional TP3 extension

### IMPULSE_HARVEST_CAPTURE
- only when impulseHarvestMode = TRUE
- TP ladder can include +20 / +30 / +50 bands when justified
