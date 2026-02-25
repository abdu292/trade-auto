namespace Brain.Application.Common.Interfaces;

public interface IMt5BridgeClient
{
    Task<bool> SendPendingTradeAsync(object tradePayload, CancellationToken cancellationToken);
    Task<bool> SendTradeStatusAsync(Guid tradeId, string status, CancellationToken cancellationToken);
}
