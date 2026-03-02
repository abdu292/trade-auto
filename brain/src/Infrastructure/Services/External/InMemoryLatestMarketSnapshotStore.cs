using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryLatestMarketSnapshotStore : ILatestMarketSnapshotStore
{
    private readonly Lock _gate = new();
    private MarketSnapshotContract? _latest;
    private readonly Queue<TickIngestionEventContract> _recentTicks = new();
    private long _totalIngested;
    private const int MaxTickHistory = 600;

    public void Upsert(MarketSnapshotContract snapshot)
    {
        lock (_gate)
        {
            _latest = snapshot;
            _totalIngested++;

            var nowUtc = DateTimeOffset.UtcNow;
            var latencyMs = Math.Max(0, (nowUtc - snapshot.Mt5ServerTime).TotalMilliseconds);
            _recentTicks.Enqueue(new TickIngestionEventContract(
                ReceivedAtUtc: nowUtc,
                Mt5ServerTime: snapshot.Mt5ServerTime,
                KsaTime: snapshot.KsaTime,
                Symbol: snapshot.Symbol,
                Session: snapshot.Session,
                Bid: snapshot.Bid,
                Ask: snapshot.Ask,
                Spread: snapshot.Spread,
                VolatilityExpansion: snapshot.VolatilityExpansion,
                TimeframeCount: snapshot.TimeframeData.Count,
                EstimatedIngestionLatencyMs: latencyMs));

            while (_recentTicks.Count > MaxTickHistory)
            {
                _recentTicks.Dequeue();
            }
        }
    }

    public bool TryGet(out MarketSnapshotContract? snapshot)
    {
        lock (_gate)
        {
            snapshot = _latest;
            return snapshot is not null;
        }
    }

    public TickIngestionTelemetryContract GetTickTelemetry(int take = 20)
    {
        lock (_gate)
        {
            var limit = Math.Clamp(take, 1, 200);
            var events = _recentTicks.ToArray();
            var recent = events.Reverse().Take(limit).ToArray();
            var latest = recent.FirstOrDefault();
            var nowUtc = DateTimeOffset.UtcNow;

            var inLast1m = events.Count(x => (nowUtc - x.ReceivedAtUtc).TotalMinutes <= 1);
            var inLast5m = events.Count(x => (nowUtc - x.ReceivedAtUtc).TotalMinutes <= 5);

            return new TickIngestionTelemetryContract(
                TotalIngested: _totalIngested,
                LastReceivedAtUtc: latest?.ReceivedAtUtc,
                LastMt5ServerTime: latest?.Mt5ServerTime,
                LastKsaTime: latest?.KsaTime,
                LastIngestionLatencyMs: latest?.EstimatedIngestionLatencyMs ?? 0,
                TicksPerMinuteLast1m: inLast1m,
                TicksPerMinuteLast5m: Math.Round(inLast5m / 5d, 2),
                RecentTicks: recent);
        }
    }
}
