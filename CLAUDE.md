# CLAUDE.md

QLBTSV: an assignment management app (giao / nộp / chấm bài tập) for the PRN212 course. There are two WPF apps, a Server and a Client, that talk over HTTP on a LAN (the Server hosts ASP.NET Core in-process; the original TCP design in Plan.md is mapped to HTTP routes).

**The spec is [docs/Plan.md](docs/Plan.md).** Read it before you implement any feature. Follow its business rules and protocol exactly, and keep it up to date when a rule changes. Project docs are written in Vietnamese. Write code, identifiers and code comments in English.

## Repository layout

```txt
db/QLBT.sql            SQL Server schema (DB: PRN212_P_QLBT). Drops and recreates the DB.
docs/                  Plan.md (spec), ProjectCreation.md (how the solution was scaffolded), report templates
Prototype/             Throwaway experiments. Reference only, do not ship.
  ServerVersion2/      Working TCP protocol prototype (Shared / TcpServer / Client)
  ExampleBcryptDotnet/ BCrypt usage sample
  TestEF/              EF Core models scaffolded from db/QLBT.sql (reference for entities)
  ServerVersion3_ASP/  Svr3.*: ASP.NET Core trial of HTTP auth + file transfer, to decide whether to migrate off raw TCP
    Svr3.Shared        DTOs, ApiRoutes, ApiStatus (same statuses as the TCP protocol)
    Svr3.Server        WPF app hosting Kestrel in-process (ServerHost.Build + StartAsync in App.OnStartup). Minimal API,
                       custom "Session" auth scheme (Bearer sessionId), streamed PUT upload / GET download. Tabs: "Server configuration"
                       (IP, port, file path, auto start when app opens, Start/Stop) and "Server log". Settings saved in
                       %LocalAppData%\Svr3.Server\settings.json (ServerSettings); DB and limits stay in appsettings.json
    Svr3.Client        Console scenario runner: PASS/FAIL checks against db/QLBT_test_ds.sql
QLBTSV_re/             Main solution (QLBTSV_re.sln)
  QLBT.Shared          classlib: ApiRoutes, ApiStatus, ApiJson, DTOs (shared by Server and Client)
  QLBT.Server          WPF: admin GUI (direct DB access) + in-process ASP.NET Core HTTP API, EF Core, BCrypt
    Asp/               ServerHost (DI, Kestrel), ServerRunner (Start/Stop), Endpoints/, SessionAndAuthentication/
    Admin/             Admin services (User/Semester/Course/ClassAdminService): DB work + rule checks, return AdminResult;
                       AdminLog (admin actions → "Server log" tab); Rows.cs (grid row models)
    Data/DbFactory.cs  DbContexts for the admin GUI, from appsettings.json (works while the HTTP server is stopped)
    Domain/            Rules shared by endpoints and admin GUI (AssignmentRules, SemesterRules, AdminResult)
    Files/             FileStorage (id-based paths), FileReceiver (streamed upload to .tmp)
    Models/            Scaffolded EF entities (never hand-edit)
    Views/             Admin tabs (UsersView, SemestersView, CoursesView, ClassesView: editor on top, list below) + AddStudentsDialog
  QLBT.Client          WPF: login, then a Teacher or Student window; talks to the server only over HTTP
    Api/               ApiClient (HTTP wrapper ported from Svr3.Client), QlbtApi (one call per operation), Session
    Views/             LoginWindow, ProfileDialog; Teacher/ (TeacherWindow 3-pane, ClassInfoDialog, GradeDialog);
                       Student/ (StudentWindow: tree by class or list by due date + detail)
    appsettings.json   { "ServerUrl": ... } (Copy Always); read by ClientSettings, no UI to change it
    UiHelper.cs        Check(ApiResult) (Unauthorized → login), file dialogs, BusyScope
```

Create missing projects by following `docs/ProjectCreation.md` (net8.0, with the package versions listed there).

## Build and run

- Requires the .NET 8 SDK and Visual Studio 2022 with ".NET desktop development" (see `.vsconfig`).
- Build: `dotnet build QLBTSV_re/QLBTSV_re.sln`, or `dotnet build Prototype/Prototype.sln` for the prototypes.
- Run: use the multi-startup "Run all" profile in VS, which starts the Server and Client together.
- Database: `sqlcmd -S "(local)" -i db/QLBT.sql`, then scaffold the EF models into `QLBTSV_re/QLBT.Server/Models` with `dotnet ef dbcontext scaffold ... --output-dir Models --no-onconfiguring --force` (the full command and why `--no-onconfiguring` is needed are in ProjectCreation.md). The context gets its connection string from DI (`AddDbContext` in `ServerHost.cs`) for endpoints, or from `Data/DbFactory.Create()` for the admin GUI, never from `OnConfiguring` (`new Prn212PQlbtContext()` has no provider). Don't hand-edit scaffolded entities. Change the schema in `db/QLBT.sql` and re-scaffold.
- Test data: `sqlcmd -S "(local)" -E -C -f 65001 -i db/QLBT_test_ds.sql` (run after QLBT.sql, safe to re-run). Every account's password is `123456`. The scenarios are listed in the file header.
- The connection string goes in `appsettings.json` (Copy to Output Directory = Always). **Never commit real passwords.**

