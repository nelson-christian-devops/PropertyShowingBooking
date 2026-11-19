using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Repositories;

namespace PropertyShowingBooking.Application.Queries.GetShowingSlots;

public sealed class GetShowingSlotsQueryHandler
    : IRequestHandler<GetShowingSlotsQuery, PaginatedResponseDto<ShowingSlotDto>>
{
    private readonly IShowingSlotRepository _repository;

    public GetShowingSlotsQueryHandler(IShowingSlotRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResponseDto<ShowingSlotDto>> Handle(
        GetShowingSlotsQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.From?.ToUniversalTime();
        var to = request.To?.ToUniversalTime();

        var totalCount = await _repository.CountAsync(
            request.PropertyId,
            from,
            to,
            cancellationToken);

        var slots = await _repository.SearchAsync(
            request.PropertyId,
            from,
            to,
            request.Page,
            request.PageSize,
            cancellationToken);

        var items = slots
            .Select(slot => new ShowingSlotDto(
                slot.Id,
                slot.PropertyId,
                slot.StartsAt.ToUniversalTime(),
                slot.DurationMinutes,
                slot.Capacity,
                slot.BookedSeats,
                slot.AvailableSeats,
                slot.CreatedAt.ToUniversalTime()))
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PaginatedResponseDto<ShowingSlotDto>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
