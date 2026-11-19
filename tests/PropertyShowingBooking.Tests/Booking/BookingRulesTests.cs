using Moq;
using PropertyShowingBooking.Application.Commands.CreateBooking;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Tests.Booking;

public sealed class BookingRulesTests
{
    private static readonly DateTimeOffset FixedCreatedAt =
        new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Booking_succeeds_when_sufficient_seats_are_available()
    {
        var slot = CreateFutureSlot(bookedSeats: 2, capacity: 10);
        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        bookingRepository.Setup(x => x.FindActiveBySlotAndEmailAsync(
                slot.Id, "renter@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Booking?)null);
        bookingRepository.Setup(x => x.CreateAtomicallyAsync(
                It.IsAny<Domain.Entities.Booking>(),
                It.IsAny<BookingHistory>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        var response = await handler.Handle(
            new CreateBookingCommand(slot.Id, "Renter@Example.com", 3),
            CancellationToken.None);

        Assert.Equal("renter@example.com", response.RenterEmail);
        Assert.Equal(3, response.PartySize);
        Assert.Equal(BookingStatus.Confirmed, response.Status);
        Assert.Equal(5, response.AvailableSeats);
    }

    [Fact]
    public async Task Booking_fails_when_insufficient_seats_remain()
    {
        var slot = CreateFutureSlot(bookedSeats: 8, capacity: 10);
        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        bookingRepository.Setup(x => x.FindActiveBySlotAndEmailAsync(
                slot.Id, "renter@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Booking?)null);
        bookingRepository.Setup(x => x.CreateAtomicallyAsync(
                It.IsAny<Domain.Entities.Booking>(),
                It.IsAny<BookingHistory>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SlotCapacityUnavailableException());

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        await Assert.ThrowsAsync<SlotCapacityUnavailableException>(() =>
            handler.Handle(new CreateBookingCommand(slot.Id, "renter@example.com", 3), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PartySize_below_one_is_rejected(int partySize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Domain.Entities.Booking.Create(Guid.NewGuid(), "renter@example.com", partySize, FixedCreatedAt));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    public void PartySize_above_six_is_rejected(int partySize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Domain.Entities.Booking.Create(Guid.NewGuid(), "renter@example.com", partySize, FixedCreatedAt));
    }

    [Fact]
    public async Task Booking_fails_for_nonexistent_slot()
    {
        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShowingSlot?)null);

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CreateBookingCommand(Guid.NewGuid(), "renter@example.com", 2), CancellationToken.None));
    }

    [Fact]
    public async Task Booking_fails_after_slot_starts()
    {
        var slot = new ShowingSlot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2000, 1, 2, 10, 0, 0, TimeSpan.Zero),
            60,
            10,
            0,
            new DateTimeOffset(2000, 1, 1, 10, 0, 0, TimeSpan.Zero));

        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateBookingCommand(slot.Id, "renter@example.com", 2), CancellationToken.None));
    }

    [Fact]
    public async Task Booking_fails_for_duplicate_active_booking()
    {
        var slot = CreateFutureSlot(bookedSeats: 1, capacity: 10);
        var existing = Domain.Entities.Booking.Create(slot.Id, "renter@example.com", 2, FixedCreatedAt);
        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        bookingRepository.Setup(x => x.FindActiveBySlotAndEmailAsync(
                slot.Id, "renter@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        await Assert.ThrowsAsync<DuplicateActiveBookingException>(() =>
            handler.Handle(new CreateBookingCommand(slot.Id, "renter@example.com", 2), CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_booking_permits_a_new_booking_by_the_same_renter()
    {
        var slot = CreateFutureSlot(bookedSeats: 2, capacity: 10);
        var cancelled = Domain.Entities.Booking.Create(slot.Id, "renter@example.com", 2, FixedCreatedAt);
        cancelled.Cancel(FixedCreatedAt.AddHours(1));

        var slotRepository = new Mock<IShowingSlotRepository>();
        var bookingRepository = new Mock<IBookingRepository>();

        slotRepository.Setup(x => x.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        bookingRepository.Setup(x => x.FindActiveBySlotAndEmailAsync(
                slot.Id, "renter@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Booking?)null);
        bookingRepository.Setup(x => x.CreateAtomicallyAsync(
                It.IsAny<Domain.Entities.Booking>(),
                It.IsAny<BookingHistory>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        var handler = new CreateBookingCommandHandler(slotRepository.Object, bookingRepository.Object);

        var response = await handler.Handle(
            new CreateBookingCommand(slot.Id, "renter@example.com", 2),
            CancellationToken.None);

        Assert.True(cancelled.IsFinal);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(BookingStatus.Confirmed, response.Status);
    }

    private static ShowingSlot CreateFutureSlot(int bookedSeats, int capacity)
    {
        return new ShowingSlot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2099, 1, 1, 10, 0, 0, TimeSpan.Zero),
            60,
            capacity,
            bookedSeats,
            FixedCreatedAt);
    }
}
