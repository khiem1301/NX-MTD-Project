namespace NX_MTD.Domain.Constants;

public static class CodeText
{
    /// <summary>"CUTTING" → "Cutting", "TEAM_LEADER" → "TeamLeader". Enum trả API là chuỗi PascalCase.</summary>
    public static string ToPascal(string code) =>
        string.Concat(code.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));

    /// <summary>"PendingApproval" → "PENDING_APPROVAL". Dùng khi API nhận giá trị PascalCase (D22).</summary>
    public static string ToCode(string pascal) =>
        System.Text.RegularExpressions.Regex.Replace(pascal.Trim(), "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
}
