namespace Brain.Application.Common.Models;

public sealed record NewsEventContract(
    string Title,
    string Currency,
    string Impact,
    DateTimeOffset EventTimeUtc);

public sealed record NewsRiskAssessmentContract(
    bool IsBlocked,
    string Reason,
    IReadOnlyCollection<NewsEventContract> NearbyEvents,
    DateTimeOffset RefreshedAtUtc,
    bool IsStale);
