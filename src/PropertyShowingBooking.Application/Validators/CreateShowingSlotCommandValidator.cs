using FluentValidation;
using PropertyShowingBooking.Application.Commands.CreateShowingSlot;

namespace PropertyShowingBooking.Application.Validators;

public sealed class CreateShowingSlotCommandValidator : AbstractValidator<CreateShowingSlotCommand>
{
    public CreateShowingSlotCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.StartsAt)
            .GreaterThan(_ => DateTimeOffset.UtcNow)
            .WithMessage("StartsAt must be in the future.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(15, 120);

        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, 20);
    }
}
