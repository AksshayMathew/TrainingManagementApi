using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Interfaces;

namespace TrainingManagementApi.Controllers;

/// <summary>Create, read, update and delete employees.</summary>
[ApiController]
[Route("api/employees")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>List employees (search, filter, sort and page are optional).</summary>
    /// <remarks>
    /// Leave every option empty to get all employees. The header <c>X-Total-Count</c> tells how many employees matched;
    /// with paging, <c>X-Page</c> and <c>X-Page-Size</c> are added too.
    ///
    /// Example: <c>search=priya</c>, <c>sortBy=Experience</c>, <c>sortOrder=Desc</c>, <c>page=1</c>, <c>pageSize=5</c>.
    /// </remarks>
    /// <response code="200">The employees (an empty list if nobody matches).</response>
    /// <response code="400">An option is invalid, for example page 0 or an unknown sortBy value. The <c>errors</c> list says which.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(
        [FromQuery] EmployeeQuery query, CancellationToken cancellationToken)
    {
        var result = await _employeeService.GetAllAsync(query, cancellationToken);

        Response.Headers["X-Total-Count"] = result.TotalCount.ToString(CultureInfo.InvariantCulture);
        if (result.Page.HasValue && result.PageSize.HasValue)
        {
            Response.Headers["X-Page"] = result.Page.Value.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Page-Size"] = result.PageSize.Value.ToString(CultureInfo.InvariantCulture);
        }

        return Ok(result.Items);
    }

    /// <summary>Get one employee by id.</summary>
    /// <param name="id">The employee id (the <c>employeeId</c> returned when the employee was created).</param>
    /// <response code="200">The employee.</response>
    /// <response code="400">The id is not a whole number (for example <c>abc</c>).</response>
    /// <response code="404">No employee has this id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Create a new employee.</summary>
    /// <remarks>Copy the <c>employeeId</c> from the response - you need it to assign training. Each e-mail address can be used only once.</remarks>
    /// <response code="201">Created. The new employee is returned and the <c>Location</c> header links to it.</response>
    /// <response code="400">A field is missing or invalid, or the e-mail address is already used. The <c>errors</c> list says which field and why.</response>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] EmployeeRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _employeeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.EmployeeId }, created);
    }

    /// <summary>Update an employee (send all fields).</summary>
    /// <param name="id">The id of the employee to change.</param>
    /// <response code="200">Updated. The new details are returned.</response>
    /// <response code="400">A field is invalid, or the e-mail address belongs to another employee.</response>
    /// <response code="404">No employee has this id.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Update(
        int id, [FromBody] EmployeeRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Delete an employee.</summary>
    /// <param name="id">The id of the employee to delete.</param>
    /// <response code="200">Deleted.</response>
    /// <response code="400">The employee still has training assignments - delete those first - or the id is not a whole number.</response>
    /// <response code="404">No employee has this id.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> Delete(int id, CancellationToken cancellationToken)
    {
        await _employeeService.DeleteAsync(id, cancellationToken);
        return Ok(new MessageDto($"Employee {id} was deleted."));
    }
}
