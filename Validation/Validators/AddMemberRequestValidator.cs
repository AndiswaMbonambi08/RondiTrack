using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}