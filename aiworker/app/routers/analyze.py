from fastapi import APIRouter, HTTPException

from app.models.contracts import MarketSnapshot, TradeSignal
from app.services.analyzer import AnalyzerService
from app.services.providers import MockAIProvider


router = APIRouter(tags=["Analyze"])


@router.post("/analyze", response_model=TradeSignal)
async def analyze(snapshot: MarketSnapshot) -> TradeSignal:
    service = AnalyzerService(provider=MockAIProvider())

    try:
        return await service.analyze(snapshot)
    except ValueError as error:
        raise HTTPException(status_code=400, detail=str(error)) from error
