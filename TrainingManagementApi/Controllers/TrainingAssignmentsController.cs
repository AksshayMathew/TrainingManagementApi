using Microsoft.AspNetCore.Mvc;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Interfaces;

namespace TrainingManagementApi.Controllers;

/// <summary>Assign courses to employees and follow their training progress.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class TrainingAssignmentsController : ControllerBase
{
    private readonly ITrainingAssignmentService _trainingService;

    public TrainingAssignmentsController(ITrainingAssignmentService trainingService)
    {
        _trainingService = trainingService;
    }

    /// <summary>Assign a course to an employee.</summary>
    /// <remarks>
    /// Use the <c>employeeId</c> and <c>courseId</c> from the responses of the Employees and Courses groups.
    /// Copy the <c>assignmentId</c> from the response to change its status later.
    /// An employee can be assigned a given course only once, and the end date cannot be before the start date.
    /// </remarks>
    /// <response code="201">Assigned. The new assignment is returned (status <c>Assigned</c> unless you sent another).</response>
    /// <response code="400">A field is missing or invalid, the end date is before the start date, or this employee already has this course. The <c>errors</c> list says which.</response>
    /// <response code="404">There is no employee or no course with the id you sent.</response>
    [HttpPost("training-assignments")]
    [ProducesResponseType(typeof(TrainingAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingAssignmentDto>> Assign(
        [FromBody] TrainingAssignmentRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _trainingService.AssignAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { assignmentId = created.AssignmentId }, created);
    }

    /// <summary>Get one training assignment by id.</summary>
    /// <param name="assignmentId">The assignment id (the <c>assignmentId</c> returned when the course was assigned).</param>
    /// <response code="200">The assignment, with employee and course names.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">No assignment has this id.</response>
    [HttpGet("training-assignments/{assignmentId}")]
    [ProducesResponseType(typeof(TrainingAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingAssignmentDto>> GetById(int assignmentId, CancellationToken cancellationToken)
    {
        return Ok(await _trainingService.GetByIdAsync(assignmentId, cancellationToken));
    }

    /// <summary>View all trainings of one employee.</summary>
    /// <param name="employeeId">The employee id.</param>
    /// <response code="200">The employee's trainings, oldest start date first (an empty list if the employee has none).</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">No employee has this id.</response>
    [HttpGet("employees/{employeeId}/trainings")]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TrainingAssignmentDto>>> GetForEmployee(
        int employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _trainingService.GetForEmployeeAsync(employeeId, cancellationToken));
    }

    /// <summary>Change the status of a training (Assigned, In Progress or Completed).</summary>
    /// <param name="assignmentId">The id of the assignment to change.</param>
    /// <response code="200">Updated. The assignment with its new status is returned.</response>
    /// <response code="400">The status is missing or not one of Assigned, In Progress, Completed.</response>
    /// <response code="404">No assignment has this id.</response>
    [HttpPut("training-assignments/{assignmentId}/status")]
    [ProducesResponseType(typeof(TrainingAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingAssignmentDto>> UpdateStatus(
        int assignmentId, [FromBody] UpdateTrainingStatusDto request, CancellationToken cancellationToken)
    {
        return Ok(await _trainingService.UpdateStatusAsync(assignmentId, request, cancellationToken));
    }

    /// <summary>Delete a training assignment.</summary>
    /// <param name="assignmentId">The id of the assignment to delete.</param>
    /// <response code="200">Deleted. The employee and the course are not affected.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">No assignment has this id.</response>
    [HttpDelete("training-assignments/{assignmentId}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> Delete(int assignmentId, CancellationToken cancellationToken)
    {
        await _trainingService.DeleteAsync(assignmentId, cancellationToken);
        return Ok(new MessageDto($"Training assignment {assignmentId} was deleted."));
    }
}
