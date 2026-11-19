namespace PropertyShowingBooking.Application.DTOs;

public sealed record ChangeBookingStatusRequest(
    string To,
    string? Reason,
    string Actor);
