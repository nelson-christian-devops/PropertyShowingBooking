using PropertyShowingBooking.Application.Contracts;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Application.Repositories;

public interface IBookingRepository
{
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> FindActiveBySlotAndEmailAsync(
        Guid slotId,
        string normalizedRenterEmail,
        CancellationToken cancellationToken = default);

    Task<BookingWithSlot?> GetByIdWithSlotAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CreateAtomicallyAsync(
        Booking booking,
        BookingHistory history,
        CancellationToken cancellationToken = default);

    Task<BookingTransitionResult> TransitionAtomicallyAsync(
        Guid bookingId,
        BookingStatus targetStatus,
        string actor,
        string? reason,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
