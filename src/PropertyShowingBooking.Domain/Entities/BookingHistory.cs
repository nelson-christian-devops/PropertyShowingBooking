using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Domain.Entities;

public sealed class BookingHistory
{
    public Guid Id { get; private init; }
    public Guid BookingId { get; private init; }
    public BookingStatus? FromStatus { get; private init; }
    public BookingStatus ToStatus { get; private init; }
    public string Actor { get; private init; }
    public string? Reason { get; private init; }
    public DateTimeOffset ChangedAt { get; private init; }

    public BookingHistory(
        Guid id,
        Guid bookingId,
        BookingStatus? fromStatus,
        BookingStatus toStatus,
        string actor,
        string? reason,
        DateTimeOffset changedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("History id cannot be empty.", nameof(id));

        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking id cannot be empty.", nameof(bookingId));

        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("Actor cannot be empty.", nameof(actor));

        Id = id;
        BookingId = bookingId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Actor = actor.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ChangedAt = changedAt;
    }

    public static BookingHistory Create(
        Guid bookingId,
        BookingStatus? fromStatus,
        BookingStatus toStatus,
        string actor,
        string? reason = null,
        DateTimeOffset? changedAt = null)
    {
        return new BookingHistory(
            Guid.NewGuid(),
            bookingId,
            fromStatus,
            toStatus,
            actor,
            reason,
            changedAt ?? DateTimeOffset.UtcNow);
    }
}
