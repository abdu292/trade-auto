import logging
from fastapi import APIRouter, HTTPException

from app.models.contracts import TradeTableReviewRequest, TradeTableReviewResult
from app.ai.config import AI_ANALYZERS
from app.ai.provider_manager import AIProviderManager

logger = logging.getLogger(__name__)
router = APIRouter(tags=["TableReview"])


@router.post("/table-review", response_model=TradeTableReviewResult)
async def table_review(request: TradeTableReviewRequest) -> TradeTableReviewResult:
    """
    AI review of an armed trade TABLE (spec_v9 last section).

    Called by Brain after a trade is armed and queued for human approval.
    Uses all configured AI providers to review the proposed TABLE and return
    a structured commentary (APPROVE / CAUTION / SKIP) with reasoning.

    This is an advisory layer only — it does not block or auto-execute any trade.
    """
    try:
        if not AI_ANALYZERS:
            logger.warning("No AI analyzers configured; returning default TABLE review.")
            return TradeTableReviewResult(
                tradeId=request.tradeId,
                action="CAUTION",
                confidence=0.4,
                reasoning="No AI providers configured; review used deterministic defaults.",
                providerVotes=["fallback:CAUTION@default"],
            )

        manager = AIProviderManager(AI_ANALYZERS)

        # Build a review context for the AI committee.
        # Market data fields (atr, adr, ma20, timeframeData) are left empty because
        # TABLE review is based solely on the armed trade parameters (in table_review_context),
        # not on raw market data. The AI provider is explicitly instructed via the task prompt
        # to evaluate the TABLE fields only.
        review_context: dict = {
            "symbol": request.symbol,
            "session": request.session,
            "sessionPhase": request.sessionPhase,
            "timestamp": None,
            "atr": 0.0,
            "adr": 0.0,
            "ma20": 0.0,
            "timeframeData": [],
            "table_review_context": {
                "task": (
                    "TABLE REVIEW mode. The rule engine has armed a pending trade order. "
                    "Review the proposed TABLE below and assess whether this is a sound trade setup. "
                    "Consider: (1) Is the entry level logical for this session and risk state? "
                    "(2) Is the TP target realistic given the risk state and efficiency score? "
                    "(3) Are there any red flags in the cause, regime or waterfall risk? "
                    "Respond with action=APPROVE if the setup looks sound, action=CAUTION if you see "
                    "minor concerns, or action=SKIP if you see significant issues. "
                    "Provide a concise reasoning (2-4 sentences). "
                    "Include the word APPROVE, CAUTION, or SKIP clearly in your response."
                ),
                "tradeId": request.tradeId,
                "cycleId": request.cycleId,
                "rail": request.rail,
                "entry": request.entry,
                "tp": request.tp,
                "grams": request.grams,
                "session": request.session,
                "sessionPhase": request.sessionPhase,
                "engineState": request.engineState,
                "cause": request.cause,
                "waterfallRisk": request.waterfallRisk,
                "riskState": request.riskState,
                "alignmentScore": request.alignmentScore,
                "efficiencyScore": request.efficiencyScore,
                "shopBuy": request.shopBuy,
                "shopSell": request.shopSell,
                "regime": request.regime,
                "regimeTag": request.regimeTag,
                "bucket": request.bucket,
                "sizeClass": request.sizeClass,
                "telegramState": request.telegramState,
                "aiSummary": request.aiSummary,
            },
        }

        decision = await manager.analyze_with_committee(
            market_context=review_context,
            min_agreement=1,
            entry_tolerance_pct=0.005,
        )

        action = "CAUTION"
        confidence = 0.5
        reasoning = "TABLE review completed."
        provider_votes: list[str] = []

        if decision.consensus_passed and decision.signal is not None:
            signal = decision.signal
            confidence = float(signal.confidence)
            reasoning = signal.reasoning or "TABLE review completed."
            provider_votes = list(decision.provider_votes or [])

            reasoning_upper = reasoning.upper()
            if "APPROVE" in reasoning_upper:
                action = "APPROVE"
            elif "SKIP" in reasoning_upper:
                action = "SKIP"
            else:
                action = "CAUTION"
        else:
            reasoning = (
                f"TABLE review committee did not reach consensus: "
                f"{decision.disagreement_reason or 'insufficient agreement'}"
            )
            provider_votes = list(decision.provider_votes or [])
            confidence = 0.35

        logger.info(
            "TABLE review complete: tradeId=%s action=%s confidence=%.2f",
            request.tradeId,
            action,
            confidence,
        )

        return TradeTableReviewResult(
            tradeId=request.tradeId,
            action=action,
            confidence=confidence,
            reasoning=reasoning,
            providerVotes=provider_votes,
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("TABLE review failed: %s", str(e))
        raise HTTPException(status_code=500, detail="TABLE review failed") from e
