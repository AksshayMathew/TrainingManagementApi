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

    /// <summary>Get all employees.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.GetAllAsync(cancellationToken));
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
