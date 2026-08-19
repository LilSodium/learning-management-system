# Learning Management System

A Canvas-style LMS built on .NET 10. Instructors create courses, manage rosters, post
assignments, and grade submissions. Students enroll, view course modules, and submit work.

There are two front ends over a shared domain library: a .NET MAUI app that runs on Windows,
Android, iOS, and Mac Catalyst, and an ASP.NET Core Web API. Both persist to SQLite through
Entity Framework Core.

## Projects

| Project | What it is |
|---|---|
| `CLI.LMS.Core` | Domain models and the service interfaces the UI talks to |
| `CLI.LMS.MAUI` | Cross-platform desktop and mobile client, 12 pages |
| `CLI.LMS.Api` | REST API over the same domain, with Swagger UI |
| `CLI.LMS.Cli` | Console version, the original entry point |

## Running it

You need the .NET 10 SDK. For the MAUI app you also need the MAUI workload:

```bash
dotnet workload install maui
```

Run the desktop client:

```bash
dotnet run --project LMS/CLI.LMS.MAUI/CLI.LMS.MAUI -f net10.0-windows10.0.19041.0
```

Run the API:

```bash
dotnet run --project LMS/CLI.LMS.Api
```

The API creates its SQLite database on first run and serves Swagger UI at `/swagger`.
Both apps keep their own database file, so they do not share state.

## API endpoints

```
GET    /api/courses
GET    /api/courses/{id}
POST   /api/courses
PUT    /api/courses/{id}
DELETE /api/courses/{id}

GET    /api/courses/{id}/roster
POST   /api/courses/{id}/roster/{studentId}
DELETE /api/courses/{id}/roster/{studentId}

GET    /api/students
GET    /api/students/{id}
POST   /api/students
PUT    /api/students/{id}
DELETE /api/students/{id}
```

## How it fits together

Pages never touch the database directly. They depend on interfaces in
`CLI.LMS.Core/Services` — `ICourseService`, `IAssignmentService`, `ISubmissionService` and
so on — and `MauiProgram.cs` registers the EF Core implementations at startup. There is a
second set of implementations backed by an in-memory sample data store, which is what the
app used before persistence existed. Swapping between them is a change to the DI
registrations and nothing else.

Some things worth pointing at:

**Roster import and export.** Rosters go in and out as CSV with `Id,Name,Code` columns.
Import skips students already enrolled and returns how many were actually added, so running
the same file twice does not duplicate anyone.

**Gradebook export.** One row per student, one column per assignment, plus a total
percentage and letter grade. Letter grades come from per-course grade ranges rather than a
fixed scale, so a course can define its own cutoffs.

**Assignment email outbox.** Posting an assignment queues a message per enrolled student.
`EmailComposer` in Core builds the message so the MAUI service and the sample data service
produce identical output. Messages land in an outbox page instead of being sent.

**Submission locking.** Once a submission is graded it cannot be edited, which closes a hole
where a student could change their answer after seeing the score.

## Origin

This started from a course skeleton for FSU COP 4870, which provided the solution layout and
an empty project structure. The application — the domain models, both front ends, the API,
the EF Core layer, and the features above — is my own work. The skeleton's MIT license is
retained in `LICENSE`.
