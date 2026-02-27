# AI Provider Decision

## Decision
Use a universal one-key platform as the default operating mode.

Chosen default: OpenRouter (single key), with committee models configured through one gateway.

## Why this decision
- Lower operational complexity: one billing account, one key, one integration path
- Easier key rotation and secret management
- Faster experimentation with model swaps without rewriting providers
- Better fit for this system because hard safety logic is deterministic in Brain; AI is advisory

## What stays true
- Safety gates in Brain remain authoritative
- AI never bypasses BLOCK logic
- Output discipline remains TABLE or NO TRADE

## Trade-offs
- Adds gateway dependency (OpenRouter availability)
- Slightly less direct provider-level control
- Potential pricing/routing differences vs direct provider contracts

## Fallback Strategy
- Keep multi-provider mode available for failover or audit comparisons
- If universal gateway fails, switch mode to multi and use direct provider keys

## Configuration
Primary (recommended):
- AI_PROVIDER_MODE=universal
- OPENROUTER_API_KEY=<your_key>
- OPENROUTER_MODELS=openai/gpt-4.1-mini,google/gemini-2.0-flash
- AI_STRATEGY=committee
- CONSENSUS_MIN_AGREEMENT=1 or 2 (based on latency/risk preference)

Fallback:
- AI_PROVIDER_MODE=multi
- OPENAI_API_KEY / GROK_API_KEY / PERPLEXITY_API_KEY / GEMINI_API_KEY

## Recommended for your use case
Start with universal mode + two models in committee for stability and speed. Keep multi mode only as emergency fallback, not as daily default.
