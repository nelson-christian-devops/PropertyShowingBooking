using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Exceptions;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Application.Commands.CreateShowingSlot;

public sealed class CreateShowingSlotCommandHandler
    : IRequestHandler<CreateShowingSlotCommand, CreateShowingSlotResponse>
{
    private readonly IShowingSlotRepository _repository;

    public CreateShowingSlotCommandHandler(IShowingSlotRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateShowingSlotResponse> Handle(
        CreateShowingSlotCommand request,
        CancellationToken cancellationToken)
    {
        var utcStartsAt = request.StartsAt.ToUniversalTime();

        if (await _repository.ExistsAsync(
                request.PropertyId,
                utcStartsAt,
                request.DurationMinutes,
                cancellationToken))
        {
            throw new DuplicateShowingSlotException();
        }

        var slot = ShowingSlot.Create(
            request.PropertyId,
            utcStartsAt,
            request.DurationMinutes,
            request.Capacity,
            DateTimeOffset.UtcNow);

        await _repository.AddAsync(slot, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new CreateShowingSlotResponse(
            slot.Id,
            slot.PropertyId,
            slot.StartsAt,
            slot.DurationMinutes,
            slot.Capacity,
            slot.BookedSeats,
            slot.AvailableSeats);
    }
}
