using System.Globalization;
using System.Text.RegularExpressions;

namespace Attendance.API.Domain;

public readonly partial record struct SchoolYear(int StartYear)
{
    public DateOnly StartDate => new(StartYear, 9, 1);
    public DateOnly EndDate => new(StartYear + 1, 8, 31);

    public static SchoolYear FromDate(DateOnly date) =>
        new(date.Month >= 9 ? date.Year : date.Year - 1);

    public static bool TryParse(string? value, out SchoolYear schoolYear)
    {
        schoolYear = default;
        if (value is null)
        {
            return false;
        }

        var match = SchoolYearPattern().Match(value);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var startYear) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var endYear) ||
            endYear != startYear + 1)
        {
            return false;
        }

        schoolYear = new SchoolYear(startYear);
        return true;
    }

    public override string ToString() => $"{StartYear:D4}-{StartYear + 1:D4}";

    [GeneratedRegex(@"^(\d{4})-(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex SchoolYearPattern();
}
