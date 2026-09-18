namespace Yashdeep.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
