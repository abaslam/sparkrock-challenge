using Attendance.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Attendance.API.Data;

public sealed class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<AttendanceCode> AttendanceCodes => Set<AttendanceCode>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var school = modelBuilder.Entity<School>();
        school.Property(x => x.Name).HasMaxLength(200);
        school.HasData(new School
        {
            Id = 1,
            Name = "Northwind Academy",
            AbsenceAlertThreshold = 10,
            Active = true
        });

        var student = modelBuilder.Entity<Student>();
        student.Property(x => x.FirstName).HasMaxLength(100);
        student.Property(x => x.LastName).HasMaxLength(100);
        student.Property(x => x.Grade).HasMaxLength(10);
        student.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId);
        student.HasData(
            new Student { Id = 1001, SchoolId = 1, FirstName = "Avery", LastName = "Chen", Grade = "8", Active = true, CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Student { Id = 1002, SchoolId = 1, FirstName = "Jordan", LastName = "Patel", Grade = "8", Active = true, CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Student { Id = 1003, SchoolId = 1, FirstName = "Morgan", LastName = "Rivera", Grade = "9", Active = true, CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        var code = modelBuilder.Entity<AttendanceCode>();
        code.HasKey(x => x.Value);
        code.Property(x => x.Value).HasMaxLength(5);
        code.Property(x => x.Description).HasMaxLength(100);
        code.HasData(
            new AttendanceCode { Value = "P", Description = "Present", IsAbsent = false, IsExcused = false, Active = true },
            new AttendanceCode { Value = "A", Description = "Absent", IsAbsent = true, IsExcused = false, Active = true },
            new AttendanceCode { Value = "E", Description = "Excused absence", IsAbsent = true, IsExcused = true, Active = true },
            new AttendanceCode { Value = "L", Description = "Late", IsAbsent = false, IsExcused = false, Active = true });

        var attendance = modelBuilder.Entity<AttendanceRecord>();
        attendance.HasIndex(x => new { x.StudentId, x.AttendDate }).IsUnique();
        attendance.Property(x => x.AttendanceCode).HasMaxLength(5);
        attendance.Property(x => x.AttendanceCodeDescription).HasMaxLength(100);
        attendance.Property(x => x.Notes).HasMaxLength(500);
        attendance.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        attendance.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
