namespace NX_MTD.Agent;

/// <summary>Tham số chạy của agent đồng bộ chấm công (đọc từ cấu hình, mục "Agent").</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Địa chỉ API backend, ví dụ http://localhost:5080.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>Khóa xác thực agent, trùng với Attendance:AgentApiKey phía backend.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Đường dẫn đầy đủ tới RJData.mdb trên máy cài Ronald Jack Pro.</summary>
    public string DbPath { get; set; } = string.Empty;

    /// <summary>OleDb provider đọc Access (.mdb).</summary>
    public string Provider { get; set; } = "Microsoft.ACE.OLEDB.12.0";

    /// <summary>Số lượt quẹt tối đa mỗi lô gửi lên API (trần của API là 5000).</summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>Chu kỳ chạy (phút) khi chưa lấy được mốc từ API. null hoặc &lt;= 0 thì dùng 10 phút.</summary>
    public int? PollIntervalMinutes { get; set; }
}
