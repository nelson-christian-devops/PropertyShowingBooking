using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;
using PropertyShowingBooking.Infrastructure.Persistence;
using PropertyShowingBooking.Infrastructure.Repositories;

namespace PropertyShowingBooking.Tests.Concurrency;

public sealed class BookingConcurrencyTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"property-showing-{Guid.NewGuid():N}.db");

    private DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=false;Default Timeout=5")
            .Options;
    }

    [Fact]
    public async Task Concurrent_reservations_never_exceed_slot_capacity()
    {
        var slotId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(CreateOptions()))
        {
            await context.Database.EnsureCreatedAsync();

            var slot = ShowingSlot.Create(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(1),
                60,
                1,
                DateTimeOffset.UtcNow);

            slotId = slot.Id;
            context.ShowingSlots.Add(slot);
            await context.SaveChangesAsync();
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            await using var context = new ApplicationDbContext(CreateOptions());
            var repository = new BookingRepository(context);
            var now = DateTimeOffset.UtcNow;
            var booking = PropertyShowingBooking.Domain.Entities.Booking.Create(slotId, $"renter{index}@example.com", 1, now);
            var history = BookingHistory.Create(
                booking.Id,
                null,
                BookingStatus.Confirmed,
                booking.RenterEmail,
                "Booking created",
                now);

            try
            {
                await repository.CreateAtomicallyAsync(booking, history);
                return "success";
            }
            catch (SlotCapacityUnavailableException)
            {
                return "capacity";
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

        await using var verificationContext = new ApplicationDbContext(CreateOptions());
        var finalSlot = await verificationContext.ShowingSlots
            .AsNoTracking()
            .SingleAsync(x => x.Id == slotId);

        Assert.Equal(1, finalSlot.BookedSeats);
        Assert.Equal(1, results.Count(x => x == "success"));
    }

    [Fact]
    public async Task Duplicate_active_booking_does_not_leave_reserved_seats()
    {
        var slotId = Guid.NewGuid();

        await using (var context = new ApplicationDbContext(CreateOptions()))
        {
            await context.Database.EnsureCreatedAsync();

            var seedSlot = ShowingSlot.Create(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(1),
                60,
                2,
                DateTimeOffset.UtcNow);

            slotId = seedSlot.Id;

            var seedNow = DateTimeOffset.UtcNow;
            var existing = PropertyShowingBooking.Domain.Entities.Booking.Create(slotId, "duplicate@example.com", 1, seedNow);

            context.ShowingSlots.Add(seedSlot);
            context.Bookings.Add(existing);
            await context.SaveChangesAsync();
        }

        await using var attemptContext = new ApplicationDbContext(CreateOptions());
        var repository = new BookingRepository(attemptContext);
        var attemptNow = DateTimeOffset.UtcNow;
        var booking = PropertyShowingBooking.Domain.Entities.Booking.Create(slotId, "duplicate@example.com", 1, attemptNow);
        var history = BookingHistory.Create(
            booking.Id,
            null,
            BookingStatus.Confirmed,
            booking.RenterEmail,
            "Booking created",
            attemptNow);

        await Assert.ThrowsAsync<DuplicateActiveBookingException>(() =>
            repository.CreateAtomicallyAsync(booking, history));

        attemptContext.ChangeTracker.Clear();
        var finalSlot = await attemptContext.ShowingSlots
            .AsNoTracking()
            .SingleAsync(x => x.Id == slotId);

        Assert.Equal(0, finalSlot.BookedSeats);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
