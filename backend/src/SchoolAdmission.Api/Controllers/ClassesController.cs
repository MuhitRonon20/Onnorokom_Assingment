using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public ClassesController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// API 1: Create a new class / shift combination.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedClassDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedClassDto>> CreateClass(
        [FromBody] CreateClassRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var created = await _admissionService.CreateClassAsync(request, ct);
        return Created($"/classes/{created.Id}", created);
    }

    /// <summary>
    /// API 2: List all admission classes with live seat counts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassDto>>> GetClasses(CancellationToken ct)
    {
        var classes = await _admissionService.GetClassesAsync(ct);
        return Ok(classes);
    }

    /// <summary>
    /// API 3: Get a single class with its live seat counts.
    /// </summary>
    [HttpGet("{classId}")]
    [ProducesResponseType(typeof(ClassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassDto>> GetClass(
        [FromRoute] string classId,
        CancellationToken ct)
    {
        var classDto = await _admissionService.GetClassByIdAsync(classId, ct);
        return Ok(classDto);
    }

    /// <summary>
    /// API 4: Create a hold reserving a seat for a student.
    /// </summary>
    [HttpPost("{classId}/holds")]
    [ProducesResponseType(typeof(HoldResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldResponseDto>> CreateHold(
        [FromRoute] string classId,
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

        var hold = await _admissionService.CreateHoldAsync(classId, studentId!, ct);
        return Created($"/holds/{hold.HoldId}", hold);
    }
}

