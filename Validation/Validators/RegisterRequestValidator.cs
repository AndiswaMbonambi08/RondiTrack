// Checks shape only: does RegisterRequest look like valid input. Never touches a repository.
using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12)
            .WithMessage("Password must be at least 12 characters.");
    }
}