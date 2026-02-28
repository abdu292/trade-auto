from datetime import datetime
from pydantic import BaseModel, Field


class TimeframeData(BaseModel):
    timeframe: str
    open: float
    high: float
    low: float
    close: float


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
    sessionHigh: float = 0.0
    sessionLow: float = 0.0
    session: str
    timestamp: datetime
    volatilityExpansion: float | None = None
    dayOfWeek: str | None = None
    mt5ServerTime: datetime | None = None
    ksaTime: datetime | None = None
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
    compressionCountM15: int = 0
    expansionCountM15: int = 0
    impulseStrengthScore: float = 0.0
    telegramState: str = "QUIET"
    panicSuspected: bool = False
    tvAlertType: str = "NONE"


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
    newsTags: list[str] = []
    summary: str = ""
