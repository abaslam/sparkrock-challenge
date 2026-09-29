using Attendance.API.Data;
using Attendance.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Attendance.API.Features.Attendance;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/schools/{schoolId:int}/attendance/{date}", SaveDailyAttendanceAsync)
            .WithName("SaveDailyAttendance")
            .WithSummary("Create or update one day of attendance for a set of students")
            .Produces<SaveDailyAttendanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> SaveDailyAttendanceAsync(
        int schoolId,
        DateOnly date,
        SaveDailyAttendanceRequest request,
        AttendanceDbContext database,
        CancellationToken cancellationToken)
    {
        var schoolExists = await database.Schools
            .AsNoTracking()
            .AnyAsync(x => x.Id == schoolId && x.Active, cancellationToken);

        if (!schoolExists)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "School not found",
                detail: $"Active school {schoolId} does not exist.");
        }

        var validationErrors = new Dictionary<string, string[]>();
        if (request.Records is null || request.Records.Count == 0)
        {
            validationErrors["records"] = ["At least one attendance record is required."];
            return Results.ValidationProblem(validationErrors);
        }

        foreach (var duplicate in request.Records
                     .Select((record, index) => new { record.StudentId, Index = index })
                     .GroupBy(x => x.StudentId)
                     .Where(group => group.Count() > 1))
        {
            foreach (var item in duplicate)
            {
                validationErrors[$"records[{item.Index}].studentId"] =
                    ["A student may appear only once in a daily submission."];
            }
        }

        for (var index = 0; index < request.Records.Count; index++)
        {
            var record = request.Records[index];
            if (string.IsNullOrWhiteSpace(record.AttendanceCode))
            {
                validationErrors[$"records[{index}].attendanceCode"] = ["Attendance code is required."];
            }
            else if (record.AttendanceCode.Trim().Length > 5)
            {
                validationErrors[$"records[{index}].attendanceCode"] = ["Attendance code cannot exceed 5 characters."];
            }

            if (record.MinutesLate < 0)
            {
                validationErrors[$"records[{index}].minutesLate"] = ["Minutes late cannot be negative."];
            }

            if (record.Notes?.Length > 500)
            {
                validationErrors[$"records[{index}].notes"] = ["Notes cannot exceed 500 characters."];
            }
        }

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var requestedStudentIds = request.Records.Select(x => x.StudentId).Distinct().ToArray();
        var students = await database.Students
            .AsNoTracking()
            .Where(x => requestedStudentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        for (var index = 0; index < request.Records.Count; index++)
        {
            var studentId = request.Records[index].StudentId;
            if (!students.TryGetValue(studentId, out var student) || !student.Active || student.SchoolId != schoolId)
            {
                validationErrors[$"records[{index}].studentId"] =
                    [$"Student {studentId} is not an active student at school {schoolId}."];
            }
        }

        var normalizedCodes = request.Records
            .Select(x => x.AttendanceCode!.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        var codes = await database.AttendanceCodes
            .AsNoTracking()
            .Where(x => normalizedCodes.Contains(x.Value) && x.Active)
            .ToDictionaryAsync(x => x.Value, cancellationToken);

        for (var index = 0; index < request.Records.Count; index++)
        {
            var code = request.Records[index].AttendanceCode!.Trim().ToUpperInvariant();
            if (!codes.ContainsKey(code))
            {
                validationErrors[$"records[{index}].attendanceCode"] =
                    [$"Attendance code '{code}' is not active or does not exist."];
            }
        }

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existingRecords = await database.AttendanceRecords
            .Where(x => x.AttendDate == date && requestedStudentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);

        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        foreach (var submitted in request.Records)
        {
            var normalizedCode = submitted.AttendanceCode!.Trim().ToUpperInvariant();
            var code = codes[normalizedCode];
            var normalizedNotes = string.IsNullOrWhiteSpace(submitted.Notes) ? null : submitted.Notes.Trim();

            if (existingRecords.TryGetValue(submitted.StudentId, out var attendance))
            {
                attendance.SchoolId = schoolId;
                attendance.AttendanceCode = code.Value;
                attendance.AttendanceCodeDescription = code.Description;
                attendance.IsAbsent = code.IsAbsent;
                attendance.IsExcused = code.IsExcused;
                attendance.MinutesLate = submitted.MinutesLate;
                attendance.Notes = normalizedNotes;
                attendance.ModifiedAtUtc = now;
                updated++;
            }
            else
            {
                database.AttendanceRecords.Add(new AttendanceRecord
                {
                    StudentId = submitted.StudentId,
                    SchoolId = schoolId,
                    AttendDate = date,
                    AttendanceCode = code.Value,
                    AttendanceCodeDescription = code.Description,
                    IsAbsent = code.IsAbsent,
                    IsExcused = code.IsExcused,
                    MinutesLate = submitted.MinutesLate,
                    Notes = normalizedNotes,
                    CreatedAtUtc = now
                });
                created++;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new SaveDailyAttendanceResponse(
            request.Records.Count,
            created,
            updated,
            SchoolYear.FromDate(date).ToString()));
    }
}

public sealed record SaveDailyAttendanceRequest(IReadOnlyList<AttendanceEntryRequest>? Records);

public sealed record AttendanceEntryRequest(
    int StudentId,
    string? AttendanceCode,
    int MinutesLate = 0,
    string? Notes = null);

public sealed record SaveDailyAttendanceResponse(
    int Submitted,
    int Created,
    int Updated,
    string SchoolYear);
