using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.Commands.ChangeBookingStatus;

public sealed class ChangeBookingStatusCommandHandler
    : IRequestHandler<ChangeBookingStatusCommand, ChangeBookingStatusResponse>
{
    private readonly IBookingRepository _bookingRepository;

    public ChangeBookingStatusCommandHandler(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<ChangeBookingStatusResponse> Handle(
        ChangeBookingStatusCommand request,
        CancellationToken cancellationToken)
    {
        var bookingWithSlot = await _bookingRepository.GetByIdWithSlotAsync(
            request.BookingId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Booking was not found.");

        var booking = bookingWithSlot.Booking;
        var slot = bookingWithSlot.Slot;

        if (booking.Status != BookingStatus.Confirmed)
            throw new BookingTransitionConflictException("Only Confirmed bookings can be transitioned.");

        var actor = request.Actor.Trim();
        var normalizedActor = actor.ToLowerInvariant();
        var isOwner = normalizedActor == "owner";
        var isRenter = normalizedActor == "renter"
            || normalizedActor == booking.RenterEmail.ToLowerInvariant();

        if (!isOwner && !isRenter)
            throw new BookingTransitionConflictException("Actor must be the owner or renter.");

        var now = DateTimeOffset.UtcNow;

        if (request.To == BookingStatus.Cancelled)
        {
            if (!isOwner && !isRenter)
                throw new BookingTransitionConflictException("Only the owner or renter can cancel a booking.");

            if (slot.StartsAt <= now)
                throw new BookingTransitionConflictException("A booking can only be cancelled before the slot starts.");
        }
        else
        {
            if (!isOwner)
                throw new BookingTransitionConflictException("Only the owner can mark a booking as Attended or NoShow.");

            if (slot.StartsAt > now)
                throw new BookingTransitionConflictException("Attended and NoShow transitions require the slot to have started.");
        }

        var result = await _bookingRepository.TransitionAtomicallyAsync(
            request.BookingId,
            request.To,
            actor,
            request.Reason,
            now,
            cancellationToken);

        return new ChangeBookingStatusResponse(
            result.Booking.Id,
            result.Booking.SlotId,
            result.Booking.Status,
            result.Slot.AvailableSeats);
    }
}
