//Custom exceptions for business-rule violations (duplicate member, inactive user, member not found). 
//Lets the domain signal what went wrong without knowing about HTTP.
namespace RondiTrack.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class DuplicateMemberException : DomainException
{
    public DuplicateMemberException(string message) : base(message) { }
}

public class InactiveUserException : DomainException
{
    public InactiveUserException(string message) : base(message) { }
}

public class MemberNotFoundException : DomainException
{
    public MemberNotFoundException(string message) : base(message) { }
}

public class DuplicateContributionException : DomainException
{
    public DuplicateContributionException(string message) : base(message) { }
}

public class ResourceNotFoundException : DomainException
{
    public ResourceNotFoundException(string message) : base(message) { }
}

public class IdempotencyMismatchException : DomainException
{
    public IdempotencyMismatchException(string message) : base(message) { }
}