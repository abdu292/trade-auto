using Brain.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class MockWhatsAppService(ILogger<MockWhatsAppService> logger) : IWhatsAppService
{
    public Task SendMessageAsync(string to, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mock WhatsApp message to {To}: {Message}", to, message);
        return Task.CompletedTask;
    }
}
