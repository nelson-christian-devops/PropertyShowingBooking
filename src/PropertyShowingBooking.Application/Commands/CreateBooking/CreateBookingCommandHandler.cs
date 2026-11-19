using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.Commands.CreateBooking;

public sealed class CreateBookingCommandHandler
    : IRequestHandler<CreateBookingCommand, CreateBookingResponse>
{
    private readonly IShowingSlotRepository _slotRepository;
    private readonly IBookingRepository _bookingRepository;

    public CreateBookingCommandHandler(
        IShowingSlotRepository slotRepository,
        IBookingRepository bookingRepository)
    {
        _slotRepository = slotRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<CreateBookingResponse> Handle(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        var slot = await _slotRepository.GetByIdAsync(request.SlotId, cancellationToken)
            ?? throw new KeyNotFoundException("Showing slot was not found.");

        var now = DateTimeOffset.UtcNow;

        if (slot.StartsAt <= now)
            throw new InvalidOperationException("The showing slot has already started.");

        var normalizedEmail = Booking.NormalizeEmail(request.RenterEmail);

        var existing = await _bookingRepository.FindActiveBySlotAndEmailAsync(
            slot.Id,
            normalizedEmail,
            cancellationToken);

        if (existing is not null)
            throw new DuplicateActiveBookingException();

        var booking = Booking.Create(slot.Id, normalizedEmail, request.PartySize, now);

        var history = BookingHistory.Create(
            booking.Id,
            fromStatus: null,
            toStatus: BookingStatus.Confirmed,
            actor: "renter",
            reason: "Booking created",
            changedAt: now);

        var availableSeats = await _bookingRepository.CreateAtomicallyAsync(
            booking,
            history,
            cancellationToken);

        return new CreateBookingResponse(
            booking.Id,
            booking.SlotId,
            booking.RenterEmail,
            booking.PartySize,
            booking.Status,
            availableSeats);
    }
}
