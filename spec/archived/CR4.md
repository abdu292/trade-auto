
# Trade Auto – Trade Scoring Layer Implementation Guide

## Purpose

Introduce a **trade scoring layer** to improve trade quality selection while preserving the system’s core design:

* deterministic rule engine authority
* AI as supporting analysis
* structured MT5 data as the primary signal source

The scoring system **must not replace the rule engine**.
It should only **rank trade opportunities that already passed structural validation**.

This aligns with the client’s strategy:

* H1 context
* M15 structure
* M5 entry
* intraday scalping rotations
* 5–12 trades per day on clean days
* no stop-loss selling
* protection must come from **avoiding bad entries**

Therefore the system must be **selective before entering trades**, not after.

---

# 1. Position of the Scoring Layer in the Decision Pipeline

The scoring layer must run **after structural validation but before execution approval**.

Decision pipeline:

```
MT5 market snapshot received
↓
Decision cycle started
↓
Rule engine evaluation
↓
IF rule engine invalid → abort cycle
↓
News filter check
↓
Telegram sentiment analysis
↓
AI analysis
↓
Trade scoring calculation
↓
Final decision
↓
Execution via MT5
```

Important rule:

```
If the rule engine rejects the setup,
the scoring system must NOT run.
```

Rule engine remains the **primary safety gate**.

---

# 2. Responsibilities of the Scoring System

The scoring system evaluates the **quality of a valid trade setup**.

It does NOT determine whether structure is valid.

Structure validation is handled exclusively by the **rule engine**.

The scoring system answers the question:

> “How strong is this valid setup?”

---

# 3. Trade Score Range

Each evaluated trade receives a score:

```
Trade Score: 0 – 100
```

Interpretation:

```
Score < 45  → NO TRADE
Score 45–60 → weak setup
Score 60–80 → valid trade
Score > 80  → high conviction setup
```

These thresholds should remain configurable.

Because the strategy targets **5–12 trades per day**, the threshold must not be overly restrictive.

---

# 4. Score Components

The score should combine multiple signals already available in the system.

Signals must come primarily from **market structure and momentum**, not AI.

Approximate weighting guideline:

```
Structure signals     ~60%
Momentum signals      ~30%
Execution quality     ~5%
AI contribution       ~3–5%
Sentiment contribution ~3–5%
```

---

# 5. Structure Signals

Structure should contribute the largest portion of the score.

Examples of signals:

```
Liquidity sweep detected
Liquidity reclaim confirmed
Higher timeframe alignment
Distance from moving averages
Market structure integrity
```

Structure signals should contribute approximately:

```
40–60 points
```

These signals are derived from the **H1/M15/M5 structural analysis already performed by the rule engine**.

---

# 6. Momentum Signals

Momentum confirms whether the structure has sufficient strength for scalping entries.

Examples:

```
ATR expanding
Volatility expansion detected
Impulse candles detected
Compression breakout
Strong candle body relative to range
```

Momentum signals may contribute:

```
20–30 points
```

---

# 7. Execution Quality Signals

Execution quality should ensure the market environment is suitable for entry.

Signals may include:

```
Spread conditions
Slippage estimate
Session liquidity
Market freeze detection
```

Execution signals should contribute approximately:

```
5–10 points
```

---

# 8. AI Contribution

AI must remain **supporting intelligence**, not the main decision driver.

AI contribution should be minimal.

Possible signals:

```
AI confidence level
AI consensus strength
AI directional validation
```

Contribution range:

```
3–10 points maximum
```

AI must **never override structural rules**.

---

# 9. Telegram Sentiment Contribution

Telegram analysis should be used only as **contextual sentiment**, never as a trade signal.

Signals may include:

```
crowd buy vs sell bias
panic detection
mixed sentiment detection
high-impact headlines
```

Contribution range:

```
-10 to +5 points
```

Examples:

```
Strong panic signals → negative score
Strong consensus bias → small positive score
Mixed sentiment → neutral
```

---

# 10. Final Trade Decision

After scoring, the system determines the final action.

Example logic:

```
If score < threshold
    reject trade

If score >= threshold
    allow trade
```

The rule engine must remain the **final authority for structural safety**.

The scoring layer only determines **trade quality ranking**.

---

# 11. Timeline Logging Requirements

Every decision cycle must log the score breakdown for transparency.

Timeline event example:

```
TRADE_SCORE_CALCULATION
```

Example log output:

```
Trade Score Breakdown

Structure Score: 38
Momentum Score: 22
Execution Score: 6
AI Score: 5
Sentiment Score: -2

Total Score: 69
Decision Tier: VALID TRADE
```

---

# 12. Final Decision Log

At the end of the decision cycle, the timeline must log a final decision summary.

Example:

```
FINAL_DECISION: TRADE_APPROVED
Score: 69
Entry: 5165.80
TakeProfit: 5178.00
```

Or:

```
FINAL_DECISION: NO_TRADE
Reason: score below threshold
Score: 41
```

---

# 13. Interaction With Rule Engine

The scoring system must **never override structural validation**.

Correct behavior:

```
Rule engine invalid → abort
Rule engine valid → score evaluated
```

Incorrect behavior (must never occur):

```
Rule engine invalid
score high
trade executed
```

The rule engine protects against:

* waterfall entries
* liquidity traps
* structural market hazards

This is critical because the strategy **does not use stop-loss exits**.

---

# 14. Benefits of the Scoring Layer

Adding this layer improves:

### Trade quality filtering

Better setups receive higher priority.

### Stability

Weak setups are rejected even if technically valid.

### Transparency

Traders can see why trades were taken or skipped.

### AI cost efficiency

AI contributes but does not dominate decisions.

### Client validation

The scoring breakdown helps experienced traders review system behavior.

---

# 15. Implementation Requirements

The scoring system must:

* use **existing market snapshot data**
* integrate into the **existing decision cycle**
* follow existing naming conventions
* log all results to the **timeline logging system**

No existing working logic should be removed unless it is clearly obsolete.

---

# Final Goal

The final decision engine should behave like:

```
Rule Engine (structure validation)
+
News filter
+
Telegram sentiment
+
AI analysis
+
Trade scoring
=
Final trade decision
```

This layered architecture creates a **robust intraday scalping engine** aligned with the client’s strategy while maintaining deterministic backend control.

---

