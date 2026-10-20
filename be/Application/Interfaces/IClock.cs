namespace NX_MTD.Application.Interfaces;

/// <summary>Giờ Việt Nam, thống nhất với dbo.fn_VnNow() mà DB dùng làm giá trị mặc định.</summary>
public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}
