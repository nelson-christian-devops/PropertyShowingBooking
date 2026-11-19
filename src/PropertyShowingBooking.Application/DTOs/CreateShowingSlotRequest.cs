namespace PropertyShowingBooking.Application.DTOs;

public sealed record CreateShowingSlotRequest(
    Guid PropertyId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    int Capacity);
