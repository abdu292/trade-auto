using Brain.Application.Features.Signals.DTOs;
using MediatR;

namespace Brain.Application.Features.Signals.Commands;

public sealed record AnalyzeSnapshotCommand(string Symbol) : IRequest<TradeSignalDto>;
