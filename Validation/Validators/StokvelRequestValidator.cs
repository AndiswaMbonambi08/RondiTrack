using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class StokvelRequestValidator : AbstractValidator<StokvelRequest>
{
    public StokvelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.ContributionAmount).GreaterThan(0);
    }
}