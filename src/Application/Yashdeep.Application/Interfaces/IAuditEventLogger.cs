using System.Collections.Generic;
using System.Threading.Tasks;
using Yashdeep.Domain.Events;
using Yashdeep.Shared.Primitives;

namespace Yashdeep.Application.Interfaces;

public interface IAuditEventLogger
{
    Task LogEventsAsync(IEnumerable<IDomainEvent> events);
    IReadOnlyList<IDomainEvent> GetLoggedEvents();
}
