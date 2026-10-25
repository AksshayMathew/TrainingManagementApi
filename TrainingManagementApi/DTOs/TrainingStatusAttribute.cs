using System.ComponentModel.DataAnnotations;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.DTOs;

/// <summary>
/// Validates that a string is one of the training statuses (Assigned / In Progress / Completed). A missing
/// value is accepted here - combine with <c>[Required]</c> where the status is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TrainingStatusAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is string text && TrainingStatuses.TryNormalize(text, out _))
        {
            return ValidationResult.Success;
        }

        var message = TrainingStatuses.InvalidStatusMessage;
        return validationContext.MemberName is null
            ? new ValidationResult(message)
            : new ValidationResult(message, new[] { validationContext.MemberName });
    }
}
