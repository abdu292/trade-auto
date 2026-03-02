import json
import logging
from fastapi import APIRouter, HTTPException

from app.models.contracts import PostTradeAnalysisRequest, PostTradeAnalysisSuggestion
from app.ai.provider_manager import AIProviderManager
from app.config import Settings

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Post-Trade"])


@router.post("/post-trade-analyze", response_model=PostTradeAnalysisSuggestion)
async def post_trade_analyze(request: PostTradeAnalysisRequest) -> PostTradeAnalysisSuggestion:
    """
    Post-trade re-analysis after order placement (PRD point 7).

    Once an order is placed, MT5 sends the new data to analyze again.
    The AI analyzes the placed deal and suggests adjustments:
    - Lower or raise entry price
    - Adjust TP level
    - Adjust expiry time
    - Cancel the order entirely
    """
    try:
        settings = Settings()
        if not settings.ai_providers:
            raise HTTPException(status_code=503, detail="No AI providers configured")

        manager = AIProviderManager(settings.ai_providers)

        # Build context including the placed trade details
        market_ctx = request.snapshot.model_dump()
        market_ctx["post_trade_context"] = {
            "trade_id": request.tradeId,
            "placed_rail": request.placedRail,
            "placed_entry": request.placedEntry,
            "placed_tp": request.placedTp,
            "placed_grams": request.placedGrams,
            "task": (
                "This order has just been placed in MT5. Re-analyze the current market context "
                "and the placed order. Determine if the entry, TP, or expiry need adjustment, "
                "or if the order should be cancelled. Respond with: "
                "action=KEEP|ADJUST_ENTRY|ADJUST_TP|CANCEL, and if adjusting provide new values."
            ),
        }

        decision = await manager.analyze_with_committee(
            market_context=market_ctx,
            min_agreement=1,
            entry_tolerance_pct=0.003,
        )

        if not decision.consensus_passed or decision.signal is None:
            return PostTradeAnalysisSuggestion(
                tradeId=request.tradeId,
                action="KEEP",
                confidence=0.5,
                reasoning=decision.disagreement_reason or "No consensus on adjustment; keeping current order.",
            )

        signal = decision.signal
        # Compare suggested entry/tp with placed values to determine action
        entry_diff_pct = abs(signal.entry - request.placedEntry) / max(1.0, request.placedEntry)
        tp_diff_pct = abs(signal.tp - request.placedTp) / max(1.0, request.placedTp)

        if signal.entry <= 0 or signal.tp <= 0:
            action = "KEEP"
            suggested_entry = None
            suggested_tp = None
        elif entry_diff_pct > 0.003 and tp_diff_pct > 0.003:
            action = "ADJUST_ENTRY"
            suggested_entry = signal.entry
            suggested_tp = signal.tp
        elif entry_diff_pct > 0.003:
            action = "ADJUST_ENTRY"
            suggested_entry = signal.entry
            suggested_tp = None
        elif tp_diff_pct > 0.003:
            action = "ADJUST_TP"
            suggested_entry = None
            suggested_tp = signal.tp
        else:
            action = "KEEP"
            suggested_entry = None
            suggested_tp = None

        return PostTradeAnalysisSuggestion(
            tradeId=request.tradeId,
            action=action,
            suggestedEntry=suggested_entry,
            suggestedTp=suggested_tp,
            confidence=signal.confidence,
            reasoning=signal.reasoning,
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Post-trade analysis failed: %s", str(e))
        raise HTTPException(status_code=500, detail="Post-trade analysis failed") from e
