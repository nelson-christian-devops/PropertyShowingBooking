namespace PropertyShowingBooking.Application.DTOs;

public sealed record PaginatedResponseDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
