namespace Yashdeep.Domain.Common.Exceptions;

public class InvalidOwnershipException : DomainException
{
    public InvalidOwnershipException(string message) : base(message)
    {
    }
}
