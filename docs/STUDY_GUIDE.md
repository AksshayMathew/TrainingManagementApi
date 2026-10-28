# Study guide: understand the project, then pass the viva

How to use this page: read parts 1-3 once (the story), skim parts 4-7 (the concepts and where they live in the code),
then drill part 8 (flashcards) out loud. Every answer is written the way you can say it: short and simple.

Contents
1. The project in one minute
2. How one request travels
3. The code files, one by one
4. C# concepts used (and where)
5. .NET / ASP.NET Core / EF Core concepts used (and where)
6. SOLID and the classic principles - how they show up
7. Design decisions you must be able to defend
8. Flashcards (92)
9. Your 2-minute explanation + "what would you improve"

---

## 1. The project in one minute

**Problem.** ABC Technologies hired many new employees. HR needs to keep track of employees, the training courses that exist,
and which employee takes which course (with dates and a status).

**What we built.** A **REST API** (a backend with no screen). A program that waits for HTTP requests such as
`POST /api/employees`, talks to a SQL Server database, and answers with JSON and a status code. You test it with
**Swagger** (a web page generated from the API) and **Postman** (a request tool).

**Technology.** C# 12, .NET 8, ASP.NET Core Web API (controllers), Entity Framework Core 8, SQL Server (LocalDB/Express),
xUnit tests, Swagger (Swashbuckle). SQLite is an optional fallback.

**The three tables and how they connect**

```
Employee 1 ----< TrainingAssignment >---- 1 Course
```
An **assignment** is the link row: "employee 5 takes course 2, 1 Feb to 5 Feb, status In Progress".

**The operations (what the assignment asked for)**
- Employees: list, get one, create, update, delete (bonus: search, filter, sort, paging)
- Courses: list, get one, create, update, delete
- Training: assign a course to an employee, update status, view all trainings of an employee, delete an assignment

---

## 2. How one request travels

Example: `POST /api/training-assignments` (assign a course).

```
Client (Swagger / Postman)
  |  1. HTTP request + JSON body
  v
RequestLoggingMiddleware        writes ONE log line when the answer is ready
  v
ExceptionHandlingMiddleware     catches any exception -> clean JSON error (never a crash)
  v
Routing -> TrainingAssignmentsController.Assign(...)
  |  2. ASP.NET turns JSON into TrainingAssignmentRequestDto and VALIDATES it (attributes + IValidatableObject).
  |     Invalid -> automatic 400 with a message per field. The code below never runs.
  v
TrainingAssignmentService.AssignAsync(...)      <- business rules that need the database
  |  3. employee exists? course exists? already assigned?   (404 / 400)
  v
AppDbContext (Entity Framework Core)
  |  4. INSERT (async, parameterised SQL)
  v
SQL Server
  |  5. the database enforces the same rules again: unique index, foreign keys, CHECK constraints
  v
Controller returns 201 Created + Location header + JSON of the new assignment
```

Memorise the sentence: **"Controller handles HTTP, Service holds the rules, DbContext talks to the database."**

---

## 3. The code files, one by one

Paths are inside `TrainingManagementApi/` (the API project) unless noted.

### Start-up and configuration
| File | What it does |
|---|---|
| `Program.cs` | The entry point (top-level statements). Builds the app: registers the database (`AddDbContext`), the three services (`AddScoped`), controllers, logging, Swagger; then sets the **middleware pipeline** in order: request log -> exception handler -> (HTTPS redirect outside Development) -> Swagger -> controllers. It ends with `public partial class Program { }` so the test project can start the app. It also chooses SQL Server or SQLite from `Database:Provider`. |
| `appsettings.json` | Settings: the LocalDB connection string, log levels, and `Database` options (`MigrateOnStartup`, retry count/delay). |
| `appsettings.Development.json` | Development-only log levels. |
| `Properties/launchSettings.json` | How `dotnet run`/F5 starts the app: URL `http://localhost:5282`, opens Swagger. Includes the `http-sqlite` profile. |
| `TrainingManagementApi.csproj` | Project file: .NET 8, nullable reference types, XML documentation (for Swagger text), NuGet packages (EF Core, Swashbuckle, ...). |
| `global.json` (repo root) | Pins the .NET SDK family (8.0.x). |

### Models (the entities - what becomes a table)
| File | What it does |
|---|---|
| `Models/Employee.cs` | The employee entity: id, name, email, department, joining date, experience, and a collection of its training assignments. |
| `Models/Course.cs` | The course entity: id, name, duration (hours), trainer, and its assignments. |
| `Models/TrainingAssignment.cs` | The link entity: employee id, course id, start/end date, status, and navigation properties to `Employee` and `Course`. |
| `Models/TrainingStatuses.cs` | Allowed status values (`Assigned`, `In Progress`, `Completed`) and `TryNormalize` which accepts `in-progress`, `IN PROGRESS`... and returns the canonical text. |
| `Models/FieldLimits.cs` | Constants (max lengths, limits) used by BOTH the database mapping and the validation attributes, so they can never disagree. |

