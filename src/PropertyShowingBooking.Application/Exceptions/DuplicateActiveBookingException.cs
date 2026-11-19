namespace PropertyShowingBooking.Application.Exceptions;

public sealed class DuplicateActiveBookingException : Exception
{
    public DuplicateActiveBookingException()
        : base("An active booking already exists for this renter and slot.")
    {
    }
}
