namespace NX_MTD.Application.Common;

/// <summary>DEV-ALL-148: cấu hình AI gợi ý bậc (mục <c>GradeSuggestion</c> trong appsettings).</summary>
public sealed class GradeSuggestionOptions
{
    /// <summary>Bản gợi ý được giữ bao lâu để tổ trưởng xem lại.</summary>
    public int RetentionHours { get; set; } = 24;
}
