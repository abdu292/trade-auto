from abc import ABC, abstractmethod

from app.models.contracts import MarketSnapshot, TradeSignal


class BaseAIProvider(ABC):
    @abstractmethod
    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        raise NotImplementedError


class MockAIProvider(BaseAIProvider):
    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        entry = round(snapshot.ma20, 5)
        tp = round(snapshot.ma20 + (snapshot.atr * 1.5), 5)

        return TradeSignal(
            rail="BUY_LIMIT",
            entry=entry,
            tp=tp,
            pe=snapshot.timestamp,
            ml=3600,
            confidence=0.72,
        )
