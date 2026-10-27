# Employee Training Management System (ABC Technologies)

A REST API, built with **.NET 8 / ASP.NET Core Web API, C#, Entity Framework Core and SQL Server**, that manages
employees, training courses and the training assigned to each employee.

| | |
|---|---|
| Runtime / SDK | .NET 8 |
| API | ASP.NET Core Web API (controllers) |
| Data | Entity Framework Core 8 + SQL Server (LocalDB / Express / Docker) |
| API docs | Swagger / OpenAPI (Swashbuckle) |
| Tests | xUnit integration tests (187) + Postman collection (43 requests) |

> New to the project? Read [docs/EXPLAINED.md](docs/EXPLAINED.md) first - it explains the problem, the approach and
> the reasons behind each decision in plain language. Preparing the demo? See [docs/DEMO_SCRIPT.md](docs/DEMO_SCRIPT.md).

---

## 1. Setup and run

### Prerequisites
- **.NET 8 SDK** (`dotnet --list-sdks` should show `8.0.x`)
- **SQL Server**: LocalDB (comes with Visual Studio) or SQL Server Express - or the optional Docker SQL Server below
- **Git**
- Visual Studio 2022 (17.8 or newer) **or** VS Code with the C# Dev Kit

### Run it (any editor)
```bash
dotnet restore
dotnet run --project TrainingManagementApi
```
Then open **http://localhost:5282** (it redirects to Swagger, which starts with a short "How to use this page" guide, lists Employees first and has "Try it out" switched on).

The first start creates the database `TrainingManagementDb` and applies the EF Core migrations by itself
(`Database:MigrateOnStartup`, see appsettings.json). Nothing else to set up.

### Run it in Visual Studio
1. Open `TrainingManagementApi.sln`.
2. Make sure `TrainingManagementApi` is the startup project (right-click > *Set as Startup Project*).
3. Press **F5**. The browser opens on Swagger (`launchUrl` is `swagger`).
4. Optional: *View > SQL Server Object Explorer* to look at the database, *Test > Test Explorer* to run the tests.

### Which database does it use?
The default connection string (`appsettings.json`) points at **SQL Server LocalDB**:

```
Server=(localdb)\MSSQLLocalDB;Database=TrainingManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

| You have | Do this |
|---|---|
| Visual Studio / LocalDB | Nothing. Check with `sqllocaldb info` (should list `MSSQLLocalDB`; create it with `sqllocaldb create MSSQLLocalDB`). |
| SQL Server Express | Use `Server=.\SQLEXPRESS;Database=TrainingManagementDb;Trusted_Connection=True;TrustServerCertificate=True` (see below how to set it). |
| Neither (e.g. personal laptop) | Use the Docker SQL Server below. |

To change the connection string **without editing tracked files**, use user-secrets (only applies on your machine):
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>" --project TrainingManagementApi
```
or an environment variable: `ConnectionStrings__DefaultConnection`.

### Optional: SQL Server in Docker
```bash
copy .env.example .env          # then edit .env and choose your own SA password
docker compose up -d            # starts SQL Server 2022 on 127.0.0.1,1433
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\use-docker-sqlserver.ps1   # once: stores the connection string for you
dotnet run --project TrainingManagementApi
```
The script reads the password from your `.env` and saves the connection string in your user-secrets store. Doing it by hand instead:
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1,1433;Database=TrainingManagementDb;User Id=sa;Password=<your password>;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true" --project TrainingManagementApi`

**Seeing `The system cannot find the file specified` / "LocalDB" errors at start-up?** The API is using the default LocalDB connection
string because no override is stored for your Windows user. Run the script above (user-secrets belong to one Windows account, so they
must be set from the same account/terminal that runs `dotnet run`), then restart the API.
Use `127.0.0.1`, not `localhost`, in that connection string: the container is published on the IPv4 loopback address only, and
`localhost` may resolve to the IPv6 address first, which refuses the connection.
SQL Server needs about 20-30 seconds to start; if the API starts sooner it retries for about 15 seconds before giving up (see *Resilience*).

### Database creation script / migrations
- EF Core migrations live in `TrainingManagementApi/Migrations` (`InitialCreate`, `AddCourse`, `AddTrainingAssignment`).
- A ready-made SQL script is in [docs/database/create-database.sql](docs/database/create-database.sql) (idempotent: safe to run twice).
- Apply migrations by hand instead of on start-up: `dotnet tool restore` then
  `dotnet ef database update --project TrainingManagementApi` (Visual Studio: Package Manager Console > `Update-Database`).
