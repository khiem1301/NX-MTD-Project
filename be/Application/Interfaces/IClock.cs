namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Đồng hồ của hệ thống, luôn trả giờ Việt Nam (Rules phụ lục, mâu thuẫn #8):
/// DB lưu giờ VN qua fn_VnNow() vì các mốc 08:15 / 08:30 / hết ngày làm việc là giờ VN.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}
