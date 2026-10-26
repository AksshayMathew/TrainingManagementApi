namespace TrainingManagementApi.DTOs;

/// <summary>A plain confirmation message, e.g. returned after a successful DELETE.</summary>
public class MessageDto
{
    /// <example>Employee 1 was deleted.</example>
    public string Message { get; set; } = string.Empty;

    public MessageDto()
    {
    }

    public MessageDto(string message)
    {
        Message = message;
    }
}
