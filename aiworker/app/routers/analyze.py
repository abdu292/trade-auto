import logging
from fastapi import APIRouter, HTTPException

from app.models.contracts import MarketSnapshot, TradeSignal
from app.services.analyzer import AnalyzerService

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Analyze"])


@router.post("/analyze", response_model=TradeSignal)
async def analyze(snapshot: MarketSnapshot) -> TradeSignal:
    """
    Analyze market data and generate trade signal using AI providers
    
    Request:
        - symbol: Trading pair (XAUUSD family, including broker suffixes)
        - close: Current price
        - OHLC + technical indicators
        - session_name: Current trading session
        
    Response:
        - rail: BUY_LIMIT | BUY_STOP
        - entry: Entry price
        - tp: Take profit
        - pe: Pending expiry (HH:MM)
        - ml: Max life (HH:MM)
        - confidence: 0.0-1.0
        - reasoning: Why this signal
    """
    try:
        analyzer = AnalyzerService()
        signal = await analyzer.analyze(snapshot)
        logger.info(f"✓ Signal generated for {snapshot.symbol}: {signal.rail} @ {signal.entry}")
        return signal
    
    except ValueError as e:
        logger.error(f"Analyzer error: {str(e)}")
        raise HTTPException(status_code=400, detail=str(e)) from e
    
    except Exception as e:
        logger.error(f"Unexpected error in analyze: {str(e)}")
        raise HTTPException(status_code=500, detail="AI analysis failed") from e
