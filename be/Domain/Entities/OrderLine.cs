using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng đơn mã hàng–màu–size
/// </summary>
public partial class OrderLine
{
    public int OrderLineId { get; set; }

    public int OrderId { get; set; }

    public int ProductVariantId { get; set; }

    public int ProductId { get; set; }

    public int OrderedQty { get; set; }

    public decimal CmtUnitPrice { get; set; }

    public virtual ICollection<CutHandoverLine> CutHandoverLines { get; set; } = new List<CutHandoverLine>();

    public virtual ICollection<CuttingLayLine> CuttingLayLines { get; set; } = new List<CuttingLayLine>();

    public virtual ICollection<DefectRecord> DefectRecords { get; set; } = new List<DefectRecord>();

    public virtual ICollection<DispatchNoteLine> DispatchNoteLines { get; set; } = new List<DispatchNoteLine>();

    public virtual ICollection<FgHandoverLine> FgHandoverLines { get; set; } = new List<FgHandoverLine>();

    public virtual ICollection<MaterialIssueLine> MaterialIssueLines { get; set; } = new List<MaterialIssueLine>();

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual ICollection<OutputRecord> OutputRecords { get; set; } = new List<OutputRecord>();

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual ICollection<QcBatch> QcBatches { get; set; } = new List<QcBatch>();

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<WorkAllocation> WorkAllocations { get; set; } = new List<WorkAllocation>();

    public virtual ICollection<WorkOrderLine> WorkOrderLines { get; set; } = new List<WorkOrderLine>();
}
