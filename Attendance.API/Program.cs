using Attendance.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AttendanceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AttendanceDatabase")));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
    database.Database.Migrate();
}

app.MapGet("/", () => Results.Ok(new { service = "Attendance API", status = "ready" }))
    .WithName("GetServiceStatus")
    .WithOpenApi();

app.Run();

public partial class Program;
