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

    /// <summary>Get all employees, optionally searched, filtered, sorted and paged.</summary>
    /// <remarks>
    /// With no query options every employee is returned. The response header <c>X-Total-Count</c> holds the number of
    /// employees that matched; when paging is used <c>X-Page</c> and <c>X-Page-Size</c> are added as well.
    /// Example: <c>GET /api/employees?search=priya&amp;sortBy=Experience&amp;sortOrder=Desc&amp;page=1&amp;pageSize=5</c>
    /// </remarks>
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
    /// <param name="id">The employee id.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Create a new employee.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] EmployeeRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _employeeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.EmployeeId }, created);
    }

    /// <summary>Replace all details of an existing employee.</summary>
    /// <param name="id">The employee id.</param>
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
    /// <param name="id">The employee id.</param>
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
