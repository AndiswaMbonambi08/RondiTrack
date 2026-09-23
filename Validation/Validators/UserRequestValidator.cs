// Checks shape only: does UserRequest look like valid input. Never touches a repository.
using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class UserRequestValidator : AbstractValidator<UserRequest>
{
    public UserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}