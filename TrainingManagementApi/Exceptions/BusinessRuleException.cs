namespace TrainingManagementApi.Exceptions;

/// <summary>
/// Thrown by a service when a request is well-formed but breaks a business rule that needs the database to
/// check (duplicate e-mail, course already assigned, ...). Becomes HTTP 400 with the same
/// <c>errors</c> shape that model-validation failures use, so clients handle one format.
/// </summary>
public class BusinessRuleException : Exception
{
    /// <summary>The request field the message belongs to (shown as the key under <c>errors</c>).</summary>
    public string Field { get; }

    public BusinessRuleException(string field, string message) : base(message)
    {
        Field = field;
    }
}
