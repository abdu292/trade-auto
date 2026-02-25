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
    session: str
    timestamp: datetime


class TradeSignal(BaseModel):
    rail: str
    entry: float
    tp: float
    pe: datetime
    ml: int
    confidence: float
