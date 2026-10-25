# The assignment explained in simple words

This page answers four questions: **What is the problem? What did we build? How does it work? Why did we choose
this approach?** Read it before the demo - it is also your cheat sheet when someone asks "why did you do it like this?".

---

## 1. The problem (in one minute)

ABC Technologies has many new employees. HR needs a small system to keep track of:

1. **Who the employees are** (name, e-mail, department, when they joined, years of experience).
2. **Which training courses exist** (name, how long, who teaches it).
3. **Who is taking which course** (and when, and whether they have started or finished).

We are asked to build only the **backend**: a *REST API*. That is a program that waits for requests such as
"create this employee" or "show me all trainings of employee 5", talks to a database, and answers with JSON.
There is no screen; testers use **Swagger** (a web page generated from the API) or **Postman** to call it.

## 2. What we built

Three things (tables) and how they connect:

```
   +-------------+          +---------------------+          +-------------+
   |  Employee   | 1      * |  TrainingAssignment | *      1 |   Course    |
   |-------------|----------|---------------------|----------|-------------|
   | EmployeeId  |          | AssignmentId        |          | CourseId    |
   | Name, Email |          | EmployeeId  (FK)    |          | CourseName  |
   | Department  |          | CourseId    (FK)    |          | Duration    |
   | JoiningDate |          | StartDate, EndDate  |          | TrainerName |
   | Experience  |          | Status              |          +-------------+
   +-------------+          +---------------------+
```

- One **employee** can have many **assignments**; one **course** can appear in many **assignments**.
- A **TrainingAssignment** is the "link" row that says *"Employee 5 takes Course 2 from 1 Feb to 5 Feb, status: In Progress"*.
  (In database language: two one-to-many relationships; together they model a many-to-many between employees and courses.)

And the operations (endpoints) required by the assignment:

| Area | Operations |
|---|---|
| Employees | list, get one, create, update, delete (+ bonus: search / filter / sort / paging) |
| Courses | list, get one, create, update, delete |
| Training | assign a course to an employee, update its status, view all trainings of an employee, delete an assignment |

## 3. How one request travels (follow the code)

Example: **"Assign course 2 to employee 5"** - `POST /api/training-assignments`

```
Client (Swagger/Postman)
   |  1. HTTP request with a JSON body
   v
ExceptionHandlingMiddleware      <- wraps everything; catches any exception and answers with clean JSON
   |
   v
TrainingAssignmentsController    <- 2. ASP.NET reads the JSON into a TrainingAssignmentRequestDto and
   |                                   VALIDATES it (required fields, end date >= start date, valid status).
   |                                   If invalid -> automatic 400 with a message per field. Done.
   v
TrainingAssignmentService        <- 3. Business rules that need the database:
   |                                   - does employee 5 exist?  does course 2 exist?   (else 404)
   |                                   - is employee 5 already assigned course 2?      (else 400)
   v
AppDbContext (Entity Framework)  <- 4. Saves the new row with INSERT ... (async, parameterised SQL)
   |
   v
SQL Server                       <- 5. The database enforces the same rules again (unique index, foreign keys, CHECKs)
   |
   v
Controller returns 201 Created + Location header + the new assignment as JSON
```

The folders mirror this: `Controllers/` -> `Services/` -> `Data/`, with `DTOs/`, `Models/`, `Interfaces/`,
`Exceptions/`, `Middleware/` and `Migrations/` supporting them. This is exactly the "Controller -> Service -> Data/EF Core
separation" the assignment asks for.

## 4. The approach and **why** (the decisions)

