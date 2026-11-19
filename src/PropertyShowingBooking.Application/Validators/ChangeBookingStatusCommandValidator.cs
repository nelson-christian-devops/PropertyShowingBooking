using FluentValidation;
using PropertyShowingBooking.Application.Commands.ChangeBookingStatus;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.Validators;

public sealed class ChangeBookingStatusCommandValidator : AbstractValidator<ChangeBookingStatusCommand>
{
    public ChangeBookingStatusCommandValidator()
    {
        RuleFor(x => x.BookingId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Actor)
            .NotEmpty()
            .MaximumLength(254);

        RuleFor(x => x.To)
            .IsInEnum()
            .Must(status => status is BookingStatus.Cancelled or BookingStatus.Attended or BookingStatus.NoShow)
            .WithMessage("Only Cancelled, Attended, and NoShow transitions are allowed.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .When(x => x.Reason is not null);
    }
}
