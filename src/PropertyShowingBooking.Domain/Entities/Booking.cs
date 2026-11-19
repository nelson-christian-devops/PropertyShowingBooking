using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Domain.Entities;

public sealed class Booking
{
    public Guid Id { get; private init; }
    public Guid SlotId { get; private init; }
    public string RenterEmail { get; private set; }
    public int PartySize { get; private init; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == BookingStatus.Confirmed;
    public bool IsFinal => Status is BookingStatus.Cancelled or BookingStatus.Attended or BookingStatus.NoShow;

    public Booking(Guid id, Guid slotId, string renterEmail, int partySize, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Booking id cannot be empty.", nameof(id));

        if (slotId == Guid.Empty)
            throw new ArgumentException("Slot id cannot be empty.", nameof(slotId));

        RenterEmail = NormalizeEmail(renterEmail);

        if (partySize is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(partySize), "Party size must be between 1 and 6.");

        Id = id;
        SlotId = slotId;
        PartySize = partySize;
        Status = BookingStatus.Confirmed;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Booking(Guid slotId, string renterEmail, int partySize, DateTimeOffset createdAt)
        : this(Guid.NewGuid(), slotId, renterEmail, partySize, createdAt)
    {
    }

    public static Booking Create(Guid slotId, string renterEmail, int partySize, DateTimeOffset? createdAt = null)
    {
        return new Booking(Guid.NewGuid(), slotId, renterEmail, partySize, createdAt ?? DateTimeOffset.UtcNow);
    }

    public static string NormalizeEmail(string renterEmail)
    {
        if (string.IsNullOrWhiteSpace(renterEmail))
            throw new ArgumentException("Renter email cannot be empty.", nameof(renterEmail));

        return renterEmail.Trim().ToLowerInvariant();
    }

    public bool TryCancel(DateTimeOffset changedAt)
        => TryChangeStatus(BookingStatus.Cancelled, changedAt);

    public void Cancel(DateTimeOffset changedAt)
    {
        if (!TryCancel(changedAt))
            throw new InvalidOperationException("Final booking statuses cannot be changed.");
    }

    public void MarkAttended(DateTimeOffset changedAt)
    {
        if (!TryAttend(changedAt))
            throw new InvalidOperationException("Final booking statuses cannot be changed.");
    }

    public void MarkNoShow(DateTimeOffset changedAt)
    {
        if (!TryMarkNoShow(changedAt))
            throw new InvalidOperationException("Final booking statuses cannot be changed.");
    }

    public bool TryAttend(DateTimeOffset changedAt)
        => TryChangeStatus(BookingStatus.Attended, changedAt);

    public bool TryMarkNoShow(DateTimeOffset changedAt)
        => TryChangeStatus(BookingStatus.NoShow, changedAt);

    public bool TryChangeStatus(BookingStatus toStatus, DateTimeOffset changedAt)
    {
        if (IsFinal)
            return false;

        if (toStatus == BookingStatus.Confirmed)
            throw new InvalidOperationException("A booking cannot return to Confirmed status.");

        Status = toStatus;
        UpdatedAt = changedAt;
        return true;
    }
}
