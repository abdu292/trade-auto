namespace Brain.Application.Common.Interfaces;

public interface IWhatsAppService
{
    Task SendMessageAsync(string to, string message, CancellationToken cancellationToken);
}
