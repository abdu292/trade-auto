using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class HazardWindow : BaseEntity<Guid>
{
    private HazardWindow()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Category { get; private set; } = "MACRO";
    public bool IsBlocked { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }

    public static HazardWindow Create(string title, string category, DateTimeOffset startUtc, DateTimeOffset endUtc, bool isBlocked = true)
    {
        return new HazardWindow
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "MACRO" : category.Trim().ToUpperInvariant(),
            IsBlocked = isBlocked,
            IsActive = true,
            StartUtc = startUtc,
            EndUtc = endUtc,
        };
    }

    public void Disable() => IsActive = false;
}
