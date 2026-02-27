namespace Brain.Application.Common.Interfaces;

public sealed record NotificationFeedItem(Guid Id, string Channel, string Title, string Message, DateTimeOffset CreatedAtUtc);

public interface INotificationFeedStore
{
    void Add(string channel, string title, string message);
    IReadOnlyCollection<NotificationFeedItem> GetLatest(int take = 50);
}
