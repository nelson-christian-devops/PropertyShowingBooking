namespace PropertyShowingBooking.Application.DTOs;

public sealed record CreateShowingSlotResponse(
    Guid Id,
    Guid PropertyId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    int Capacity,
    int BookedSeats,
    int AvailableSeats);
