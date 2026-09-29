namespace Attendance.API.Domain;

public sealed class Student
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Grade { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
