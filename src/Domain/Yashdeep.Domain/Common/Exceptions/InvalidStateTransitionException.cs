namespace Yashdeep.Domain.Common.Exceptions;

public class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string message) : base(message)
    {
    }
}
