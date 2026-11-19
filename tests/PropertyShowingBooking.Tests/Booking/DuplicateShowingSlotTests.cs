using Moq;
using PropertyShowingBooking.Application.Commands.CreateShowingSlot;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;

namespace PropertyShowingBooking.Tests.Booking;

public sealed class DuplicateShowingSlotTests
{
    [Fact]
    public async Task Existing_exact_slot_returns_conflict()
    {
        var repository = new Mock<IShowingSlotRepository>();
        repository.Setup(x => x.ExistsAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateShowingSlotCommandHandler(repository.Object);

        await Assert.ThrowsAsync<DuplicateShowingSlotException>(() =>
            handler.Handle(new CreateShowingSlotCommand(
                Guid.NewGuid(),
                new DateTimeOffset(2026, 12, 20, 10, 0, 0, TimeSpan.Zero),
                60,
                10), CancellationToken.None));

        repository.Verify(x => x.AddAsync(
            It.IsAny<PropertyShowingBooking.Domain.Entities.ShowingSlot>(),
            It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
