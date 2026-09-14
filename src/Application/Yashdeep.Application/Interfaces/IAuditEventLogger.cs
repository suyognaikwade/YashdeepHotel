using System.Collections.Generic;
using System.Threading.Tasks;
using Yashdeep.Domain.Events;

namespace Yashdeep.Application.Interfaces;

public interface IAuditEventLogger
{
    Task LogEventsAsync(IEnumerable<IDomainEvent> events);
    IReadOnlyList<IDomainEvent> GetLoggedEvents();
}
