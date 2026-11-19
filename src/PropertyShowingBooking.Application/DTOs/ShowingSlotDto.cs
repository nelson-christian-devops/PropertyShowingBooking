namespace PropertyShowingBooking.Application.DTOs;

public sealed record ShowingSlotDto(
    Guid Id,
    Guid PropertyId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    int Capacity,
    int BookedSeats,
    int AvailableSeats,
    DateTimeOffset CreatedAt);
