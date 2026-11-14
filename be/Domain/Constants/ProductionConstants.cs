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

    /// <summary>Tổ trưởng nhập sai sản lượng công đoạn: trừ khỏi sản lượng và tiền của công nhân, không phải hàng lỗi vật lý (DEV-ALL-79).</summary>
    public const string OutputError = "OUTPUT_ERR";

    public static readonly IReadOnlyList<string> All = new[] { Rework, Scrap, OutputError };
}

/// <summary>DefectRecord.Status.</summary>
public static class DefectStatuses
{
    public const string Recorded = "RECORDED";
    /// <summary>Chỉ còn ở dữ liệu cũ (từ thời công nhân được phản đối, DEV-ALL-106): ứng dụng không còn chuyển phiếu sang trạng thái này, nhưng phiếu cũ vẫn tính còn hiệu lực và sửa / hủy được như phiếu đã ghi.</summary>
    public const string Confirmed = "CONFIRMED";
    public const string Cancelled = "CANCELLED";
    /// <summary>Chỉ database dùng (CK_DR_Status, trigger khóa tháng); ứng dụng chưa chuyển phiếu sang trạng thái này nên không có trong <see cref="All"/> và giao diện.</summary>
    public const string Locked = "LOCKED";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Recorded, Confirmed, Cancelled };

    /// <summary>Phiếu còn hiệu lực nên còn sửa / hủy được (nếu qua các ràng buộc ở DefectService).</summary>
    public static readonly IReadOnlySet<string> Editable = new HashSet<string> { Recorded, Confirmed };
}

/// <summary>Tổ trưởng / tổ phó chỉ được sửa, hủy phiếu hàng hỏng trong số giờ này kể từ lúc lập phiếu (DEV-ALL-106). Quản đốc / Admin không bị giới hạn giờ.</summary>
public static class DefectEditWindow
{
    public const int Hours = 24;
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
