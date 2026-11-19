using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Contracts;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;
using PropertyShowingBooking.Infrastructure.Persistence;

namespace PropertyShowingBooking.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BookingRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        => _dbContext.Bookings.AddAsync(booking, cancellationToken).AsTask();

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Booking?> FindActiveBySlotAndEmailAsync(
        Guid slotId,
        string normalizedRenterEmail,
        CancellationToken cancellationToken = default)
    {
        var email = Booking.NormalizeEmail(normalizedRenterEmail);

        return _dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SlotId == slotId
                    && x.RenterEmail == email
                    && x.Status == BookingStatus.Confirmed,
                cancellationToken);
    }

    public async Task<BookingWithSlot?> GetByIdWithSlotAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await (
            from booking in _dbContext.Bookings.AsNoTracking()
            join slot in _dbContext.ShowingSlots.AsNoTracking()
                on booking.SlotId equals slot.Id
            where booking.Id == id
            select new { Booking = booking, Slot = slot })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new BookingWithSlot(result.Booking, result.Slot);
    }

    public async Task<int> CreateAtomicallyAsync(
        Booking booking,
        BookingHistory history,
        CancellationToken cancellationToken = default)
    {
        const int maximumAttempts = 3;

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            await using var transaction = await BeginTransactionWithRetryAsync(cancellationToken);

            try
            {
                var slot = await _dbContext.ShowingSlots
                    .FirstOrDefaultAsync(x => x.Id == booking.SlotId, cancellationToken);

                if (slot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new KeyNotFoundException("Showing slot was not found.");
                }

                if (slot.StartsAt <= booking.CreatedAt)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException("The showing slot has already started.");
                }

                var affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE ShowingSlots
                    SET BookedSeats = BookedSeats + {booking.PartySize}
                    WHERE Id = {booking.SlotId}
                      AND BookedSeats + {booking.PartySize} <= Capacity
                    """,
                    cancellationToken);

                if (affectedRows == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new SlotCapacityUnavailableException();
                }

                await _dbContext.Bookings.AddAsync(booking, cancellationToken);
                await _dbContext.BookingHistories.AddAsync(history, cancellationToken);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (
                    exception.InnerException is SqliteException sqliteException
                    && sqliteException.SqliteErrorCode == 19
                    && sqliteException.Message.Contains("Bookings.SlotId", StringComparison.OrdinalIgnoreCase)
                    && sqliteException.Message.Contains("Bookings.RenterEmail", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    throw new DuplicateActiveBookingException();
                }

                await _dbContext.Entry(slot).ReloadAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return slot.AvailableSeats;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                if (attempt == maximumAttempts)
                    throw new BookingDatabaseBusyException();

                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqliteException sqliteException
                && sqliteException.SqliteErrorCode is 5 or 6)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                if (attempt == maximumAttempts)
                    throw new BookingDatabaseBusyException();

                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
        }

        throw new BookingDatabaseBusyException();
            }

            public async Task<BookingTransitionResult> TransitionAtomicallyAsync(
                Guid bookingId,
                BookingStatus targetStatus,
                string actor,
                string? reason,
                DateTimeOffset changedAt,
                CancellationToken cancellationToken = default)
            {
                const int maximumAttempts = 3;

                for (var attempt = 1; attempt <= maximumAttempts; attempt++)
                {
                    await using var transaction = await BeginTransactionWithRetryAsync(cancellationToken);

                    try
                    {
                        var booking = await _dbContext.Bookings
                            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

                        if (booking is null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            throw new KeyNotFoundException("Booking was not found.");
                        }

                        var slot = await _dbContext.ShowingSlots
                            .FirstOrDefaultAsync(x => x.Id == booking.SlotId, cancellationToken);

                        if (slot is null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            throw new BookingTransitionConflictException("The associated showing slot was not found.");
                        }

                        if (booking.Status != BookingStatus.Confirmed)
                            throw new BookingTransitionConflictException("Only Confirmed bookings can be transitioned.");

                        if (targetStatus == BookingStatus.Confirmed)
                            throw new BookingTransitionConflictException("A Confirmed status transition is not allowed.");

                        if (targetStatus == BookingStatus.Cancelled && slot.StartsAt <= changedAt)
                            throw new BookingTransitionConflictException("Cancelled bookings must be transitioned before the slot starts.");

                        if (targetStatus is BookingStatus.Attended or BookingStatus.NoShow && slot.StartsAt > changedAt)
                            throw new BookingTransitionConflictException("Attended and NoShow transitions require the slot to have started.");

                        var affectedBookings = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"""
                            UPDATE Bookings
                            SET Status = {targetStatus.ToString()}, UpdatedAt = {changedAt}
                            WHERE Id = {bookingId} AND Status = 'Confirmed'
                            """,
                            cancellationToken);

                        if (affectedBookings != 1)
                            throw new BookingTransitionConflictException("The booking status changed before the transition could complete.");

                        if (targetStatus == BookingStatus.Cancelled)
                        {
                            var affectedSlots = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                                $"""
                                UPDATE ShowingSlots
                                SET BookedSeats = BookedSeats - {booking.PartySize}
                                WHERE Id = {slot.Id} AND BookedSeats >= {booking.PartySize}
                                """,
                                cancellationToken);

                            if (affectedSlots != 1)
                                throw new BookingTransitionConflictException("Booked seats could not be released.");

                            booking.Cancel(changedAt);
                            await _dbContext.Entry(slot).ReloadAsync(cancellationToken);
                        }
                        else if (targetStatus == BookingStatus.Attended)
                        {
                            booking.MarkAttended(changedAt);
                        }
                        else
                        {
                            booking.MarkNoShow(changedAt);
                        }

                        var history = BookingHistory.Create(
                            booking.Id,
                            fromStatus: BookingStatus.Confirmed,
                            toStatus: targetStatus,
                            actor: actor,
                            reason: reason,
                            changedAt: changedAt);

                        await _dbContext.BookingHistories.AddAsync(history, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        return new BookingTransitionResult(booking, slot);
                    }
                    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _dbContext.ChangeTracker.Clear();

                        if (attempt == maximumAttempts)
                            throw new BookingDatabaseBusyException();

                        await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
                    }
                    catch (DbUpdateException exception) when (
                        exception.InnerException is SqliteException sqliteException
                        && sqliteException.SqliteErrorCode is 5 or 6)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _dbContext.ChangeTracker.Clear();

                        if (attempt == maximumAttempts)
                            throw new BookingDatabaseBusyException();

                        await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
                    }
                }

                throw new BookingDatabaseBusyException();
            }

            private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionWithRetryAsync(
                CancellationToken cancellationToken)
            {
                const int maximumAttempts = 3;

                for (var attempt = 1; attempt <= maximumAttempts; attempt++)
                {
                    try
                    {
                        return await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                    }
                    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
                    {
                        _dbContext.ChangeTracker.Clear();

                        if (attempt == maximumAttempts)
                            throw new BookingDatabaseBusyException();

                        await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
                    }
                }

                throw new BookingDatabaseBusyException();
            }

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => _dbContext.SaveChangesAsync(cancellationToken);
}