### Data (talking to the database)
| File | What it does |
|---|---|
| `Data/AppDbContext.cs` | The EF Core "session" with the database. Exposes `DbSet<Employee>`, `DbSet<Course>`, `DbSet<TrainingAssignment>`; loads all configuration classes in one line. |
| `Data/Configurations/EmployeeConfiguration.cs` | Fluent API for the Employee table: singular table name, required fields, max lengths, **unique index on Email**, CHECK `Experience >= 0`. |
| `Data/Configurations/CourseConfiguration.cs` | Course table: required fields, CHECK `Duration > 0`. |
| `Data/Configurations/TrainingAssignmentConfiguration.cs` | The relationships: Employee 1-to-many and Course 1-to-many with `DeleteBehavior.Restrict`; **unique index (EmployeeId, CourseId)**; CHECKs for `EndDate >= StartDate` and valid status. |
| `Data/DatabaseInitializer.cs` | An extension method run at start-up: applies EF migrations (or creates the SQLite tables), retries a few times if the database is not ready, and **never throws** (logs instead) so the app cannot crash on start-up. |
| `Migrations/*` | Generated by EF: `InitialCreate` (Employee), `AddCourse`, `AddTrainingAssignment`, plus a model snapshot. They are the version history of the database structure. |

### DTOs (what goes in and out of the API)
| File | What it does |
|---|---|
| `DTOs/EmployeeRequestDto.cs` | Input for create/update employee, with validation attributes and clear messages. Uses `int?`/`DateTime?` so a *missing* number/date is reported instead of silently becoming 0. |
| `DTOs/EmployeeDto.cs` | Output shape of an employee. |
| `DTOs/CourseRequestDto.cs`, `CourseDto.cs` | Same idea for courses. |
| `DTOs/TrainingAssignmentRequestDto.cs` | Input for assigning. Implements `IValidatableObject` for the cross-field rule "end date not before start date". |
| `DTOs/UpdateTrainingStatusDto.cs` | Input for changing the status only. |
| `DTOs/TrainingAssignmentDto.cs` | Output, including employee and course names. |
| `DTOs/TrainingStatusAttribute.cs` | A **custom validation attribute**: accepts only the allowed statuses. |
| `DTOs/EmployeeQuery.cs` | Bonus: the optional query-string options (search, department, sortBy, sortOrder, page, pageSize) with enums and ranges. |
| `DTOs/PagedResult.cs` | A generic `PagedResult<T>`: items + total count (+ page info). |
| `DTOs/MessageDto.cs` | `{ "message": "..." }` returned after a delete. |

### Interfaces and Services (the business rules)
| File | What it does |
|---|---|
| `Interfaces/IEmployeeService.cs`, `ICourseService.cs`, `ITrainingAssignmentService.cs` | The contracts the controllers depend on. |
| `Services/EmployeeService.cs` | Employee logic: trims text, stores e-mail lower-case, checks duplicate e-mail, handles the race where two requests use the same e-mail at once (unique index -> same friendly 400), blocks deleting an employee who has trainings, and the bonus search/filter/sort/paging queries. |
| `Services/CourseService.cs` | Course logic: CRUD, and blocks deleting a course that is assigned. |
| `Services/TrainingAssignmentService.cs` | The core feature: assign (employee exists? course exists? not assigned before? save; handle a race), get one, list for an employee (404 if the employee does not exist), update status, delete. Uses one shared projection (`ToDto`) so the response shape is defined once. |

### Controllers (HTTP only)
| File | What it does |
|---|---|
| `Controllers/EmployeesController.cs` | `GET /api/employees` (+ query options, `X-Total-Count` header), `GET {id}`, `POST` (201 + Location), `PUT {id}`, `DELETE {id}`. |
| `Controllers/CoursesController.cs` | The same five operations for courses. |
| `Controllers/TrainingAssignmentsController.cs` | `POST /api/training-assignments`, `GET /api/training-assignments/{id}`, `GET /api/employees/{employeeId}/trainings`, `PUT .../{id}/status`, `DELETE .../{id}`. |

Controllers contain almost no logic: read the request, call a service, return a status code.

### Errors and logging
| File | What it does |
|---|---|
| `Exceptions/NotFoundException.cs` | Thrown by services when an id does not exist -> becomes 404. |
| `Exceptions/BusinessRuleException.cs` | Thrown when a rule is broken (duplicate e-mail, already assigned...) -> becomes 400 in the same JSON shape as validation errors. |
| `Middleware/ExceptionHandlingMiddleware.cs` | The global safety net: maps the two exceptions to 404/400, anything else to a generic 500 with a `traceId` (details only in the log), ignores client cancellations. |
| `Middleware/RequestLoggingMiddleware.cs` | One log line per API call: `POST /api/employees -> 400 <reason> (240 ms)`. |

