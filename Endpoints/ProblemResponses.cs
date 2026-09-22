// One place that produces every error response, so every endpoint returns the same
// RFC 9457 problem+json shape regardless of which one fails.
namespace RondiTrack.Endpoints;

public static class ProblemResponses
{
    public static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail);

    public static IResult BadRequest(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: detail);

    public static IResult Conflict(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);

    public static IResult Unprocessable(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: detail);
}