using System.Text.RegularExpressions;

namespace SchoolAdmission.Api.Common;

public static class StudentIdValidator
{
    private static readonly Regex StudentIdRegex = new(@"^\d{17}$", RegexOptions.Compiled);

    public static (bool IsValid, string? ErrorMessage) Validate(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (false, "The 'X-Student-Id' HTTP header is required.");
        }

        var trimmed = studentId.Trim();
        if (!StudentIdRegex.IsMatch(trimmed))
        {
            return (false, "The Student ID must be a string containing exactly 17 numeric digits.");
        }

        return (true, null);
    }
}

