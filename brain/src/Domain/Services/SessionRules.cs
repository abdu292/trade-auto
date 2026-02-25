using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Services;

public static class SessionRules
{
    public static bool IsTradingAllowed(SessionType session, IEnumerable<SessionState> sessionStates)
    {
        return sessionStates.Any(x => x.Session == session && x.IsEnabled);
    }
}
