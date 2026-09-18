namespace Yashdeep.Domain.Exceptions;

public class TenantAuthorizationException : Exception
{
    public TenantAuthorizationException(string message) : base(message) { }
}

public class BranchAuthorizationException : Exception
{
    public BranchAuthorizationException(string message) : base(message) { }
}

public class OutletAuthorizationException : Exception
{
    public OutletAuthorizationException(string message) : base(message) { }
}

public class DeviceAuthorizationException : Exception
{
    public DeviceAuthorizationException(string message) : base(message) { }
}
