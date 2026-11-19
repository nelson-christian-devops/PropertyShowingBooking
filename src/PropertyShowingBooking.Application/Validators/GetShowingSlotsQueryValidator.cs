using FluentValidation;
using PropertyShowingBooking.Application.Queries.GetShowingSlots;

namespace PropertyShowingBooking.Application.Validators;

public sealed class GetShowingSlotsQueryValidator : AbstractValidator<GetShowingSlotsQuery>
{
    public GetShowingSlotsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("From must be less than or equal to To.");
    }
}
