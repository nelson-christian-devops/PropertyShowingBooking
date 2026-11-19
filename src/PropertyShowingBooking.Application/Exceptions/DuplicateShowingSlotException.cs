namespace PropertyShowingBooking.Application.Exceptions;

public sealed class DuplicateShowingSlotException : Exception
{
    public DuplicateShowingSlotException()
        : base("A showing slot already exists for this property, start time, and duration.")
    {
    }
}
