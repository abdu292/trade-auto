from app.models.contracts import TradeSignal


class SignalParser:
    def validate(self, signal: TradeSignal) -> TradeSignal:
        if signal.rail not in {"BUY_LIMIT", "BUY_STOP"}:
            raise ValueError("Unsupported rail type")
        if signal.entry <= 0 or signal.tp <= 0:
            raise ValueError("Entry and TP must be positive")
        if signal.ml <= 0:
            raise ValueError("ML must be positive")
        if signal.confidence < 0 or signal.confidence > 1:
            raise ValueError("Confidence must be between 0 and 1")
        return signal
