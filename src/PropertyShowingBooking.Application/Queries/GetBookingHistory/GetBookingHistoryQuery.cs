using MediatR;
using PropertyShowingBooking.Application.DTOs;

namespace PropertyShowingBooking.Application.Queries.GetBookingHistory;

public sealed record GetBookingHistoryQuery(Guid BookingId)
    : IRequest<GetBookingHistoryResponse>;
