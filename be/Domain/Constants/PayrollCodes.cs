namespace NX_MTD.Domain.Constants;

/// <summary>Trạng thái bảng lương tháng (Payroll.Status) — UC31/UC32, GBR-12.</summary>
public static class PayrollStatus
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

/// <summary>Nhóm khoản trên phiếu lương (PayslipItem.ItemGroup) — UC31 import Excel.</summary>
public static class PayslipItemGroup
{
    public const string Addition = "ADDITION";
    public const string Deduction = "DEDUCTION";
    public const string Insurance = "INSURANCE";
    public const string Info = "INFO";

    public static readonly string[] All = [Addition, Deduction, Insurance, Info];
}
