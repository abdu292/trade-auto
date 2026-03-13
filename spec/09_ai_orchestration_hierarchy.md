# بسم الله الرحمن الرحيم
# AI ORCHESTRATION + COST / LATENCY HIERARCHY
Version: ORCHESTRATION-v2.0

## Objective
Maximize safe rotations while minimizing latency and API cost.

## Hierarchy
1. GROK = primary brain
2. PERPLEXITY = validator / macro contradiction engine
3. CHATGPT = deep fallback / arbitration
4. GEMINI = final consistency audit

## Default Flow
A. EA builds normalized context packet
B. Send packet to GROK
C. If GROK returns TRADE_TABLE:
   -> send TABLE + packet to PERPLEXITY
D. If PERPLEXITY returns VALIDATED:
   -> send to GEMINI if audit layer enabled
E. If GEMINI approves:
   -> execute
F. If GROK returns NO_TRADE but the orchestrator detects:
   - strong candidate zone
   - contradiction
   - missed-opportunity conditions
   -> send to CHATGPT fallback
G. Any fallback SAFE_TABLE must still go through PERPLEXITY validation, then GEMINI audit if enabled

## Hard Law
No later AI may override:
- hard safety blockers
- capital blockers
- TABLE legality
