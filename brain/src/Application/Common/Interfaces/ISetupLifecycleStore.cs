using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Spec v9/v10 — Per-symbol setup lifecycle tracking store.
/// Persists ARMED candidates across decision cycles so the engine can diagnose
/// whether an entry window was missed due to late evaluation.
/// </summary>
public interface ISetupLifecycleStore
{
    /// <summary>
    /// Store or replace the armed candidate for a symbol.
    /// Called when the decision stack returns ProceedToAi=true (structure fully valid).
    /// </summary>
    void StoreArmedCandidate(string symbol, ArmedSetupCandidate candidate);

    /// <summary>
    /// Get the current candidate for a symbol. Returns null when no candidate exists.
    /// </summary>
    ArmedSetupCandidate? GetCandidate(string symbol);

    /// <summary>
    /// Transition the candidate to ORDER_PLANTED state.
    /// Called after the pending trade is successfully queued.
    /// </summary>
    void MarkOrderPlanted(string symbol, string cycleId);

    /// <summary>
    /// Transition the candidate to PASSED_OVEREXTENDED state.
    /// Called when the engine cycles into OVEREXTENDED but the candidate entry is no longer reachable.
    /// </summary>
    void MarkPassedOverextended(string symbol, string reason);

    /// <summary>
    /// Transition the candidate to INVALIDATED state.
    /// Called when safety conditions change (waterfall HIGH, active hazard, spread explosion).
    /// </summary>
    void MarkInvalidated(string symbol, string reason);

    /// <summary>
    /// Remove the candidate for a symbol entirely (e.g. on system reset or study lock).
    /// </summary>
    void Clear(string symbol);

    /// <summary>
    /// Get a monitoring snapshot of the current lifecycle state for a symbol.
    /// </summary>
    SetupLifecycleStatusContract GetStatus(string symbol);
}
