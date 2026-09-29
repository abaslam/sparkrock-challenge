namespace Attendance.API.Domain;

public sealed class AttendanceCode
{
    public required string Value { get; set; }
    public required string Description { get; set; }
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public bool Active { get; set; } = true;
}
