using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public HoldsController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// API 5: Get status and expiration details of a specific hold.
    /// </summary>
    [HttpGet("{holdId}")]
    [ProducesResponseType(typeof(GetHoldResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetHoldResponseDto>> GetHold(
        [FromRoute] string holdId,
        CancellationToken ct)
    {
        var hold = await _admissionService.GetHoldByIdAsync(holdId, ct);
        return Ok(hold);
    }

    /// <summary>
    /// API 6: Confirm admission fee payment for a hold.
    /// </summary>
    [HttpPost("{holdId}/confirm")]
    [ProducesResponseType(typeof(ConfirmHoldResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfirmHoldResponseDto>> ConfirmHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Student ID",
                Detail = errorMessage
            });
        }

        var response = await _admissionService.ConfirmHoldAsync(holdId, studentId!, ct);
        return Ok(response);
    }

    /// <summary>
    /// API 7: Cancel a held admission application.
    /// </summary>
    [HttpDelete("{holdId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Student ID",
                Detail = errorMessage
            });
        }

        await _admissionService.CancelHoldAsync(holdId, studentId!, ct);
        return NoContent();
    }
}

