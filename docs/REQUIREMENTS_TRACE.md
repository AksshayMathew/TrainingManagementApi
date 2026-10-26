# Requirements trace: assignment -> code -> tests

Section numbers refer to the assignment brief (an internal document that is not included in this repository). Paths are relative to `TrainingManagementApi/` (code) and
`TrainingManagementApi.Tests/` (tests). Use this to answer "where did you implement X?" quickly.

## 2. Technology requirements
| Requirement | Where |
|---|---|
| .NET 8, C#, ASP.NET Core Web API | `TrainingManagementApi.csproj` (`net8.0`, `Microsoft.NET.Sdk.Web`), `global.json` |
| Entity Framework Core + SQL Server | `Data/AppDbContext.cs`, packages `Microsoft.EntityFrameworkCore.SqlServer` 8.0.33, connection string in `appsettings.json` |
| Swagger / OpenAPI | `Program.cs` (`AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI`, XML comments) - test: `SwaggerTests` |
| VS Code or Visual Studio, Git | works with both (`.sln`, `launchSettings.json`); Git history = one commit per step |

## 3.1 Employee
| Requirement | Where | Tests |
|---|---|---|
| Fields `EmployeeId, EmployeeName, Email, Department, JoiningDate, Experience` | `Models/Employee.cs` | |
| `EmployeeId` primary key | `Data/Configurations/EmployeeConfiguration.cs` | |
| Name / Department / JoiningDate required | `DTOs/EmployeeRequestDto.cs` (`[Required]`), non-null columns | `EmployeesApiTests.Create_WithInvalidData_Returns400_NamingTheBadField` |
| Email required **and unique** | DTO `[Required]`, `EmployeeService` (check + race handling), unique index `UX_Employee_Email` | `Create_WithDuplicateEmail_...`, `Create_WithSameEmailInDifferentCase_...`, `Create_WithSameEmailAtTheSameTime_...` (SQL Server) |
| Experience 0 or greater | DTO `[Range(0,60)]`, CHECK `CK_Employee_Experience_NonNegative` | `Create_WithZeroExperience_IsAllowed`, invalid-data theory (-1, 61, missing) |
| `GET /api/employees`, `GET /api/employees/{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` | `Controllers/EmployeesController.cs` | `EmployeesApiTests` (all verbs, 200/201/400/404) |

## 3.2 Course
| Requirement | Where | Tests |
|---|---|---|
| Fields `CourseId, CourseName, Duration, TrainerName` | `Models/Course.cs`, `Data/Configurations/CourseConfiguration.cs` | |
| Name and Trainer required | `DTOs/CourseRequestDto.cs` | `CoursesApiTests.Create_WithInvalidData_Returns400_NamingTheBadField` |
| Duration greater than 0 | DTO `[Range(1,10000)]`, CHECK `CK_Course_Duration_Positive` | `Create_WithDurationZero_SaysItMustBeGreaterThanZero` |
| `GET /api/courses`, `POST`, `PUT /{id}`, `DELETE /{id}` (+ `GET /{id}` for the Location header) | `Controllers/CoursesController.cs` | `CoursesApiTests` |

## 3.3 Training assignment
| Requirement | Where | Tests |
|---|---|---|
| Fields `AssignmentId, EmployeeId, CourseId, StartDate, EndDate, Status` | `Models/TrainingAssignment.cs`, `TrainingAssignmentConfiguration.cs` | |
| FK to Employee and Course | configuration (`HasOne ... HasForeignKey`) | `Assign_WithUnknownEmployee_...`, `Assign_WithUnknownCourse_...` |
| StartDate required; EndDate on/after StartDate | `DTOs/TrainingAssignmentRequestDto.cs` (`IValidatableObject`), CHECK `CK_TrainingAssignment_EndDate_OnOrAfterStartDate` | `Assign_WithEndDateBeforeStartDate_SaysSoClearly`, `Assign_WhenEndDateEqualsStartDate_IsAllowed` |
| Status Assigned / In Progress / Completed | `Models/TrainingStatuses.cs`, `DTOs/TrainingStatusAttribute.cs`, CHECK `CK_TrainingAssignment_Status_Valid` | `TrainingStatusesTests`, `Assign_WithInvalidStatus_ListsTheAllowedValues` |
| **Assign a course to an employee** | `POST /api/training-assignments` -> `TrainingAssignmentService.AssignAsync` | `Assign_WithValidData_Returns201_...` |
| **Update training status** | `PUT /api/training-assignments/{id}/status` -> `UpdateStatusAsync` | `UpdateStatus_*` |
| **View all trainings for an employee** | `GET /api/employees/{employeeId}/trainings` -> `GetForEmployeeAsync` | `GetForEmployee_*` |
| **Delete a training assignment** | `DELETE /api/training-assignments/{id}` -> `DeleteAsync` | `Delete_*` |

