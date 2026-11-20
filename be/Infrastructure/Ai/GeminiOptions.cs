namespace NX_MTD.Infrastructure.Ai;

/// <summary>
/// DEV-ALL-148: cấu hình gọi Gemini (mục <c>Gemini</c>). <c>ApiKey</c> là secret: đặt trong User Secrets /
/// <c>appsettings.Development.json</c> (gitignore) hoặc biến môi trường <c>Gemini__ApiKey</c>, không commit.
/// </summary>
public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.1-flash-lite";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    public int TimeoutSeconds { get; set; } = 90;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Số token suy luận của model; 0 = tắt suy luận (nhanh, rẻ).</summary>
    public int ThinkingBudget { get; set; } = 1024;
}
