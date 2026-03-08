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
}