## 4. Database design
| Requirement | Where |
|---|---|
| Tables `Employee`, `Course`, `TrainingAssignment` with PKs | `Data/Configurations/*` (`ToTable("Employee")` etc.), `Migrations/` |
| Employee 1-to-many TrainingAssignment; Course 1-to-many TrainingAssignment | `TrainingAssignmentConfiguration` (`HasOne/WithMany`), navigation collections on `Employee`/`Course` |
| Creation script / migrations | `Migrations/` (3 migrations), `docs/database/create-database.sql` |

## 5. Validation requirements
| Rule | Where | Test |
|---|---|---|
| Employee name cannot be empty | `EmployeeRequestDto` | `Create_WithInvalidData_...` ("", "   ", missing, null) |
| Email must be unique | see 3.1 | see 3.1 |
| Experience cannot be negative | see 3.1 | see 3.1 |
| Course duration greater than zero | see 3.2 | see 3.2 |
| Training end date not before start date | see 3.3 | see 3.3 |
| Same course not assigned twice to an employee | `TrainingAssignmentService.AssignAsync`, unique index `UX_TrainingAssignment_Employee_Course` | `Assign_TheSameCourseTwiceToTheSameEmployee_Returns400`, `Assign_TheSameCourseAtTheSameTime_...` (SQL Server) |
| Clear validation messages | every DTO attribute has an `ErrorMessage`; `BusinessRuleException` uses the same `errors` shape | tests assert message text, e.g. `Assign_WithInvalidStatus_ListsTheAllowedValues` |

## 6. HTTP status codes
| Situation | Where | Test |
|---|---|---|
| 200 OK (read/update/delete) | controllers `Ok(...)` | all read/update tests |
| 201 Created | `CreatedAtAction(...)` in each controller | `Create_WithValidData_Returns201_...` (Employees, Courses, Training) |
| 400 Bad Request | `[ApiController]` model validation + `BusinessRuleException` mapping in the middleware | invalid-data theories, duplicate tests, bad-id tests |
| 404 Not Found | `NotFoundException` mapping in the middleware | `*_WithUnknownId_Returns404*` |
| 500 Internal Server Error | middleware default branch | `ErrorHandlingTests` |

## 7. Technical expectations
| Expectation | Where |
|---|---|
| Controller -> Service -> Data/EF Core | `Controllers/`, `Services/`, `Data/` |
| Dependency Injection | constructor injection everywhere; registrations in `Program.cs` |
| DTOs | `DTOs/` (request, response, query, paging) |
| async/await for database calls | all service methods return `Task` and take a `CancellationToken` |
| EF Core migrations create/update the database | `Migrations/`, `Data/DatabaseInitializer.cs` (applied at start-up), `dotnet ef database update` |
| Basic exception handling; global middleware (bonus) | `Middleware/ExceptionHandlingMiddleware.cs`, `Exceptions/` - tests `ErrorHandlingTests`, `StartupResilienceTests` |
| Swagger enabled; test every endpoint | `Program.cs`; evidence in `docs/evidence`; Postman collection `postman/` |
| Meaningful names, no duplicated code | one request DTO per resource shared by POST/PUT; one projection (`ToDto`) for assignments; constants in `FieldLimits`/`TrainingStatuses` |
| Basic logging | `ILogger<T>` in services, middleware and start-up (`appsettings.json` log levels) |

## 8. Suggested project structure
`Controllers/ Models/ DTOs/ Services/ Interfaces/ Data/ Middleware/ Migrations/ Program.cs appsettings.json` - all present
(plus `Exceptions/`, which holds the two custom exception types).

## 10. Deliverables
| Deliverable | Where |
|---|---|
| Complete source code | `TrainingManagementApi/`, `TrainingManagementApi.Tests/` |
| Database creation script and/or migrations | `TrainingManagementApi/Migrations/`, `docs/database/create-database.sql` |
| Swagger screenshots / exported API test evidence | `docs/evidence/swagger-ui-overview.png`, `openapi.json`, `api-test-run.md` |
| Postman collection | `postman/TrainingManagementApi.postman_collection.json` |
| README with setup, API list, assumptions, limitations | `README.md` |
| Git repository with meaningful commits | one commit per step, tagged `step-NN-...` (see `docs/TRANSFER_GUIDE.md`) |

> The Swagger *screenshot* in `docs/evidence` is an overview of all endpoints. For the submission it is worth adding your own screenshots
> of a few executed requests (for example the 201 of "assign course" and a 400 validation error) taken from Swagger on your machine.

## 12. Bonus challenges
| Bonus | Status |
|---|---|
| Employee search by name or department | **Done** (`?search=`, `?department=`) |
| Filtering, sorting, pagination | **Done** (`sortBy`, `sortOrder`, `page`, `pageSize`, `X-Total-Count`) |
| Global exception middleware | **Done** |
| JWT authentication and role-based authorization | Not done |
| E-mail notification after a course assignment | Not done |
| Dockerize the API | Not done (a Docker SQL Server for development is provided) |
| Deploy to Azure App Service | Not done |
