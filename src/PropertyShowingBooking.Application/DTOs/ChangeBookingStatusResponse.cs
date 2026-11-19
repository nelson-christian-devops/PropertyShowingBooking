using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.DTOs;

public sealed record ChangeBookingStatusResponse(
    Guid BookingId,
    Guid SlotId,
    BookingStatus Status,
    int AvailableSeats);
