# Brain → PRETABLE → Pattern Detector → Decision Engine → MT5 → UI

```mermaid
flowchart LR
    A[Market Data / MT5 Feed] --> B[Layer A: Safety & Structure]
    B --> B1[FAIL / Hazard / Panic Rules]
    B --> B2[Waterfall Shield]
    B --> B3[Capital Utilization]
    B --> B4[Portfolio Exposure]
    B --> B5[Structure Validation]

    B5 --> C[Pattern Detector]
    C --> D[Layer B: PRETABLE Risk Intelligence]
    D --> D1[SAFE / CAUTION / BLOCK]
    D --> D2[Impulse Exhaustion Guard]
    D --> D3[Dynamic Session Risk]

    D --> E[Decision Engine]
    E --> E1[Execution Mode Selection]
    E --> E2[Pending-before-level law]
    E --> E3[BUY_LIMIT / BUY_STOP only]

    E --> F[MT5 Order Router]
    F --> G[Pending Orders with TP + Expiry]
    G --> H[SLIPS / Ledger]

    B --> I[UI State Stream]
    C --> I
    D --> I
    E --> I
    G --> I
    H --> I

    I --> J[Graphical Trade Map UI]
```

## Notes
- Pattern Detector must actively gate PRETABLE, not just log.
- PRETABLE changes **size only** for CAUTION; it cannot override legality.
- MT5 must only receive BUY_LIMIT or BUY_STOP.
- UI is a visualization layer only, not a second decision engine.
