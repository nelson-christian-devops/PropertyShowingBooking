# PropertyShowingBooking

## Project Overview

PropertyShowingBooking is a .NET 8 ASP.NET Core Web API for creating property showing slots, booking seats, transitioning booking statuses, and retrieving booking history.

The solution uses domain entities, CQRS-style MediatR handlers, EF Core 8 repositories, SQLite persistence, FluentValidation, Swagger/OpenAPI, centralized ProblemDetails error handling, and xUnit tests.

## Technology Stack

- C# / .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQLite
- MediatR
- FluentValidation
- Swagger / Swashbuckle
- xUnit
- Moq
- Microsoft.AspNetCore.Mvc.Testing

## Solution Architecture

The solution separates concerns into Domain, Application, Infrastructure, API, and Tests projects:

- **Domain** contains entities and booking/slot business rules.
- **Application** contains MediatR commands and queries, DTOs, validators, repository contracts, and pipeline behavior.
- **Infrastructure** contains EF Core DbContext, entity configurations, migrations, repositories, and SQLite setup.
- **API** contains controllers, dependency injection startup, Swagger, and centralized exception handling.
- **Tests** contains unit, concurrency, transition, and HTTP integration tests.

## Project Structure

```text
PropertyShowingBooking.slnx
src/
  PropertyShowingBooking.Api/
  PropertyShowingBooking.Application/
  PropertyShowingBooking.Domain/
  PropertyShowingBooking.Infrastructure/
 tests/
  PropertyShowingBooking.Tests/
```

## Database Design

SQLite is configured with `PropertyShowingBooking.db` through the `PropertyShowingBooking` connection string.

Main tables:

- `ShowingSlots`
- `Bookings`
- `BookingHistories`
- `__EFMigrationsHistory`

Important constraints and relationships:

- ShowingSlot 1-to-many Booking
- Booking 1-to-many BookingHistory
- Booking status is stored as a string enum
- Date/time values use SQLite TEXT storage
- `AvailableSeats` is calculated and is not a database column
- A filtered unique index protects duplicate confirmed bookings:
  `SlotId + RenterEmail WHERE Status = Confirmed`

## API Endpoints

| Method | Route | Description |
|---|---|---|
| POST | `/slots` | Create a showing slot |
| GET | `/slots` | Search and paginate showing slots |
| POST | `/slots/{id}/bookings` | Create a booking |
| POST | `/bookings/{id}/transitions` | Change booking status |
| GET | `/bookings/{id}/history` | Retrieve chronological booking history |

Swagger is available at `/swagger` in Development.

## Booking Business Rules

- Renter email is trimmed and normalized to lowercase.
- Party size must be from 1 through 6.
- A slot must exist and must not have started.
- Available seats must cover the requested party size.
- An active renter cannot have a second confirmed booking for the same slot.
- Seat reservation and booking/history writes use the EF Core transaction boundary.

## State Transition Rules

- `Confirmed -> Cancelled`: owner or renter, before the slot starts; releases seats.
- `Confirmed -> Attended`: owner only, after the slot starts.
- `Confirmed -> NoShow`: owner only, after the slot starts.
- `Confirmed` cannot be returned to.
- `Cancelled`, `Attended`, and `NoShow` are final states.
- A successful transition creates a `BookingHistory` entry.

## Concurrency Handling

SQLite writes are serialized by the database's write lock. Booking reservation uses a conditional atomic update:

```sql
UPDATE ShowingSlots
SET BookedSeats = BookedSeats + @PartySize
WHERE Id = @SlotId
  AND BookedSeats + @PartySize <= Capacity;
```

The affected row count determines whether seats were reserved. The booking, history, and seat update are committed or rolled back together. Duplicate confirmed bookings are also protected by the filtered unique index.

SQLite busy/locked responses are retried briefly, then mapped to HTTP 503. Capacity exhaustion and duplicate active bookings return HTTP 409.

