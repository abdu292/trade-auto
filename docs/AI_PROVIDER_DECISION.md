# AI Provider Decision

## Decision
Use **Grok as the sole live decision engine** for both local and production runtime, with OpenRouter transport now and direct xAI transport available later.

## Why this decision
- Exact alignment with `spec/spec_v3.md` live-path statement (Grok drives NEWS/ANALYZE/TABLE).
- Same behavior between local and production (no runtime mode drift).
- Simpler operational validation: one live model path, one health parity target.

## What stays true
- Safety gates in Brain remain authoritative
- AI never bypasses BLOCK logic
- Output discipline remains TABLE or NO TRADE

## Trade-offs
- Higher direct dependency on xAI availability for live path.
- Less model diversity in live runtime (by design for strict parity).

## Fallback Strategy (operational emergency only)
- Keep a documented fallback runbook outside live parity profile.
- Do not mix fallback providers into normal runtime if strict parity is required.

## Configuration
Live runtime (local + production):
- GROK_RUNTIME_TRANSPORT=openrouter
- OPENROUTER_API_KEY=<your_key>
- GROK_OPENROUTER_MODEL=x-ai/grok-2-latest

Later (optional direct transport):
- GROK_RUNTIME_TRANSPORT=direct
- GROK_API_KEY=<your_key>
- GROK_MODEL=grok-2-latest

## Recommended for your use case
Run Grok-only in both local and production so behavior is identical end-to-end. Use OpenRouter transport now for speed, then move to direct xAI key when you are ready, while keeping the same Grok-only live semantics.