## Architecture rules

- **Protocol (HTTP):** routes are constants in `QLBT.Shared/ApiRoutes.cs`; the operation → route table is in Plan.md ("Ánh xạ sang HTTP"). Auth = `Authorization: Bearer {sessionId}` (sessions in memory, lost on Stop). JSON is camelCase with enums as names (`ApiJson.Options`). Uploads are `PUT` with the raw file as the body and `?fileName=`; the client sends `Expect: 100-continue` and every check runs before the body is read. Downloads are `GET` with the name in `Content-Disposition`. Errors return an `ApiError { status, error }` body.
- **Statuses:** `Ok | InvalidCredentials | Unauthorized | Forbidden | NotFound | Rejected | Error` (HTTP 200 / 401 / 401 / 403 / 404 / 409 / 500).
  - `Unauthorized`: no session, or the session is invalid. The client clears the SessionId and returns to the login screen.
  - `Forbidden`: wrong role, not the owner, or the student is suspended in that class.
  - `Rejected`: the user has permission but the request breaks a rule (assignment closed, submission already graded, semester already started).
- **Security:** take the role and identity only from the server-side session. Never trust IDs or roles sent by the client. Every operation checks ownership: a teacher sees only classes they teach, and a student sees only their own classes and submissions. Store passwords as BCrypt hashes.
- **Closed state:** `closed = IsClosed || (now > Deadline && !AllowLate)`. Grading is allowed only when the assignment is closed.
- **Submission checks, in order:** member and not suspended → not closed → already graded means reject, ungraded means replace → `IsLate = now > Deadline`. Write the upload to `{submissionId}.tmp` and swap it in only after every byte has arrived.
- **File storage:** paths are built from IDs only: `storage/{semesterId}/{classId}/{assignmentId}/assignment` and `.../submissions/{submissionId}`. The original file name is stored in the DB.
- **Deletes:** delete from the DB in a transaction first, then delete the files.
- **Semesters and classes:** they can be edited or deleted only before the semester starts. After it starts, the start date is locked and the end date can only move later. A class never changes its semester.
- **Admin GUI:** works on the DB directly (not over HTTP), whether or not the server is running. Views are code-behind UserControls that stay thin: load → bind → call an `Admin/*Service` → show `AdminResult.Error`. Business rules live in the services / `Domain/`, not in views. One short-lived `DbFactory.Create()` context per operation.
- **Users:** username and role are fixed after creation; a user with any class (taught or enrolled) cannot be deleted, suspend them in the class instead. Suspension (per class) can be toggled at any time: `is_active = 0, left_at = now` and back.
- **Courses:** a class = group name + course + semester (`uq_classes` = name, semester_id, course_id); there is no major column. Teacher, course and name can change before the semester starts. A course used by a class cannot be deleted.
- **One class per course per semester:** a student may be active in only one class of a course in a semester (suspended enrolments do not count). The DB enforces it: `class_students` copies `semester_id` / `course_id` from the class (composite FK, `ON UPDATE CASCADE`) and has the filtered unique index `uq_class_students_course`. `ClassAdminService` checks first for a readable message (add students, un-suspend, change course) and the picker hides students who would break it. Always set `SemesterId` / `CourseId` when inserting a `ClassStudent`.
- **Client:** code-behind like the server views; every call goes through `QlbtApi` and every result through `UiHelper.Check` (shows the server's `error`; `Unauthorized` → `App.SessionExpired()` → login window). `ApiClient` never throws on network errors: they come back as `Error` "Cannot reach server". Buttons mirror the server rules (grade only when closed, no submit when closed / suspended / graded) but the server stays the authority. Downloads use a Save dialog with the name from `Content-Disposition`.
- **Changing a class's course:** EF treats `(Id, SemesterId, CourseId)` on `Class` as an alternate key and refuses to modify `CourseId`; update it with SQL (`ExecuteSqlInterpolatedAsync`), the DB cascades it to `class_students`.
- **SQL scripts:** both start with `SET QUOTED_IDENTIFIER ON` (required by the filtered index; sqlcmd defaults to OFF).

## Code style

Match the prototype code:

- File-scoped namespaces, `Nullable` and `ImplicitUsings` enabled.
- Explicit types rather than `var`, braces on every block, and `sealed` classes by default.
- Async I/O (`async Task`) for network and file work.
- Short `///` XML doc comments on public members to explain intent and wire behavior.
- Keep new `ApiRoutes` and endpoint handlers in sync with the operation table and HTTP mapping in Plan.md.

## Git

- **Never commit or push without being asked.** See `.claude/rules/git.md`.
- Work on feature branches (`feature/...`, `fix/...`, `chore/...`). Use imperative commit messages with the first line under 72 characters. See `docs/GitHelp.md`.
- Don't commit `bin/`, `obj/`, `.vs/`, secrets or `storage/` data.
