# Micro Mode Simulation — 2237.42 AED Free Balance

## Assumptions
- Free cash only: **2237.42 AED**
- Existing **2292g gold** kept separate for this test mode
- USD→AED = **3.674**
- 1 troy ounce = **31.1035g**
- Shop Buy = **MT5 + 0.80**
- Capital buffer = **0.995**
- No minimum grams at shop side

## Max Buyable Grams by Example MT5 Price

| MT5 Price | Shop Buy | AED/gram | Max grams from 2237.42 AED |
|---:|---:|---:|---:|
| 5171.00 | 5171.80 | 610.90 | 3.64 |
| 5100.00 | 5100.80 | 602.52 | 3.69 |
| 5050.00 | 5050.80 | 596.61 | 3.73 |
| 5000.00 | 5000.80 | 590.70 | 3.77 |

## Session-Aware CAUTION Effective Size Examples

These examples show how much of the max grams would be used if PRETABLE = CAUTION and the current bootstrap Dynamic Session Risk modifier is applied.

| MT5 Price | Max grams | Japan 0.60 | India 0.60 | London 0.65 | New York 0.55 |
|---:|---:|---:|---:|---:|---:|
| 5171.00 | 3.64 | 2.19 | 2.19 | 2.37 | 2.00 |
| 5100.00 | 3.69 | 2.22 | 2.22 | 2.40 | 2.03 |
| 5050.00 | 3.73 | 2.24 | 2.24 | 2.43 | 2.05 |
| 5000.00 | 3.77 | 2.26 | 2.26 | 2.45 | 2.07 |

## Suggested Micro Mode Rollout

### Phase 1
- One pending order at a time
- BUY_LIMIT or BUY_STOP only
- TP + expiry mandatory
- No ladder yet

### Phase 2
- Allow engine to choose between single BUY_LIMIT, single BUY_STOP, or STAND_DOWN

### Phase 3
- Allow ladder only after live confidence

## What this validates
- pending-before-level law
- TP + expiry enforcement
- slips generation
- ledger update flow
- UI overlay correctness

## Important
This is a **live experience mode**, not a profit-max sizing mode. The main purpose is to confirm the engine places strategic pending orders correctly with the currently free AED balance.