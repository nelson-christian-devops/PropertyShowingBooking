using MediatR;
using PropertyShowingBooking.Application.DTOs;

namespace PropertyShowingBooking.Application.Commands.CreateBooking;

public sealed record CreateBookingCommand(
    Guid SlotId,
    string RenterEmail,
    int PartySize) : IRequest<CreateBookingResponse>;