| Decision | What we did | Why |
|---|---|---|
| **Layers** | Controller (HTTP only) -> Service (rules) -> DbContext (data) | Each class has one job, so code is short, readable and testable. Changing a rule never touches HTTP code. |
| **Interfaces + Dependency Injection** | Controllers depend on `IEmployeeService`, not on the class; `Program.cs` registers the real class | The assignment asks for DI. It also lets tests swap a service for a fake one (we do this to prove error handling). |
| **DTOs** (Data Transfer Objects) | Separate classes for what comes in (`EmployeeRequestDto`) and goes out (`EmployeeDto`) instead of exposing the database entity | Clients can't set fields they shouldn't (e.g. the Id), the database can change without breaking the API, and validation lives in one place. One request DTO is shared by POST and PUT to avoid duplicated code. |
| **async / await** | Every database call is `async` and takes a `CancellationToken` | A waiting request doesn't block a thread, so the server handles many users; if the client disconnects, the work is cancelled. |
| **EF Core migrations** | `InitialCreate` -> `AddCourse` -> `AddTrainingAssignment`, applied automatically at start-up | The database structure is created from code, versioned in Git and identical on every laptop. A SQL script is also provided. |
| **Table names singular, Fluent API configuration** | `Employee`, `Course`, `TrainingAssignment` (as in the assignment) configured in `Data/Configurations` | Matches the assignment exactly; keeps the entity classes clean. |
| **Rules enforced twice (or three times)** | DTO validation -> service check -> database constraint | *Defence in depth.* The first gives friendly messages, the service handles rules needing a lookup, the database is the final guard that no bug or race can bypass. See section 5. |
| **Global exception middleware** | One class catches every exception | Controllers stay free of try/catch; the client never sees a stack trace; every failure is logged once with a `traceId`. The API **cannot crash from a request**. |
| **Custom exceptions** | `NotFoundException` (-> 404), `BusinessRuleException` (-> 400) | A service just says what went wrong; the middleware decides how HTTP shows it. |
| **400 for duplicates** (not 409) | Duplicate e-mail/assignment return 400 | The assignment's status-code table lists exactly 200/201/400/404/500; duplicates are listed under *validation*. |
| **Restrict instead of cascade delete** | You can't delete an employee/course that has trainings; you get a clear 400 | Cascade would silently destroy training history. Safer, and easy to explain. |
| **E-mail stored lower-case** | `Priya@X.com` is saved as `priya@x.com` | Makes "unique" mean the same thing on every database (SQL Server ignores case, SQLite doesn't). |
| **Nullable `int?`/`DateTime?` in request DTOs** | Used only for *required* numbers/dates | A plain `int` silently becomes `0` when the client forgets it, hiding the mistake. `int?` + `[Required]` reports "Experience is required". |
| **Swagger + XML comments** | `///` comments become the descriptions you see in Swagger | Documentation that can't go stale; "test every endpoint" is one click each. |
| **Tests as the safety net** | 173 integration tests that call the real API over HTTP; same tests run on SQLite (fast) and real SQL Server | "No mistakes" needs proof. A refactor that breaks a rule fails a test immediately. |

## 5. Where each business rule lives

Take the rule **"An employee must not be assigned the same course twice"**:

1. **Service** checks first (`AnyAsync(...)`) -> friendly 400: *"This employee is already assigned to this course."*
2. **Database** has a **unique index** on (`EmployeeId`, `CourseId`). If two users click at the same instant and both pass
   check 1, the index lets only one INSERT through. The service catches the failure of the second one and still answers
   with the same friendly 400 (not a 500). We tested this with 8 simultaneous requests: exactly 1 succeeds.

The same idea applies to unique e-mail, "end date not before start date" (DTO + CHECK constraint), "duration > 0", valid status, and
"don't delete what is still referenced" (service + foreign key).

## 6. Errors and status codes (the contract with the client)

| Code | When | Body |
|---|---|---|
| **200 OK** | read / update / delete succeeded | the data (or a short message for delete) |
| **201 Created** | something was created | the new item + `Location` header pointing at it |
| **400 Bad Request** | invalid input: missing/invalid field, duplicate, broken JSON, bad id format, blocked delete | `{ "errors": { "Field": ["clear message"] } }` |
| **404 Not Found** | the id (or the referenced employee/course) does not exist | `{ "detail": "Employee with id 7 was not found." }` |
| **500** | a genuine bug / database down | generic message + `traceId` (details are in the log only) |

The *traceId* is the link between what the client saw and what is in the log - how real support teams find a problem.

## 7. How we know it works (three levels of testing)

1. **Automated integration tests** (`dotnet test`): start the real API in memory and call it over HTTP - happy paths, every
   validation rule, every 404, broken input, duplicates, concurrency, delete protection, error handling, start-up without database.
2. **The same tests on real SQL Server** (set `TMS_TEST_SQLSERVER`): proves the migrations, constraints and queries behave on the database the
   assignment requires, not only on a stand-in.
3. **Postman collection run** (43 requests, 74 checks) against the live API + Swagger screenshot - the "evidence" deliverable
   (see `docs/evidence`).

## 8. Glossary

| Term | Meaning |
|---|---|
| **REST API** | A web service where URLs are nouns (`/api/employees/5`) and HTTP verbs are actions (GET read, POST create, PUT replace, DELETE remove). |
| **Entity / Model** | A C# class that EF Core maps to a table (`Employee`). |
| **DTO** | A C# class used only to carry data in/out of the API. |
| **EF Core** | Entity Framework Core - the library that turns C# (LINQ) into SQL and back. |
| **DbContext** | The EF Core object that represents a session with the database. |
| **Migration** | A versioned script (generated as C#) that changes the database structure. |
| **Dependency Injection (DI)** | Instead of `new EmployeeService()`, the framework creates objects and hands them to constructors that ask for them. |
| **Middleware** | A step in the request pipeline that sees every request/response (we use one for errors). |
| **Unique index** | A database rule: no two rows may have the same value(s) in these column(s). |
| **Foreign key (FK)** | A column that must point to an existing row in another table. |
| **async/await** | Lets a method wait for the database without blocking the thread. |
| **Swagger / OpenAPI** | A machine-readable description of the API plus a web page to try it. |
| **Problem details** | The standard JSON shape (RFC 7807) for HTTP errors. |
