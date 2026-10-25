namespace TrainingManagementApi.Exceptions;

/// <summary>Thrown by a service when a requested record does not exist. Becomes HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id {key} was not found.")
    {
    }
}
