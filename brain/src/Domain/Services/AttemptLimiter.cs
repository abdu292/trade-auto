namespace Brain.Domain.Services;

public static class AttemptLimiter
{
    public static bool CanAttempt(int attempts, int maxAttempts) => attempts < maxAttempts;
}
