# CLAUDE.md

QLBTSV: an assignment management app (giao / nộp / chấm bài tập) for the PRN212 course. There are two WPF apps, a Server and a Client, that talk over TCP on a LAN.

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
  QLBTSV_re.Shared     classlib: protocol DTOs, MessageType, Wire framing
  QLBTSV_re.Server     WPF: admin GUI (direct DB access) and TCP server, EF Core, BCrypt
  QLBTSV_re.Client     WPF: separate Teacher and Student UIs, talks to the server only over TCP
```

The main solution is still being scaffolded. Create its projects by following `docs/ProjectCreation.md` (net8.0, with the package versions listed there). When you build the real Shared and Server code, port it from `Prototype/ServerVersion2`: the `Wire` framing, the `Request`/`Response` types, `RequestContext`, `SessionsManager` and `FileStorage`.

## Build and run

- Requires the .NET 8 SDK and Visual Studio 2022 with ".NET desktop development" (see `.vsconfig`).
- Build: `dotnet build QLBTSV_re/QLBTSV_re.sln`, or `dotnet build Prototype/Prototype.sln` for the prototypes.
- Run: use the multi-startup "Run all" profile in VS, which starts the Server and Client together.
- Database: `sqlcmd -S "(local)" -i db/QLBT.sql`, then scaffold the EF models into `QLBTSV_re.Server/Models` with `dotnet ef dbcontext scaffold ... --output-dir Models --force` (the full command is in ProjectCreation.md). Don't hand-edit scaffolded entities. Change the schema in `db/QLBT.sql` and re-scaffold.
- Test data: `sqlcmd -S "(local)" -E -C -f 65001 -i db/QLBT_test_ds.sql` (run after QLBT.sql, safe to re-run). Every account's password is `123456`. The scenarios are listed in the file header.
- The connection string goes in `appsettings.json` (Copy to Output Directory = Always). **Never commit real passwords.**

## Architecture rules

- **Protocol:** one TCP connection carries one request. Frame = `int32 length (LE)` + UTF-8 JSON + optional file bytes, and the file bytes follow only when `FileLength` is set. Enums are serialized as names. The JSON limit is 1 MB; files are streamed with no limit. The order is: client `WriteRequest → [WriteFile] → ReadResponse → [ReadFile]`. When the server rejects an upload, it still drains the upload bytes before it responds.
- **Statuses:** `Ok | InvalidCredentials | Unauthorized | Forbidden | NotFound | Rejected | Error`.
  - `Unauthorized`: no session, or the session is invalid. The client clears the SessionId and returns to the login screen.
  - `Forbidden`: wrong role, not the owner, or the student is suspended in that class.
  - `Rejected`: the user has permission but the request breaks a rule (assignment closed, submission already graded, semester already started).
- **Security:** take the role and identity only from the server-side session. Never trust IDs or roles sent by the client. Every operation checks ownership: a teacher sees only classes they teach, and a student sees only their own classes and submissions. Store passwords as BCrypt hashes.
- **Closed state:** `closed = IsClosed || (now > Deadline && !AllowLate)`. Grading is allowed only when the assignment is closed.
- **Submission checks, in order:** member and not suspended → not closed → already graded means reject, ungraded means replace → `IsLate = now > Deadline`. Write the upload to `{submissionId}.tmp` and swap it in only after every byte has arrived.
- **File storage:** paths are built from IDs only: `storage/{semesterId}/{classId}/{assignmentId}/assignment` and `.../submissions/{submissionId}`. The original file name is stored in the DB.
- **Deletes:** delete from the DB in a transaction first, then delete the files.
- **Semesters and classes:** they can be edited or deleted only before the semester starts. After it starts, the start date is locked and the end date can only move later. A class never changes its semester.
- The Server admin GUI works on the DB directly and does not go through TCP.

## Code style

Match the prototype code:

- File-scoped namespaces, `Nullable` and `ImplicitUsings` enabled.
- Explicit types rather than `var`, braces on every block, and `sealed` classes by default.
- Async I/O (`async Task`) for network and file work.
- Short `///` XML doc comments on public members to explain intent and wire behavior.
- Keep new `MessageType` values and handlers in sync with the operation table in Plan.md.

## Git

- **Never commit or push without being asked.** See `.claude/rules/git.md`.
- Work on feature branches (`feature/...`, `fix/...`, `chore/...`). Use imperative commit messages with the first line under 72 characters. See `docs/GitHelp.md`.
- Don't commit `bin/`, `obj/`, `.vs/`, secrets or `storage/` data.
