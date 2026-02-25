using Brain.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class MockCalendarService(ILogger<MockCalendarService> logger) : ICalendarService
{
    public Task ScheduleEventAsync(string title, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mock calendar event {Title}: {Start} -> {End}", title, startUtc, endUtc);
        return Task.CompletedTask;
    }
}