## Error Handling

The API uses centralized ASP.NET Core exception handling with `ProblemDetails`:

- `400` invalid request or validation failure
- `404` missing booking or slot
- `409` booking or transition conflict
- `500` unexpected internal error
- `503` temporary SQLite busy/lock condition

Production exception responses do not expose stack traces. Swagger is enabled only in Development.

## How to Run the Application

From the solution root:

```powershell
dotnet restore PropertyShowingBooking.slnx
dotnet run --project src/PropertyShowingBooking.Api/PropertyShowingBooking.Api.csproj
```

In Development, open:

- Swagger UI: `http://localhost:<port>/swagger`

The API starts by applying pending EF Core migrations.

## Database Migration Instructions

Create a migration:

```powershell
dotnet ef migrations add <MigrationName> --project src/PropertyShowingBooking.Infrastructure/PropertyShowingBooking.Infrastructure.csproj --startup-project src/PropertyShowingBooking.Api/PropertyShowingBooking.Api.csproj --context PropertyShowingBooking.Infrastructure.Persistence.ApplicationDbContext --output-dir Persistence/Migrations
```

Apply migrations:

```powershell
dotnet ef database update --project src/PropertyShowingBooking.Infrastructure/PropertyShowingBooking.Infrastructure.csproj --startup-project src/PropertyShowingBooking.Api/PropertyShowingBooking.Api.csproj --context PropertyShowingBooking.Infrastructure.Persistence.ApplicationDbContext
```

The API startup path also calls `Database.Migrate()`.

## How to Execute Tests

Run all tests:

```powershell
dotnet test tests/PropertyShowingBooking.Tests/PropertyShowingBooking.Tests.csproj
```

Run integration tests:

```powershell
dotnet test tests/PropertyShowingBooking.Tests/PropertyShowingBooking.Tests.csproj --filter FullyQualifiedName~PropertyShowingBooking.Tests.Integration
```

Each `WebApplicationFactory` instance uses a separate temporary SQLite database and removes it after disposal.

## Sample API Requests

Create slot:

```http
POST /slots
Content-Type: application/json

{
  "propertyId": "b4f5b7df-35af-4f8d-a99d-e53ad8ca2328",
  "startsAt": "2026-12-01T10:00:00Z",
  "durationMinutes": 60,
  "capacity": 10
}
```

Create booking:

```http
POST /slots/{slotId}/bookings
Content-Type: application/json

{
  "renterEmail": "renter@example.com",
  "partySize": 3
}
```

Transition booking:

```http
POST /bookings/{bookingId}/transitions
Content-Type: application/json

{
  "to": "Cancelled",
  "reason": "Unable to attend",
  "actor": "renter"
}
```

Get history:

```http
GET /bookings/{bookingId}/history
```

## Main Design Decisions

- Clean separation among Domain, Application, Infrastructure, and API projects.
- MediatR isolates HTTP controllers from application orchestration.
- EF Core remains inside Infrastructure.
- DTO records prevent EF entities from becoming API contracts.
- SQLite is used for its portability and relational constraint support.
- Conditional UPDATE and an active-confirmed filtered index protect booking capacity and duplicate renter reservations.
- Initial booking and status transitions are committed atomically with history entries.

## Limitations and Future Improvements

- SQLite has process/database-level write locking and is less suitable than a network database for high concurrent throughput.
- Owner identity is actor-based rather than backed by authentication/authorization. There is no JWT or Entra ID integration.
- No API endpoints exist for listing booking details beyond slot search, booking creation, transitions, and history.
- No explicit cursor pagination or distributed cache is included.
- SQLite busy 503 handling can use a more configurable retry policy and structured logging.
- Production deployment should use managed relational storage if horizontal scaling is required.

## AI Use

GitHub Copilot Agent Mode assisted using master prompt with project setup, code generation, implementation, refactoring, test creation, documentation, and verification commands.
