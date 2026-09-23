// A minimal-API endpoint filter that runs FluentValidation on a request DTO before the
// endpoint's own code runs. If validation fails, it throws RequestValidationException,
// which the centralized handler turns into a 400 problem+json response.
using FluentValidation;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Validation;

public class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is not null)
        {
            var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
            if (validator is not null)
            {
                var result = await validator.ValidateAsync(request);
                if (!result.IsValid)
                {
                    var message = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));
                    throw new RequestValidationException(message);
                }
            }
        }

        return await next(context);
    }
}