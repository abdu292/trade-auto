# بسم الله الرحمن الرحيم
# WATERFALL DETECTION ENGINE
Version: WATERFALL-v2.0

## Goal
Separate dangerous waterfall continuation from valid flush reversal.

## WATERFALL_CONTINUATION Signals
- repeated bearish closes near lows
- shelf breaks continue
- no meaningful rebound
- volatility expansion in continuation direction
- structure deterioration persists

## FLUSH_REVERSAL_ATTEMPT Signals
- sharp drop into known support shelf
- lower rejection
- no decisive lower extension
- zone starts holding
- reclaim or rebuild potential appears

## Hard Rule
If classified as WATERFALL_CONTINUATION:
- new buy blocked

If classified as FLUSH_REVERSAL_ATTEMPT:
- provisional candidate allowed if all other blockers remain clear
