using MediatR;
using PropertyShowingBooking.Application.DTOs;

namespace PropertyShowingBooking.Application.Commands.CreateShowingSlot;

public sealed record CreateShowingSlotCommand(
    Guid PropertyId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    int Capacity) : IRequest<CreateShowingSlotResponse>;
