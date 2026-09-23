// RondiTrack's exception hierarchy. Every exception here names one way a request can
// legitimately fail once it's past validation, and carries the status code and title
// the centralized handler should use for it. Validation (malformed input) is handled
// separately by FluentValidation before code ever reaches this hierarchy.
using Microsoft.AspNetCore.Http;

namespace RondiTrack.Domain.Exceptions;

public abstract class RondiTrackException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }

    protected RondiTrackException(string message) : base(message) { }
}

// A request that's well-formed on its own but fails a check that has nothing to do
// with a specific resource or business rule (e.g. a missing required header).
public class RequestValidationException : RondiTrackException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string Title => "Bad Request";

    public RequestValidationException(string message) : base(message) { }
}

// The thing the caller asked for doesn't exist.
public class NotFoundException : RondiTrackException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Not Found";

    public NotFoundException(string message) : base(message) { }
}

// The request is valid and the resources involved exist, but the combination violates
// a rule about the current state of a resource (duplicate membership, duplicate
// contribution for a cycle already paid).
public class ConflictException : RondiTrackException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflict";

    public ConflictException(string message) : base(message) { }
}

// Specifically for a reused Idempotency-Key with a different payload. Classified
// separately from ConflictException: a duplicate contribution conflicts with the
// STATE of the stokvel, but an idempotency mismatch conflicts with the RETRY CONTRACT
// itself, the request contradicts an earlier promise made under the same key, not
// the resource's current state. That distinction is why this is 422, not 409.
public class IdempotencyConflictException : RondiTrackException
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "Unprocessable Entity";

    public IdempotencyConflictException(string message) : base(message) { }
}