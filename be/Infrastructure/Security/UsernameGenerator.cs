using System.Globalization;
using System.Text;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Security;

/// <summary>Họ tên Việt → username: Phạm Hoàng Thái → ThaiPH.</summary>
public sealed class UsernameGenerator : IUsernameGenerator
{
    public string FromFullName(string fullName)
    {
        var parts = (fullName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RemoveDiacritics)
            .Select(OnlyLetters)
            .Where(p => p.Length > 0)
            .ToArray();

        if (parts.Length == 0) return "User";

        var given = Capitalize(parts[^1]);
        if (parts.Length == 1) return given;

        var initials = string.Concat(parts.Take(parts.Length - 1).Select(p => char.ToUpperInvariant(p[0])));
        return given + initials;
    }

    private static string OnlyLetters(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetter(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd').Replace('Đ', 'D');
    }
}
