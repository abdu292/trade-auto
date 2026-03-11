# Product Requirements Document

## Trade Auto System Enhancement

---

# 1. Purpose

Enhance the existing Trade Auto system to support a deterministic rule-based decision engine, AI-assisted validation, full decision observability, and a development replay capability using historical MT5 market data.

The system must allow both the developer and the trader to understand exactly how decisions are made and must allow testing of the full decision pipeline using historical data without waiting for real market candles.

Existing working components must be reused wherever possible. This document defines behavior and architecture but does not enforce naming conventions or API patterns so the implementation can follow existing standards.

---

# 2. System Philosophy

The system must follow a strict decision hierarchy:

Market Data
→ Rule Engine
→ News / Macro Risk Filters
→ Sentiment Evaluation
→ AI Analysis
→ Risk Gates
→ Trade Intent Generation
→ Execution

AI must **never generate trades independently**.
The deterministic backend rule engine must always generate the setup candidate first.

---

# 3. Architecture Overview

The architecture remains composed of the existing components:

Backend decision service
AI analysis worker
MT5 execution adapter
Operator monitoring UI

This enhancement focuses on improving the internal decision pipeline and observability rather than changing system structure.

---

# 4. Timeframe Strategy Model

The system must operate using the following timeframe stack:

H1 — context
M15 — setup structure
M5 — entry precision

Other timeframes should not influence the decision pipeline unless already implemented and proven necessary.

---

# 5. Market Data Ingestion

## Data Source

Market data is provided by the MT5 environment through the existing integration layer.

If a market data ingestion mechanism already exists, it must be reused.

---

## Candle Close Transmission

The system must process market data only on candle close events.

Required timeframes:

H1
M15
M5

Each candle close should trigger a decision cycle.

Tick-level streaming is not required.

---

## Candle Data

Each candle event should include:

symbol
timeframe
open
high
low
close
volume (tick volume acceptable)
ATR (if available)
RSI (if available)
moving average value (if available)
candle open time
candle close time
session identifier if already calculated.

Existing data formats should be reused if they already contain equivalent information.

---

# 6. Decision Cycle

Each candle close must trigger a complete decision cycle.

A decision cycle includes:

market data ingestion
rule engine evaluation
news risk evaluation
telegram sentiment scoring
AI analysis
risk gate evaluation
trade intent generation or cycle abort.

Each cycle must generate a unique cycle identifier to link all logs and decisions.

---

# 7. Deterministic Rule Engine

The rule engine must generate a **setup candidate** before AI analysis occurs.

The rule engine operates across three layers.

---

## H1 Context Layer

The context layer evaluates the broader market state.

Examples of signals:

liquidity sweep
reclaim candles
trend alignment
directional momentum

This layer produces a context classification such as:

bullish context
bearish context
neutral context.

---

## M15 Setup Layer

The setup layer identifies structural trade opportunities.

Possible signals include:

volatility compression
range contraction
base formation
repeated support or resistance interaction

Example indicators may include decreasing candle ranges or stable ATR values.

This stage determines whether a valid setup structure exists.

---

## M5 Entry Layer

The entry layer determines whether the setup is actionable.

Typical conditions include:

compression break
retest after reclaim
momentum shift

If entry conditions are not satisfied, the cycle must abort.

---

## Setup Candidate

A setup candidate exists only when:

H1 context valid
M15 setup valid
M5 entry valid

If any layer fails, the system aborts the cycle.

---

# 8. Waterfall Risk Detection

Because the strategy avoids forced stop-loss exits, the system must aggressively avoid entering unstable market moves.

The rule engine must detect waterfall risk conditions including:

rapid ATR expansion
large candle range expansion
abnormal spreads
session volatility spikes
recent news release shocks

If waterfall risk is detected, the cycle must abort before AI analysis.

---

# 9. Economic News Integration

The system must integrate an economic calendar data source.

ForexFactory economic calendar data should be used.

News data should refresh approximately every five minutes.

The system must detect high-impact USD events.

Trading must be blocked if a high-impact event occurs within a defined risk window surrounding the current time.

---

# 10. Telegram Sentiment

Telegram channels may provide useful market sentiment but must not generate trade signals.

Telegram analysis should produce a sentiment score representing overall crowd bias.

The sentiment score should fall within a numerical range representing negative to positive bias.

This sentiment score modifies contextual confidence but must never override safety gates or rule engine decisions.

---

# 11. AI Analysis Layer

