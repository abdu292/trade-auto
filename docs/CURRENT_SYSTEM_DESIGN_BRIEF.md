# Trade Auto — Current System Design Brief

## 1) System purpose

Trade Auto is a multi-component automated trading platform focused on XAUUSD. It combines:

- Rule-based execution and safety controls (Brain backend)
- AI-assisted signal and market-mode analysis (AI Worker)
- Broker-side order execution through MetaTrader 5 (MT5 EA)
- Operator-facing monitoring/control UI (Flutter app)

The design goal is deterministic, safety-first trade execution where AI is an input signal layer, but backend risk/guard rails remain the final authority.

---

## 2) High-level architecture

Monorepo components:

- **brain/**: ASP.NET Core (.NET 10) Clean Architecture service
  - Layers: Domain, Application, Infrastructure, Web
  - Acts as the core decision engine and integration hub
- **aiworker/**: FastAPI service
  - Provides `POST /analyze` and `POST /mode`
  - Supports committee-based model consensus
- **mt5ea/**: MQL5 Expert Advisor
  - Pulls pending trade commands from Brain
  - Reports execution status back to Brain
- **ui/**: Flutter app (Riverpod + Dio)
  - Runtime monitoring and operational controls
  - Strategy profile switching and environment switch (Production/Local)

Supporting assets include specs/prompts/docs plus startup/deploy scripts.

---

## 3) Runtime responsibilities by component

### Brain (authoritative runtime)

Primary responsibilities:

- Maintains strategy profiles (`Standard`, `WarPremium`) and active profile routing
- Hosts trading/risk/session/signal APIs
- Applies deterministic gating, risk guards, sizing, and veto logic
- Integrates TradingView webhook signals
- Polls/consumes AI Worker outputs (`/mode`, `/analyze`)
- Exposes MT5 endpoints consumed by EA (`/mt5/pending-trades`, `/mt5/trade-status`)
- Runs background jobs (session scheduling, signal polling)
- Persists state via EF Core (SQL Server)

Important design point: Brain remains final decision authority. AI can block eligibility (e.g., quorum failure) but does not bypass backend safety gates.

### AI Worker (analysis + mode feed)

Primary responsibilities:

- Produces structured market mode (`WAR_PREMIUM`, `DEESCALATION_RISK`, `UNKNOWN`) with confidence + TTL
- Produces structured trade analysis via `/analyze`
- Supports multi-provider committee mode with quorum threshold (`CONSENSUS_MIN_AGREEMENT`)
- Enriches context from Telegram and optional external RSS feeds
- Uses configurable transport for Grok (`openrouter` currently preferred, `direct` optional)

Committee design intent: if consensus is weak, output becomes conservative (`NO_TRADE` semantics), reducing model-driven false positives.

### MT5 EA (broker execution adapter)

Primary responsibilities:

- Poll Brain for pending trade instructions
- Execute on MT5 terminal using MQL5 trade primitives
- Post order/trade status callbacks to Brain
- Include API key (`X-API-Key`) on `/mt5/*` calls
- Apply local execution/risk guard helpers in EA modules

### UI (operations console)

Primary responsibilities:

- Show health, runtime telemetry, and trade/session state
- Allow profile switching (`Standard` / `WarPremium`)
- Allow endpoint environment switch (`Production` / `Local`)
- Provide operator visibility rather than direct broker execution

---

## 4) End-to-end flow (current)

1. **Signal/context ingestion**
   - Brain ingests internal/external signals (including TradingView)
   - AI Worker ingests Telegram + optional RSS and produces mode/analysis outputs

2. **Decision phase in Brain**
   - Brain combines strategy profile + risk gates + AI outputs
   - Applies deterministic veto/eligibility logic
   - Produces trade intents only when all guards pass

3. **Execution handoff to MT5**
   - EA fetches pending trades from Brain
   - EA executes orders in MT5
   - EA posts resulting status updates back to Brain

4. **Monitoring and operator control**
   - UI queries Brain endpoints for current system state
   - Operator can switch strategy profile and confirm active runtime behavior

---

## 5) Operational modes and profiles

- **Strategy profiles (Brain-side):**
  - `Standard` (default)
  - `WarPremium` (stricter war-expansion behavior and kill-switch style handling)

- **AI mode feed (AI Worker output):**
  - `WAR_PREMIUM`, `DEESCALATION_RISK`, `UNKNOWN`
  - Returned with confidence, keywords, and TTL

- **AI transport/model setup:**
  - Typically Grok via OpenRouter in current setup
  - Optional direct Grok path exists
  - Committee can include OpenAI/Gemini/(optional Perplexity)

Key separation: strategy profile choice does not automatically change AI transport; transport is configured via AI Worker environment variables.

---

## 6) Security and deployment posture (current)

- Local startup script (`start-local.ps1`) launches Brain and AI Worker and checks `/health`
- MT5-facing endpoints are API-key protected and can enforce IP allowlist
- Telegram credentials and model API keys are environment/secret managed
- Azure deployment and go-live checklists exist in docs for production hardening

---

## 7) Current strengths

- Clear separation of concerns between decision engine, AI analysis, broker adapter, and UI
- Deterministic backend gating prevents blind model execution
- Committee-based AI design reduces single-model fragility
- Strategy profile switching is built-in and operationally visible
- Simulator and monitoring endpoints support runtime validation

---

## 8) Known gaps / not-yet-full strict parity

Based on current parity status docs:

- Full micro-structure proof from raw lower-timeframe bars is still simplified in parts
- Telegram escalation/de-escalation threshold logic can be further hardened
- Cross-asset macro feed completeness is partial
- Historical pattern overlay is not fully integrated into live scoring/rotation logic
- Full strict v4 backtest/replay evidence package is pending

So the system is production-capable in a guarded form, but strict-spec “100% parity” is explicitly not yet claimed.

---

## 9) Concise architecture summary for external AI review

"Trade Auto is a safety-first algorithmic trading platform where a .NET backend (Brain) is the final decision authority, a FastAPI AI service provides structured mode/analysis signals using committee consensus, an MT5 EA executes approved trades, and a Flutter UI provides monitoring/control. The system favors deterministic backend veto logic over model autonomy, supports switchable strategy profiles (`Standard`/`WarPremium`), and currently runs with partial-but-not-full strict parity to its most demanding war-premium spec." 
