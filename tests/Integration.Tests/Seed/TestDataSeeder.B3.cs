using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>S0 của IT_B3: chỉ bổ sung phần còn thiếu so với S0 Batch 2 (màu Trắng, bìa, công đoạn, nhóm, đơn giá, dữ liệu PR2).</summary>
public sealed partial class TestDataSeeder
{
    public async Task<B3Context> SeedBatch3BaseAsync(B2Context b2, CancellationToken ct = default)
    {
        var k = new B3Context();
        var pid = b2.Pr1.ProductId;

        // --- PR1: màu Trắng, nhóm bìa 1, bìa S1 của OD1 ---
        k.Pr1White = new ProductColor { ProductId = pid, Color = "Trắng", CreatedAt = _now };
        _db.ProductColors.Add(k.Pr1White);
        await _db.SaveChangesAsync(ct);
        k.Pr1WhiteM = new ProductVariant { ProductId = pid, ProductColorId = k.Pr1White.ProductColorId, SizeCode = "M", Status = RecordStatus.Active };
        _db.ProductVariants.Add(k.Pr1WhiteM);
        _db.ProductMarkerComponents.Add(new ProductMarkerComponent { ProductId = pid, MarkerNo = 1, ProductComponentId = b2.Pr1Front.ProductComponentId });
        _db.ProductMarkerFabrics.Add(new ProductMarkerFabric
        {
            ProductId = pid, MarkerNo = 1, ProductColorId = b2.Pr1Black.ProductColorId, MaterialId = b2.Ma1.MaterialId,
        });
        await _db.SaveChangesAsync(ct);

        k.Marker1 = new ProductMarker
        {
            OrderId = b2.Od1.OrderId, ProductId = pid, MarkerNo = 1, MarkerCode = "S1", LengthM = 5.20m, WidthM = 1.50m, Status = RecordStatus.Active,
        };
        _db.ProductMarkers.Add(k.Marker1);
        await _db.SaveChangesAsync(ct);
        _db.ProductMarkerRatios.AddRange(
            new ProductMarkerRatio { ProductMarkerId = k.Marker1.ProductMarkerId, SizeCode = "M", PiecesPerPly = 2 },
            new ProductMarkerRatio { ProductMarkerId = k.Marker1.ProductMarkerId, SizeCode = "L", PiecesPerPly = 1 });

        // --- PR1: nhóm công đoạn, công đoạn, chi tiết ---
        k.Og1 = new OperationGroup { ProductId = pid, GroupName = "Nhóm May" };
        k.Og2 = new OperationGroup { ProductId = pid, GroupName = "Nhóm Rỗng" };
        _db.OperationGroups.AddRange(k.Og1, k.Og2);
        await _db.SaveChangesAsync(ct);

        k.Op1 = await InsertOpAsync(pid, "CD01", 1, false, RecordStatus.Active, k.Og1.OperationGroupId, ct);
        k.Op2 = await InsertOpAsync(pid, "CD02", 2, false, RecordStatus.Active, null, ct);
        k.Op3 = await InsertOpAsync(pid, "CD99", 99, true, RecordStatus.Active, null, ct);
        k.Op4 = await InsertOpAsync(pid, "CD98", 98, true, RecordStatus.Discontinued, null, ct);
        k.Op5 = await InsertOpAsync(pid, "CD05", 5, false, RecordStatus.Discontinued, k.Og1.OperationGroupId, ct);
        // FK ghép (OperationId, ProductId) làm EF không resolve được khóa tạm: chèn thẳng bằng SQL.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.OperationComponent (OperationId, ProductComponentId, ProductId) VALUES ({k.Op1.OperationId}, {b2.Pr1Front.ProductComponentId}, {pid})", ct);

        k.PrD1 = Rate(k.Op1, new DateOnly(2026, 12, 1), 1500m, PieceRateStatuses.Draft);
        k.PrP1 = Rate(k.Op2, new DateOnly(2026, 12, 1), 2000m, PieceRateStatuses.PendingApproval);
        k.PrA1 = Rate(k.Op1, new DateOnly(2026, 1, 1), 1200m, PieceRateStatuses.Approved);
        k.PrR1 = Rate(k.Op2, new DateOnly(2026, 11, 1), 1900m, PieceRateStatuses.Rejected);
        k.PrR1.DecisionNote = "Sai giá";
        _db.PieceRates.AddRange(k.PrD1, k.PrP1, k.PrA1, k.PrR1);

        // --- PR2: đơn OD2, chi tiết C9, nhóm OG9, công đoạn OP9 (trùng mã CD01 với OP1), đơn giá chờ duyệt ---
        k.Od2 = await SeedB2OrderForPr2Async(ct);
        k.Pr2Blue = _db.ProductColors.Local.Single(c => c.ProductId == b2.Pr2.ProductId);
        k.Pr2BlueM = _db.ProductVariants.Local.Single(v => v.ProductId == b2.Pr2.ProductId);
        k.C9 = new ProductComponent
        {
            ProductId = b2.Pr2.ProductId, ComponentName = "Thân sau", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active,
        };
        k.Og9 = new OperationGroup { ProductId = b2.Pr2.ProductId, GroupName = "Nhóm B" };
        _db.ProductComponents.Add(k.C9);
        _db.OperationGroups.Add(k.Og9);
        await _db.SaveChangesAsync(ct);

        k.Op9 = await InsertOpAsync(b2.Pr2.ProductId, "CD01", 1, false, RecordStatus.Active, null, ct);
        k.PrP9 = Rate(k.Op9, new DateOnly(2026, 12, 1), 2100m, PieceRateStatuses.PendingApproval);
        _db.PieceRates.Add(k.PrP9);
        await _db.SaveChangesAsync(ct);
        return k;
    }

    /// <summary>
    /// Chèn bằng SQL: EF cấu hình Operation↔Product là 1-1 (HasOne…WithOne) nên khi nạp nhiều Operation cùng ProductId, fixup đặt lại
    /// khóa ngoại của dòng trước (xem IT_B3_068). Trả về entity không được theo dõi.
    /// </summary>
    private async Task<Operation> InsertOpAsync(int productId, string code, short seq, bool final, string status, int? groupId, CancellationToken ct)
    {
        var name = "Công đoạn " + code;
        var id = (await _db.Database.SqlQuery<int>($"""
            INSERT INTO dbo.Operation (ProductId, OperationGroupId, OperationCode, OperationName, StationSequence, TeamType, IsFinalStep, Status)
            OUTPUT INSERTED.OperationId AS [Value]
            VALUES ({productId}, {groupId}, {code}, {name}, {seq}, {TeamTypes.Sewing}, {final}, {status})
            """).ToListAsync(ct)).Single();
        return new Operation
        {
            OperationId = id, ProductId = productId, OperationCode = code, OperationName = name, StationSequence = seq,
            TeamType = TeamTypes.Sewing, IsFinalStep = final, Status = status, OperationGroupId = groupId,
        };
    }

    private PieceRate Rate(Operation op, DateOnly date, decimal rate, string status) => new()
    {
        OperationId = op.OperationId, EffectiveDate = date, Rate = rate, Status = status,
        SubmittedAt = status == PieceRateStatuses.Draft ? null : _now,
        ApprovedAt = status == PieceRateStatuses.Approved ? _now : null,
    };
}
