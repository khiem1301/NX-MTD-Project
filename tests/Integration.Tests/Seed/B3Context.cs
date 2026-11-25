using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>ID động của S0 Batch 3 (định mức mã hàng & công đoạn), dựng trên B2Context: PR1 = Pr1, PR2 = Pr2, OD1 = Od1.</summary>
public sealed class B3Context
{
    public ProductColor Pr1White { get; set; } = null!;
    public ProductVariant Pr1WhiteM { get; set; } = null!;
    public ProductMarker Marker1 { get; set; } = null!;   // OD1, PR1, MarkerNo 1, "S1", 5.20 × 1.50, ratio M:2 L:1

    public Operation Op1 { get; set; } = null!;   // CD01 thuộc OG1, chi tiết [C1]
    public Operation Op2 { get; set; } = null!;   // CD02
    public Operation Op3 { get; set; } = null!;   // CD99 cuối ACTIVE
    public Operation Op4 { get; set; } = null!;   // CD98 cuối, ngừng
    public Operation Op5 { get; set; } = null!;   // CD05 ngừng, thuộc OG1
    public OperationGroup Og1 { get; set; } = null!;
    public OperationGroup Og2 { get; set; } = null!;

    public PieceRate PrD1 { get; set; } = null!;  // OP1, 2026-12-01, DRAFT 1500
    public PieceRate PrP1 { get; set; } = null!;  // OP2, 2026-12-01, PENDING 2000
    public PieceRate PrA1 { get; set; } = null!;  // OP1, 2026-01-01, APPROVED 1200
    public PieceRate PrR1 { get; set; } = null!;  // OP2, 2026-11-01, REJECTED "Sai giá"

    public CustomerOrder Od2 { get; set; } = null!;
    public ProductColor Pr2Blue { get; set; } = null!;
    public ProductVariant Pr2BlueM { get; set; } = null!;
    public ProductComponent C9 { get; set; } = null!;
    public OperationGroup Og9 { get; set; } = null!;
    public Operation Op9 { get; set; } = null!;   // CD01 của PR2
    public PieceRate PrP9 { get; set; } = null!;  // OP9, PENDING
}
