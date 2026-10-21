namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Đọc tham số trong bảng SystemSetting. Giá trị chưa chốt phải cấu hình được, không hard-code
/// (Rules mục 0.8); khi thiếu khóa thì dùng giá trị mặc định truyền vào.
/// </summary>
public interface ISystemSettingReader
{
    Task<string?> GetAsync(string key, CancellationToken ct);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct);
    Task<TimeOnly> GetTimeAsync(string key, TimeOnly fallback, CancellationToken ct);
}
