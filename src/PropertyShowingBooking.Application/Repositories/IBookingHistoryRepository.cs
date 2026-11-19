using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Application.Repositories;

public interface IBookingHistoryRepository
{
    Task AddAsync(BookingHistory history, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingHistory>> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
