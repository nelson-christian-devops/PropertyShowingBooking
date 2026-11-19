namespace PropertyShowingBooking.Application.Exceptions;

public sealed class SlotCapacityUnavailableException : Exception
{
    public SlotCapacityUnavailableException()
        : base("There are not enough available seats.")
    {
    }
}
