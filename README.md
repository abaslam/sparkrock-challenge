# Attendance API

A clean .NET 8 replacement for the legacy VB6/SQL Server attendance module. It saves a school's daily attendance as an atomic batch, returns a student's September–August attendance history, and reports chronic absenteeism.

## Run locally

Requires the .NET 8 SDK. From this directory:

```powershell
dotnet restore
dotnet run --project .\Attendance.API\Attendance.API.csproj
```

The first run applies the EF Core migration and creates `Attendance.API/attendance.db`. Swagger is available at `http://localhost:5253/swagger`. Run the integration suite with `dotnet test`.

Seed data uses school `1` (threshold `10`), students `1001`, `1002`, and `1003`, and attendance codes `P` (present), `A` (absent), `E` (excused absence), and `L` (late). Runnable requests are in `Attendance.API/Attendance.API.http`.

| Operation | Endpoint |
| --- | --- |
| Save or replace students' attendance for a date | `PUT /api/schools/{schoolId}/attendance/{yyyy-MM-dd}` |
| Get history; school year is optional | `GET /api/students/{studentId}/attendance?schoolYear=2026-2027` |
| Get chronic-absence status | `GET /api/students/{studentId}/chronic-absence?schoolYear=2026-2027` |

## Architecture

ASP.NET Core Minimal APIs keep the small HTTP surface explicit. EF Core with SQLite provides a zero-setup durable local database, real transactions, constraints, and migrations. A unique `(StudentId, AttendDate)` index makes daily saves idempotent. Batch input is fully validated before one transaction writes anything. Attendance records snapshot the code description and absent/excused flags so later reference-data changes cannot rewrite history. Counts and chronic status are queried from attendance records instead of maintaining summary rows that can drift out of sync. Errors use standard Problem Details responses.

Tests exercise the HTTP endpoints through `WebApplicationFactory` against a fresh in-memory SQLite database, including batch creation, updates, atomic validation failures, school-year boundaries, ordering, and the chronic threshold.

## Legacy ambiguities

The supplied code referenced missing `Schools`, `SchoolTerms`, `AttendanceSubmissionLog`, roster procedure, and `SchoolYear` function definitions. Terms, submission auditing, Crystal Reports, the desktop UI, and alert lifecycle were therefore left out. School years follow the visible September–August rule. The chronic threshold defaults to the legacy fallback of 10. Both excused and unexcused codes count when marked absent, matching the stored procedure. UTC supplies audit timestamps and the default current school year. Authentication was not inferable and is outside this replacement's scope.
