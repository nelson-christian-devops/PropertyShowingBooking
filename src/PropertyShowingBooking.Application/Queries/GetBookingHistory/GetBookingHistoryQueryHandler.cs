using MediatR;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Repositories;

namespace PropertyShowingBooking.Application.Queries.GetBookingHistory;

public sealed class GetBookingHistoryQueryHandler
    : IRequestHandler<GetBookingHistoryQuery, GetBookingHistoryResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingHistoryRepository _historyRepository;

    public GetBookingHistoryQueryHandler(
        IBookingRepository bookingRepository,
        IBookingHistoryRepository historyRepository)
    {
        _bookingRepository = bookingRepository;
        _historyRepository = historyRepository;
    }

    public async Task<GetBookingHistoryResponse> Handle(
        GetBookingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            request.BookingId,
            cancellationToken);

        if (booking is null)
            throw new KeyNotFoundException("Booking was not found.");

        var entries = await _historyRepository.GetByBookingIdAsync(
            request.BookingId,
            cancellationToken);

        var history = entries
            .Select(entry => new BookingHistoryDto(
                entry.FromStatus,
                entry.ToStatus,
                entry.Actor,
                entry.Reason,
                entry.ChangedAt.ToUniversalTime()))
            .ToList();

        return new GetBookingHistoryResponse(request.BookingId, history);
    }
}
