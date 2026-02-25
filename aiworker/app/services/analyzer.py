from app.models.contracts import MarketSnapshot, TradeSignal
from app.parsers.signal_parser import SignalParser
from app.services.providers import BaseAIProvider


class AnalyzerService:
    def __init__(self, provider: BaseAIProvider) -> None:
        self._provider = provider
        self._parser = SignalParser()

    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        raw_signal = await self._provider.analyze(snapshot)
        return self._parser.validate(raw_signal)
