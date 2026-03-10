using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Spec v9/v10 — Thread-safe in-memory implementation of the setup lifecycle store.
/// Registered as a singleton so the ARMED candidate persists across polling cycles.
/// </summary>
public sealed class InMemorySetupLifecycleStore : ISetupLifecycleStore
{
    private readonly ConcurrentDictionary<string, ArmedSetupCandidate> _candidates
        = new(StringComparer.OrdinalIgnoreCase);

    public void StoreArmedCandidate(string symbol, ArmedSetupCandidate candidate)
        => _candidates[symbol] = candidate;

    public ArmedSetupCandidate? GetCandidate(string symbol)
        => _candidates.TryGetValue(symbol, out var c) ? c : null;

    public void MarkOrderPlanted(string symbol, string cycleId)
    {
        if (_candidates.TryGetValue(symbol, out var c))
        {
            _candidates[symbol] = c with
            {
                LifecycleState = SetupLifecycleState.OrderPlanted,
                PlantedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    public void MarkPassedOverextended(string symbol, string reason)
    {
        if (_candidates.TryGetValue(symbol, out var c))
        {
            _candidates[symbol] = c with
            {
                LifecycleState = SetupLifecycleState.PassedOverextended,
                InvalidationReason = reason,
            };
        }
    }

    public void MarkInvalidated(string symbol, string reason)
    {
        if (_candidates.TryGetValue(symbol, out var c))
        {
            _candidates[symbol] = c with
            {
                LifecycleState = SetupLifecycleState.Invalidated,
                InvalidationReason = reason,
            };
        }
    }

    public void Clear(string symbol) => _candidates.TryRemove(symbol, out _);

    public SetupLifecycleStatusContract GetStatus(string symbol)
    {
        var c = GetCandidate(symbol);
        return new SetupLifecycleStatusContract(
            Symbol: symbol,
            LifecycleState: c?.LifecycleState ?? "NONE",
            PathType: c?.PathType,
            BaseLevel: c?.BaseLevel,
            LidLevel: c?.LidLevel,
            TriggerCondition: c?.TriggerCondition,
            CreatedAt: c?.CreatedAt,
            ExpiryWindow: c?.ExpiryWindow,
            InvalidationReason: c?.InvalidationReason,
            PlantedAt: c?.PlantedAt);
    }
}
