using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.DTOs;

public sealed record CreateBookingResponse(
    Guid BookingId,
    Guid SlotId,
    string RenterEmail,
    int PartySize,
    BookingStatus Status,
    int AvailableSeats);
