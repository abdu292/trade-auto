from fastapi import FastAPI

from app.routers.health import router as health_router
from app.routers.analyze import router as analyze_router


app = FastAPI(title="Trade Auto AI Worker", version="1.0.0")

app.include_router(health_router)
app.include_router(analyze_router)
