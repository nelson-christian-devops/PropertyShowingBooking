using FluentValidation;
using PropertyShowingBooking.Application.Commands.CreateBooking;

namespace PropertyShowingBooking.Application.Validators;

public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.SlotId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.RenterEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(x => x.PartySize)
            .InclusiveBetween(1, 6);
    }
}
