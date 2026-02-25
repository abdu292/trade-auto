using Brain.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.Background;

public sealed class SessionSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionSchedulerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var sessionCount = await db.SessionStates.CountAsync(stoppingToken);
            logger.LogDebug("Session scheduler heartbeat. SessionStates={Count}", sessionCount);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
