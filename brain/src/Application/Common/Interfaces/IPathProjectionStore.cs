using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Stores latest path projection (STATE_06B) for app UI — chart / where rates are heading. Not for execution.
/// </summary>
public interface IPathProjectionStore
{
    void Set(PathProjectionContract projection, DateTimeOffset atUtc);
    (PathProjectionContract? Projection, DateTimeOffset? AtUtc) Get();
}