- Create a new migration after changing a model: `dotnet ef migrations add <Name> --project TrainingManagementApi --output-dir Migrations`.
- Regenerate the SQL script: `dotnet ef migrations script --idempotent --project TrainingManagementApi --output docs/database/create-database.sql`
  (then re-add the `CREATE DATABASE` header comment block at the top).

---

## 2. API list

Base URL: `http://localhost:5282`. All bodies are JSON. Every endpoint is documented and testable in Swagger.

### Employees
| Method | URL | What it does | Success | Possible errors |
|---|---|---|---|---|
| GET | `/api/employees` | List employees (optional search/filter/sort/paging, see below) | 200 | 400 invalid option |
| GET | `/api/employees/{id}` | One employee | 200 | 400 bad id, 404 |
| POST | `/api/employees` | Create | 201 + `Location` | 400 validation / duplicate e-mail |
| PUT | `/api/employees/{id}` | Replace all fields | 200 | 400, 404 |
| DELETE | `/api/employees/{id}` | Delete | 200 | 404, 400 if the employee still has trainings |

Optional query options for the list (bonus): `search` (name or department), `department`, `sortBy`
(`Id`, `Name`, `Department`, `JoiningDate`, `Experience`), `sortOrder` (`Asc`/`Desc`), `page`, `pageSize` (1-100, default 10).
Without options you get everybody. The `X-Total-Count` header holds the number of matches (`X-Page`/`X-Page-Size` when paging).
Example: `GET /api/employees?search=priya&sortBy=Experience&sortOrder=Desc&page=1&pageSize=5`

### Courses
| Method | URL | What it does | Success | Possible errors |
|---|---|---|---|---|
| GET | `/api/courses` | List courses | 200 | |
| GET | `/api/courses/{id}` | One course | 200 | 400, 404 |
| POST | `/api/courses` | Create | 201 + `Location` | 400 |
| PUT | `/api/courses/{id}` | Replace all fields | 200 | 400, 404 |
| DELETE | `/api/courses/{id}` | Delete | 200 | 404, 400 if still assigned |

### Training assignments
| Method | URL | What it does | Success | Possible errors |
|---|---|---|---|---|
| POST | `/api/training-assignments` | **Assign a course to an employee** | 201 + `Location` | 400 (duplicate, dates, status), 404 (unknown employee/course) |
| GET | `/api/employees/{employeeId}/trainings` | **View all trainings of an employee** | 200 (empty list if none) | 400, 404 unknown employee |
| PUT | `/api/training-assignments/{assignmentId}/status` | **Update the status** | 200 | 400 invalid status, 404 |
| DELETE | `/api/training-assignments/{assignmentId}` | **Delete an assignment** | 200 | 404 |
| GET | `/api/training-assignments/{assignmentId}` | One assignment | 200 | 400, 404 |

### Examples
Create an employee:
```http
POST /api/employees
Content-Type: application/json

{ "employeeName": "Priya Sharma", "email": "priya.sharma@abctechnologies.com", "department": "Engineering",
  "joiningDate": "2026-01-15T00:00:00", "experience": 3 }
```
```http
201 Created
Location: http://localhost:5282/api/employees/1

{ "employeeId": 1, "employeeName": "Priya Sharma", "email": "priya.sharma@abctechnologies.com",
  "department": "Engineering", "joiningDate": "2026-01-15T00:00:00", "experience": 3 }
```
Assign a course:
```http
POST /api/training-assignments
{ "employeeId": 1, "courseId": 1, "startDate": "2026-02-01T00:00:00", "endDate": "2026-02-05T00:00:00" }
```
```http
201 Created
{ "assignmentId": 1, "employeeId": 1, "employeeName": "Priya Sharma", "courseId": 1,
  "courseName": "ASP.NET Core Web API Fundamentals", "trainerName": "Anita Desai",
  "startDate": "2026-02-01T00:00:00", "endDate": "2026-02-05T00:00:00", "status": "Assigned" }
```
Validation error (every problem is listed, keyed by field):
```http
400 Bad Request
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "EmployeeName": ["Employee name is required."],
              "Experience": ["Experience cannot be negative and cannot be more than 60 years."] },
  "traceId": "00-..." }
```
Not found:
```http
404 Not Found
{ "title": "Resource not found.", "status": 404, "detail": "Employee with id 999 was not found.", "traceId": "0H..." }
```

### Status codes used
| Situation | Status |
|---|---|
| Successful read / update / delete | 200 OK |
| Successful create | 201 Created (with a `Location` header) |
| Invalid input (validation, duplicate, business rule, bad id format, broken JSON) | 400 Bad Request |
| Record not found | 404 Not Found |
| Unhandled server error | 500 Internal Server Error (generic JSON, details only in the log) |