### Swagger
| File | What it does |
|---|---|
| `Swagger/SwaggerConfiguration.cs` | Makes the Swagger page friendly: groups in a sensible order with descriptions, a quick-start guide, plain-words explanation of each response code, example error bodies, "Try it out" on by default, the root address redirects to `/swagger`, a small script that puts a fresh e-mail in the create-employee example. |

### Tests, scripts, docs
| File | What it does |
|---|---|
| `TrainingManagementApi.Tests/Infrastructure/ApiFactory.cs` | Starts the real API in memory for tests with its own in-memory SQLite database (or a real SQL Server if `TMS_TEST_SQLSERVER` is set). |
| `...Tests/EmployeesApiTests.cs`, `CoursesApiTests.cs`, `TrainingAssignmentsApiTests.cs` | Call the API over HTTP: happy paths, every validation rule, 404s, duplicates, broken JSON. |
| `...Tests/EmployeeSearchTests.cs` | The bonus features. |
| `...Tests/ErrorHandlingTests.cs`, `StartupResilienceTests.cs` | Prove the app never crashes: unhandled exceptions, database down, bad settings. |
| `...Tests/RequestLoggingTests.cs`, `SwaggerTests.cs`, `SqliteFallbackTests.cs`, `TrainingStatusesTests.cs` | Log lines, Swagger content, the SQLite mode, status parsing. |
| `postman/TrainingManagementApi.postman_collection.json` | 43 requests with test scripts covering every endpoint and the 400/404 cases. |
| `scripts/run-postman-collection.js` | Runs that collection without installing anything (Node). |
| `docs/*` | README, explanations, demo script, requirements trace, this guide. |

---

## 4. C# concepts used (and where)

| Concept | Where | In simple words |
|---|---|---|
| **Classes, properties, encapsulation** | `Models/*`, `DTOs/*` | A class bundles data (properties) and behaviour. `{ get; set; }` is a property. |
| **Interfaces** | `Interfaces/*` | A contract: "any class that can do these things". The controller depends on the contract, not on a concrete class. |
| **Inheritance / implementing** | `EmployeeService : IEmployeeService`, custom exceptions `: Exception`, `TrainingStatusAttribute : ValidationAttribute` | A class can promise to follow an interface or extend a base class. |
| **Generics** | `PagedResult<T>`, `ILogger<EmployeeService>`, `ActionResult<T>`, `IQueryable<Employee>` | One class that works for any type, checked at compile time. |
| **async / await + Task** | every service method, `CancellationToken` | Wait for the database without blocking the thread. `await` pauses *this* method only. |
| **LINQ** | `Where`, `OrderBy`, `Select`, `AnyAsync`, `ToListAsync`, `Skip/Take` in services | Query collections/databases with readable C#; EF translates it to SQL. |
| **Lambda expressions** | `e => e.Email == email` | A tiny anonymous function. |
| **Expression trees** | `Expression<Func<TrainingAssignment, TrainingAssignmentDto>> ToDto` | A lambda kept as *data* so EF Core can translate it into SQL (a normal `Func` cannot). |
| **Nullable reference types** | `string?`, `Nullable enable` in the csproj | The compiler warns when something could be null. `int?` is a *nullable value type*. |
| **Null handling operators** | `?? throw new NotFoundException(...)`, `?.`, `!` | Concise "if null, then ..." code. |
| **Extension methods** | `ApplyMigrationsAsync(this WebApplication)`, `AddApiDocumentation(this IServiceCollection)` | Add methods to an existing type without changing it. |
| **Attributes** | `[ApiController]`, `[Route]`, `[HttpGet]`, `[Required]`, `[Range]`, `[EmailAddress]`, `[ProducesResponseType]` | Metadata that tells the framework what to do. |
| **Custom attribute** | `TrainingStatusAttribute` | Your own validation rule reusable on any property. |
| **Enums** | `EmployeeSortField`, `SortOrder` | A fixed list of named choices; ASP.NET binds `sortBy=Name` to it. |
| **switch expressions / pattern matching** | `ApplySorting`, `OrderKey`, middleware `switch (exception)` | A compact multi-branch decision that returns a value. |
| **Static classes / members** | `FieldLimits`, `TrainingStatuses`, mapping helpers | Things that need no instance. |
| **const / readonly** | `FieldLimits`, `private readonly AppDbContext _db` | Values that never change after being set. |
| **Constructor injection** | every service/controller constructor | Dependencies arrive through the constructor (see DI below). |
| **Exceptions** | `NotFoundException`, `BusinessRuleException`, try/catch around `SaveChangesAsync` | Signal and handle abnormal situations. |
| **Collections** | `List<T>`, `IReadOnlyList<T>`, `Dictionary` | Typed groups of items. |
| **String interpolation / raw strings** | `$"..."`, `"""..."""` in Swagger intro | Readable text building. |
| **Top-level statements, partial class** | `Program.cs` | No `Main` boilerplate; `partial class Program` lets tests reference the entry point. |
| **init-only properties** | `PagedResult<T>` | Settable only when the object is created. |
| **IValidatableObject** | `TrainingAssignmentRequestDto` | Cross-field validation in plain C#. |

