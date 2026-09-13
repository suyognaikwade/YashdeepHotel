namespace Yashdeep.Domain.Outbox;

public enum OutboxStatus
{
    Pending = 0,
    InFlight = 1,
    Synced = 2,
    Failed = 3,
    DeadLetter = 4
}
