namespace NX_MTD.Domain.Constants;

// Luồng may + QC (KI-01…KI-11, Minh làm hộ Kiều). Mã DB khớp CHECK trong NX-MTD_Database.sql.

/// <summary>Operation.Status.</summary>
public static class OperationStatuses
{
    public const string Active = "ACTIVE";
    public const string Discontinued = "DISCONTINUED";
}

/// <summary>PieceRate.Status — DRAFT → PENDING_APPROVAL → APPROVED / REJECTED (UC25, UC26).</summary>
public static class PieceRateStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    /// <summary>Kế toán chỉ sửa được khi chưa gửi duyệt hoặc đã bị từ chối.</summary>
    public static readonly IReadOnlySet<string> Editable = new HashSet<string> { Draft, Rejected };
}

/// <summary>DefectRecord.DefectType: REWORK không trừ sản lượng, SCRAP trừ (UC56).</summary>
public static class DefectTypes
{
    public const string Rework = "REWORK";
    public const string Scrap = "SCRAP";
}

/// <summary>DefectRecord.Status.</summary>
public static class DefectStatuses
{
    public const string Recorded = "RECORDED";
    public const string Cancelled = "CANCELLED";
    public const string Locked = "LOCKED";
}

/// <summary>DefectRecord.DefectReasonCode. OTHER bắt buộc ghi chú (CK_DR_Other).</summary>
public static class DefectReasons
{
    public const string SkippedStitch = "SKIPPED_STITCH";
    public const string CrookedSeam = "CROOKED_SEAM";
    public const string WrongSize = "WRONG_SIZE";
    public const string Torn = "TORN";
    public const string Stained = "STAINED";
    public const string Other = "OTHER";

    /// <summary>Các lý do người dùng được chọn khi ghi phiếu hàng hỏng.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        SkippedStitch, CrookedSeam, WrongSize, Torn, Stained, Other,
    };
}

/// <summary>OutputRecord.Status.</summary>
public static class OutputStatuses
{
    public const string Saved = "SAVED";
    public const string Locked = "LOCKED";
}
