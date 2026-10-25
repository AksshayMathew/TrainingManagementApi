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

    /// <summary>Get all courses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CourseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _courseService.GetAllAsync(cancellationToken));
    }

    /// <summary>Get one course by id.</summary>
    /// <param name="id">The course id.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _courseService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Create a new course.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseDto>> Create(
        [FromBody] CourseRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _courseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.CourseId }, created);
    }

    /// <summary>Replace all details of an existing course.</summary>
    /// <param name="id">The course id.</param>
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
    /// <param name="id">The course id.</param>
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
