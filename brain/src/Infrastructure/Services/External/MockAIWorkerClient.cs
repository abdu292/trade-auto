using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class MockAIWorkerClient : IAIWorkerClient
{
    public Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, string? cycleId, CancellationToken cancellationToken)
    {
        var result = new TradeSignalContract(
            Rail: "BUY_LIMIT",
            Entry: snapshot.Ma20,
            Tp: snapshot.Ma20 + (snapshot.Atr * 1.5m),
            Pe: DateTimeOffset.UtcNow.AddMinutes(20),
            Ml: 3600,
            Confidence: 0.74m,
            SafetyTag: "SAFE",
            DirectionBias: "BULLISH",
            AlignmentScore: 0.76m,
            NewsTags: ["no_high_impact_news"],
            Summary: "Mock AI alignment supports buy-first accumulation.",
            ConsensusPassed: true,
            AgreementCount: 2,
            RequiredAgreement: 2,
            DisagreementReason: null,
            ProviderVotes: ["mock-grok:BUY_LIMIT@entry", "mock-openai:BUY_LIMIT@entry"],
            CycleId: cycleId);

        return Task.FromResult(result);
    }

    public Task<ModeSignalContract?> GetModeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        var mode = snapshot.HasPanicDropSequence || snapshot.PanicSuspected
            ? "DEESCALATION_RISK"
            : (snapshot.IsExpansion && snapshot.HasImpulseCandles ? "WAR_PREMIUM" : "UNKNOWN");

        ModeSignalContract result = new(
            Mode: mode,
            Confidence: mode == "UNKNOWN" ? 0.45m : 0.72m,
            Keywords: mode == "DEESCALATION_RISK"
                ? ["panic", "selloff"]
                : (mode == "WAR_PREMIUM" ? ["escalation", "risk"] : []),
            TtlSeconds: mode == "DEESCALATION_RISK" ? 1800 : 900,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        return Task.FromResult<ModeSignalContract?>(result);
    }

    public Task<StudyRefinementSuggestionContract?> StudyAnalyzeAsync(
        MarketSnapshotContract snapshot,
        StudyContextContract context,
        CancellationToken cancellationToken)
    {
        // Mock: return a reasonable default verdict for dev/test environments
        var result = new StudyRefinementSuggestionContract(
            StudyCycleId: context.StudyCycleId,
            BottomPermissionVerdict: "CORRECT",
            WaterfallVerdict: "CORRECT",
            RuleAdjustments: [],
            Confidence: 0.6,
            Reasoning: $"Mock study: {context.ConsecutiveWaterfallFailures} waterfall failure(s) reviewed. No rule changes recommended in mock mode.",
            ProviderVotes: ["mock-study:CORRECT@default"]);

        return Task.FromResult<StudyRefinementSuggestionContract?>(result);
    }
}
