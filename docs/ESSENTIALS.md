# Trade Auto Essentials

This is the only high-level operations document you need day to day.

## Scope
- Instrument: XAUUSD only
- Execution: BUY_LIMIT / BUY_STOP only
- No sell-first, no hedging, no SL, no market order
- Single TP, explicit MT5 expiry on every order

## Safety Gates (Deterministic)
- Output is always either:
  - one TABLE candidate (entry, grams, TP, expiry), or
  - NO TRADE
- BLOCK (no trade) if waterfall guard triggers:
  - expansion + impulse candles + HIGH news impact
  - Friday London/NY overlap + expansion
  - panic-drop sequence

## Runtime Flow
1. MT5 posts market snapshot to Brain
2. Brain classifies regime and risk
3. Brain requests AI analysis from aiworker
4. Decision engine builds TABLE candidate or NO TRADE
5. Candidate goes to manual approval queue
6. Approve -> MT5 pending order queue; Reject -> discarded
7. BUY trigger and TP hit both generate slip + ledger updates + WhatsApp message

## Manual Approval Endpoints
- GET /api/monitoring/approvals
- POST /api/monitoring/approvals/{tradeId}/approve
- POST /api/monitoring/approvals/{tradeId}/reject

## Monitoring Endpoints
- GET /health
- GET /api/monitoring/ledger
- GET /api/monitoring/notifications

## Constants
- Shop spread: ±0.80 USD/oz
- 1 oz = 31.1035 g
- 1 USD = 3.674 AED
- KSA = MT5 + 50 minutes

## Source of Truth
- Spec: ../spec/spec_v3.md
- Implemented SOP: ../spec/SOP_SPEC_V3_IMPLEMENTATION.md
