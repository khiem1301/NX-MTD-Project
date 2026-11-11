using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đơn từ (10 loại, gồm yêu cầu NVL / BTP)
/// </summary>
public partial class Request
{
    public int RequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public string RequestTypeCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? WorkOrderId { get; set; }

    public int? KpiTargetId { get; set; }

    public int? MaterialIssueId { get; set; }

    public int? MaterialReceiptId { get; set; }

    public int? DefectRecordId { get; set; }

    public DateOnly? TargetDate { get; set; }

    /// <summary>SHORT_DELIVERY giao bù từ 2 lần trở lên: ngày giao bù đến.</summary>
    public DateOnly? TargetDateTo { get; set; }

    /// <summary>SHORT_DELIVERY chọn giao bù: số lần dự kiến giao bù.</summary>
    public short? ExpectedDeliveryCount { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public int? CreatedBy { get; set; }

    public int? CreatedByTeamId { get; set; }

    /// <summary>Đơn Khác (DEV-ALL-91): vai trò xử lý người tạo chọn; gửi tổ trưởng thì kèm tổ của người tạo.</summary>
    public string? HandlerRoleCode { get; set; }

    public int? HandlerTeamId { get; set; }

    public virtual Team? HandlerTeam { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public string? ResultNote { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ShortageResolution { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual ICollection<AttendanceAdjustment> AttendanceAdjustments { get; set; } = new List<AttendanceAdjustment>();

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual Team? CreatedByTeam { get; set; }

    public virtual DefectRecord? DefectRecord { get; set; }

    public virtual KpiTarget? KpiTarget { get; set; }

    public virtual MaterialIssue? MaterialIssue { get; set; }

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public virtual MaterialReceipt? MaterialReceipt { get; set; }

    public virtual ICollection<MaterialReceipt> MaterialReceipts { get; set; } = new List<MaterialReceipt>();

    public virtual CustomerOrder? Order { get; set; }

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();

    public virtual WorkOrder? WorkOrder { get; set; }

    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
