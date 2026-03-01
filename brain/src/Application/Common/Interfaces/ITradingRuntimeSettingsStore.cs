namespace Brain.Application.Common.Interfaces;

public interface ITradingRuntimeSettingsStore
{
    string GetSymbol();

    void SetSymbol(string symbol);
}