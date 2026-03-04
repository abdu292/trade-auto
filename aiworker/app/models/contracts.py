from datetime import datetime
from typing import Any
from pydantic import BaseModel, Field


class TimeframeData(BaseModel):
    timeframe: str
    open: float
    high: float
    low: float
    close: float
    volume: int = 0
    candleStartTime: datetime | None = None
    candleCloseTime: datetime | None = None
    candleBodySize: float = 0.0
    upperWickSize: float = 0.0
    lowerWickSize: float = 0.0
    candleRange: float = 0.0
    ma20Value: float = 0.0
    ma20Distance: float = 0.0
    rsi: float = 0.0
    atr: float = 0.0


class MarketSnapshot(BaseModel):
    symbol: str = Field(min_length=1, max_length=20)
    timeframeData: list[TimeframeData]
    atr: float
    adr: float
    ma20: float
    ma20H4: float = 0.0
    ma20H1: float = 0.0
    ma20M30: float = 0.0
    rsiH1: float = 0.0
    rsiM15: float = 0.0
    atrH1: float = 0.0
    atrM15: float = 0.0
    previousDayHigh: float = 0.0
    previousDayLow: float = 0.0
    weeklyHigh: float = 0.0
    weeklyLow: float = 0.0
    dayOpen: float = 0.0
    weekOpen: float = 0.0
    sessionHigh: float = 0.0
    sessionLow: float = 0.0
    sessionHighJapan: float = 0.0
    sessionLowJapan: float = 0.0
    sessionHighIndia: float = 0.0
    sessionLowIndia: float = 0.0
    sessionHighLondon: float = 0.0
    sessionLowLondon: float = 0.0
    sessionHighNy: float = 0.0
    sessionLowNy: float = 0.0
    previousSessionHigh: float = 0.0
    previousSessionLow: float = 0.0
    ema50H1: float = 0.0
    ema200H1: float = 0.0
    adrUsedPct: float = 0.0
    session: str
    timestamp: datetime
    volatilityExpansion: float | None = None
    dayOfWeek: str | None = None
    mt5ServerTime: datetime | None = None
    ksaTime: datetime | None = None
    uaeTime: datetime | None = None
    indiaTime: datetime | None = None
    internalClockUtc: datetime | None = None
    utcReferenceTime: datetime | None = None
    timeSkewMs: float = 0.0
    mt5ToKsaOffsetMinutes: int = 50
    telegramImpactTag: str = "LOW"
    tradingViewConfirmation: str = "NEUTRAL"
    isCompression: bool = False
    isExpansion: bool = False
    isAtrExpanding: bool = False
    hasOverlapCandles: bool = False
    hasImpulseCandles: bool = False
    hasLiquiditySweep: bool = False
    hasPanicDropSequence: bool = False
    isPostSpikePullback: bool = False
    isLondonNyOverlap: bool = False
    isBreakoutConfirmed: bool = False
    isUsRiskWindow: bool = False
    isFriday: bool = False
    bid: float = 0.0
    ask: float = 0.0
    spread: float = 0.0
    spreadMedian60m: float = 0.0
    spreadMax60m: float = 0.0
    # Spread stats 1m/5m (spec_v5.md A1)
    spreadMin1m: float = 0.0
    spreadAvg1m: float = 0.0
    spreadMax1m: float = 0.0
    spreadMin5m: float = 0.0
    spreadAvg5m: float = 0.0
    spreadMax5m: float = 0.0
    compressionCountM15: int = 0
    expansionCountM15: int = 0
    impulseStrengthScore: float = 0.0
    telegramState: str = "QUIET"
    panicSuspected: bool = False
    tvAlertType: str = "NONE"
    sessionPhase: str = "UNKNOWN"
    # Account state (spec_v5.md A1)
    freeMargin: float = 0.0
    equity: float = 0.0
    balance: float = 0.0
    # Tick/market quality (PRD)
    tickRatePer30s: float = 0.0
    freezeGapDetected: bool = False
    slippageEstimatePoints: float = 0.0
    sessionVwap: float = 0.0
    systemFetchedGoldRate: float = 0.0
    rateDeltaUsd: float = 0.0
    rateAuthority: str = "MT5"
    authoritativeRate: float = 0.0
    cycleId: str | None = None
    # Compression and order/account snapshots (PRD)
    compressionRangesM15: list[float] = Field(default_factory=list)
    pendingOrders: list[dict[str, Any]] = Field(default_factory=list)
    openPositions: list[dict[str, Any]] = Field(default_factory=list)
    orderExecutionEvents: list[dict[str, Any]] = Field(default_factory=list)


class PostTradeAnalysisRequest(BaseModel):
    """Request for post-trade re-analysis after order placement (PRD point 7)."""
    tradeId: str
    placedRail: str
    placedEntry: float
    placedTp: float
    placedGrams: float
    snapshot: MarketSnapshot


class PostTradeAnalysisSuggestion(BaseModel):
    """Suggestion returned after post-trade re-analysis."""
    tradeId: str
    action: str  # KEEP | ADJUST_ENTRY | ADJUST_TP | CANCEL
    suggestedEntry: float | None = None
    suggestedTp: float | None = None
    confidence: float
    reasoning: str


class TradeSignal(BaseModel):
    rail: str
    entry: float
    tp: float
    pe: datetime
    ml: int
    confidence: float
    safetyTag: str = "CAUTION"
    directionBias: str = "BULLISH"
    alignmentScore: float = 0.5
    newsImpactTag: str = "LOW"
    tvConfirmationTag: str = "NEUTRAL"
    newsTags: list[str] = Field(default_factory=list)
    summary: str = ""
    consensusPassed: bool = True
    agreementCount: int = 1
    requiredAgreement: int = 1
    disagreementReason: str | None = None
    providerVotes: list[str] = Field(default_factory=list)
    modeHint: str = "UNKNOWN"
    modeConfidence: float = 0.5
    modeTtlSeconds: int = 900
    modeKeywords: list[str] = Field(default_factory=list)
    regimeTag: str = "STANDARD"
    riskState: str = "CAUTION"
    geoHeadline: str = "NONE"
    dxyBias: str = "NEUTRAL"
    yieldsBias: str = "NEUTRAL"
    crossMetalsBias: str = "NEUTRAL"
    cbFlow: str = "UNKNOWN"
    instPositioning: str = "UNKNOWN"
    eventRisk: str = "LOW"
    promptRefs: list[str] = Field(default_factory=list)
    providerModels: list[str] = Field(default_factory=list)
    aiTraceJson: str | None = None
    cycleId: str | None = None
