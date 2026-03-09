namespace Brain.Application.Common.Interfaces;

public interface ITradingRuntimeSettingsStore
{
    string GetSymbol();

    void SetSymbol(string symbol);

    /// <summary>
    /// Returns whether Auto Trade mode is enabled (trades routed directly to MT5).
    /// Defaults to false — all ARMED trades go to the approval queue until the client enables this toggle.
    /// </summary>
    bool GetAutoTradeEnabled();

    void SetAutoTradeEnabled(bool enabled);

    /// <summary>
    /// Returns the minimum trade size in grams. Defaults to 0.1 g (configurable).
    /// Orders computed below this threshold are rejected by the decision engine.
    /// The default is intentionally small to support MICRO_ROTATION_MODE live testing
    /// without a minimum gram floor. Set higher to enforce a hard minimum for normal trading.
    /// </summary>
    decimal GetMinTradeGrams();

    void SetMinTradeGrams(decimal grams);

    /// <summary>
    /// Returns whether Micro Rotation Mode is enabled (refinement spec §D).
    /// When enabled: single pending trade at a time, no staggered ladder, uses free cash only,
    /// BUY_LIMIT / BUY_STOP only with mandatory TP and expiry.
    /// Designed for safe live experience testing with small free balance.
    /// Defaults to false.
    /// </summary>
    bool GetMicroRotationEnabled();

    void SetMicroRotationEnabled(bool enabled);
}