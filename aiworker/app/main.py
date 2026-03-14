import logging
from pathlib import Path

from dotenv import load_dotenv
from fastapi import FastAPI

from app.routers.health import router as health_router
from app.routers.analyze import router as analyze_router
from app.routers.mode import router as mode_router
from app.routers.post_trade import router as post_trade_router
from app.routers.study import router as study_router
from app.routers.table_review import router as table_review_router

# Load .env from aiworker dir and repo root so one key works for brain + aiworker
load_dotenv()
try:
    repo_root = Path(__file__).resolve().parent.parent.parent
    load_dotenv(repo_root / ".env")
except Exception:
    pass

logger = logging.getLogger(__name__)

app = FastAPI(title="Trade Auto AI Worker", version="1.0.0")


@app.on_event("startup")
def _log_ai_analyzer_config() -> None:
    import os
    from app.ai.config import AI_ANALYZERS, OPENROUTER_API_KEY
    n = len(AI_ANALYZERS)
    key_status = "set" if (OPENROUTER_API_KEY and len(OPENROUTER_API_KEY.strip()) > 4) else "not set"
    logger.info("OPENROUTER_API_KEY: %s (use .env or env var in aiworker process)", key_status)
    if n > 0:
        logger.info(
            "AI analyzers configured: %d (%s). Live and replay use the same providers.",
            n,
            ", ".join(a.name for a in AI_ANALYZERS[:5]) + (" ..." if n > 5 else ""),
        )
    else:
        logger.warning(
            "No AI analyzers configured — using deterministic fallback only. "
            "Set OPENROUTER_API_KEY (and OPENROUTER_MULTI_MODEL_COMMITTEE=true) or OPENAI_API_KEY / PERPLEXITY_API_KEY / GEMINI_API_KEY in .env or environment. "
            "Same key can be used for both replay and live."
        )


app.include_router(health_router)
app.include_router(analyze_router)
app.include_router(mode_router)
app.include_router(post_trade_router)
app.include_router(study_router)
app.include_router(table_review_router)
