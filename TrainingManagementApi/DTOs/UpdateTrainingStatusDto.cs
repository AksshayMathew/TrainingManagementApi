using System.ComponentModel.DataAnnotations;

namespace TrainingManagementApi.DTOs;

/// <summary>Data a client sends to change the status of a training assignment.</summary>
public class UpdateTrainingStatusDto
{
    /// <summary>New status: Assigned, In Progress or Completed (not case-sensitive).</summary>
    /// <example>In Progress</example>
    [Required(ErrorMessage = "Status is required.")]
    [TrainingStatus]
    public string? Status { get; set; }
}
