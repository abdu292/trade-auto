# Trade Auto SOP (Spec v3 Deterministic Implementation)

## 0. System Principles (Non-Negotiable)
- Instrument: `XAUUSD` only.
- Execution: pending buy rails only (`BUY_LIMIT`/`BUY_STOP`), single TP, no market orders, no sell-first, no hedging, no SL.
- Waterfall protection: automatic BLOCK when guard conditions trigger.
- Output discipline: either one final TABLE candidate (manual-approval queue) or NO TRADE.
- Ledger discipline: BUY SLIP on trigger; SELL SLIP on TP; both to WhatsApp + ledger state update.
- Constants:
  - Shop spread: ±`0.80` USD/oz.
  - Conversion: `1 oz = 31.1035 g`.
  - Conversion: `1 USD = 3.674 AED`.
  - Time: `KSA = MT5 + 50 minutes`.
  - Every order has explicit MT5 expiry.

## 1. Architecture Overview
### 1.1 Modules
- Data Layer: MT5 snapshot + TradingView webhook + Telegram parsing.
- Indicator/Tag Engine: MT5-computed values and normalized booleans/tags.
- Regime/Risk Classifier: deterministic SAFE/CAUTION/BLOCK + regime tag.
- AI Orchestrator: ChatGPT-orchestrated committee with Perplexity/Gemini/Grok providers.
- Decision Engine: entry/TP/expiry/sizing/re-entry controls.
- Execution Controller: manual approval queue → approve/reject → MT5 pending queue.
- Slip/Ledger: BUY/SELL slips + ledger balances.
- Audit/Monitoring: logs + notification feed + approval queue.

### 1.2 Deployment Phases
- MVP:
  - Strong BLOCK logic.
  - Manual approval default.
  - Slip/ledger automation.
- v1:
  - Telegram ingestion with structured HIGH/MODERATE/LOW impact tags.
  - Improved regime tags from richer indicators.
- v2:
  - Semi-auto only in SAFE Japan/India windows.
  - Dynamic sizing + controlled re-entry ladder.

## 2. Data Integrations
### 2.1 MT5 Integration (Required)
- Pull/accept snapshot fields: MTF OHLC, ATR/ADR/MA/RSI, session/range levels, pattern booleans, spread-related execution constants, account exposure context.
- Push orders: pending only with explicit expiry and TP.
- Time dual display: MT5 + KSA.

### 2.2 TradingView (2 indicators, confirmation-only)
- Ingest via webhook.
- Normalize to `CONFIRM` / `NEUTRAL` / `CONTRADICT`.
- Used as score/risk modulation only; never overrides safety gates.

### 2.3 Telegram Integration
- Channel monitoring and message parsing.
- Structured impact tagging:
  - `HIGH`: high-impact/news-spike class.
  - `MODERATE`: caution macro class.
  - `LOW`: noise/low-impact class.
- Audit payload stores channel, timestamp, category, headline text.

## 3. Indicator Engine (MT5 Full Set)
Computed/ingested fields (normalized):
- Trend anchors: `MA20` for H4/H1/M30.
- Momentum: `RSI H1`, `RSI M15`.
- Volatility: `ATR H1`, `ATR M15`, `ATR/ADR` expansion.
- Session structure: previous day high/low, session high/low.
- Structure detectors: compression, overlap candles, impulse candles, liquidity sweep, panic-drop sequence, post-spike pullback, breakout confirmation, London/NY overlap.

## 4. Regime & Risk Classifier
### 4.1 Regimes (one active)
- `COMPRESSION`
- `EXPANSION`
- `NEWS_SPIKE`
- `POST_SPIKE_PULLBACK`
- `FRIDAY_HIGH_RISK`

### 4.2 Permission Map
- `SAFE` → allow TABLE.
- `CAUTION` → allow TABLE with reduced size + shorter expiry.
- `BLOCK` → NO TRADE only.

### 4.3 Waterfall Guard (auto-block)
BLOCK if any:
1. Expansion + impulse candles + Telegram `HIGH` impact.
2. Friday + London/NY overlap + expansion.
3. Panic drop sequence.

### 4.4 Thresholds (configurable defaults)
- Expansion threshold: `ATR/ADR >= 1.15`.
- Compression threshold: `ATR/ADR <= 0.82` with overlap.
- Post-spike pullback: `1.00 <= ATR/ADR < 1.15` and no fresh impulse.

## 5. AI Orchestration
### 5.1 ChatGPT (orchestrator)
- Builds structured query context from MT5 + regime + tags.
- Produces safety-aligned buy-only candidate signal.

### 5.2 Perplexity
- Fetches macro/news/geo context tags.

### 5.3 Gemini
- Cross-check tags for contradictions/confirmation.

### 5.4 Grok
- Fast sanity-format layer.
- Cannot override safety BLOCK gates.

