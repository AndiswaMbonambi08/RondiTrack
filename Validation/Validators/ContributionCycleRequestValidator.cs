using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class ContributionCycleRequestValidator : AbstractValidator<ContributionCycleRequest>
{
    public ContributionCycleRequestValidator()
    {
        RuleFor(x => x.StokvelId).NotEqual(Guid.Empty);
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.TargetAmount).GreaterThan(0);
    }
}