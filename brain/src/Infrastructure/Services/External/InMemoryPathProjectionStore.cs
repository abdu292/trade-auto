using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryPathProjectionStore : IPathProjectionStore
{
    private PathProjectionContract? _projection;
    private DateTimeOffset? _atUtc;
    private readonly object _gate = new();

    public void Set(PathProjectionContract projection, DateTimeOffset atUtc)
    {
        lock (_gate)
        {
            _projection = projection;
            _atUtc = atUtc;
        }
    }

    public (PathProjectionContract? Projection, DateTimeOffset? AtUtc) Get()
    {
        lock (_gate)
        {
            return (_projection, _atUtc);
        }
    }
}
