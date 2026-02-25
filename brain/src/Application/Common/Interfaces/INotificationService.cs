namespace Brain.Application.Common.Interfaces;

public interface INotificationService
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken);
}