## 6. Decision Engine
### 6.1 Entry Rules
- COMPRESSION: `BUY_LIMIT` near defended floor (`sessionLow/previousDayLow` anchored).
- EXPANSION: `BUY_STOP` only if breakout is confirmed.
- POST_SPIKE_PULLBACK: deeper `BUY_LIMIT` only in SAFE/CAUTION.
- NEWS_SPIKE/FRIDAY_HIGH_RISK/BLOCK: NO TRADE.

### 6.2 TP Rules
- Single TP.
- ATR-scaled by regime.

### 6.3 Expiry Rules
- Explicit MT5 expiry always.
- Session dynamic defaults:
  - Japan/India: longer.
  - New York / caution states: shorter.
- Expired pending orders auto-cancel on MT5 side.

### 6.4 Sizing Rules
- SAFE normal; CAUTION reduced; BLOCK none.
- Volatility and exposure penalties applied.
- Cash affordability cap applied in grams.

### 6.5 Re-Entry Ladder
- Allowed only if cash/exposure/spacing/risk checks pass.
- Re-entry size decays with open buy count (controlled, non-doubling).

## 7. Slips + Ledger Automation
### 7.1 BUY Trigger
- BUY SLIP generated and posted to WhatsApp.
- Ledger: cash down, grams up.

### 7.2 TP Hit (SELL close)
- SELL SLIP generated and posted to WhatsApp.
- Ledger: cash up (principal + profit), grams down.
- Profit tracked separately as `NetProfitAed` while credited amount is full sell credit.

### 7.3 Ledger Required Fields
- `CashAed`
- `GoldGrams`
- `OpenExposurePercent`
- `DeployableCashAed`
- `OpenBuyCount`

## 8. UI / Control Surface (Minimal + Safe)
- Manual mode default:
  - show regime tag,
  - risk state,
  - final TABLE candidate or NO TRADE,
  - APPROVE/REJECT actions.
- Semi-auto is phase-gated (v2) for SAFE Japan/India windows only.

## 9. Deliverables
### 9.1 SOP
- This document is the deterministic SOP.

### 9.2 Data Schema
- Market snapshot schema: includes MTF prices, indicator values, structure booleans, risk tags, MT5/KSA times.
- Pending order schema:
  - `Id`, `Symbol`, `Type`, `Price`, `Tp`, `Expiry`, `Ml`, `Grams`, `AlignmentScore`, `Regime`, `RiskTag`.
- Slip schema:
  - `SlipType`, `TradeId`, `Grams`, `Mt5Price`, `ShopPrice`, `AmountAed`, `NetProfitAed`, `CashBalanceAed`, `GoldBalanceGrams`, `Mt5Time`, `KsaTime`, `Message`.
- Ledger schema:
  - `CashAed`, `GoldGrams`, `OpenExposurePercent`, `DeployableCashAed`, `OpenBuyCount`.

### 9.3 Formula Definitions
- Volatility expansion: `volExp = ATR / ADR` (if ADR > 0).
- AED conversion:
  - `usdPerGram = usdPerOunce / 31.1035`
  - `aedAmount = usdPerGram * grams * 3.674`
- Shop prices:
  - Buy debit uses `mt5Buy + 0.80`.
  - Sell credit uses `mt5Sell - 0.80`.

### 9.4 Regime Rules and Thresholds
- Deterministic thresholds listed in section 4.4 and used by classifier.

### 9.5 AI Prompt Templates
#### ChatGPT Query Generator Prompt
```text
You are the orchestrator for XAUUSD physical-style pending-buy trading.
Use only buy-first logic (BUY_LIMIT/BUY_STOP), single TP, no SL, no market orders, no hedging.
Input includes MT5 indicators/tags, Telegram impact tag (HIGH/MODERATE/LOW), TradingView confirmation (CONFIRM/NEUTRAL/CONTRADICT), regime context, and session timing.
Return strict JSON only:
{
  "rail": "BUY_LIMIT|BUY_STOP",
  "entry": <number>,
  "tp": <number>,
  "pe": "HH:MM",
  "ml": "HH:MM",
  "confidence": <0.0-1.0>,
  "reasoning": "..."
}
If unsafe, return {"signal": null}.
```

#### Perplexity JSON Response Prompt
```text
Return JSON tags only for XAUUSD risk context:
{
  "high_impact_events": ["..."],
  "geo_headlines": ["..."],
  "risk_sentiment": "RISK_ON|RISK_OFF|MIXED",
  "dxy_direction": "UP|DOWN|FLAT",
  "real_yields_direction": "UP|DOWN|FLAT",
  "impact_tag": "HIGH|MODERATE|LOW"
}
No prose.
```

#### Gemini JSON Response Prompt
```text
Provide cross-check JSON only:
{
  "impact_tag": "HIGH|MODERATE|LOW",
  "direction_bias": "BULLISH|BEARISH|NEUTRAL",
  "contradiction": true|false,
  "notes": "short"
}
No prose.
```

#### Grok Table Formatting Prompt
```text
Format final decision without changing safety result.
If BLOCK: output "NO TRADE" only.
Else output one table row with fields:
entry, grams, tp, expiry_mt5, expiry_ksa, regime, risk_tag.
Do not change values.
```
