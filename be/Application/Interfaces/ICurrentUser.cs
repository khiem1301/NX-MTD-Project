namespace NX_MTD.Application.Interfaces;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int AccountId { get; }
    int EmployeeId { get; }
    /// <summary>Chuỗi role trong Roles.cs, vd. "TeamLeader".</summary>
    string Role { get; }
    int? TeamId { get; }
    /// <summary>Mã loại tổ trong DB: CUTTING / SEWING / OFFICE; null nếu không có tổ.</summary>
    string? TeamTypeCode { get; }
}