### Validation rules
| Rule | Where it is enforced |
|---|---|
| Employee name required (max 100) | request validation |
| E-mail required, valid format, **unique** (not case-sensitive) | request validation + service check + unique index |
| Department required (max 100) | request validation |
| Joining date required | request validation |
| Experience required, 0 to 60 | request validation + database CHECK |
| Course name and trainer required | request validation |
| Course duration greater than 0 (hours) | request validation + database CHECK |
| Training start/end dates required, **end date not before start date** | request validation + database CHECK |
| Status is Assigned / In Progress / Completed (any casing) | request validation + database CHECK |
| **Same course cannot be assigned to an employee twice** | service check + unique index (EmployeeId, CourseId) |
| Employee and course must exist when assigning | service (404) + foreign keys |
| Employee/course with trainings cannot be deleted | service (400) + foreign keys (Restrict) |

---

## 3. Project structure

```
TrainingManagementApi.sln
global.json                          pins the SDK family (8.0.x)
.config/dotnet-tools.json            dotnet-ef as a local tool
docker-compose.yml, .env.example     OPTIONAL SQL Server for development
TrainingManagementApi/               the API project
  Controllers/    HTTP only: read the request, call a service, return the result
  Services/       business rules + EF Core queries (EmployeeService, CourseService, TrainingAssignmentService)
  Interfaces/     IEmployeeService, ICourseService, ITrainingAssignmentService (what the controllers depend on)
  DTOs/           shapes of requests/responses + validation attributes
  Models/         entities that become tables (Employee, Course, TrainingAssignment) + constants
  Data/           AppDbContext, per-table Configurations/, DatabaseInitializer (migrate on start-up)
  Exceptions/     NotFoundException, BusinessRuleException
  Middleware/     ExceptionHandlingMiddleware (global error handling)
  Migrations/     EF Core migrations (the database history)
  Program.cs      wiring: DI, Swagger, middleware, start-up
  appsettings*.json
TrainingManagementApi.Tests/         xUnit integration tests
postman/                             Postman collection
scripts/                             run-postman-collection.js (no-install runner), export-step.ps1 (transfer helper)
docs/                                explanations, demo script, SQL script, test evidence
```

Request flow: **Controller -> Service -> AppDbContext (EF Core) -> SQL Server**. Details in [docs/EXPLAINED.md](docs/EXPLAINED.md).

### Database design
```
Employee 1 ----< TrainingAssignment >---- 1 Course
```
| Table | Key columns | Constraints |
|---|---|---|
| `Employee` | `EmployeeId` (PK, identity), `EmployeeName`, `Email`, `Department`, `JoiningDate`, `Experience` | unique index on `Email`; CHECK `Experience >= 0` |
| `Course` | `CourseId` (PK), `CourseName`, `Duration`, `TrainerName` | CHECK `Duration > 0` |
| `TrainingAssignment` | `AssignmentId` (PK), `EmployeeId` (FK), `CourseId` (FK), `StartDate`, `EndDate`, `Status` | unique (`EmployeeId`,`CourseId`); CHECK `EndDate >= StartDate`; CHECK valid `Status`; both FKs `ON DELETE NO ACTION` |

---

## 4. Testing

| What | How | Where the proof is |
|---|---|---|
| Automated tests (187) | `dotnet test` (uses in-memory SQLite, nothing to install) | |
| Same tests on real SQL Server | set `TMS_TEST_SQLSERVER` to a connection string **without** a database name, then `dotnet test` - each test class builds a throw-away database from the real migrations and drops it afterwards | all 187 passed |
| Manual / Swagger | run the API, open `/swagger`, *Try it out* | [docs/evidence/swagger-ui-overview.png](docs/evidence/swagger-ui-overview.png), [docs/evidence/openapi.json](docs/evidence/openapi.json) |
| Postman | import [postman/TrainingManagementApi.postman_collection.json](postman/TrainingManagementApi.postman_collection.json), set `baseUrl`, run the collection | |
| Postman without Postman (**the API must be running in another window first**) | `node scripts/run-postman-collection.js postman/TrainingManagementApi.postman_collection.json --report docs/evidence/api-test-run.md` (Node 18+, no install) | [docs/evidence/api-test-run.md](docs/evidence/api-test-run.md) - 43 requests, 74 assertions, 0 failures against SQL Server |

