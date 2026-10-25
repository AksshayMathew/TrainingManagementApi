# Moving the project to your office laptop, one step at a time

This repository was built as **16 small steps**. Each step is one Git commit with a tag (`step-00-...` to `step-15-...`),
and **every step builds on its own** (each was checked out in a clean folder, built and tested).
You bring the steps to the office laptop one by one: copy the step, build it, run it, commit it with your office identity.
The office repository then grows in the same order the project was built.

> **Read this once.** The assessment gives marks for *demo and explanation*, so you must be able to explain every file.
> Work through each step yourself (run it, break it, fix it), read [EXPLAINED.md](EXPLAINED.md) and rehearse [DEMO_SCRIPT.md](DEMO_SCRIPT.md).
> Commit with the **real date and time** of when you do the work - do not back-date commits.

---

## 1. What the office laptop needs (check before step 1)

| Need | How to check | If missing |
|---|---|---|
| .NET 8 SDK | `dotnet --list-sdks` shows `8.0.xxx` | install the .NET 8 SDK (Visual Studio 2022 17.8+ includes it) |
| Git | `git --version` | install Git for Windows |
| SQL Server LocalDB or Express | `sqllocaldb info` lists `MSSQLLocalDB` | Visual Studio Installer > Individual components > *SQL Server Express LocalDB*; or `sqllocaldb create MSSQLLocalDB` |
| Visual Studio 2022 (recommended) or VS Code + *C# Dev Kit* | open it | |
| NuGet access to nuget.org | `dotnet restore` works | if your office uses an internal feed, add it to `nuget.config` |

**Keep the folder path short**, e.g. `C:\work\TrainingManagementApi`. Windows has a 260-character path limit; deeply nested
folders (Desktop/OneDrive/...) can make builds or tests fail with confusing errors. (We hit exactly this while verifying.)

## 2. Getting this repository onto the office laptop

You need the repository **with its history and tags** (the helper script reads the tags). Pick what your office allows:

- **Clone from GitHub** (if your office network allows it): `git clone <your personal repo url> C:\work\source-repo`
- **Git bundle** (one file, no network): on the personal laptop `git bundle create tms.bundle --all`, copy `tms.bundle`
  (USB / allowed cloud drive), then at the office `git clone tms.bundle C:\work\source-repo`
- **Copy the whole folder** including the hidden `.git` folder (but not `bin`/`obj`/`.env`).

Call this the **source repo**. You never edit it - it is only the place steps are copied from.

## 3. Create the office repository with your office identity

```powershell
mkdir C:\work\TrainingManagementApi
cd C:\work\TrainingManagementApi
git init -b main
git config user.name  "Your Name"                 # --local is the default here: only this repository
git config user.email "your.name@yourcompany.com"  # your OFFICE address
git config --local --list | findstr user          # double-check before the first commit
```
Using the repository-local config (not `--global`) keeps your office and personal identities from getting mixed up.

## 4. The routine for every step

```powershell
# 1. copy the step from the source repo into the office repo
cd C:\work\source-repo
powershell -ExecutionPolicy Bypass -File scripts\export-step.ps1 -Step 1 -Target C:\work\TrainingManagementApi

# 2. check it
cd C:\work\TrainingManagementApi
dotnet build                  # must succeed        (Visual Studio: Ctrl+Shift+B)
dotnet test                   # from step 5 on       (Visual Studio: Test > Run All Tests)
dotnet run --project TrainingManagementApi    # from step 4 on: try the new endpoints in Swagger

# 3. commit it - with YOUR message
git add -A
git commit -m "Add .NET 8 Web API solution"
```
Tips:
- `-ListOnly` shows what a step would copy without touching anything. `-Snapshot` copies the *whole* project as of that step (to catch up).
- Run `dotnet tool restore` once (step 1 onwards) if you want to use `dotnet ef`.
- Don't worry that the copy shows many files for one step - that is the commit. Read `git diff --staged` before you commit; it is the best way to learn.
- If `-ExecutionPolicy Bypass` is blocked by policy, open the `.ps1` file and run the same `git archive` command by hand, or ask for the files to be copied in Explorer.

