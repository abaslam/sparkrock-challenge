using System.Net;
using System.Net.Http.Json;
using Attendance.API.Data;
using Attendance.API.Domain;
using Attendance.API.Features.Attendance;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Attendance.API.Tests;

public sealed class AttendanceEndpointsTests
{
    [Fact]
    public async Task SaveDailyAttendance_CreatesMultipleStudentRecords()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        var response = await SaveAsync(client, new DateOnly(2026, 9, 15),
            new(1001, "A", Notes: "Ill"),
            new(1002, "L", MinutesLate: 12));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SaveDailyAttendanceResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Submitted);
        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal("2026-2027", result.SchoolYear);
    }

    [Fact]
    public async Task SaveDailyAttendance_UpdatesExistingStudentDateWithoutDuplicating()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();
        var date = new DateOnly(2026, 10, 1);

        (await SaveAsync(client, date, new AttendanceEntryRequest(1001, "P"))).EnsureSuccessStatusCode();
        var update = await SaveAsync(client, date, new AttendanceEntryRequest(1001, "E", Notes: "Medical"));

        update.EnsureSuccessStatusCode();
        var saveResult = await update.Content.ReadFromJsonAsync<SaveDailyAttendanceResponse>();
        Assert.NotNull(saveResult);
        Assert.Equal(0, saveResult.Created);
        Assert.Equal(1, saveResult.Updated);

        var history = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>(
            "/api/students/1001/attendance?schoolYear=2026-2027");
        Assert.NotNull(history);
        var record = Assert.Single(history.Records);
        Assert.Equal("E", record.AttendanceCode);
        Assert.Equal("Medical", record.Notes);
    }

    [Fact]
    public async Task SaveDailyAttendance_InvalidStudentRejectsWholeBatch()
    {
        await AssertRejectedAtomicallyAsync(
            new AttendanceEntryRequest(9999, "P"));
    }

    [Fact]
    public async Task SaveDailyAttendance_InvalidCodeRejectsWholeBatch()
    {
        await AssertRejectedAtomicallyAsync(
            new AttendanceEntryRequest(1002, "UNKNOWN"));
    }

    [Fact]
    public async Task SaveDailyAttendance_DuplicateStudentRejectsWholeBatch()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        var response = await SaveAsync(client, new DateOnly(2026, 11, 3),
            new(1001, "P"),
            new(1001, "A"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var history = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>(
            "/api/students/1001/attendance?schoolYear=2026-2027");
        Assert.NotNull(history);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task SaveDailyAttendance_StudentFromAnotherSchoolRejectsWholeBatch()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
            database.Schools.Add(new School { Id = 2, Name = "Southwind Academy" });
            database.Students.Add(new Student
            {
                Id = 2001,
                SchoolId = 2,
                FirstName = "Taylor",
                LastName = "Nguyen",
                Grade = "8",
                Active = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            await database.SaveChangesAsync();
        }

        var response = await SaveAsync(client, new DateOnly(2026, 11, 4),
            new(1001, "P"),
            new(2001, "P"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var history = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>(
            "/api/students/1001/attendance?schoolYear=2026-2027");
        Assert.NotNull(history);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task AttendanceHistory_FiltersBySchoolYearAndSortsNewestFirst()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        (await SaveAsync(client, new DateOnly(2025, 8, 31), new AttendanceEntryRequest(1001, "P"))).EnsureSuccessStatusCode();
        (await SaveAsync(client, new DateOnly(2025, 9, 1), new AttendanceEntryRequest(1001, "A"))).EnsureSuccessStatusCode();
        (await SaveAsync(client, new DateOnly(2026, 8, 31), new AttendanceEntryRequest(1001, "E"))).EnsureSuccessStatusCode();

        var history = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>(
            "/api/students/1001/attendance?schoolYear=2025-2026");

        Assert.NotNull(history);
        Assert.Equal("2025-2026", history.SchoolYear);
        Assert.Equal(2, history.Records.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), history.Records[0].Date);
        Assert.Equal(new DateOnly(2025, 9, 1), history.Records[1].Date);
    }

    [Fact]
    public async Task ChronicAbsence_CountsExcusedAndUnexcusedAbsencesAtThreshold()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        for (var day = 1; day <= 10; day++)
        {
            var code = day % 2 == 0 ? "E" : "A";
            (await SaveAsync(client, new DateOnly(2026, 9, day), new AttendanceEntryRequest(1001, code))).EnsureSuccessStatusCode();
        }

        var status = await client.GetFromJsonAsync<ChronicAbsenceStatusResponse>(
            "/api/students/1001/chronic-absence?schoolYear=2026-2027");

        Assert.NotNull(status);
        Assert.Equal(10, status.TotalAbsences);
        Assert.Equal(10, status.Threshold);
        Assert.True(status.IsChronicallyAbsent);
    }

    [Fact]
    public async Task ChronicAbsence_ReportsZeroWhenStudentHasNoAbsences()
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<ChronicAbsenceStatusResponse>(
            "/api/students/1003/chronic-absence?schoolYear=2026-2027");

        Assert.NotNull(status);
        Assert.Equal(0, status.TotalAbsences);
        Assert.False(status.IsChronicallyAbsent);
    }

    [Theory]
    [InlineData("/api/students/9999/attendance?schoolYear=2026-2027", HttpStatusCode.NotFound)]
    [InlineData("/api/students/9999/chronic-absence?schoolYear=2026-2027", HttpStatusCode.NotFound)]
    [InlineData("/api/students/1001/attendance?schoolYear=2026-28", HttpStatusCode.BadRequest)]
    [InlineData("/api/students/1001/chronic-absence?schoolYear=2026-2028", HttpStatusCode.BadRequest)]
    public async Task QueryEndpoints_ReturnExpectedErrors(string uri, HttpStatusCode expectedStatus)
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(uri);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task AssertRejectedAtomicallyAsync(AttendanceEntryRequest invalidRecord)
    {
        using var factory = new AttendanceApiFactory();
        using var client = factory.CreateClient();

        var response = await SaveAsync(client, new DateOnly(2026, 11, 2),
            new(1001, "P"),
            invalidRecord);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var history = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>(
            "/api/students/1001/attendance?schoolYear=2026-2027");
        Assert.NotNull(history);
        Assert.Empty(history.Records);
    }

    private static Task<HttpResponseMessage> SaveAsync(
        HttpClient client,
        DateOnly date,
        params AttendanceEntryRequest[] records) =>
        client.PutAsJsonAsync(
            $"/api/schools/1/attendance/{date:yyyy-MM-dd}",
            new SaveDailyAttendanceRequest(records));
}