Example for the real-SQL-Server run (PowerShell):
```powershell
$env:TMS_TEST_SQLSERVER = "Server=127.0.0.1,1433;User Id=sa;Password=<your password>;TrustServerCertificate=True;Encrypt=False"
dotnet test
```

---

## 5. Logging, error handling and resilience
- **Global exception middleware** (`Middleware/ExceptionHandlingMiddleware.cs`): turns *any* exception into JSON
  (problem details + `traceId`). Expected problems (404/400) show up as one warning line per call in the request log; unexpected ones as errors with the full exception
  while the client only sees a generic 500 (the reason is shown only in the Development environment).
- **Logging**: `ILogger` is used for important operations (create/update/delete/assign - ids only, no personal data) and all errors.
  Output goes to the console as ONE line per API call, e.g. `POST /api/employees -> 400 An employee with this email already exists. (240 ms)` (SQL statements are no longer printed).
- **Start-up resilience**: migrations are retried (5 times, 3 s apart - `Database:StartupRetries`, `Database:StartupRetryDelaySeconds`)
  because the database server may still be starting. If it never comes up the problem is logged and the API **keeps running**
  (data calls return a clean 500). Wrong values in settings fall back to defaults instead of crashing.
- **Race conditions**: two requests creating the same e-mail / assigning the same course at the same time cannot both succeed
  (unique indexes) and the loser gets a normal 400, not a 500 (tested against real SQL Server).

---

## 6. Assumptions
The assignment leaves some details open. These are the decisions taken (each is easy to change):

1. **Duration** of a course is in **hours**.
2. **E-mail** is trimmed and stored in lower case; uniqueness therefore ignores case (`A@x.com` = `a@x.com`).
3. `JoiningDate`, `Experience`, `StartDate` and **`EndDate` are required** (the assignment lists a rule for each, so a missing value is reported rather than silently becoming 0 or 0001-01-01).
   `EndDate` may equal `StartDate`. Experience is limited to 0-60 years, course duration to 1-10000, as sanity bounds.
4. **Duplicate e-mail, duplicate assignment and a blocked delete return 400**, because the assignment's status-code table has no 409 and lists these under "validation".
5. **DELETE returns 200 with a short message** (the table has no 204).
6. An employee can be assigned a course **only once, ever** - even after completing it. To re-assign, delete the old assignment first.
7. Assigning to an **unknown employee or course returns 404** ("record not found").
8. **Status** is accepted in any casing (`in progress`, `IN-PROGRESS`) and stored as `Assigned` / `In Progress` / `Completed`; it defaults to `Assigned`. Any status may change to any other (no workflow rules were requested).
9. **Employees or courses that still have training assignments cannot be deleted** (400 with a clear message) so training history is never lost by accident.
10. `PUT` replaces all fields (full update).
11. A non-numeric id (`/api/employees/abc`) is a **400**; an id that is a number but does not exist (or is 0/negative) is a **404**.
12. Dates are **calendar dates without time zone** - send them like `2026-01-15T00:00:00` (no `Z` / offset).
13. Course names are **not unique** (the same course can be run by different trainers).
14. Swagger is enabled in every environment because this is a training project.

## 7. Known limitations and ideas for production
- **No authentication / authorization** (JWT + roles is a listed bonus, not implemented).
- Not implemented bonuses: e-mail notification after assignment, Dockerising the API, Azure App Service deployment.
- Swagger is open in every environment - restrict or disable it in production.
- No optimistic concurrency tokens: with simultaneous edits of the same row the last save wins.
- Hard deletes only (no soft delete / audit trail); no history of status changes.
- Dates carry no time zone; sorting/paging exists only for the employee list.
- Migrations run automatically at start-up for convenience; in production run them as a deployment step and set `Database:MigrateOnStartup` to `false`.
- Logging is console-only; a real deployment would add a structured sink (Serilog/Application Insights), health checks and rate limiting.

## 8. More documentation
| Document | Purpose |
|---|---|
| [docs/EXPLAINED.md](docs/EXPLAINED.md) | The assignment in simple words: what, how and **why** |
| [docs/DEMO_SCRIPT.md](docs/DEMO_SCRIPT.md) | 10-minute demo plan + likely questions and answers |
| [docs/REQUIREMENTS_TRACE.md](docs/REQUIREMENTS_TRACE.md) | Every requirement -> the code and tests that satisfy it |
| [docs/TRANSFER_GUIDE.md](docs/TRANSFER_GUIDE.md) | Moving the project to another laptop one commit at a time |

_The original assignment brief is an internal document, so it is intentionally not part of this repository._
