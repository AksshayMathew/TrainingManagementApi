using System.ComponentModel.DataAnnotations;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.DTOs;

public enum EmployeeSortField
{
    Id,
    Name,
    Department,
    JoiningDate,
    Experience
}

public enum SortOrder
{
    Asc,
    Desc
}

/// <summary>Optional query-string options for <c>GET /api/employees</c>. With none of them the full list is returned.</summary>
public class EmployeeQuery
{
    /// <summary>Text to look for in the employee name or department (not case-sensitive, matches part of a word).</summary>
    /// <example>priya</example>
    [StringLength(FieldLimits.EmployeeNameMaxLength, ErrorMessage = "Search text cannot be longer than {1} characters.")]
    public string? Search { get; set; }

    /// <summary>Only employees of exactly this department (not case-sensitive).</summary>
    /// <example>Engineering</example>
    [StringLength(FieldLimits.DepartmentMaxLength, ErrorMessage = "Department cannot be longer than {1} characters.")]
    public string? Department { get; set; }

    /// <summary>Field to sort by: Id (default), Name, Department, JoiningDate or Experience.</summary>
    [EnumDataType(typeof(EmployeeSortField), ErrorMessage = "SortBy must be one of: Id, Name, Department, JoiningDate, Experience.")]
    public EmployeeSortField SortBy { get; set; } = EmployeeSortField.Id;

    /// <summary>Asc (default) or Desc.</summary>
    [EnumDataType(typeof(SortOrder), ErrorMessage = "SortOrder must be Asc or Desc.")]
    public SortOrder SortOrder { get; set; } = SortOrder.Asc;

    /// <summary>Page number, starting at 1. Giving a page (or a page size) turns paging on.</summary>
    [Range(1, MaxPage, ErrorMessage = "Page must be between {1} and {2}.")]
    public int? Page { get; set; }

    /// <summary>Items per page (1-100). Default when paging: 10.</summary>
    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between {1} and {2}.")]
    public int? PageSize { get; set; }

    public const int MaxPage = 1_000_000;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 10;

    /// <summary>True when the client asked for a page rather than the whole list.</summary>
    public bool IsPaged => Page.HasValue || PageSize.HasValue;
}
