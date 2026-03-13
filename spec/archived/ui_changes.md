UI REQUIREMENTS ADD-ON (for quick decisions + full halal ledger integrity)

0) CORE PRINCIPLE
Everything the UI shows must be derived from:
(A) Ledger (last confirmed slip chain) + (B) MT5 live orders/positions + (C) Shop spread rules.
No “estimated” balances unless clearly labeled.

------------------------------------------------------------
1) CAPITAL DASHBOARD (TOP CARD — ALWAYS VISIBLE)
------------------------------------------------------------
1.1 Available Capital (Cash AED)
- Cash_AED (ledger truth)

1.2 Gold Holdings
- Gold_g (ledger truth)
- Gold_AED_equivalent (mark-to-market using Shop Sell = MT5_bid − 0.80)

1.3 Net Equity (Total)
- Equity_AED = Cash_AED + Gold_AED_equivalent

1.4 Net Purchase Power (IMPORTANT)
- PurchasePower_AED = Cash_AED + (optional credit_line_AED if shop provides any, else 0)
- Show “Shop Credit Line” as separate field (0 if not applicable)

1.5 Deployable
- Deployable_AED = min( Cash_AED , allowed_by_caps )
Where allowed_by_caps comes from:
  - Session exposure cap (by session + day filter)
  - Risk profile cap (Balanced/Conservative)
  - Strategy MODE_TAG cap (NORMAL/WAR_PREMIUM/DEESC)
Display both:
- Deployable_AED_NOW
- Deployable_AED_NEXT_30M (if hazard window upcoming, reduce to 0)

1.6 Deployed
- Deployed_AED = sum(AED cost of all open positions + all pending buy orders reserved)
Each component shown:
- OpenPositions_AED
- PendingOrders_Reserved_AED

------------------------------------------------------------
2) ACCOUNT STATUS (HEALTH + SAFETY)
------------------------------------------------------------
2.1 MT5 Connection Status
- Connected/Disconnected + last tick age (seconds)
- Ingestion rate (ticks/min)

2.2 Trade Engine Status
- Automation Active: YES/NO
- Execution Mode: MANUAL / HYBRID_AUTO
- Active Strategy Profile: NORMAL / WAR_PREMIUM / DEESCALATION_RISK
- Active Risk Profile: Balanced / Conservative (with max DD %)

2.3 Order/Position Status
- Open Buys: count + list (slot C1-A / C1-B)
- Pending Orders: count + list (entry, TP, expiry, grams, status)
- Queue Depth / Approval Queue (already present)

2.4 Hazard & Locks
- Hazard Active: count + next hazard begins in (mm:ss)
- Quiet Window: active/inactive + remaining time
- Emergency Pause: active/inactive

------------------------------------------------------------
3) LEDGER ACTIONS (WITHDRAWALS / ADDITIONS / ADJUSTMENTS)
------------------------------------------------------------
Add a “Ledger Actions” module with 3 buttons:
- ADD CAPITAL (Deposit / transfer-in)
- WITHDRAW CAPITAL (transfer-out)
- SHOP ADJUSTMENT (fees/charges/manual correction with reason)

Each action creates a “Ledger Slip” (non-trade slip) with:
- Action ID
- Amount AED
- Method (cash/bank/transfer)
- Timestamp (Server + KSA)
- Pre/Post balances (Cash_AED, Gold_g)
- Note/Reason (mandatory)
These actions must also be WhatsApp-forwarded like trade slips.

------------------------------------------------------------
4) COMPOUNDING & GROWTH TRACKING (4x TARGET)
------------------------------------------------------------
4.1 Capital Compounding Tracker
- Starting_Investment_AED (user-set once)
- Current_Equity_AED (computed)
- Multiple = Current_Equity / Starting_Investment
- “4x Milestone” progress bar + estimated needed profit AED (no time promises)

4.2 Pull-Out Rule (when 4x)
- When Multiple ≥ 4.0: UI raises “READY TO PULL ORIGINAL CAPITAL” alert
(Just alert; execution is manual.)

------------------------------------------------------------
5) PROFIT REPORTING (SESSION / DAILY / WEEKLY)
------------------------------------------------------------
Profit is computed ONLY from closed SELL slips (realized).
5.1 Today (KSA day)
- Realized Profit AED (today)
- #Rotations completed
- Avg profit per rotation
- Hit rate (TP hit % of filled buys)

5.2 Session-wise (Japan / India / London / NY)
For each session:
- Realized Profit AED
- Rotations count
- Average cycle time (fill-to-TP)
- Waterfall blocks triggered (count)
- “Sleep ratio” = time with Deployable_AED>0 but no active/pending orders (in eligible windows)

5.3 Weekly
- Realized Profit AED (Mon–Sun KSA)
- Best session of week
- Worst session of week
- #NO-TRADE blocks (why categories)

------------------------------------------------------------
6) QUICK DECISION PANEL (ONE SCREEN “GO / NO-GO”)
------------------------------------------------------------
This is the “operator cockpit”:
- Current Session + Phase (early/mid/peak/late)
- MODE_TAG + Regime + WaterfallRisk
- Correlation Stack summary (CONFIRMING/DIVERGING + DXY tailwind/headwind + Geo tag)
- Next Hazard Window countdown
- Permission lights:
  Rail-A: ALLOWED / DEEP-ONLY / BLOCKED
  Rail-B: ALLOWED / BLOCKED
- Recommended action:
  “TABLE READY” or “NO TRADE – CAPITAL PROTECTED”
- If TABLE READY: show 1–2 rows with one-tap “Approve/Place” in MANUAL mode

------------------------------------------------------------
7) REQUIRED COMPUTATIONS (SHOP SPREAD + GRAMS)
------------------------------------------------------------
Everywhere UI shows prices, show both:
- MT5 price (USD/oz)
- Shop price (USD/oz) = MT5 ± 0.80
And for AED conversions:
- Use 1 oz = 31.1035 g
- 1 USD = 3.674 AED
Compute AED cost of a buy row:
- NetPoints = (ShopSell - ShopBuy) for profit calc
- BuyCost_AED = grams/31.1035 * ShopBuy * 3.674
- SellCredit_AED = grams/31.1035 * ShopSell * 3.674
Profit_AED = SellCredit_AED - BuyCost_AED
(Profit is logged separately; SellCredit_AED is added fully to cash.)

------------------------------------------------------------
8) OPTIONAL (HIGH VALUE) “ANOMALY ALERTS”
------------------------------------------------------------
Alerts for quick defense:
- Spread spike (current spread > 2x 5m avg)
- Tick drought (no ticks > N seconds)
- Macro stale (Macro Age > threshold) => downgrade confidence / block Rail-B
- Telegram panic surge (many “NewsCandidate” in short time) => hazard window auto-block
- MT5 desync (server time mismatch) => block until fixed

------------------------------------------------------------
9) MINIMUM UI LAYOUT RECOMMENDATION
------------------------------------------------------------
Tab 1: Dashboard (Capital + Deployable/Deployed + Permissions + Quick Decision)
Tab 2: Trades (orders/positions + slips history)
Tab 3: Sessions (session profits + controls)
Tab 4: Risk (hazards + profiles + anomaly alerts)
Tab 5: Ledger (deposits/withdrawals + full chain + export)

If you implement the above, I can look at ONE screen and decide instantly:
- Can we trade now?
- How much can we deploy safely?
- What is already deployed?
- Are hazards/war-premium/de-escalation risks blocking?
- How much profit we made today / this session / this week?