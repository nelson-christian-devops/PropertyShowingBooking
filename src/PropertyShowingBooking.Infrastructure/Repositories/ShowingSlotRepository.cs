using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Infrastructure.Persistence;

namespace PropertyShowingBooking.Infrastructure.Repositories;

public sealed class ShowingSlotRepository : IShowingSlotRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ShowingSlotRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ShowingSlot slot, CancellationToken cancellationToken = default)
        => _dbContext.ShowingSlots.AddAsync(slot, cancellationToken).AsTask();

    public Task<ShowingSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.ShowingSlots
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid propertyId,
        DateTimeOffset startsAt,
        int durationMinutes,
        CancellationToken cancellationToken = default)
    {
        var utcStartsAt = startsAt.ToUniversalTime();

        return _dbContext.ShowingSlots
            .AsNoTracking()
            .AnyAsync(x => x.PropertyId == propertyId
                && x.StartsAt == utcStartsAt
                && x.DurationMinutes == durationMinutes,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ShowingSlot>> SearchAsync(
        Guid? propertyId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");

        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");

        var query = ApplyFilters(_dbContext.ShowingSlots.AsNoTracking(), propertyId, from, to);

        return await query
            .OrderBy(x => x.StartsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid? propertyId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(_dbContext.ShowingSlots.AsNoTracking(), propertyId, from, to);
        return await query.CountAsync(cancellationToken);
    }

    private static IQueryable<ShowingSlot> ApplyFilters(
        IQueryable<ShowingSlot> query,
        Guid? propertyId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (propertyId.HasValue)
            query = query.Where(x => x.PropertyId == propertyId.Value);

        if (from.HasValue)
            query = query.Where(x => x.StartsAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.StartsAt <= to.Value);

        return query;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteErrorCode == 19
            && sqliteException.Message.Contains("ShowingSlots.PropertyId", StringComparison.OrdinalIgnoreCase)
            && sqliteException.Message.Contains("ShowingSlots.StartsAt", StringComparison.OrdinalIgnoreCase)
            && sqliteException.Message.Contains("ShowingSlots.DurationMinutes", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateShowingSlotException();
        }
    }
}
