# Trade Auto – Rule Engine & Observability Improvements

## Purpose

This document describes a small set of **architectural and logic improvements** that should be implemented in the existing system to improve:

* trade decision quality
* market structure detection
* system observability
* decision traceability for debugging and trader review

These improvements are **incremental enhancements**, not a redesign.

The current architecture and standards must remain intact.

If any of these capabilities already exist in the system, the implementation should **reuse existing components rather than duplicating them**.

---

# 1. Rule Engine Improvement – Impulse Confirmation Layer

## Objective

Improve entry quality by adding a lightweight **impulse confirmation check** before the rule engine approves a trade setup.

This enhancement helps ensure that trades are taken only when the market shows **real directional energy**, which is essential for the client’s scalping style.

The client's strategy is:

* H1 context
* M15 structure
* M5 entry

The system already evaluates structure, but it should also confirm **short-term momentum expansion** before allowing the setup.

---

## Concept

Many losing scalping trades occur when:

* structure appears valid
* but the market lacks immediate momentum

The system should verify that the **entry timeframe shows clear impulse behavior**.

This is a simple filter that significantly improves trade quality.

---

## How the Impulse Filter Works

After the rule engine confirms structural validity:

1. Evaluate the most recent entry timeframe candles (typically M5).
2. Determine whether price movement shows signs of **momentum expansion**.

Possible indicators of impulse:

* large candle body relative to recent candles
* low wick-to-body ratio
* candle range expansion
* price moving away from short-term averages
* increased tick activity or volume

The system does not need a complex algorithm.

The purpose is simply to confirm that:

**“price is actively moving in the direction of the potential trade.”**

---

## Decision Logic

If structural conditions pass but impulse is weak:

The rule engine should reject the setup.

If impulse confirms the direction of the setup:

The decision pipeline continues.

This filter reduces entries during:

* weak consolidation
* slow market movement
* fake breakouts

---

# 2. Market Regime Logging Improvement

## Objective

Improve timeline clarity by logging market regime detection as a **separate explicit event**.

Currently the regime is embedded inside rule-engine output.

Separating this improves observability.

---

## Current Situation

The rule engine logs an abort event containing the regime.

This hides the reasoning chain.

Example:

Cycle start → Rule engine abort → Final decision.

The timeline does not clearly show the regime evaluation step.

---

## Improvement

The system should produce a dedicated timeline event immediately after regime evaluation.

Recommended logical sequence:

Cycle started

→ Market regime detected
→ Rule engine evaluation
→ AI evaluation (if applicable)
→ Final decision

---

## Benefit

This allows developers and traders to see:

* what regime was detected
* why the system considered the market tradable or not
* how that affected the rule engine

This improves transparency and debugging.

---

# 3. Market Activity Detection Improvement

## Objective

Improve the accuracy of the system’s **market activity detection**.

The current system classifies markets as “dead” partly using tick activity measurements.

However, depending on broker feed behavior, raw tick-rate measurements can be unreliable.

---

## Improvement

Market activity evaluation should rely more heavily on **candle-derived activity signals**.

Possible signals include:

* candle volume
* candle range expansion
* ATR changes
* candle body sizes

These indicators are already included in the MT5 snapshot.

The regime detector should prioritize these metrics when evaluating activity.

---

## Expected Result

This reduces false classification of markets as inactive when the price is actually moving.

The regime detector becomes more stable across different broker feeds.

---

# 4. Timeline Logging Improvements

## Objective

Improve decision traceability so that both developers and traders can understand the exact reasoning chain that led to each decision.

The timeline log is already well designed and should remain the primary debugging tool.

---

## Improvement

Each decision cycle should produce a clearly ordered chain of events.

Recommended event flow:

Cycle started

Market regime detected

Rule engine evaluation

Impulse filter result

AI analysis result (if applicable)

Score evaluation (if applicable)

Final decision

---

## Additional Context Logging

For every decision cycle, the timeline should include:

Market context

* session
* spread
* ATR
* ADR
* price snapshot

Regime analysis

* detected regime
* explanation of regime classification

Rule engine evaluation

* H1 context result
* M15 structure result
* M5 entry conditions

Impulse confirmation

* whether momentum expansion was detected
* relevant signals used for the decision

AI evaluation

* only executed when the rule engine approves the setup

Final decision

* trade approved or rejected
* primary reason

---

# 5. AI Invocation Control

The current behavior should remain unchanged.

AI should **only run when the rule engine validates the structural setup**.

If the rule engine rejects the setup, the system should skip AI evaluation entirely.

This preserves the deterministic nature of the backend.

---

# 6. Compatibility With Existing Architecture

The following constraints must be respected:

* existing architecture must remain unchanged
* existing naming conventions must be preserved
* existing endpoints must not be renamed
* existing components must be reused when possible
* timeline logging format should remain compatible with current logs

If similar functionality already exists in the system, the implementation should enhance or reuse it instead of creating parallel logic.

---

# 7. Expected Impact

These improvements should produce the following benefits:

Improved trade quality

Better filtering of weak setups

Clearer system decision traceability

More accurate regime detection

Better developer and trader visibility into system reasoning

Minimal complexity increase

---

# Final Goal

The trading engine should operate as a transparent layered decision system:

Market snapshot ingestion

→ Market regime detection
→ Rule engine structural validation
→ Impulse confirmation
→ AI evaluation (if applicable)
→ Final decision

Every step in this pipeline must produce clear timeline logs so that both developers and traders can observe the exact reasoning behind each decision.

This ensures the system remains **deterministic, explainable, and safe for automated trading**.
