using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Events;

namespace Yashdeep.Infrastructure.Persistence;

public class InMemoryAuditEventLogger : IAuditEventLogger
{
    private readonly ConcurrentBag<IDomainEvent> _loggedEvents = new();

    public Task LogEventsAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var ev in events)
        {
            _loggedEvents.Add(ev);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<IDomainEvent> GetLoggedEvents()
    {
        return _loggedEvents.ToList().AsReadOnly();
    }
}