---

## 5. .NET / ASP.NET Core / EF Core concepts used (and where)

**ASP.NET Core**
- **Web API with controllers** - `[ApiController]` gives automatic model validation (400), binding from JSON, and problem-details responses.
- **Routing** - `[Route("api/employees")]`, `[HttpGet("{id}")]` map URLs to methods.
- **Model binding** - JSON -> DTO, URL segment -> `int id`, query string -> `EmployeeQuery`.
- **Model validation** - DataAnnotations + `IValidatableObject`; failures return `ValidationProblemDetails` (400).
- **Dependency Injection (DI)** - `AddScoped<IEmployeeService, EmployeeService>()`. *Scoped* = one instance per HTTP request (same lifetime as the DbContext).
- **Middleware pipeline** - a chain each request passes through; order matters (request log -> exception handler -> ... -> controllers).
- **Configuration** - `appsettings.json`, user-secrets, environment variables; later sources override earlier ones.
- **Environments** - `Development` vs `Production` (e.g. error details only shown in Development).
- **Logging** - `ILogger<T>` with levels (info/warn/error), structured message templates.
- **Swagger/OpenAPI** - Swashbuckle generates the JSON description and the UI; XML comments become descriptions.
- **Status codes and `ProblemDetails` (RFC 7807)** - standard error JSON.
- **CancellationToken** - stops work if the client disconnects.

**Entity Framework Core**
- **DbContext / DbSet** - the unit of work and the tables.
- **Code-first + migrations** - C# classes define the database; migrations version it.
- **Fluent API (`IEntityTypeConfiguration<T>`)** - table names, keys, indexes, relationships, constraints.
- **Relationships** - one-to-many with foreign keys; `Restrict` = the database refuses deletes that would orphan rows.
- **Change tracker** - EF remembers loaded entities; `SaveChangesAsync` writes the differences. `AsNoTracking()` is used for read-only queries (faster).
- **Projection** - `Select(ToDto)` loads only the columns needed and joins employee/course in one query.
- **Concurrency/conflicts** - `DbUpdateException` / `DbUpdateConcurrencyException` are caught to turn races into clean answers.
- **Providers** - SQL Server (default) and SQLite (fallback) behind the same model.

**Testing**
- **xUnit** (`[Fact]`, `[Theory]`, `MemberData`), **integration tests** with `WebApplicationFactory<Program>` (real app in memory, real HTTP calls), in-memory SQLite for speed, optional real SQL Server.

---

## 6. SOLID and the classic principles - how they show up

**S - Single Responsibility.** Each class has one reason to change. Controllers: HTTP. Services: rules. `AppDbContext`: data access. DTOs: shapes. Middleware: cross-cutting concerns. `EmployeeConfiguration`: mapping only.

**O - Open/Closed.** Open for extension, closed for modification. Examples: a new endpoint group means a new controller + service, no edits to the old ones; new validation rules are new attributes; EF configurations are picked up automatically by `ApplyConfigurationsFromAssembly`, so a new table needs no change in `AppDbContext.OnModelCreating`; new middleware plugs into the pipeline. *Honest note:* adding a new status still means editing `TrainingStatuses` (one place).

**L - Liskov Substitution.** Any `IEmployeeService` can replace another without the controller noticing. Proof in the code: the tests replace the real service with a `ThrowingEmployeeService` (an alternative implementation) and the controller still works.

**I - Interface Segregation.** Three small, focused interfaces (`IEmployeeService`, `ICourseService`, `ITrainingAssignmentService`) instead of one huge "IEverythingService"; a controller only sees what it needs.

**D - Dependency Inversion.** High-level code (controllers) depends on abstractions (interfaces), not on concrete classes; the concrete classes are wired in `Program.cs` by DI. That is also what makes the services replaceable in tests.

