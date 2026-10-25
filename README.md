# Learning Management System

[![CI](https://github.com/LilSodium/learning-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/LilSodium/learning-management-system/actions/workflows/ci.yml)

**Live demo:** https://lms-api-cphl.onrender.com (the free host sleeps when idle, so the first visit can take about a minute). API documentation is at https://lms-api-cphl.onrender.com/swagger.

A Canvas-style LMS in .NET 10. Instructors create courses, manage rosters, post assignments,
and grade work; students enroll, read modules, and submit.

- `CLI.LMS.Core`: domain models and service interfaces
- `CLI.LMS.MAUI`: desktop/mobile client (12 pages)
- `CLI.LMS.Api`: ASP.NET Core REST API with Swagger, plus a web client (plain HTML, CSS and JavaScript in `wwwroot`) served at the root
- `CLI.LMS.Api.Tests`: 38 xUnit integration tests, run by GitHub Actions on every push

Both apps store data in SQLite through Entity Framework Core.

## Features

- Roster import/export as CSV (re-importing the same file is safe)
- Gradebook export with per-course letter-grade cutoffs
- Assignment emails queued to an outbox for every enrolled student
- Graded submissions lock, so students can't resubmit after seeing a score
- Pages depend on interfaces, so the app can swap between the EF Core and in-memory data layers

## Running it

Requires the .NET 10 SDK.

```bash
dotnet run --project LMS/CLI.LMS.Api                 # web client at /, Swagger at /swagger
dotnet test LMS/CLI.LMS.Api.Tests                    # tests
dotnet workload install maui                         # once, for the desktop client
dotnet run --project LMS/CLI.LMS.MAUI/CLI.LMS.MAUI -f net10.0-windows10.0.19041.0
```

The hosted API runs from the `Dockerfile` on Render (`render.yaml`). Its database resets to
the sample data when the service restarts.

## Origin

Started from a course skeleton for FSU COP 4870, which supplied the solution layout and empty
projects. The skeleton's MIT license is kept in `LICENSE`.
