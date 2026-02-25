using Brain.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class MockNotificationService(ILogger<MockNotificationService> logger) : INotificationService
{
    public Task NotifyAsync(string title, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mock notification sent: {Title} - {Message}", title, message);
        return Task.CompletedTask;
    }
}
