namespace PropertyShowingBooking.Application.Exceptions;

public sealed class BookingTransitionConflictException : Exception
{
    public BookingTransitionConflictException(string message = "The booking transition is not allowed.")
        : base(message)
    {
    }
}
