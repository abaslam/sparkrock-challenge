namespace Attendance.API.Domain;

public sealed class School
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int AbsenceAlertThreshold { get; set; } = 10;
    public bool Active { get; set; } = true;
}
