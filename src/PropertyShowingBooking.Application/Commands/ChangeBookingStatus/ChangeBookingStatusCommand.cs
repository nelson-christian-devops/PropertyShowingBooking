using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.Commands.ChangeBookingStatus;

public sealed record ChangeBookingStatusCommand(
    Guid BookingId,
    BookingStatus To,
    string? Reason,
    string Actor) : IRequest<ChangeBookingStatusResponse>;
