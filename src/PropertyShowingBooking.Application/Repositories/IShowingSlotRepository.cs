using PropertyShowingBooking.Domain.Entities;

namespace PropertyShowingBooking.Application.Repositories;

public interface IShowingSlotRepository
{
    Task AddAsync(ShowingSlot slot, CancellationToken cancellationToken = default);

    Task<ShowingSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid propertyId,
        DateTimeOffset startsAt,
        int durationMinutes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShowingSlot>> SearchAsync(
        Guid? propertyId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid? propertyId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
