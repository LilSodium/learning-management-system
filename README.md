# Learning Management System

An LMS modeled on Canvas, built with .NET 10. Instructors create courses, manage rosters,
post assignments, and grade work. Students enroll, read course modules, and submit.

Two front ends sit on a shared domain library. One is a .NET MAUI app that runs on Windows,
Android, iOS, and Mac Catalyst. The other is an ASP.NET Core Web API. Both store data in
SQLite through Entity Framework Core.

## Projects

`CLI.LMS.Core` holds the domain models and the service interfaces the UI depends on.
`CLI.LMS.MAUI` is the client, 12 pages, on desktop and mobile.
`CLI.LMS.Api` exposes the same domain over REST and serves Swagger UI.
`CLI.LMS.Cli` is the original console version.

## Running it

Requires the .NET 10 SDK. The MAUI app also needs its workload:

```bash
dotnet workload install maui
```

Desktop client:

```bash
dotnet run --project LMS/CLI.LMS.MAUI/CLI.LMS.MAUI -f net10.0-windows10.0.19041.0
```

API:

```bash
dotnet run --project LMS/CLI.LMS.Api
```

The API builds its database on first run and serves Swagger at `/swagger`. Each app keeps
its own database file, so they do not share state.

## API

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

## Notes on the design

Pages never touch the database. They depend on interfaces in `CLI.LMS.Core/Services`, and
`MauiProgram.cs` registers the Entity Framework implementations at startup. A second set of
implementations reads from a sample data store held in memory, which is what the app used
before it had persistence. Switching between them means editing those registrations and
nothing else.

Rosters import and export as CSV with Id, Name, and Code columns. Import skips anyone
already enrolled and reports how many it added, so running the same file twice is safe.

The gradebook exports one row per student and one column per assignment, with a total
percentage and a letter grade. Each course sets its own grade cutoffs instead of sharing a
fixed scale.

Posting an assignment queues an email for every enrolled student. `EmailComposer` in Core
builds the text, so both service implementations produce the same message. Mail collects in
an outbox page rather than going out.

A graded submission locks. Students cannot edit or resubmit after seeing a score.

## Origin

Started from a course skeleton for FSU COP 4870, which supplied the solution layout and
empty projects. Everything else is mine: the models, both front ends, the API, the data
layer, and the features above. The skeleton's MIT license is kept in `LICENSE`.
