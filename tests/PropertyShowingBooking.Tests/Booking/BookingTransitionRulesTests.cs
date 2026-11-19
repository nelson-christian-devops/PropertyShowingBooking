using Moq;
using PropertyShowingBooking.Application.Commands.ChangeBookingStatus;
using PropertyShowingBooking.Application.Contracts;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Tests.Booking;

public sealed class BookingTransitionRulesTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirmed_can_be_cancelled_by_renter_before_slot_starts()
    {
        var result = await CancelAsync("renter", startFuture: true);

        Assert.Equal(BookingStatus.Cancelled, result.Booking.Status);
    }

    [Fact]
    public async Task Confirmed_can_be_cancelled_by_owner_before_slot_starts()
    {
        var result = await CancelAsync("owner", startFuture: true);

        Assert.Equal(BookingStatus.Cancelled, result.Booking.Status);
    }

    [Fact]
    public async Task Confirmed_cannot_be_cancelled_after_slot_starts()
    {
        var booking = CreateBooking();
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T11:00:00Z"), bookedSeats: 3);
        var repository = CreateRepository(booking, slot);

        var handler = new ChangeBookingStatusCommandHandler(repository.Object);

        await Assert.ThrowsAsync<BookingTransitionConflictException>(() =>
            handler.Handle(new ChangeBookingStatusCommand(
                booking.Id, BookingStatus.Cancelled, "Unable to attend", "renter"), CancellationToken.None));
    }

    [Fact]
    public async Task Confirmed_can_be_marked_attended_by_owner_after_slot_starts()
    {
        var result = await OwnerPostStartTransitionAsync(BookingStatus.Attended);

        Assert.Equal(BookingStatus.Attended, result.Booking.Status);
    }

    [Fact]
    public async Task Confirmed_can_be_marked_no_show_by_owner_after_slot_starts()
    {
        var result = await OwnerPostStartTransitionAsync(BookingStatus.NoShow);

        Assert.Equal(BookingStatus.NoShow, result.Booking.Status);
    }

    [Fact]
    public async Task Renter_cannot_mark_booking_attended()
    {
        var booking = CreateBooking();
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T11:00:00Z"), bookedSeats: 3);
        var repository = CreateRepository(booking, slot);

        var handler = new ChangeBookingStatusCommandHandler(repository.Object);

        await Assert.ThrowsAsync<BookingTransitionConflictException>(() =>
            handler.Handle(new ChangeBookingStatusCommand(
                booking.Id, BookingStatus.Attended, null, "renter"), CancellationToken.None));
    }

    [Fact]
    public async Task Renter_cannot_mark_booking_no_show()
    {
        var booking = CreateBooking();
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T11:00:00Z"), bookedSeats: 3);
        var repository = CreateRepository(booking, slot);

        var handler = new ChangeBookingStatusCommandHandler(repository.Object);

        await Assert.ThrowsAsync<BookingTransitionConflictException>(() =>
            handler.Handle(new ChangeBookingStatusCommand(
                booking.Id, BookingStatus.NoShow, null, "renter"), CancellationToken.None));
    }

    [Fact]
    public void Attended_is_final()
    {
        var booking = CreateBooking();
        booking.MarkAttended(FixedTime);

        Assert.True(booking.IsFinal);
        Assert.False(booking.TryCancel(FixedTime.AddHours(1)));
    }

    [Fact]
    public void NoShow_is_final()
    {
        var booking = CreateBooking();
        booking.MarkNoShow(FixedTime);

        Assert.True(booking.IsFinal);
        Assert.False(booking.TryAttend(FixedTime.AddHours(1)));
    }

    [Fact]
    public void Cancelled_is_final()
    {
        var booking = CreateBooking();
        booking.Cancel(FixedTime);

        Assert.True(booking.IsFinal);
        Assert.False(booking.TryMarkNoShow(FixedTime.AddHours(1)));
    }

    [Fact]
    public void Cancellation_releases_correct_seats()
    {
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T13:00:00Z"), bookedSeats: 5);
        var booking = CreateBooking(partySize: 3);

        slot.ReleaseSeats(booking.PartySize);

        Assert.Equal(2, slot.BookedSeats);
        Assert.Equal(8, slot.AvailableSeats);
    }

    [Fact]
    public void Repeated_cancellation_cannot_release_seats_twice()
    {
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T13:00:00Z"), bookedSeats: 5);
        var booking = CreateBooking(partySize: 3);

        booking.Cancel(FixedTime);
        slot.ReleaseSeats(booking.PartySize);

        Assert.False(booking.TryCancel(FixedTime.AddMinutes(1)));
        Assert.Equal(2, slot.BookedSeats);
    }

    private async Task<BookingTransitionResult> CancelAsync(string actor, bool startFuture)
    {
        var booking = CreateBooking();
        var slot = CreateSlot(
            startFuture
                ? DateTimeOffset.Parse("2099-06-01T13:00:00Z")
                : DateTimeOffset.Parse("2026-06-01T11:00:00Z"),
            bookedSeats: 3);

        var repository = CreateRepository(booking, slot, targetStatus: BookingStatus.Cancelled);
        var handler = new ChangeBookingStatusCommandHandler(repository.Object);

        var response = await handler.Handle(new ChangeBookingStatusCommand(
            booking.Id,
            BookingStatus.Cancelled,
            "Unable to attend",
            actor), CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, response.Status);

        return new BookingTransitionResult(booking, slot);
    }

    private async Task<BookingTransitionResult> OwnerPostStartTransitionAsync(BookingStatus targetStatus)
    {
        var booking = CreateBooking();
        var slot = CreateSlot(DateTimeOffset.Parse("2026-06-01T11:00:00Z"), bookedSeats: 3);
        var repository = CreateRepository(booking, slot, targetStatus);
        var handler = new ChangeBookingStatusCommandHandler(repository.Object);

        var response = await handler.Handle(new ChangeBookingStatusCommand(
            booking.Id,
            targetStatus,
            null,
            "owner"), CancellationToken.None);

        Assert.Equal(targetStatus, response.Status);

        return new BookingTransitionResult(booking, slot);
    }

    private static Mock<IBookingRepository> CreateRepository(
        Domain.Entities.Booking booking,
        ShowingSlot slot,
        BookingStatus? targetStatus = null)
    {
        var mock = new Mock<IBookingRepository>();
        mock.Setup(x => x.GetByIdWithSlotAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingWithSlot(booking, slot));

        if (targetStatus.HasValue)
        {
            mock.Setup(x => x.TransitionAtomicallyAsync(
                    booking.Id,
                    targetStatus.Value,
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    if (targetStatus.Value == BookingStatus.Cancelled)
                        booking.Cancel(DateTimeOffset.UtcNow);
                    else if (targetStatus.Value == BookingStatus.Attended)
                        booking.MarkAttended(DateTimeOffset.UtcNow);
                    else
                        booking.MarkNoShow(DateTimeOffset.UtcNow);

                    if (targetStatus.Value == BookingStatus.Cancelled)
                        slot.ReleaseSeats(booking.PartySize);

                    return new BookingTransitionResult(booking, slot);
                });
        }

        return mock;
    }

    private static Domain.Entities.Booking CreateBooking(int partySize = 3)
    {
        return new Domain.Entities.Booking(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "renter@example.com",
            partySize,
            FixedTime);
    }

    private static ShowingSlot CreateSlot(DateTimeOffset startsAt, int bookedSeats)
    {
        return new ShowingSlot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startsAt,
            60,
            10,
            bookedSeats,
            FixedTime.AddDays(-1));
    }
}
