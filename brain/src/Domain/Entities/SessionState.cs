using Brain.Domain.Common;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Entities;

public sealed class SessionState : BaseEntity<SessionStateId>
{
    private SessionState()
    {
    }

    public SessionType Session { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SessionState Create(SessionType session, bool isEnabled)
    {
        return new SessionState
        {
            Id = SessionStateId.New(),
            Session = session,
            IsEnabled = isEnabled,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
