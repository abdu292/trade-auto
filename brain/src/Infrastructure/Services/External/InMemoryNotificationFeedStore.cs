using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryNotificationFeedStore : INotificationFeedStore
{
    private readonly ConcurrentQueue<NotificationFeedItem> _items = new();

    public void Add(string channel, string title, string message)
    {
        _items.Enqueue(new NotificationFeedItem(Guid.NewGuid(), channel, title, message, DateTimeOffset.UtcNow));

        while (_items.Count > 200)
        {
            _items.TryDequeue(out _);
        }
    }

    public IReadOnlyCollection<NotificationFeedItem> GetLatest(int take = 50)
    {
        return _items
            .ToArray()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();
    }
}
