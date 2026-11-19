namespace PropertyShowingBooking.Application.DTOs;

public sealed record CreateBookingRequest(string RenterEmail, int PartySize);
