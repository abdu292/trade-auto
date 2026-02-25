using Brain.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class MockMt5BridgeClient(ILogger<MockMt5BridgeClient> logger) : IMt5BridgeClient
{
    public Task<bool> SendPendingTradeAsync(object tradePayload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mock MT5 pending trade accepted: {Payload}", tradePayload);
        return Task.FromResult(true);
    }

    public Task<bool> SendTradeStatusAsync(Guid tradeId, string status, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mock MT5 trade status {TradeId} -> {Status}", tradeId, status);
        return Task.FromResult(true);
    }
}
