using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class RecordContributionRequestValidator : AbstractValidator<RecordContributionRequest>
{
    // R1 000 000. A guard against absurd values, well inside what the amount column can hold.
    public const long MaxAmountInCents = 100_000_000;

    public RecordContributionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.ContributionCycleId).NotEqual(Guid.Empty);
        RuleFor(x => x.AmountInCents)
            .GreaterThan(0).WithMessage("The amount must be greater than zero.")
            .LessThanOrEqualTo(MaxAmountInCents).WithMessage("The amount can be at most R1 000 000 (100 000 000 cents).");
    }
}