## 5. The 16 steps

Suggested pace follows the assignment's 3-day plan. The commit messages below are *suggestions in plain wording* - use your own.

| Day | Step | Tag | What it adds | How to check it | Suggested commit message |
|---|---|---|---|---|---|
| 1 | 0 | `step-00-docs-and-repo-hygiene` | the assignment text in `docs/`, `.gitattributes`, `.editorconfig` | files appear | `Add assignment notes and repo settings` |
| 1 | 1 | `step-01-scaffold` | solution + Web API project, `global.json`, `dotnet-ef` tool, Swagger | `dotnet build` ok; `dotnet run` shows Swagger (empty) | `Create .NET 8 Web API solution` |
| 1 | 2 | `step-02-ef-core-employee-entity` | EF Core packages, `Employee`, `AppDbContext`, first migration, start-up migrate | `dotnet run` creates `TrainingManagementDb`; look at the `Employee` table | `Add EF Core, Employee entity and first migration` |
| 1 | 3 | `step-03-error-handling` | `NotFoundException`, `BusinessRuleException`, global exception middleware | `dotnet build` ok | `Add global exception handling middleware` |
| 1 | 4 | `step-04-employee-crud` | Employee DTOs, service, controller | Swagger: create / get / update / delete an employee; try a bad e-mail | `Implement Employee CRUD API` |
| 1 | 5 | `step-05-employee-tests` | xUnit test project, employee/error/Swagger tests | `dotnet test` -> 49 passed, 1 skipped | `Add integration tests for employees` |
| 2 | 6 | `step-06-course-crud` | `Course` entity + migration, DTOs, service, controller, tests | Swagger: courses; `dotnet test` -> 75 passed, 1 skipped | `Add Course CRUD with tests` |
| 2 | 7 | `step-07-training-assignment-model` | `TrainingAssignment` entity, relationships, constraints, migration | `dotnet run`; check the 3 tables and foreign keys in SQL Server Object Explorer | `Add TrainingAssignment entity and relationships` |
| 2 | 8 | `step-08-training-assignment-module` | assign / view / update status / delete + all rules, delete protection, tests | Swagger: assign a course, try a duplicate; `dotnet test` -> 141 passed, 2 skipped | `Implement training assignment APIs and validations` |
| 3 | 9 | `step-09-employee-search-paging` | search, department filter, sort, paging on `GET /api/employees` (bonus) | Swagger: `?search=...&sortBy=Name&page=1&pageSize=5` | `Add employee search, sorting and paging` |
| 3 | 10 | `step-10-dev-sqlserver-compose` | optional Docker SQL Server (`docker-compose.yml`, `.env.example`) | **skip it if you use LocalDB** | `Add optional docker compose for SQL Server` |
| 3 | 11 | `step-11-database-script` | `docs/database/create-database.sql` | open it | `Add database creation script` |
| 3 | 12 | `step-12-postman-and-evidence` | Postman collection, no-install runner script, evidence folder | import the collection in Postman and run it | `Add Postman collection and API test evidence` |
| 3 | 13 | `step-13-startup-resilience` | migration retry at start-up, safer settings parsing, tests | `dotnet test` -> 171 passed, 2 skipped | `Retry migrations at start-up and harden settings` |
| 3 | 14 | `step-14-readme-and-docs` | README, explanation, demo script, trace, this guide | read them | `Add README and project documentation` |
| 3 | 15 | `step-15-final-review-fixes` | final review: newer test packages (no known vulnerabilities), clearer "database not found" start-up message, Docker connection note (`127.0.0.1`) | `dotnet test` -> 171 passed, 2 skipped | `Upgrade test packages and clarify start-up messages` |

