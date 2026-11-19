using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Application.Contracts;

public sealed record BookingTransitionResult(Booking Booking, ShowingSlot Slot);
