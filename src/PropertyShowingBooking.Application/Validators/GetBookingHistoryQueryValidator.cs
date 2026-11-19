using FluentValidation;
using PropertyShowingBooking.Application.Queries.GetBookingHistory;

namespace PropertyShowingBooking.Application.Validators;

public sealed class GetBookingHistoryQueryValidator : AbstractValidator<GetBookingHistoryQuery>
{
    public GetBookingHistoryQueryValidator()
    {
        RuleFor(x => x.BookingId)
            .NotEqual(Guid.Empty);
    }
}
