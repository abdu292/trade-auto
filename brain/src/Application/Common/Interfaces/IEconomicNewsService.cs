using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IEconomicNewsService
{
    Task<NewsRiskAssessmentContract> AssessAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
