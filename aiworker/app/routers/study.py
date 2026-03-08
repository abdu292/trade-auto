import logging
from fastapi import APIRouter, HTTPException

from app.models.contracts import StudyRefinementRequest, StudyRefinementSuggestion
from app.ai.config import AI_ANALYZERS
from app.ai.provider_manager import AIProviderManager

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Study"])


def _is_list_item_line(line: str) -> bool:
    """Return True if a text line looks like a bullet point or numbered list item."""
    stripped = line.strip()
    if stripped.startswith("-"):
        return True
    if len(stripped) > 2 and stripped[0].isdigit() and stripped[1] in ".):":
        return True
    return False


@router.post("/study-analyze", response_model=StudyRefinementSuggestion)
async def study_analyze(request: StudyRefinementRequest) -> StudyRefinementSuggestion:
    """
    Autonomous study/self-crosscheck refinement endpoint (PRD point 4).

    Called by Brain when STUDY_LOCK is active after consecutive waterfall failures.
    Uses ALL configured analyzers (not just the lead model) to perform a deeper review
    of recent blocked candidates and waterfall events, then suggests rule adjustments.

    Returns verdicts on:
    - bottomPermissionVerdict: TOO_STRICT | CORRECT | TOO_LOOSE
    - waterfallVerdict: CORRECT | OVER_SENSITIVE | UNDER_SENSITIVE
    - ruleAdjustments: list of natural-language rule change suggestions
    """
    try:
        study_cycle_id = request.studyCycleId or f"study_{request.snapshot.cycleId}"

        if not AI_ANALYZERS:
            logger.warning("No AI analyzers configured; returning default study verdict.")
            return StudyRefinementSuggestion(
                studyCycleId=study_cycle_id,
                bottomPermissionVerdict="CORRECT",
                waterfallVerdict="CORRECT",
                ruleAdjustments=[],
                confidence=0.4,
                reasoning="No AI providers configured; study used deterministic defaults.",
                providerVotes=["fallback-study:CORRECT@default"],
            )

        # Use ALL analyzers (not just lead) for study — this is the key difference
        # from the normal live path which uses only the lead model.
        manager = AIProviderManager(AI_ANALYZERS)

        # Build study context with recent failure information
        market_ctx = request.snapshot.model_dump()
        market_ctx["study_context"] = {
            "study_cycle_id": study_cycle_id,
            "consecutive_waterfall_failures": request.consecutiveWaterfallFailures,
            "recent_blocked_candidates": request.recentBlockedCandidates,
            "recent_waterfall_reasons": request.recentWaterfallReasons,
            "task": (
                "STUDY & SELF-CROSSCHECK mode. The system has paused live trading after "
                f"{request.consecutiveWaterfallFailures} consecutive waterfall failures. "
                "Analyze the provided market context and blocked trade candidates. "
                "Determine: (1) Is the BottomPermission gate too strict, too loose, or correct? "
                "(2) Is waterfall detection over-sensitive, under-sensitive, or correct? "
                "(3) What specific rule adjustments, if any, should be made? "
                "Respond with bottomPermissionVerdict=TOO_STRICT|CORRECT|TOO_LOOSE, "
                "waterfallVerdict=CORRECT|OVER_SENSITIVE|UNDER_SENSITIVE, "
                "and a list of 0-3 specific actionable rule adjustment suggestions."
            ),
        }

        decision = await manager.analyze_with_committee(
            market_context=market_ctx,
            min_agreement=1,
            entry_tolerance_pct=0.003,
        )

        provider_votes: list[str] = []
        bottom_verdict = "CORRECT"
        waterfall_verdict = "CORRECT"
        rule_adjustments: list[str] = []
        confidence = 0.5
        reasoning = "Study analysis completed with deterministic defaults."

        if decision.consensus_passed and decision.signal is not None:
            signal = decision.signal
            confidence = float(signal.confidence)
            reasoning = signal.reasoning or "Study analysis completed."
            provider_votes = list(decision.provider_votes or [])

            # Parse verdicts from the reasoning text
            reasoning_upper = reasoning.upper()

            if "TOO_STRICT" in reasoning_upper:
                bottom_verdict = "TOO_STRICT"
            elif "TOO_LOOSE" in reasoning_upper:
                bottom_verdict = "TOO_LOOSE"
            else:
                bottom_verdict = "CORRECT"

            if "OVER_SENSITIVE" in reasoning_upper or "OVERSENSITIVE" in reasoning_upper:
                waterfall_verdict = "OVER_SENSITIVE"
            elif "UNDER_SENSITIVE" in reasoning_upper or "UNDERSENSITIVE" in reasoning_upper:
                waterfall_verdict = "UNDER_SENSITIVE"
            else:
                waterfall_verdict = "CORRECT"

            # Extract rule adjustments from the reasoning (each line starting with '-' or a number)
            for line in reasoning.splitlines():
                stripped = line.strip()
                if _is_list_item_line(line):
                    adjustment = stripped.lstrip("-0123456789.) ").strip()
                    if adjustment and len(adjustment) > 10:
                        rule_adjustments.append(adjustment)

            rule_adjustments = rule_adjustments[:3]
        else:
            reasoning = (
                f"Study committee did not reach consensus "
                f"(failures={request.consecutiveWaterfallFailures}): "
                f"{decision.disagreement_reason or 'insufficient agreement'}"
            )
            provider_votes = list(decision.provider_votes or [])
            confidence = 0.35

        logger.info(
            "Study analysis complete: cycleId=%s bottomVerdict=%s waterfallVerdict=%s adjustments=%d",
            study_cycle_id,
            bottom_verdict,
            waterfall_verdict,
            len(rule_adjustments),
        )

        return StudyRefinementSuggestion(
            studyCycleId=study_cycle_id,
            bottomPermissionVerdict=bottom_verdict,
            waterfallVerdict=waterfall_verdict,
            ruleAdjustments=rule_adjustments,
            confidence=confidence,
            reasoning=reasoning,
            providerVotes=provider_votes,
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Study analysis failed: %s", str(e))
        raise HTTPException(status_code=500, detail="Study analysis failed") from e
