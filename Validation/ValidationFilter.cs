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
                    var errors = result.Errors
                        .GroupBy(e => CamelCase(e.PropertyName))
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                    throw new RequestValidationException(message, errors);
                }
            }
        }

        return await next(context);
    }

    // "ContributionAmount" -> "contributionAmount"; "Items[0].Name" -> "items[0].name"
    private static string CamelCase(string path) =>
        string.Join('.', path.Split('.').Select(s =>
            s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}