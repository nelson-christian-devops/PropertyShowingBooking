using MediatR;
using PropertyShowingBooking.Application.DTOs;

namespace PropertyShowingBooking.Application.Queries.GetShowingSlots;

public sealed record GetShowingSlotsQuery(
    Guid? PropertyId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20)
    : IRequest<PaginatedResponseDto<ShowingSlotDto>>;