Notes
- Step 10's Docker files are optional; the evidence files in step 12 were produced on the personal laptop - **replace them with your own
  screenshots and run output from the office laptop** (see section 8) before you submit.
- You do **not** need to copy `scripts/export-step.ps1` itself to the office repo.
- If you must hand in less history, you can merge neighbours (e.g. steps 10-13 in one commit) - use `-Step` for each, then commit once.
- Step 15 changes files from earlier steps (test `.csproj`, `DatabaseInitializer.cs`, README, this guide): the script simply overwrites them, which is what you want.

## 6. Visual Studio walkthrough (recommended at the office)

1. **File > Open > Project/Solution** -> `TrainingManagementApi.sln`. (A `.sln` was used on purpose so any VS 2022 opens it.)
2. Right-click `TrainingManagementApi` > **Set as Startup Project**. Choose the **http** (or https) profile next to the green arrow.
3. **F5**: Swagger opens at `http://localhost:5282/swagger` and the database is created on first start.
4. **View > SQL Server Object Explorer** > `(localdb)\MSSQLLocalDB` > Databases > `TrainingManagementDb` to see tables and data.
5. **Test > Test Explorer** to run the xUnit tests (needs steps up to 5 copied).
6. **Git Changes** window to commit. Click the `...` > *Settings* if you need to check the user name/e-mail Git will use.
7. **Package Manager Console** (Tools > NuGet Package Manager), default project `TrainingManagementApi`:
   `Add-Migration <Name>`, `Update-Database`, `Script-Migration -Idempotent` (the `Microsoft.EntityFrameworkCore.Tools` package is already referenced).

VS Code instead: install *C# Dev Kit*, open the folder, then `dotnet run --project TrainingManagementApi` in the terminal (or
`dotnet watch --project TrainingManagementApi` to restart on save) and use Swagger in the browser.

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `A compatible .NET SDK was not found` | install any .NET 8 SDK (`global.json` accepts 8.0.100 or newer within 8.0) |
| `A network-related or instance-specific error ... LocalDB` | LocalDB instance missing: `sqllocaldb create MSSQLLocalDB` then `sqllocaldb start MSSQLLocalDB`; or use another server via user-secrets (README section 1) |
| The API takes ~30-40 s to start, then every data call is a 500; the log says `The system cannot find the file specified` | LocalDB is not installed / the instance does not exist (see the LocalDB row above). The API retries 5 times before giving up, hence the delay |
| The API starts but every data call is a 500 | read the console: the start-up message says why the migration failed (server unreachable, bad connection string) |
| `Failed to restore` / NuGet errors | the office network or proxy blocks nuget.org: configure the internal feed in `nuget.config` |
| Build errors about long paths | move the repo to a short folder such as `C:\work\...` |
| Port 5282 already in use | stop the other instance, or change `applicationUrl` in `Properties/launchSettings.json` |
| `dotnet ef` not found | run `dotnet tool restore` in the folder that contains `.config/dotnet-tools.json` |
| Browser warns about the https certificate | `dotnet dev-certs https --trust`, or use the **http** profile |
| `export-step.ps1` cannot run | use `powershell -ExecutionPolicy Bypass -File ...`, as shown in section 4 |

## 8. Before you hand in

1. `dotnet build` and `dotnet test` are green on the office laptop.
2. Run the API and take **your own Swagger screenshots** of a few executed requests (a 201 for *assign course*, a 400 validation error, a 404). Save them in `docs/evidence/`.
3. Optionally run the Postman collection on the office laptop and export the result (Postman: *Run collection > Export results*), or use
   `node scripts\run-postman-collection.js postman\TrainingManagementApi.postman_collection.json --report docs\evidence\api-test-run-office.md` if Node is installed.
4. Read the README: update the clone/setup lines if your office repo location differs; the *Assumptions* and *Known limitations* sections should match what you will say in the demo.
5. Practise the 10-minute demo out loud with [DEMO_SCRIPT.md](DEMO_SCRIPT.md).
