using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Signals.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.Background;

public sealed class SignalPollingBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SignalPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();

            try
            {
                var snapshot = await marketData.GetSnapshotAsync("EURUSD", stoppingToken);
                await mediator.Send(new AnalyzeSnapshotCommand(snapshot), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Signal polling iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
