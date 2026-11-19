namespace PropertyShowingBooking.Application.DTOs;

public sealed record GetBookingHistoryResponse(
    Guid BookingId,
    IReadOnlyList<BookingHistoryDto> History);
