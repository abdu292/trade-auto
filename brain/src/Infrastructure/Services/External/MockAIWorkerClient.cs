using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class MockAIWorkerClient : IAIWorkerClient
{
    public Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
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
            ProviderVotes: ["mock-grok:BUY_LIMIT@entry", "mock-openai:BUY_LIMIT@entry"]);

        return Task.FromResult(result);
    }
}
