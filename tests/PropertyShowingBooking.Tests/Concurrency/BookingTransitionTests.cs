using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;
using PropertyShowingBooking.Infrastructure.Persistence;
using PropertyShowingBooking.Infrastructure.Repositories;

namespace PropertyShowingBooking.Tests.Concurrency;

public sealed class BookingTransitionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"property-transition-{Guid.NewGuid():N}.db");

    private DbContextOptions<ApplicationDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=false;Default Timeout=5")
            .Options;

    [Fact]
    public async Task Cancellation_releases_seats_once_and_second_transition_is_rejected()
    {
        var slotId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        await using (var context = new ApplicationDbContext(CreateOptions()))
        {
            await context.Database.EnsureCreatedAsync();

            var slot = new ShowingSlot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(1),
                60,
                5,
                3,
                DateTimeOffset.UtcNow);

            var booking = new PropertyShowingBooking.Domain.Entities.Booking(
                bookingId,
                slot.Id,
                "renter@example.com",
                3,
                DateTimeOffset.UtcNow);

            slotId = slot.Id;
            context.ShowingSlots.Add(slot);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(CreateOptions()))
        {
            var repository = new BookingRepository(context);
            await repository.TransitionAtomicallyAsync(
                bookingId,
                BookingStatus.Cancelled,
                "renter@example.com",
                "Unable to attend",
                DateTimeOffset.UtcNow);

            await Assert.ThrowsAsync<BookingTransitionConflictException>(() =>
                repository.TransitionAtomicallyAsync(
                    bookingId,
                    BookingStatus.Cancelled,
                    "renter@example.com",
                    "Duplicate",
                    DateTimeOffset.UtcNow));
        }

        await using var verification = new ApplicationDbContext(CreateOptions());
        var finalSlot = await verification.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);
        var finalBooking = await verification.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId);
        var historyCount = await verification.BookingHistories.CountAsync(x => x.BookingId == bookingId);

        Assert.Equal(0, finalSlot.BookedSeats);
        Assert.Equal(BookingStatus.Cancelled, finalBooking.Status);
        Assert.Equal(1, historyCount);
    }

    [Fact]
    public async Task Attended_transition_before_start_is_rejected_without_changes()
    {
        var slotId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        await using (var context = new ApplicationDbContext(CreateOptions()))
        {
            await context.Database.EnsureCreatedAsync();

            var slot = new ShowingSlot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(1),
                60,
                5,
                2,
                DateTimeOffset.UtcNow);

            var booking = new PropertyShowingBooking.Domain.Entities.Booking(
                bookingId,
                slot.Id,
                "renter@example.com",
                2,
                DateTimeOffset.UtcNow);

            slotId = slot.Id;
            context.ShowingSlots.Add(slot);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
        }

        await using var attemptContext = new ApplicationDbContext(CreateOptions());
        var repository = new BookingRepository(attemptContext);

        await Assert.ThrowsAsync<BookingTransitionConflictException>(() =>
            repository.TransitionAtomicallyAsync(
                bookingId,
                BookingStatus.Attended,
                "owner",
                null,
                DateTimeOffset.UtcNow));

        var finalSlot = await attemptContext.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);
        var finalBooking = await attemptContext.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId);

        Assert.Equal(2, finalSlot.BookedSeats);
        Assert.Equal(BookingStatus.Confirmed, finalBooking.Status);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
