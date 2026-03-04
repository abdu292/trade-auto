using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Signals.Commands;
using Brain.Application.Features.Signals.DTOs;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using MediatR;

namespace Brain.Application.Features.Signals.Handlers;

public sealed class AnalyzeSnapshotCommandHandler(
    IAIWorkerClient aiWorkerClient,
    IApplicationDbContext dbContext) : IRequestHandler<AnalyzeSnapshotCommand, TradeSignalDto>
{
    public async Task<TradeSignalDto> Handle(AnalyzeSnapshotCommand request, CancellationToken cancellationToken)
    {
        var snapshot = request.Snapshot;
        var signal = await aiWorkerClient.AnalyzeAsync(snapshot, cycleId: null, cancellationToken);

        var entity = TradeSignal.Create(
            snapshot.Symbol,
            new RailType(signal.Rail),
            new Price(signal.Entry),
            new Price(signal.Tp),
            signal.Pe,
            signal.Ml,
            signal.Confidence);

        dbContext.TradeSignals.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TradeSignalDto(
            entity.Id.Value,
            entity.Symbol,
            entity.Rail.Value,
            entity.Entry.Value,
            entity.TakeProfit.Value,
            entity.PendingExpirationUtc,
            entity.MaxLifeSeconds,
            entity.Confidence,
            entity.CreatedAtUtc);
    }
}
