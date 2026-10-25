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
    /// An employee can be assigned a given course only once. If the employee or the course does not exist the
    /// response is 404; a duplicate assignment or an end date before the start date is a 400.
    /// </remarks>
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
    /// <param name="assignmentId">The assignment id.</param>
    [HttpGet("training-assignments/{assignmentId}")]
    [ProducesResponseType(typeof(TrainingAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingAssignmentDto>> GetById(int assignmentId, CancellationToken cancellationToken)
    {
        return Ok(await _trainingService.GetByIdAsync(assignmentId, cancellationToken));
    }

    /// <summary>View all trainings assigned to an employee.</summary>
    /// <param name="employeeId">The employee id.</param>
    [HttpGet("employees/{employeeId}/trainings")]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TrainingAssignmentDto>>> GetForEmployee(
        int employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _trainingService.GetForEmployeeAsync(employeeId, cancellationToken));
    }

    /// <summary>Update the status of a training assignment (Assigned, In Progress, Completed).</summary>
    /// <param name="assignmentId">The assignment id.</param>
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
    /// <param name="assignmentId">The assignment id.</param>
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
