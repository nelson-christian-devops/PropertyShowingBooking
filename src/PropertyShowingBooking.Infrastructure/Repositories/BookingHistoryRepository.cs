using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Infrastructure.Persistence;

namespace PropertyShowingBooking.Infrastructure.Repositories;

public sealed class BookingHistoryRepository : IBookingHistoryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BookingHistoryRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(BookingHistory history, CancellationToken cancellationToken = default)
        => _dbContext.BookingHistories.AddAsync(history, cancellationToken).AsTask();

    public async Task<IReadOnlyList<BookingHistory>> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BookingHistories
            .AsNoTracking()
            .Where(x => x.BookingId == bookingId)
            .OrderBy(x => x.ChangedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
