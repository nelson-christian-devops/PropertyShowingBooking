using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.DTOs;

public sealed record BookingHistoryDto(
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    string Actor,
    string? Reason,
    DateTimeOffset ChangedAt);
