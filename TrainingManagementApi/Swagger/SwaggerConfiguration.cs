using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace TrainingManagementApi.Swagger;

/// <summary>Everything that makes the Swagger page easy to use: friendly groups in a sensible order, a quick-start guide, and UI settings.</summary>
public static class SwaggerConfiguration
{
    private const string Employees = "Employees";
    private const string Courses = "Courses";
    private const string Training = "Training assignments";

    /// <summary>The order a first-time user needs the groups in: employees and courses first, then assign.</summary>
    private static readonly string[] GroupOrder = { Employees, Courses, Training };

    private const string Introduction = """
        Manage **employees**, **training courses** and the **training assigned to each employee** (ABC Technologies).

        ### How to use this page
        1. Click a coloured bar to open an endpoint, change the example if you like, and press the blue **Execute** button. *Try it out* is already switched on.
        2. The first time, follow this order (each step needs an id from the step before):

        | Step | Do this | Where |
        |---|---|---|
        | 1 | Create an employee and **copy `employeeId`** from the response | *Employees* > `POST /api/employees` |
        | 2 | Create a course and **copy `courseId`** | *Courses* > `POST /api/courses` |
        | 3 | Assign the course to the employee (paste both ids) and **copy `assignmentId`** | *Training assignments* > `POST /api/training-assignments` |
        | 4 | See all trainings of the employee | *Training assignments* > `GET /api/employees/{employeeId}/trainings` |
        | 5 | Change the status to `In Progress` / `Completed` | *Training assignments* > `PUT /api/training-assignments/{assignmentId}/status` |

        ### Reading the result
        | Code | Meaning |
        |---|---|
        | **200 / 201** | It worked (201 = something new was created). |
        | **400** | Something you sent is not valid. The `errors` list says which field and why. |
        | **404** | The id you used does not exist. |
        | **500** | A problem on the server. Quote the `traceId` when you report it. |

        **Tips:** use the *Filter by tag* box to find an endpoint quickly. Dates look like `2026-02-01T00:00:00`. An e-mail address can be used by only one employee.
        An employee or course that still has trainings cannot be deleted - delete the training assignments first.
        """;

    private const string Styles = """
        <style>
          .swagger-ui .topbar { display: none; }                     /* the 'select a definition' bar only confuses when there is one API */
          .swagger-ui .info { margin: 24px 0 8px; }
          .swagger-ui .info .title { font-size: 2rem; }
          .swagger-ui .info .description, .swagger-ui .info li, .swagger-ui .info p { font-size: 15px; line-height: 1.55; }
          .swagger-ui .info table { margin: 8px 0 14px; }
          .swagger-ui .info table td, .swagger-ui .info table th { padding: 6px 12px; border: 1px solid #d8dde6; font-size: 14px; }
          .swagger-ui .info table th { background: #f3f5f9; }
          .swagger-ui .filter-container { margin: 0 0 14px; }
          .swagger-ui .opblock-tag { font-size: 1.5rem; padding: 14px 10px; border-bottom: 2px solid #3b4151; }
          .swagger-ui .opblock-tag small { font-size: 14px; color: #3b4151; }
          .swagger-ui .opblock .opblock-summary-description { font-weight: 600; color: #1b1b1b; font-size: 14.5px; }
          .swagger-ui .opblock .opblock-summary-path { font-size: 15px; }
          .swagger-ui .opblock .opblock-section-header h4, .swagger-ui table.parameters td { font-size: 14px; }
          .swagger-ui .btn.execute { font-size: 16px; padding: 10px 34px; }
          .swagger-ui .responses-inner h4, .swagger-ui .response-col_description { font-size: 14px; }
        </style>
        """;

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Employee Training Management API",
                Version = "v1",
                Description = Introduction
            });

            // Show the /// comments from the code as descriptions. Guarded so a missing file can never stop start-up.
            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile);
            }

            options.TagActionsBy(api => new[] { GroupName(api) });
            options.OrderActionsBy(OrderKey);
            options.DocumentFilter<GroupDescriptionsFilter>();
        });

        return services;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        // Swagger stays on in every environment because this is a training project. For production, disable it or protect it.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Training Management API v1");
            options.DocumentTitle = "Employee Training API";
            options.DocExpansion(DocExpansion.List);          // groups open, endpoints listed
            options.DefaultModelsExpandDepth(-1);             // hide the technical "Schemas" section
            options.EnableTryItOutByDefault();                // no extra "Try it out" click
            options.EnableFilter();                           // search box
            options.EnableDeepLinking();                      // the address bar links to an endpoint
            options.DisplayRequestDuration();
            options.HeadContent = Styles;
        });

        // Typing just the address (http://localhost:5282) lands on the documentation instead of a 404.
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

        return app;
    }

    private static string GroupName(ApiDescription api) =>
        api.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
            ? controller switch
            {
                "Employees" => Employees,
                "Courses" => Courses,
                "TrainingAssignments" => Training,
                _ => controller ?? "Other"
            }
            : "Other";

    /// <summary>Within a group: list, create, get one, update, delete - the way people think about it.</summary>
    private static string OrderKey(ApiDescription api)
    {
        var group = Array.IndexOf(GroupOrder, GroupName(api));
        var path = api.RelativePath ?? string.Empty;
        var hasId = path.Contains('{');
        var method = api.HttpMethod?.ToUpperInvariant();

        var rank = (method, hasId) switch
        {
            ("GET", false) => 1,
            ("POST", _) => 2,
            ("GET", true) => 3,
            ("PUT", _) => 4,
            ("DELETE", _) => 5,
            _ => 6
        };

        return $"{(group < 0 ? 9 : group)}_{rank}_{path}";
    }

    /// <summary>Puts the groups in order and adds a one-paragraph description to each.</summary>
    private sealed class GroupDescriptionsFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Tags = new List<OpenApiTag>
            {
                new() { Name = Employees, Description = "The people who work at the company. **Start here:** you need an employee before you can assign training." },
                new() { Name = Courses, Description = "The training courses on offer (duration is in hours). Create a course before assigning it." },
                new() { Name = Training, Description = "Assign a course to an employee, follow progress, and see all trainings of one employee. Needs an existing employee id and course id." }
            };
        }
    }
}