**Classic principles**
- **DRY** (don't repeat yourself): one request DTO shared by POST and PUT; one projection `ToDto`; constants in `FieldLimits`/`TrainingStatuses` used by both validation and the database.
- **KISS**: plain services over `DbContext` (no extra repository layer EF already provides); simple exceptions mapped by one middleware.
- **YAGNI**: no features beyond the assignment except the three listed bonuses.
- **Separation of concerns / layered architecture**: Controller -> Service -> Data.
- **Defence in depth**: each rule is checked in validation, in the service, and in the database.
- **Fail safely**: the global exception handler and the never-throwing start-up code.
- **Encapsulation**: services keep rules private; callers only see the interface.

**Design patterns you can name**: Dependency Injection, Middleware (chain of responsibility), Factory (`WebApplicationFactory`, `ProblemDetailsFactory`), Unit of Work (`DbContext`), DTO, Builder (`WebApplication.CreateBuilder`), Extension-method "fluent" configuration.

---

## 7. Design decisions you must be able to defend

| Decision | Reason |
|---|---|
| DTOs instead of exposing entities | Clients cannot set fields they should not, the database can change without breaking the API, no circular references between Employee and TrainingAssignment. |
| Services + interfaces | Rules reusable and testable; controllers stay thin; DI swaps implementations. |
| No repository layer | `DbContext` already is a unit of work/repository; another layer would only repeat it (KISS). |
| async everywhere | Threads are released while the database works, so the API scales. |
| Custom exceptions + one middleware | Controllers need no try/catch; every failure becomes consistent JSON. |
| 400 for duplicates (not 409) | The assignment's status table lists only 200/201/400/404/500 and puts uniqueness under validation. |
| DELETE returns 200 + message | Same reason (204 is not in the assignment's table). |
| `Restrict` instead of cascade delete | Cascade would silently delete training history. |
| E-mail stored lower-case | "Unique" means the same on every database. |
| `int?` / `DateTime?` in request DTOs | A missing number must be reported, not become 0 or 0001-01-01. |
| Rules enforced 3 times | Friendly message (validation), rule needing a lookup (service), final guarantee even under races (database). |
| Start-up never throws | The API must not crash because the database is late; it retries and logs. |
| Tests on SQLite by default | Fast, nothing to install; the same tests also pass on real SQL Server. |

---

## 8. Flashcards

Format: **Q** the question, **A** the answer in simple words. Cover the answer, say it aloud, then check.

### A. The project
**Q1. What does this project do?**
**A.** It is a REST API for ABC Technologies HR: it manages employees, training courses, and which employee takes which course, with dates and a status.

**Q2. Why is it called a REST API?**
**A.** It exposes resources as URLs (`/api/employees/5`) and uses HTTP verbs for actions: GET reads, POST creates, PUT updates, DELETE removes. It answers with JSON and HTTP status codes.

**Q3. Is there a frontend?**
**A.** No. The assignment asks for the backend only. Swagger and Postman are used to test it. A frontend could call the same API later.

**Q4. Name the three entities and their relationship.**
**A.** Employee, Course, TrainingAssignment. Employee has many assignments, Course has many assignments; the assignment links one employee to one course.

**Q5. Explain the architecture in one sentence.**
**A.** Controller -> Service -> DbContext -> SQL Server: the controller handles HTTP, the service holds the rules, the DbContext talks to the database.

**Q6. Which technologies are used?**
**A.** C#, .NET 8, ASP.NET Core Web API, Entity Framework Core, SQL Server (LocalDB/Express), Swagger, xUnit; SQLite as an optional fallback.

**Q7. Which bonus features did you do?**
**A.** Search by name/department, filtering + sorting + pagination on the employee list, and the global exception middleware.

**Q8. How do you start the project?**
**A.** `dotnet run --project TrainingManagementApi`; the first start creates the database and tables by itself; open `http://localhost:5282` for Swagger.

### B. C# concepts
**Q9. What is async/await and why did you use it?**
**A.** It lets a method wait for slow work (the database) without blocking its thread. The thread can serve other requests meanwhile, so the API handles more users.

**Q10. What is a Task?**
**A.** An object that represents work that will finish later; `await` pauses the method until it is done and gives you the result.

**Q11. What is LINQ?**
**A.** Language Integrated Query: write `Where`, `OrderBy`, `Select` in C#. With EF Core the query is turned into SQL.

**Q12. What is a lambda?**
**A.** A short inline function, e.g. `e => e.Email == email` ("for each employee e, check the email").

**Q13. Why `Expression<Func<...>>` for `ToDto`?**
**A.** An expression is a lambda kept as data, so EF Core can read it and translate it to SQL. A normal `Func` is compiled code EF cannot translate.

**Q14. What are nullable reference types?**
**A.** A compiler feature (`Nullable enable`): `string` is not allowed to be null, `string?` is. It catches null mistakes at compile time.

**Q15. Difference between `int` and `int?`?**
**A.** `int?` can also be null ("no value"). Request DTOs use `int?` for required numbers so a missing value can be detected and reported instead of silently becoming 0.

**Q16. What does `?? throw new NotFoundException(...)` do?**
**A.** "If the result is null, throw this exception instead." One line for the not-found check.

**Q17. What is an extension method?**
**A.** A static method that looks like it belongs to an existing type, e.g. `app.ApplyMigrationsAsync()` or `services.AddApiDocumentation()`, without modifying that type.

**Q18. What is a generic? Example?**
**A.** Code that works for any type: `PagedResult<T>` holds a list of anything plus the total count.

**Q19. What is an interface and why use it?**
**A.** A contract of methods. Code depends on the contract so the real class can be swapped (for tests, or another implementation).

**Q20. What is an attribute?**
**A.** Metadata in square brackets, e.g. `[Required]`, `[HttpGet]`, that tells the framework how to treat a class or method.

**Q21. What is a custom attribute? Where did you use one?**
**A.** Your own attribute. `TrainingStatusAttribute` validates that a status is Assigned, In Progress, or Completed.

**Q22. What is an enum? Where used?**
**A.** A fixed set of named values. `EmployeeSortField` (Id, Name, Department...) and `SortOrder` (Asc, Desc) for the query options.

**Q23. What is a switch expression?**
**A.** A compact `switch` that returns a value, e.g. choosing the sort in `ApplySorting`.

**Q24. What does `readonly` mean?**
**A.** The field can be set only in the constructor, e.g. `private readonly AppDbContext _db;`.

**Q25. What is `partial class Program`?**
**A.** The app uses top-level statements, so the compiler generates `Program`. Declaring it `partial` and `public` lets the test project start the app with `WebApplicationFactory<Program>`.

**Q26. What is an init-only property?**
**A.** A property that can be set only while the object is being created, e.g. in `PagedResult<T>`.

**Q27. try/catch/finally - where used?**
**A.** Around `SaveChangesAsync` to turn database conflicts into friendly errors; in the middleware to turn exceptions into JSON; `finally` in the request logger so the log line is always written.

### C. ASP.NET Core
**Q28. What does `[ApiController]` do?**
**A.** Turns on API conveniences: automatic 400 when validation fails, binding from the JSON body, standard problem-details errors.

**Q29. What is routing?**
**A.** Matching a URL + HTTP verb to a controller method, e.g. `GET /api/employees/5` -> `GetById(5)`.

**Q30. What is model binding?**
**A.** ASP.NET fills method parameters from the request: JSON body -> DTO, URL part -> `int id`, query string -> `EmployeeQuery`.

**Q31. What is Dependency Injection?**
**A.** Instead of creating objects with `new`, the framework creates them and passes them into constructors. We register services in `Program.cs`.

**Q32. What does `AddScoped` mean?**
**A.** One instance per HTTP request. Services and the DbContext share that lifetime. (Singleton = one for the whole app; Transient = new every time.)

**Q33. What is middleware?**
**A.** A component in the request pipeline that can run code before and after the next one. Order matters; ours: request log -> exception handler -> Swagger -> controllers.

**Q34. Why is the exception middleware first (after the logger)?**
**A.** So it wraps everything below it and can catch an exception from any layer.

**Q35. What does the global exception middleware do?**
**A.** NotFoundException -> 404, BusinessRuleException -> 400, anything else -> logged as an error and answered with a generic 500 JSON that has a traceId. The app never shows a stack trace and never crashes from a request.

**Q36. Why not try/catch in every controller?**
**A.** Repeated code. One middleware handles all errors the same way.

**Q37. What is `ProblemDetails`?**
**A.** The standard JSON error format (RFC 7807): `title`, `status`, `detail`, plus our `traceId`.

**Q38. What is the traceId for?**
**A.** It links what the client saw to the log entry, so support can find the exact failure.

**Q39. What is `CreatedAtAction`?**
**A.** Returns 201 Created and a `Location` header pointing to the new resource's URL.

**Q40. What is `ActionResult<T>`?**
**A.** A return type that can be either the data (T) or any HTTP result (404, 400...). Swagger also learns the response type from it.

**Q41. How is configuration read?**
**A.** From `appsettings.json`, then environment-specific files, then user-secrets and environment variables; later sources override earlier ones.

**Q42. What is the difference between Development and Production here?**
**A.** In Development the 500 response shows the real error message (for debugging); in Production it is generic. HTTPS redirect is only used outside Development.

**Q43. What is user-secrets?**
**A.** A per-user store outside the project for private settings such as a connection string, so they are never committed.

**Q44. What is Swagger/OpenAPI?**
**A.** OpenAPI is a standard description of the API; Swagger UI is the page generated from it where you can try each endpoint. Swashbuckle creates both from the code and the `///` comments.

**Q45. What did you do to make Swagger easier to use?**
**A.** Groups in logical order with descriptions, a quick-start guide, plain-words response explanations, realistic examples, "Try it out" on by default, and `/` redirects to Swagger.

**Q46. What does the request-logging middleware add?**
**A.** One readable line per API call with method, URL, status, reason and time, e.g. `POST /api/employees -> 400 An employee with this email already exists. (240 ms)`.

### D. Entity Framework Core and the database
**Q47. What is EF Core?**
**A.** An ORM: it maps C# classes to tables and turns LINQ into SQL, so we rarely write SQL by hand.

**Q48. What is a DbContext?**
**A.** The object representing a session with the database: it holds the `DbSet`s (tables), tracks changes, and saves them.

**Q49. What is code-first? What are migrations?**
**A.** We write the C# model first and EF creates the database. A migration is a versioned script (generated as C#) that changes the database structure; ours are InitialCreate, AddCourse, AddTrainingAssignment.

**Q50. How is the database created when the app starts?**
**A.** `DatabaseInitializer` applies the migrations at start-up (retrying if the database is not ready) and never throws.

**Q51. Why separate configuration classes (Fluent API)?**
**A.** `IEntityTypeConfiguration<T>` keeps table rules (names, lengths, indexes, relationships) out of the entity classes and out of `AppDbContext`; `ApplyConfigurationsFromAssembly` loads them all.

**Q52. How is "email must be unique" guaranteed?**
**A.** Three layers: format validation in the DTO, a duplicate check in the service for a friendly message, and a unique index in the database as the final guarantee (also under simultaneous requests).

**Q53. How is "the same course not twice for an employee" guaranteed?**
**A.** The service checks first; a unique index on (EmployeeId, CourseId) is the final guard. If two requests race, the loser gets the same friendly 400.

**Q54. What is a foreign key? What does `Restrict` do?**
**A.** A column that must point to an existing row. With `Restrict`, the database refuses to delete an employee/course that still has assignments; the service turns that into a clear 400.

**Q55. What is a CHECK constraint? Examples?**
**A.** A rule the database enforces on every row: `Experience >= 0`, `Duration > 0`, `EndDate >= StartDate`, status in the allowed list.

**Q56. What does `AsNoTracking()` do?**
**A.** Tells EF not to track the loaded entities (no change tracking) - faster for read-only queries.

**Q57. What is a projection?**
**A.** Selecting just the fields you need, e.g. `Select(ToDto)`: EF joins Employee and Course and returns only what the response needs, in one query.

**Q58. What does `SaveChangesAsync` do?**
**A.** Writes all tracked changes (insert/update/delete) to the database in one transaction.

**Q59. How do you prevent SQL injection?**
**A.** EF Core always sends parameters, never builds SQL by joining strings. We also tested search text like `'; DROP TABLE Employee;--` - it is treated as plain text.

**Q60. How does paging work?**
**A.** `Skip((page - 1) * pageSize).Take(pageSize)` with a stable order (the Id as a tie-breaker); the total count goes in the `X-Total-Count` header.

**Q61. How does search work and is it case-sensitive?**
**A.** `ToLower().Contains(term)` on name or department, so it ignores case on every database.

**Q62. What happens if SQL Server is down?**
**A.** Start-up retries 5 times then logs a critical message and keeps running; data calls return a clean 500 JSON. When the database is back, requests work again.

### E. HTTP, validation and errors
**Q63. Which status codes do you return and when?**
**A.** 200 OK (read/update/delete), 201 Created (create), 400 invalid input (validation, duplicate, bad id format, blocked delete), 404 not found, 500 unexpected server error.

**Q64. Why 400 and not 409 for duplicates?**
**A.** The assignment's table lists only 200/201/400/404/500 and groups uniqueness under validation. 409 would be a fair refinement.

**Q65. Why does DELETE return 200 with a message instead of 204?**
**A.** 204 is not in the assignment's table; 200 with a short message is clear for the client and within the allowed codes.

**Q66. Assigning a course to a non-existing employee - which code?**
**A.** 404, because the referenced record was not found.

**Q67. `GET /api/employees/abc` or `/api/employees/999999`?**
**A.** `abc` is 400 (not a valid id); 999999 is 404 (valid number, no such employee).

**Q68. How do you validate "end date not before start date"?**
**A.** `TrainingAssignmentRequestDto` implements `IValidatableObject`; its `Validate` method adds an error on `EndDate`. A database CHECK backs it up.

**Q69. Why are some properties `int?`/`DateTime?` in the request DTOs?**
**A.** A plain `int` becomes 0 when the client forgets it. With `int?` plus `[Required]` the client gets "Experience is required".

**Q70. What does a validation error response look like?**
**A.** `{ "title": "...", "status": 400, "errors": { "Email": ["..."] }, "traceId": "..." }` - every wrong field is listed with a clear message.

**Q71. Are status values case-sensitive?**
**A.** No. `in progress`, `IN-PROGRESS` are accepted and stored as `In Progress`; anything else (e.g. `Done`) is a 400 listing the allowed values.

**Q72. Why is the e-mail stored in lower case?**
**A.** So "unique" means the same on every database (SQL Server ignores case, SQLite does not).

### F. Testing
**Q73. How did you test the API?**
**A.** Integration tests with xUnit and `WebApplicationFactory`: they start the real app in memory and call it over HTTP. Plus a Postman collection (43 requests) and manual testing in Swagger.

**Q74. Why SQLite in tests?**
**A.** It is fast and needs nothing installed. The same tests also pass on real SQL Server by setting `TMS_TEST_SQLSERVER`.

**Q75. What is `[Fact]` vs `[Theory]`?**
**A.** `[Fact]` is one test; `[Theory]` runs the same test with several data sets (`InlineData`/`MemberData`), e.g. all invalid employee bodies.

**Q76. How did you prove the app does not crash?**
**A.** Tests for unhandled exceptions, a missing/unreachable database, bad settings; and a stress run of about 9,500 hostile requests with zero 500 errors and the app still running.

**Q77. How did you test two users acting at the same time?**
**A.** On real SQL Server, 8 simultaneous requests with the same e-mail / same course: exactly one succeeds, the others get a clean 400.

**Q78. What does Postman add?**
**A.** A saved, repeatable set of requests with automatic checks and variables (it passes the new ids from one request to the next).

### G. SOLID and design
**Q79. Explain SOLID with your project.**
**A.** S: controller/service/DbContext each do one job. O: add a controller+service without editing old ones; configurations load automatically. L: any `IEmployeeService` can replace another (tests swap in a failing one). I: three small service interfaces. D: controllers depend on interfaces; DI supplies the classes.

**Q80. Where do you use Dependency Inversion exactly?**
**A.** `EmployeesController` takes `IEmployeeService` in its constructor; `Program.cs` registers `EmployeeService` for it.

**Q81. Where do you follow DRY?**
**A.** One request DTO for POST and PUT, one projection `ToDto` for assignments, shared constants in `FieldLimits`/`TrainingStatuses`.

**Q82. Why no repository pattern?**
**A.** EF Core's `DbContext` is already a unit of work and repository; another layer would only repeat it (KISS/YAGNI).

**Q83. What is defence in depth?**
**A.** The same rule is checked in several layers (validation, service, database) so no bug or race condition can break it.

**Q84. What is separation of concerns?**
**A.** Each part has a clear job: HTTP in controllers, rules in services, storage in the data layer, errors in middleware.

### H. Tricky and "improve it" questions
**Q85. What would you improve for production?**
**A.** Authentication/authorization (JWT + roles), concurrency tokens so two editors do not overwrite each other, soft delete/audit log, run migrations in the deployment pipeline, health checks, structured logging, protect or disable Swagger.

**Q86. Known limitations?**
**A.** No authentication, hard deletes, dates carry no time zone, last write wins on simultaneous edits, Swagger is open in every environment.

**Q87. A client sends the same create request twice quickly. What happens?**
**A.** For employees: the first is created (201), the second gets 400 "email already exists" - even if both arrive at the same instant, because the unique index lets only one in.

**Q88. What if two people delete the same employee?**
**A.** One gets 200; the other gets 404 (the service catches the "row already gone" conflict).

**Q89. Why is the API not using the entity classes as responses?**
**A.** DTOs keep the API stable and safe: no accidental exposure of fields, no circular Employee <-> Assignment references, and validation lives on the input DTOs.

**Q90. What happens when the connection string is wrong at start-up?**
**A.** The app logs a clear critical message after the retries and keeps running; data endpoints answer a generic 500 until it is fixed and the app restarted.

**Q91. How would you add authentication?**
**A.** `AddAuthentication().AddJwtBearer(...)`, a login endpoint that issues tokens, and `[Authorize(Roles = "HR")]` on the write endpoints.

**Q92. How would you add a new entity, say "Trainer"?**
**A.** Add the model, a configuration class, a migration, DTOs, a service + interface registered in `Program.cs`, and a controller. Existing code stays unchanged (Open/Closed).

---

## 9. Your 2-minute explanation (say it in your own words)

"ABC Technologies needs HR to track employees, courses, and who is taking which course, so I built a REST API in .NET 8 with ASP.NET Core
and Entity Framework Core on SQL Server. It has three tables: Employee, Course, and TrainingAssignment, which links an employee to a course
with dates and a status. The code is layered: controllers only handle HTTP, services hold the business rules, and the DbContext talks to the
database. I used DTOs for input and output, dependency injection with interfaces, async/await for all database calls, and EF migrations to create the database.
Validation happens in three places: attributes on the request, checks in the service, and constraints in the database, which also protects against
two users acting at the same time. Errors are handled in one global exception middleware that returns clean JSON with a trace id, so the API never crashes.
I tested it with 186 automated tests, a Postman collection and Swagger. As bonuses I added search by name or department, filtering, sorting and paging, and the global
middleware. For production I would add JWT authentication, concurrency tokens and health checks."

**Demo order (10 minutes):** problem -> architecture (Program.cs, a controller, a service) -> database relationship (TrainingAssignmentConfiguration) ->
one API end to end in Swagger (create employee, create course, assign, view, change status) -> validation and errors (duplicate e-mail, wrong id, delete with trainings) ->
one improvement for production.
