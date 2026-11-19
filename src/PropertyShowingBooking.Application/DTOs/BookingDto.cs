using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.DTOs;

public sealed record BookingDto(
    Guid Id,
    Guid SlotId,
    string RenterEmail,
    int PartySize,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
