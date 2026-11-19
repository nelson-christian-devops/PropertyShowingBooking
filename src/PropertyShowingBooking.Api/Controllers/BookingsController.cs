using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyShowingBooking.Application.Commands.ChangeBookingStatus;
using PropertyShowingBooking.Application.DTOs;
using PropertyShowingBooking.Application.Queries.GetBookingHistory;
using PropertyShowingBooking.Domain.Enums;

namespace PropertyShowingBooking.Api.Controllers;

[ApiController]
[Route("bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(GetBookingHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetBookingHistoryResponse>> GetHistory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new GetBookingHistoryQuery(id),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/transitions")]
    [ProducesResponseType(typeof(ChangeBookingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChangeBookingStatusResponse>> Transition(
        [FromRoute] Guid id,
        [FromBody] ChangeBookingStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BookingStatus>(request.To, ignoreCase: true, out var targetStatus)
            || targetStatus == BookingStatus.Confirmed)
        {
            throw new ArgumentException("Target status must be Cancelled, Attended, or NoShow.");
        }

        var command = new ChangeBookingStatusCommand(
            id,
            targetStatus,
            request.Reason,
            request.Actor);

        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }
}
