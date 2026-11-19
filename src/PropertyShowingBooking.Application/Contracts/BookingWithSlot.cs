using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Application.Contracts;

public sealed record BookingWithSlot(Booking Booking, ShowingSlot Slot);
