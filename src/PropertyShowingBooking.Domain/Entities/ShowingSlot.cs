namespace PropertyShowingBooking.Domain.Entities;

public sealed class ShowingSlot
{
    public Guid Id { get; private init; }
    public Guid PropertyId { get; private init; }
    public DateTimeOffset StartsAt { get; private init; }
    public int DurationMinutes { get; private init; }
    public int Capacity { get; private init; }
    public int BookedSeats { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    public int AvailableSeats => Capacity - BookedSeats;

    public ShowingSlot(
        Guid id,
        Guid propertyId,
        DateTimeOffset startsAt,
        int durationMinutes,
        int capacity,
        int bookedSeats,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Slot id cannot be empty.", nameof(id));

        if (propertyId == Guid.Empty)
            throw new ArgumentException("Property id cannot be empty.", nameof(propertyId));

        if (startsAt <= createdAt)
            throw new ArgumentException("Showing start time must be in the future.", nameof(startsAt));

        if (durationMinutes is < 15 or > 120)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be between 15 and 120 minutes.");

        if (capacity is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be between 1 and 20.");

        if (bookedSeats < 0)
            throw new ArgumentOutOfRangeException(nameof(bookedSeats), "Booked seats cannot be negative.");

        if (bookedSeats > capacity)
            throw new ArgumentOutOfRangeException(nameof(bookedSeats), "Booked seats cannot exceed capacity.");

        Id = id;
        PropertyId = propertyId;
        StartsAt = startsAt;
        DurationMinutes = durationMinutes;
        Capacity = capacity;
        BookedSeats = bookedSeats;
        CreatedAt = createdAt;
    }

    public ShowingSlot(
        Guid propertyId,
        DateTimeOffset startsAt,
        int durationMinutes,
        int capacity)
        : this(Guid.NewGuid(), propertyId, startsAt, durationMinutes, capacity, 0, DateTimeOffset.UtcNow)
    {
    }

    public static ShowingSlot Create(
        Guid propertyId,
        DateTimeOffset startsAt,
        int durationMinutes,
        int capacity,
        DateTimeOffset? createdAt = null)
    {
        var creationTime = createdAt ?? DateTimeOffset.UtcNow;

        return new ShowingSlot(
            Guid.NewGuid(),
            propertyId,
            startsAt,
            durationMinutes,
            capacity,
            bookedSeats: 0,
            creationTime);
    }

    public void ReserveSeats(int partySize)
    {
        if (partySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(partySize), "Party size must be positive.");

        if (partySize > AvailableSeats)
            throw new InvalidOperationException("There are not enough available seats.");

        BookedSeats += partySize;
    }

    public void ReleaseSeats(int partySize)
    {
        if (partySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(partySize), "Party size must be positive.");

        if (partySize > BookedSeats)
            throw new InvalidOperationException("Cannot release more seats than have been booked.");

        BookedSeats -= partySize;
    }
}
