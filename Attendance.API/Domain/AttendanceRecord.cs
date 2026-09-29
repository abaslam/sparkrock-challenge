namespace Attendance.API.Domain;

public sealed class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public DateOnly AttendDate { get; set; }
    public required string AttendanceCode { get; set; }
    public required string AttendanceCodeDescription { get; set; }
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public int MinutesLate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public Student Student { get; set; } = null!;
    public School School { get; set; } = null!;
}
