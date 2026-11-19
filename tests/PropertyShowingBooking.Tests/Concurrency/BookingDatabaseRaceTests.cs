using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;
using PropertyShowingBooking.Infrastructure.Persistence;
using PropertyShowingBooking.Infrastructure.Repositories;

namespace PropertyShowingBooking.Tests.Concurrency;

public sealed class BookingDatabaseRaceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"property-race-{Guid.NewGuid():N}.db");

    private DbContextOptions<ApplicationDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=false;Default Timeout=5")
            .Options;

    [Fact]
    public async Task Same_renter_two_concurrent_requests_create_only_one_active_booking()
    {
        var slotId = await SeedSlotAsync(capacity: 5, bookedSeats: 0);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
        {
            await using var context = new ApplicationDbContext(CreateOptions());
            var repository = new BookingRepository(context);
            var now = DateTimeOffset.UtcNow;
            var booking = PropertyShowingBooking.Domain.Entities.Booking.Create(slotId, "same@example.com", 1, now);
            var history = BookingHistory.Create(booking.Id, null, BookingStatus.Confirmed, "renter", "Booking created", now);

            try
            {
                await repository.CreateAtomicallyAsync(booking, history);
                return "success";
            }
            catch (DuplicateActiveBookingException)
            {
                return "duplicate";
            }
            catch (BookingDatabaseBusyException)
            {
                return "busy";
            }
        }));

        await using var verification = new ApplicationDbContext(CreateOptions());
        var activeCount = await verification.Bookings.CountAsync(x =>
            x.SlotId == slotId && x.Status == BookingStatus.Confirmed && x.RenterEmail == "same@example.com");
        var slot = await verification.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);

        Assert.Equal(1, activeCount);
        Assert.Equal(1, results.Count(x => x == "success"));
        Assert.Equal(1, slot.BookedSeats);
        Assert.True(slot.BookedSeats <= slot.Capacity);
    }

    [Fact]
    public async Task Two_concurrent_cancellation_requests_release_seats_once()
    {
        var (slotId, bookingId) = await SeedBookingAsync(partySize: 3, bookedSeats: 3);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var context = new ApplicationDbContext(CreateOptions());
            var repository = new BookingRepository(context);

            try
            {
                await repository.TransitionAtomicallyAsync(
                    bookingId,
                    BookingStatus.Cancelled,
                    "renter",
                    "Unable to attend",
                    DateTimeOffset.UtcNow.AddDays(-1));
                return "success";
            }
            catch (BookingTransitionConflictException)
            {
                return "conflict";
            }
            catch (BookingDatabaseBusyException)
            {
                return "busy";
            }
        }));

        await using var verification = new ApplicationDbContext(CreateOptions());
        var slot = await verification.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);
        var booking = await verification.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId);
        var historyCount = await verification.BookingHistories.CountAsync(x => x.BookingId == bookingId);

        Assert.Equal(0, slot.BookedSeats);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(1, historyCount);
        Assert.Equal(1, results.Count(x => x == "success"));
    }

    [Fact]
    public async Task Cancellation_and_new_booking_remain_within_capacity()
    {
        var (slotId, bookingId) = await SeedBookingAsync(partySize: 2, bookedSeats: 2);

        await using var cancellationContext = new ApplicationDbContext(CreateOptions());
        var cancellationRepository = new BookingRepository(cancellationContext);

        await using var bookingContext = new ApplicationDbContext(CreateOptions());
        var bookingRepository = new BookingRepository(bookingContext);

        var cancellationTask = cancellationRepository.TransitionAtomicallyAsync(
            bookingId,
            BookingStatus.Cancelled,
            "renter",
            "Cancelled",
            DateTimeOffset.UtcNow.AddDays(-1));

        await Task.Delay(20);

        var now = DateTimeOffset.UtcNow;
        var newBooking = PropertyShowingBooking.Domain.Entities.Booking.Create(slotId, "new@example.com", 2, now);
        var history = BookingHistory.Create(newBooking.Id, null, BookingStatus.Confirmed, "renter", "Booking created", now);
        var bookingTask = bookingRepository.CreateAtomicallyAsync(newBooking, history);

        await Task.WhenAll(cancellationTask, bookingTask);

        await using var verification = new ApplicationDbContext(CreateOptions());
        var slot = await verification.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);

        Assert.Equal(2, slot.BookedSeats);
        Assert.Equal(slot.Capacity - 2, slot.AvailableSeats);
        Assert.True(slot.BookedSeats <= slot.Capacity);
        Assert.True(slot.BookedSeats >= 0);
    }

    [Fact]
    public async Task Conflicting_transitions_commit_only_one_state()
    {
        var (slotId, bookingId) = await SeedBookingAsync(partySize: 1, bookedSeats: 1);

        var tasks = new[]
        {
            TransitionAsync(bookingId, BookingStatus.Cancelled),
            TransitionAsync(bookingId, BookingStatus.Attended)
        };

        var results = await Task.WhenAll(tasks);

        await using var verification = new ApplicationDbContext(CreateOptions());
        var booking = await verification.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId);
        var historyCount = await verification.BookingHistories.CountAsync(x => x.BookingId == bookingId);

        Assert.True(results.Count(x => x == "success") == 1);
        Assert.Equal(1, historyCount);
        Assert.True(booking.Status is BookingStatus.Cancelled or BookingStatus.Attended);
    }

    private async Task<string> TransitionAsync(Guid bookingId, BookingStatus status)
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        var repository = new BookingRepository(context);

        try
        {
            await repository.TransitionAtomicallyAsync(
                bookingId,
                status,
                status == BookingStatus.Cancelled ? "renter" : "owner",
                status.ToString(),
                status == BookingStatus.Cancelled
                    ? DateTimeOffset.UtcNow.AddDays(-1)
                    : DateTimeOffset.UtcNow.AddDays(3));
            return "success";
        }
        catch (BookingTransitionConflictException)
        {
            return "conflict";
        }
        catch (BookingDatabaseBusyException)
        {
            return "busy";
        }
    }

    private async Task<Guid> SeedSlotAsync(int capacity, int bookedSeats)
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await context.Database.EnsureCreatedAsync();

        var slot = new ShowingSlot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(2),
            60,
            capacity,
            bookedSeats,
            DateTimeOffset.UtcNow);

        context.ShowingSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    private async Task<(Guid SlotId, Guid BookingId)> SeedBookingAsync(int partySize, int bookedSeats)
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await context.Database.EnsureCreatedAsync();

        var slot = new ShowingSlot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(2),
            60,
            10,
            bookedSeats,
            DateTimeOffset.UtcNow);

        var booking = new PropertyShowingBooking.Domain.Entities.Booking(
            Guid.NewGuid(),
            slot.Id,
            "race@example.com",
            partySize,
            DateTimeOffset.UtcNow);

        context.ShowingSlots.Add(slot);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return (slot.Id, booking.Id);
    }

    public void Dispose()
    {
        foreach (var path in new[] { _databasePath, _databasePath + "-shm", _databasePath + "-wal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
