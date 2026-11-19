namespace PropertyShowingBooking.Application.Exceptions;

public sealed class BookingDatabaseBusyException : Exception
{
    public BookingDatabaseBusyException()
        : base("The booking database is temporarily busy. Please retry.")
    {
    }
}
