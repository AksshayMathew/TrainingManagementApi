# 10-minute demo script

The assignment (section 13) asks for a 10-minute walkthrough covering: **the business problem, the architecture, the database
relationship, one API end to end, validation / error handling, and one area to improve for production.**
Below is a minute-by-minute plan. Say it in your own words - the bullets are prompts, not a script to read out.

## Before you start (2 minutes of preparation)
- Start the API (`dotnet run --project TrainingManagementApi`, or F5 in Visual Studio) and open Swagger: http://localhost:5282/swagger
- Have these files open in tabs: `Program.cs`, `Controllers/TrainingAssignmentsController.cs`,
  `Services/TrainingAssignmentService.cs`, `Data/Configurations/TrainingAssignmentConfiguration.cs`,
  `Middleware/ExceptionHandlingMiddleware.cs`.
- Have SQL Server Object Explorer (Visual Studio) or SSMS open on `TrainingManagementDb`.
- Optional: run `dotnet test` once beforehand so you can say "187 tests, all green".

## Timeline

### 0:00 - 1:30 The business problem
- ABC Technologies hired new employees; HR needs to know **who the employees are, which courses exist, and who is taking what**.
- I built the **backend REST API**: .NET 8, ASP.NET Core Web API, EF Core, SQL Server, documented with Swagger.

### 1:30 - 3:30 Architecture
- Show the solution tree. Say: **Controller -> Service -> Data (EF Core)**.
  - Controller: only HTTP (read request, call service, return status code).
  - Service: the business rules and the database queries.
  - DbContext: talks to SQL Server.
- Mention DTOs (what the API accepts/returns), interfaces + dependency injection (`Program.cs`), async/await everywhere,
  global exception middleware, logging.
- Open `Program.cs` and point at: `AddDbContext`, `AddScoped<IEmployeeService, EmployeeService>`, `UseMiddleware<ExceptionHandlingMiddleware>`.

### 3:30 - 5:00 Database and the relationship
- Show the three tables in SQL Server Object Explorer / the diagram in `docs/EXPLAINED.md`.
- **Employee 1-to-many TrainingAssignment, Course 1-to-many TrainingAssignment.** Assignment = link row with dates and status.
- Open `TrainingAssignmentConfiguration.cs`: two foreign keys, **unique index (EmployeeId, CourseId)** = "same course not twice", CHECK constraints.
- Show `Migrations/` and say the database is created from code (`dotnet ef`), also available as `docs/database/create-database.sql`.

### 5:00 - 7:30 One API end to end: "assign a course to an employee"
In Swagger:
1. `POST /api/employees` -> **201**, point at the `Location` header and the returned id.
2. `POST /api/courses` -> **201** (note `duration` is hours).
3. `POST /api/training-assignments` with those ids -> **201**; status is `Assigned`.
4. `GET /api/employees/{id}/trainings` -> the list shows employee, course, trainer, dates.
5. `PUT /api/training-assignments/{id}/status` with `"In Progress"` -> **200**.
Then show the code path in the editor: controller -> `AssignAsync` (exists? duplicate? save) -> SQL (visible in the console
because Development logs EF's SQL).

### 7:30 - 9:15 Validation and error handling
In Swagger, one failure of each kind (this is the part reviewers like):
- Empty name / negative experience -> **400** listing **every** wrong field.
- Same e-mail again -> **400** "An employee with this email already exists."
- Assign the same course again -> **400**; end date before start date -> **400**.
- `GET /api/employees/99999` -> **404** with a readable message; `GET /api/employees/abc` -> **400**.
- Try to delete an employee that has trainings -> **400** (history is protected).
Then open `ExceptionHandlingMiddleware.cs`: "any unexpected exception becomes a clean 500 with a traceId; the details only go to the log".
If asked, show that the API survives a stopped database: tests `StartupResilienceTests` / `ErrorHandlingTests`.

### 9:15 - 10:00 One thing I would improve for production
Pick **one** and explain it well (the README, section 7, has more):
- **Authentication/authorization** (JWT + roles): today anyone can call every endpoint; HR vs employee roles would restrict deletes.
- or **optimistic concurrency** (a row version) so two HR users editing the same employee can't overwrite each other silently.
- or **run migrations in the deployment pipeline** and turn off start-up migration, plus health checks and structured logging.

Closing line: *"187 automated tests pass on SQLite and on real SQL Server, and the Postman run of 43 requests has no failures."*

---

## Questions you may be asked (with short answers)

**Why a service layer? Why not put the code in the controller?**
Controllers should only deal with HTTP. Rules in a service can be reused, tested and changed without touching the web layer.

**Why interfaces for services?**
So the controller depends on an abstraction, DI can inject any implementation, and tests can replace it (we replace the employee service with one that throws, to test error handling).

**What is a DTO and why not return the entity?**
A DTO is a class made only for the API contract. Returning entities would expose database structure, risk over-posting (a client setting fields it shouldn't) and create circular references between Employee and TrainingAssignment.

**How is "email must be unique" guaranteed?**
Three layers: DTO validation (format), service check (friendly message), **unique index** in SQL Server (cannot be bypassed). Under simultaneous requests the index wins and the service converts the failure into the same 400.

**Why 400 and not 409 for duplicates?**
The assignment's status table has no 409 and lists uniqueness under validation. 409 would be an acceptable refinement; it is a one-line change in the middleware/exceptions.

**What does `async/await` buy you?**
The thread is released while SQL Server works, so the API can serve many requests with few threads. `CancellationToken` stops the work when the client disconnects.

**What are migrations?**
Versioned changes to the database structure generated from the model: `InitialCreate`, `AddCourse`, `AddTrainingAssignment`. `Database.MigrateAsync()` applies the ones not yet applied.

**What happens if SQL Server is down?**
Start-up retries 5 times then logs a critical error but keeps running; data calls return a clean 500 JSON. When the database is back, requests work again (restart only if the schema was never created).

**Why `DeleteBehavior.Restrict`?**
Cascade would silently delete training records when an employee is deleted. We block the delete and tell the client why.

**Why is `Experience` an `int?` in the request class?**
So a missing value is reported ("Experience is required") instead of silently becoming 0. The entity itself uses a plain `int`.

**How do you prevent SQL injection?**
EF Core always sends parameters, never concatenated strings (we also tested a `'; DROP TABLE` search text - treated as plain text).

**How would you add authentication?**
`AddAuthentication().AddJwtBearer(...)`, issue tokens from a login endpoint, add `[Authorize(Roles = "HR")]` to the write endpoints.

**Why does Swagger show text from the code?**
`<GenerateDocumentationFile>` writes the `///` comments to an XML file that Swashbuckle reads (`IncludeXmlComments` in `Program.cs`).

**How do you know it works?**
187 integration tests (SQLite and real SQL Server), a 43-request Postman run, and a Swagger export - all in `docs/evidence` or reproducible with one command.

**What would you do differently with more time?**
Auth, concurrency tokens, soft delete + audit log, status workflow rules (e.g. no going back from Completed), health checks, CI pipeline running the tests.
