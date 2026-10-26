using Microsoft.AspNetCore.Mvc;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Interfaces;

namespace TrainingManagementApi.Controllers;

/// <summary>Create, read, update and delete training courses.</summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    /// <summary>List all courses.</summary>
    /// <response code="200">The courses (an empty list if there are none yet).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CourseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _courseService.GetAllAsync(cancellationToken));
    }

    /// <summary>Get one course by id.</summary>
    /// <param name="id">The course id (the <c>courseId</c> returned when the course was created).</param>
    /// <response code="200">The course.</response>
    /// <response code="400">The id is not a whole number (for example <c>abc</c>).</response>
    /// <response code="404">No course has this id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _courseService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Create a new course.</summary>
    /// <remarks>Copy the <c>courseId</c> from the response - you need it to assign the course to an employee. Duration is in hours and must be above 0.</remarks>
    /// <response code="201">Created. The new course is returned and the <c>Location</c> header links to it.</response>
    /// <response code="400">A field is missing or invalid (for example duration 0). The <c>errors</c> list says which field and why.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseDto>> Create(
        [FromBody] CourseRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _courseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.CourseId }, created);
    }

    /// <summary>Update a course (send all fields).</summary>
    /// <param name="id">The id of the course to change.</param>
    /// <response code="200">Updated. The new details are returned.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="404">No course has this id.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> Update(
        int id, [FromBody] CourseRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await _courseService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Delete a course.</summary>
    /// <param name="id">The id of the course to delete.</param>
    /// <response code="200">Deleted.</response>
    /// <response code="400">The course is still assigned to employees - delete those training assignments first - or the id is not a whole number.</response>
    /// <response code="404">No course has this id.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> Delete(int id, CancellationToken cancellationToken)
    {
        await _courseService.DeleteAsync(id, cancellationToken);
        return Ok(new MessageDto($"Course {id} was deleted."));
    }
}
