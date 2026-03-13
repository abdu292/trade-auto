namespace Brain.Application.Common.Models;

/// <summary>
/// STATE_06B_PATH_PROJECTION — displayable directional map for operator visibility only.
/// Not for execution; feeds app UI (chart / where rates are heading).
/// </summary>
public sealed record PathProjectionContract(
    string PathBias,
    string? KeyMagnets,
    string? NextTestZone,
    string? InvalidationShelf,
    string? SessionTargetCorridor,
    string? ConfidenceBand,
    string? SummaryLine);
