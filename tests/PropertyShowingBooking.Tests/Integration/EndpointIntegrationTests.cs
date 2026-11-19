using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PropertyShowingBooking.Domain.Entities;
using PropertyShowingBooking.Domain.Enums;
using PropertyShowingBooking.Infrastructure.Persistence;

namespace PropertyShowingBooking.Tests.Integration;

public sealed class EndpointIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Post_slots_returns_201_and_persists_slot()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var propertyId = Guid.NewGuid();
        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        var response = await client.PostAsJsonAsync("/slots", new
        {
            propertyId,
            startsAt,
            durationMinutes = 60,
            capacity = 10
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.NotEqual(Guid.Empty, Guid.Parse(root.GetProperty("id").GetString()!));
        Assert.Equal(propertyId, Guid.Parse(root.GetProperty("propertyId").GetString()!));
        Assert.Equal(60, root.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(10, root.GetProperty("capacity").GetInt32());
        Assert.Equal(0, root.GetProperty("bookedSeats").GetInt32());
        Assert.Equal(10, root.GetProperty("availableSeats").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slotId = Guid.Parse(root.GetProperty("id").GetString()!);
        var slot = await db.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);

        Assert.Equal(propertyId, slot.PropertyId);
        Assert.Equal(0, slot.BookedSeats);
        Assert.Equal(10, slot.AvailableSeats);
    }

    [Fact]
    public async Task Get_slots_returns_created_slot_and_available_seats()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 8);

        var response = await client.GetAsync("/slots?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items, element =>
            Guid.Parse(element.GetProperty("id").GetString()!) == slotId);

        Assert.Equal(8, item.GetProperty("availableSeats").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.True(root.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Post_booking_returns_201_and_persists_booking_and_history()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 6);

        var response = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "renter@example.com",
            partySize = 3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var bookingId = Guid.Parse(root.GetProperty("bookingId").GetString()!);

        Assert.Equal(slotId, Guid.Parse(root.GetProperty("slotId").GetString()!));
        Assert.Equal("renter@example.com", root.GetProperty("renterEmail").GetString());
        Assert.Equal(3, root.GetProperty("partySize").GetInt32());
        Assert.Equal("Confirmed", root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("availableSeats").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId);
        var slot = await db.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);
        var history = await db.BookingHistories.AsNoTracking().SingleAsync(x => x.BookingId == bookingId);

        Assert.Equal(3, slot.BookedSeats);
        Assert.Equal(3, slot.AvailableSeats);
        Assert.Equal("renter@example.com", booking.RenterEmail);
        Assert.Equal("Confirmed", history.ToStatus.ToString());
        Assert.Equal("renter", history.Actor);
    }

    [Fact]
    public async Task Post_booking_over_capacity_returns_409()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 2);

        var valid = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "one@example.com",
            partySize = 1
        });
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);

        var excessive = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "two@example.com",
            partySize = 2
        });

        Assert.Equal(HttpStatusCode.Conflict, excessive.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await db.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);

        Assert.Equal(1, slot.BookedSeats);
        Assert.Equal(1, slot.AvailableSeats);
    }

    [Fact]
    public async Task Get_booking_history_returns_initial_confirmed_entry()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 5);

        var bookingResponse = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "history@example.com",
            partySize = 2
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);

        using var bookingDocument = JsonDocument.Parse(await bookingResponse.Content.ReadAsStringAsync());
        var bookingId = Guid.Parse(bookingDocument.RootElement.GetProperty("bookingId").GetString()!);

        var historyResponse = await client.GetAsync($"/bookings/{bookingId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyDocument = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        var root = historyDocument.RootElement;

        Assert.Equal(bookingId, Guid.Parse(root.GetProperty("bookingId").GetString()!));
        var entries = root.GetProperty("history").EnumerateArray().ToList();
        var initial = Assert.Single(entries);

        Assert.True(initial.TryGetProperty("fromStatus", out var fromStatus));
        Assert.Equal(JsonValueKind.Null, fromStatus.ValueKind);
        Assert.Equal("Confirmed", initial.GetProperty("toStatus").GetString());
        Assert.Equal("renter", initial.GetProperty("actor").GetString());
        Assert.Equal("Booking created", initial.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Duplicate_active_booking_returns_409()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 5);
        var request = new
        {
            renterEmail = "duplicate@example.com",
            partySize = 1
        };

        var first = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Booking_transition_and_repeated_transition_return_200_then_409()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 5);
        var bookingResponse = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "transition@example.com",
            partySize = 2
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);

        using var bookingDocument = JsonDocument.Parse(await bookingResponse.Content.ReadAsStringAsync());
        var bookingId = Guid.Parse(bookingDocument.RootElement.GetProperty("bookingId").GetString()!);

        var cancellation = await client.PostAsJsonAsync($"/bookings/{bookingId}/transitions", new
        {
            to = "Cancelled",
            reason = "Unable to attend",
            actor = "renter"
        });
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);

        var repeated = await client.PostAsJsonAsync($"/bookings/{bookingId}/transitions", new
        {
            to = "Cancelled",
            reason = "Repeated",
            actor = "renter"
        });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    [Fact]
    public async Task Duplicate_slot_returns_409()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var propertyId = Guid.NewGuid();
        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var slotRequest = new
        {
            propertyId,
            startsAt,
            durationMinutes = 60,
            capacity = 10
        };

        var first = await client.PostAsJsonAsync("/slots", slotRequest);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/slots", slotRequest);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_bookings_never_exceed_remaining_capacity()
    {
        using var factory = new PropertyShowingBookingWebApplicationFactory();
        var client = factory.CreateClient();

        var slotId = await CreateSlotAsync(client, capacity: 5);

        var initial = await client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
        {
            renterEmail = "initial@example.com",
            partySize = 2
        });
        Assert.Equal(HttpStatusCode.Created, initial.StatusCode);

        var requests = Enumerable.Range(1, 6)
            .Select(index => client.PostAsJsonAsync($"/slots/{slotId}/bookings", new
            {
                renterEmail = $"concurrent{index}@example.com",
                partySize = 1
            }))
            .ToList();

        var responses = await Task.WhenAll(requests);
        var statuses = responses.Select(response => response.StatusCode).ToList();

        Assert.Equal(3, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(3, statuses.Count(status => status == HttpStatusCode.Conflict));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await db.ShowingSlots.AsNoTracking().SingleAsync(x => x.Id == slotId);

        Assert.Equal(5, slot.BookedSeats);
        Assert.Equal(0, slot.AvailableSeats);
        Assert.True(slot.BookedSeats <= slot.Capacity);

        var bookingCount = await db.Bookings.AsNoTracking()
            .CountAsync(x => x.SlotId == slotId && x.Status == BookingStatus.Confirmed);
        Assert.Equal(4, bookingCount);
    }

    private static async Task<Guid> CreateSlotAsync(HttpClient client, int capacity)
    {
        var response = await client.PostAsJsonAsync("/slots", new
        {
            propertyId = Guid.NewGuid(),
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            durationMinutes = 60,
            capacity
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Guid.Parse(document.RootElement.GetProperty("id").GetString()!);
    }
}
