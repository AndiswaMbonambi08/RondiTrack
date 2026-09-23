using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validation.Validators;

public class RecordContributionRequestValidator : AbstractValidator<RecordContributionRequest>
{
    public RecordContributionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.ContributionCycleId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}