# AI Provider Decision

## Decision
Use **multi-model committee as the live decision engine** for both local and production runtime, with hard `NO_TRADE` when quorum is not reached.

## Why this decision
- Meets the requirement to analyze with additional AIs before execution.
- Reduces single-model failure risk by enforcing quorum before trade authorization.
- Keeps deterministic Brain safety gates authoritative after AI consensus passes.

## What stays true
- Safety gates in Brain remain authoritative
- AI never bypasses BLOCK logic
- Output discipline remains TABLE or NO TRADE

## Trade-offs
- Higher latency/cost due to multiple model calls.
- More operational configuration (multiple API keys/models).
- Potentially fewer trades when models disagree (intentional safety behavior).

## Disagreement Policy
- If committee agreement is below threshold, response is `NO_TRADE`.
- Brain blocks queueing and logs the reason as `AI_QUORUM_FAILED`.
- No fallback trade is emitted when consensus fails.

## Configuration
Live runtime (local + production):
- AI_STRATEGY=committee
- CONSENSUS_MIN_AGREEMENT=2
- GROK_RUNTIME_TRANSPORT=openrouter
- OPENROUTER_API_KEY=<your_key>
- GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast
- OPENAI_API_KEY=<your_key>
- OPENAI_MODEL=gpt-4.1-mini
- GEMINI_API_KEY=<your_key>
- GEMINI_MODEL=gemini-2.0-flash
- (optional) PERPLEXITY_API_KEY=<your_key>
- (optional) PERPLEXITY_MODEL=sonar

Optional Grok direct transport:
- GROK_RUNTIME_TRANSPORT=direct
- GROK_API_KEY=<your_key>
- GROK_MODEL=grok-2-latest

## Recommended for your use case
Run committee mode in both local and production so behavior is identical end-to-end. Keep Grok in the committee, require at least two agreeing analyzers, and allow Brain to execute only after quorum passes.
