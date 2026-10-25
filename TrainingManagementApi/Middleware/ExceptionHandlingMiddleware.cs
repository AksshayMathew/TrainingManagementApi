using Microsoft.AspNetCore.Mvc;
using TrainingManagementApi.Exceptions;

namespace TrainingManagementApi.Middleware;

/// <summary>
/// Last line of defence: turns any exception thrown while handling a request into a clean JSON error
/// (RFC 7807 "problem details") instead of a stack trace or a dropped connection.
/// Expected problems (404, 400) are logged as warnings; anything else is logged as an error with the
/// full exception and answered with a generic 500 so internal details never reach the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private const string ProblemJson = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the connection mid-request. Nobody is listening, so this is not an error.
            _logger.LogInformation("Request cancelled by the client: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                // Too late to send a clean error body; let the server abort the connection.
                _logger.LogError(ex, "Unhandled exception after the response started: {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                throw;
            }

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case NotFoundException notFound:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Resource not found.",
                    Detail = notFound.Message
                };
                LogExpected(context, problem);
                break;

            case BusinessRuleException rule:
                problem = new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [rule.Field] = new[] { rule.Message }
                })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                };
                LogExpected(context, problem);
                break;

            case BadHttpRequestException badRequest:
                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "The request could not be processed.",
                    Detail = badRequest.Message
                };
                LogExpected(context, problem);
                break;

            default:
                _logger.LogError(exception, "Unhandled exception: {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    // Generic text for clients; the real reason is only exposed while developing.
                    Detail = _environment.IsDevelopment()
                        ? exception.Message
                        : "Something went wrong on the server. Please try again later or contact support with the trace id."
                };
                break;
        }

        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(
            problem, problem.GetType(), options: null, contentType: ProblemJson);
    }

    private void LogExpected(HttpContext context, ProblemDetails problem) =>
        _logger.LogWarning("{Method} {Path} -> {StatusCode}: {Detail}",
            context.Request.Method, context.Request.Path, problem.Status, problem.Detail ?? problem.Title);
}