AI analysis occurs only when a setup candidate exists.

The backend sends a summarized market context to the AI worker.

The AI returns structured analysis containing:

confidence level
potential veto
context interpretation
target guidance if applicable.

If the AI response indicates a veto or confidence below a defined threshold, the cycle must abort.

---

# 12. Risk Gate Evaluation

After AI analysis, additional risk gates must be applied.

Examples include:

news risk
waterfall risk
volatility anomalies
session constraints

If any gate fails, the cycle must abort.

---

# 13. Trade Intent Generation

When all layers pass successfully, the system generates a trade intent.

Trade intent contains:

entry price
target price
position size
expiry timing
reasoning context.

Trade intents are stored and made available to the MT5 execution adapter.

---

# 14. Execution Adapter

The execution adapter periodically checks for pending trade intents.

When a trade is available:

the order is executed through MT5
execution status is returned to the backend
the decision timeline is updated.

---

# 15. Decision Timeline Logging

Every decision cycle must produce detailed logs.

Each stage in the pipeline must record an event.

Examples of stages include:

market data received
context evaluation
setup detection
entry evaluation
news check
sentiment scoring
AI request
AI response
risk gate evaluation
trade intent generated
trade executed
cycle aborted.

Each event must include:

timestamp
cycle identifier
stage description
structured metadata.

---

# 16. Timeline Storage

Timeline events must be stored in the existing database.

Structured data storage must allow later querying and analysis.

Human-readable reports can be generated dynamically when requested.

---

# 17. Timeline Viewing

The system must provide a way for the operator and trader to inspect recent decision cycles.

The UI should display:

recent cycles
stage-by-stage decisions
AI reasoning
rule engine reasoning
abort reasons.

This feature is primarily used during development and testing.

---

# 18. Historical Replay Engine

The system must support replaying historical market data for development testing.

Replay mode replaces the MT5 live data source but must reuse the same decision pipeline.

No duplicate decision logic should be implemented.

---

# 19. Historical Data Requirements

Historical candles should be exported from MT5.

Recommended dataset:

two to three months of data.

Minimum dataset:

four weeks.

Required timeframes:

H1
M15
M5.

---

# 20. Importing Historical Data

Historical candles should be imported into the system from CSV files.

Typical CSV fields include:

timestamp
open
high
low
close
volume.

Additional fields may be ignored.

---

# 21. Replay Engine Behavior

Replay must simulate live market flow.

For each candle close event:

feed candle data to the market data ingestion pipeline
trigger a normal decision cycle
process rule engine
evaluate news risk
calculate sentiment score
perform AI analysis
apply risk gates
generate trade intent or abort
log the decision timeline.

Replay must use the exact same processing pipeline as live data.

---

# 22. Replay Execution Safety

Replay mode must never execute real trades.

Trade intents produced during replay must only be recorded for analysis.

Execution adapters must remain disabled during replay.

---

# 23. Replay Speed

Replay should allow accelerated simulation.

Developers should be able to process large datasets quickly.

Example usage:

simulate months of market activity within minutes.

---

# 24. Replay Controls

Replay mode should support:

starting replay
pausing replay
resuming replay
stopping replay.

Pause functionality is important for inspecting decision cycles.

---

# 25. Development Workflow

Typical development workflow:

export historical MT5 data
import into replay engine
run replay simulation
inspect timeline logs
refine rules and AI prompts
repeat testing.

This workflow allows rapid development without waiting for live candles.

---

# 26. Observability Requirements

Every decision cycle must record:

context classification
setup state
entry state
news risk state
sentiment score
AI confidence
final decision.

These logs must allow both developer and trader to understand system behavior.

---

# 27. Legacy Logic Cleanup

Legacy or unused code paths should be reviewed.

If they are inconsistent with the architecture described here or not functioning properly, they should be removed.

Working components should be preserved and aligned rather than rewritten.

---

# 28. Security

MT5 communication must remain protected using the existing authentication mechanism.

Existing security mechanisms should be reused.

---

# 29. Acceptance Criteria

The enhancement is complete when:

the rule engine generates setup candidates independently
AI acts only as confirmation or veto
news filtering blocks risky periods
Telegram sentiment modifies context scoring
all decision cycles are fully logged
historical replay can simulate the full pipeline
operators can inspect decision timelines.

---

# Expected Result

The system becomes:

deterministic
transparent
testable
developer friendly
collaborative between developer and trader.