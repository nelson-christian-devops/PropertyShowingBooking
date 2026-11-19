using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyShowingBooking.Application.Commands.CreateBooking;
using PropertyShowingBooking.Application.Commands.CreateShowingSlot;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Queries.GetShowingSlots;

namespace PropertyShowingBooking.Api.Controllers;

[ApiController]
[Route("slots")]
public sealed class SlotsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SlotsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<ShowingSlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponseDto<ShowingSlotDto>>> Get(
        [FromQuery] Guid? propertyId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetShowingSlotsQuery(propertyId, from, to, page, pageSize);
        var response = await _mediator.Send(query, cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateShowingSlotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateShowingSlotResponse>> Create(
        [FromBody] CreateShowingSlotRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateShowingSlotCommand(
            request.PropertyId,
            request.StartsAt,
            request.DurationMinutes,
            request.Capacity);

        var response = await _mediator.Send(command, cancellationToken);

        return Created(string.Empty, response);
    }

    [HttpPost("{id:guid}/bookings")]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateBookingResponse>> CreateBooking(
        [FromRoute] Guid id,
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBookingCommand(id, request.RenterEmail, request.PartySize);
        var response = await _mediator.Send(command, cancellationToken);

        return Created($"/slots/{id}/bookings/{response.BookingId}", response);
    }
}
