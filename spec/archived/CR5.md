Below is a **clean implementation document** you can give directly to your AI Copilot agent.
It contains **conceptual architecture instructions only** (no code, no naming changes), and it respects your requirement that the system must **continue using the existing standards and structure of the application**.

---

# Trade Auto – Market Regime Detection Design Document

## Purpose

Introduce a **Market Regime Detection layer** to improve trade decision quality by identifying the current **market environment** before the trading logic executes.

This enhancement aims to:

* improve trade win rate
* reduce trades taken during poor market conditions
* align system behavior with the client’s **intraday scalping strategy**
* preserve the system’s **rule-engine-first architecture**

This layer **does not replace the rule engine** and **does not generate trade signals**.
Its purpose is to determine **when the market environment is favorable for the strategy**.

---

# 1. Concept of Market Regime

Market regime describes **how the market is behaving at a given moment**.

Different trading strategies perform better in different regimes.

For the current system, the strategy performs best when the market shows:

* volatility expansion
* directional momentum
* liquidity sweeps followed by reclaim
* strong intraday movement

The strategy performs poorly when the market shows:

* tight consolidation
* overlapping candles
* low volatility
* erratic or chaotic spikes

The market regime layer classifies the current environment so that the system can **adapt its behavior accordingly**.

---

# 2. Relationship With Strategy Profiles

The system already supports **multiple strategy profiles**:

* Standard
* WarPremium

These profiles define **how the engine behaves under certain macro conditions**.

Examples of differences between strategy profiles may include:

* scoring thresholds
* news sensitivity
* volatility assumptions
* risk guards

Strategy profiles answer the question:

**“How should the system behave?”**

Market regime detection answers a different question:

**“What kind of market environment exists right now?”**

Both layers should coexist.

---

# 3. Role of Market Regime Detection

Market regime detection evaluates **current market behavior** using existing signals produced by the MT5 market snapshot.

The regime layer helps determine whether the current environment is:

* favorable for scalping
* neutral
* risky or unstable

The regime result should influence whether the system:

* trades normally
* reduces activity
* aborts trading cycles

---

# 4. Placement in the Decision Pipeline

Market regime detection should be executed early in the decision cycle, immediately after the MT5 snapshot is received.

Recommended pipeline order:

1. Market snapshot ingestion
2. Market regime detection
3. Rule engine structural validation
4. News filtering
5. Telegram sentiment evaluation
6. AI analysis
7. Trade scoring
8. Final decision
9. Execution

The regime layer does not replace the rule engine.
It simply provides additional context about **market conditions**.

---

# 5. Market Regime Categories

The system should classify the market into a small set of clear regimes.

Possible regimes include:

**Expansion**

Market shows increasing volatility and directional momentum.
This environment is favorable for intraday scalping.

**Compression**

Market shows tight ranges, overlapping candles, and reduced volatility.
This environment typically produces false breakouts and slow price movement.

**Trending**

Market shows directional continuation over multiple candles and timeframes.
Momentum entries may perform well.

**Chaotic / Unstable**

Market shows erratic spikes, large wicks, or sudden movements.
This environment is risky for automated entries.

**Low Liquidity**

Market movement is weak, spreads may widen, and volume is low.
Trade opportunities are limited.

The system may choose to support fewer regimes initially if simplicity is preferred.

---

# 6. Signals Used for Regime Detection

The current MT5 snapshot already contains multiple indicators that can be used to determine the regime.

Examples include:

Volatility indicators

* ATR values
* ATR expansion signals
* volatility expansion flags

Structure indicators

* compression detection
* overlapping candles
* candle range statistics

Momentum indicators

* impulse strength score
* directional candle bodies
* breakout confirmations

Market quality indicators

* spread statistics
* slippage estimates
* tick activity

These signals should be combined to determine the most likely market regime.

No new external data sources are required.

---

# 7. Interaction With the Rule Engine

Market regime detection does **not replace structural validation**.

The rule engine remains responsible for validating:

* H1 context
* M15 structure
* M5 entry conditions
* liquidity sweep patterns
* reclaim confirmations

Regime detection simply helps determine **whether the environment is suitable for the strategy**.

For example:

If the regime is identified as compression, the system may reduce trade frequency or require stronger setups before allowing trades.

If the regime is identified as chaotic, the system may abort trading cycles.

---

# 8. Interaction With Trade Scoring

Market regime can optionally influence the **trade scoring layer**.

Examples of influence may include:

* positive score adjustment during expansion regimes
* negative score adjustment during compression regimes
* trade blocking during chaotic regimes

However, regime detection should not override the rule engine.

---

# 9. Logging Requirements

The system must log regime detection results in the timeline logging system.

Each decision cycle should include a clear regime entry.

The log should show:

* detected regime
* key signals that led to the classification
* whether the regime allows or restricts trading

The purpose of this logging is to allow both developers and traders to understand **why trades were allowed or rejected based on market conditions**.

---

# 10. Observability for Traders

The regime classification should be visible in the system logs so that the client can observe how the engine interprets market conditions.

This transparency helps traders validate whether the system’s interpretation matches their own market view.

---

# 11. Expected Benefits

Introducing market regime detection is expected to improve system performance by:

Reducing trades during poor market environments

Avoiding scalping entries during tight consolidation

Filtering unstable market conditions

Improving trade quality and consistency

Providing clearer diagnostic insight into system decisions

---

# 12. Implementation Constraints

While implementing this feature, the following constraints must be respected:

* Existing application architecture must remain intact
* Existing naming conventions should be preserved
* Existing APIs and helper endpoints should not be renamed
* Existing logic should remain unless clearly obsolete
* Timeline logging must remain fully compatible with current log structure

If parts of the required functionality already exist, the implementation should **reuse them rather than duplicating them**.

---

# Final Objective

The final trading engine architecture should behave as a layered decision system:

Market data ingestion

→ Market regime detection
→ Rule engine structural validation
→ News filtering
→ Telegram sentiment interpretation
→ AI analysis
→ Trade scoring
→ Final trade decision
→ MT5 execution

This layered structure ensures that trades occur only when:

* the market structure is valid
* the environment is favorable
* macro risk is acceptable
* contextual analysis supports the setup
* the trade quality meets the scoring threshold

This approach aligns with the system’s design goal of **safe, deterministic, and explainable automated trading**.
