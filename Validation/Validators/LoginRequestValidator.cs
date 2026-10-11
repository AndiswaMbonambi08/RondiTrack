// Checks shape only. No length rule on the password here: login must not reveal the password policy.
using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}