# AI Worker

FastAPI service that receives `MarketSnapshot` JSON and returns `TradeSignal` JSON.

## Run

```bash
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

## Endpoints

- `GET /health`
- `POST /analyze`

Default provider is `MockAIProvider` so the service runs without external APIs.
