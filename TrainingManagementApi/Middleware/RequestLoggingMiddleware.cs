using System.Diagnostics;

namespace TrainingManagementApi.Middleware;

/// <summary>
/// Writes ONE readable line per API call, for example:
/// <c>POST /api/employees -> 400 An employee with this email already exists. (14 ms)</c>.
/// Successful calls are information, client mistakes (4xx) are warnings, server problems (5xx) are errors.
/// </summary>
public class RequestLoggingMiddleware
{
    /// <summary>Other parts of the pipeline put the human-readable reason for a 4xx answer here.</summary>
    public const string ReasonKey = "RequestLog.Reason";

    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger("Request");   // short category keeps the console readable
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // The documentation page and its files are not API calls.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var timer = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            timer.Stop();
            var status = context.Response.StatusCode;
            var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            var reason = context.Items[ReasonKey] as string;

            _logger.Log(level, "{Method} {Path}{Query} -> {Status} {Reason}({Elapsed} ms)",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                status,
                string.IsNullOrEmpty(reason) ? string.Empty : reason + " ",
                timer.ElapsedMilliseconds);
        }
    }
}
