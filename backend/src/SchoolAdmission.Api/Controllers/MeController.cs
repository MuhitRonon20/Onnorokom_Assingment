using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public MeController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// API 8: Retrieve all admission applications for the authenticated student.
    /// </summary>
    [HttpGet("holds")]
    [ProducesResponseType(typeof(IReadOnlyList<MyHoldDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MyHoldDto>>> GetMyHolds(
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

        var holds = await _admissionService.GetHoldsForStudentAsync(studentId!, ct);
        return Ok(holds);
    }
}

