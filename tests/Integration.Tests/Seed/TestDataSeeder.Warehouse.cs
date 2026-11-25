using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedWarehouseAsync(CancellationToken ct)
    {
        _ctx.WorkOrderOpen = Wo("WO-IT-OPEN", WorkOrderStatuses.Open);
        _ctx.WorkOrderCutting = Wo("WO-IT-CUT", WorkOrderStatuses.Cutting);
        _ctx.WorkOrderDone = Wo("WO-IT-DONE", WorkOrderStatuses.Completed, completedAt: _now.AddDays(-1));
        _ctx.WorkOrderCancelled = Wo("WO-IT-CANC", WorkOrderStatuses.Cancelled);
        _db.WorkOrders.AddRange(
            _ctx.WorkOrderOpen, _ctx.WorkOrderCutting, _ctx.WorkOrderDone, _ctx.WorkOrderCancelled);

        _ctx.ReceiptPending = new MaterialReceipt
        {
            ReceiptCode = "GRN-IT-01",
            OrderId = _ctx.OrderInProduction.OrderId,
            Source = MaterialSources.Customer,
            ReceiptDate = _t,
            Status = ReceiptStatuses.Pending,
            SubmittedAt = _now,
        };
        _ctx.ReceiptConfirmed = new MaterialReceipt
        {
            ReceiptCode = "GRN-IT-02",
            OrderId = _ctx.OrderInProduction.OrderId,
            Source = MaterialSources.Customer,
            ReceiptDate = _t.AddDays(-3),
            Status = ReceiptStatuses.Confirmed,
            SubmittedAt = _now.AddDays(-3),
            ConfirmedBy = _ctx.Warehouse01.EmployeeId,
            ConfirmedAt = _now.AddDays(-3),
        };
        _ctx.ReceiptReturned = new MaterialReceipt
        {
            ReceiptCode = "GRN-IT-03",
            OrderId = _ctx.OrderInProduction.OrderId,
            Source = MaterialSources.Customer,
            ReceiptDate = _t.AddDays(-2),
            Status = ReceiptStatuses.Returned,
            ReturnReason = "IT return reason",
            SubmittedAt = _now.AddDays(-2),
        };
        _db.MaterialReceipts.AddRange(_ctx.ReceiptPending, _ctx.ReceiptConfirmed, _ctx.ReceiptReturned);
        await _db.SaveChangesAsync(ct);

        _db.MaterialReceiptLines.AddRange(
            new MaterialReceiptLine
            {
                MaterialReceiptId = _ctx.ReceiptPending.MaterialReceiptId,
                MaterialId = _ctx.MatFab01.MaterialId,
                AcceptedQty = 50m,
            },
            new MaterialReceiptLine
            {
                MaterialReceiptId = _ctx.ReceiptConfirmed.MaterialReceiptId,
                MaterialId = _ctx.MatFab01.MaterialId,
                AcceptedQty = 500m,
            },
            new MaterialReceiptLine
            {
                MaterialReceiptId = _ctx.ReceiptReturned.MaterialReceiptId,
                MaterialId = _ctx.MatFab02.MaterialId,
                AcceptedQty = 10m,
            });

        _ctx.IssuePending = new MaterialIssue
        {
            IssueCode = "ISS-IT-01",
            WarehouseType = WarehouseTypes.Material,
            OrderId = _ctx.OrderInProduction.OrderId,
            ReceivingTeamId = _ctx.TeamCut01.TeamId,
            IssueDate = _t,
            IssuedBy = _ctx.Warehouse01.EmployeeId,
            Status = IssueStatuses.PendingReceipt,
            CreatedAt = _now,
        };
        _ctx.IssueReceived = new MaterialIssue
        {
            IssueCode = "ISS-IT-02",
            WarehouseType = WarehouseTypes.Material,
            OrderId = _ctx.OrderInProduction.OrderId,
            ReceivingTeamId = _ctx.TeamCut01.TeamId,
            IssueDate = _t.AddDays(-1),
            IssuedBy = _ctx.Warehouse01.EmployeeId,
            Status = IssueStatuses.Received,
            ReceivedBy = _ctx.WorkerCut01.EmployeeId,
            ReceivedAt = _now.AddDays(-1),
            CreatedAt = _now.AddDays(-1),
        };
        _ctx.IssueRejected = new MaterialIssue
        {
            IssueCode = "ISS-IT-03",
            WarehouseType = WarehouseTypes.Material,
            OrderId = _ctx.OrderInProduction.OrderId,
            ReceivingTeamId = _ctx.TeamCut01.TeamId,
            IssueDate = _t.AddDays(-1),
            IssuedBy = _ctx.Warehouse01.EmployeeId,
            Status = IssueStatuses.Rejected,
            RejectReason = "IT reject issue",
            CreatedAt = _now.AddDays(-1),
        };
        _db.MaterialIssues.AddRange(_ctx.IssuePending, _ctx.IssueReceived, _ctx.IssueRejected);
        await _db.SaveChangesAsync(ct);

        // ISS-IT-01 xin nhiều hơn tồn MAT-FAB-02 (thiếu hàng)
        _db.MaterialIssueLines.AddRange(
            new MaterialIssueLine
            {
                MaterialIssueId = _ctx.IssuePending.MaterialIssueId,
                MaterialId = _ctx.MatFab02.MaterialId,
                IssuedQty = 100m,
            },
            new MaterialIssueLine
            {
                MaterialIssueId = _ctx.IssueReceived.MaterialIssueId,
                MaterialId = _ctx.MatFab01.MaterialId,
                IssuedQty = 20m,
                ConfirmedQty = 20m,
            },
            new MaterialIssueLine
            {
                MaterialIssueId = _ctx.IssueRejected.MaterialIssueId,
                MaterialId = _ctx.MatFab01.MaterialId,
                IssuedQty = 5m,
            });

        // OnHandQty chỉ đổi qua usp_Stock_Post (trg_StockBalance_Check).
        await PostStockAsync(
            WarehouseTypes.Material, _ctx.MatFab01.MaterialId, 1_000m,
            StockDocTypes.MaterialReceipt, _ctx.ReceiptConfirmed.MaterialReceiptId, ct);
        await PostStockAsync(
            WarehouseTypes.Material, _ctx.MatFab02.MaterialId, 10m, // thiếu so với ISS-IT-01 (100)
            StockDocTypes.MaterialReceipt, _ctx.ReceiptConfirmed.MaterialReceiptId, ct);

        _ctx.StockFab01 = await _db.StockBalances.AsNoTracking().SingleAsync(
            s => s.WarehouseType == WarehouseTypes.Material && s.MaterialId == _ctx.MatFab01.MaterialId, ct);
        _ctx.StockFab02 = await _db.StockBalances.AsNoTracking().SingleAsync(
            s => s.WarehouseType == WarehouseTypes.Material && s.MaterialId == _ctx.MatFab02.MaterialId, ct);

        _ctx.StocktakePending = new Stocktake
        {
            StocktakeCode = "STK-IT-01",
            WarehouseType = WarehouseTypes.Material,
            StocktakeDate = _t,
            Status = StocktakeStatuses.PendingApproval,
            CreatedAt = _now,
        };
        _ctx.StocktakeApproved = new Stocktake
        {
            StocktakeCode = "STK-IT-02",
            WarehouseType = WarehouseTypes.Material,
            StocktakeDate = _t.AddDays(-7),
            Status = StocktakeStatuses.Approved,
            CreatedAt = _now.AddDays(-7),
            ApprovedAt = _now.AddDays(-6),
        };
        _ctx.StocktakeReturned = new Stocktake
        {
            StocktakeCode = "STK-IT-03",
            WarehouseType = WarehouseTypes.Material,
            StocktakeDate = _t.AddDays(-3),
            Status = StocktakeStatuses.Returned,
            ReturnReason = "IT stocktake returned",
            CreatedAt = _now.AddDays(-3),
        };
        _db.Stocktakes.AddRange(_ctx.StocktakePending, _ctx.StocktakeApproved, _ctx.StocktakeReturned);
        await _db.SaveChangesAsync(ct);
    }

    private WorkOrder Wo(string code, string status, DateTime? completedAt = null) => new()
    {
        WorkOrderCode = code,
        OrderId = _ctx.OrderInProduction.OrderId,
        ProductId = _ctx.Product001.ProductId,
        CuttingTeamId = _ctx.TeamCut01.TeamId,
        DueDate = _t.AddDays(7),
        Status = status,
        FabricTolerancePercent = 5m,
        CreatedAt = _now,
        CompletedAt = completedAt,
    };

    private async Task PostStockAsync(
        string warehouseType,
        int materialId,
        decimal qtyChange,
        string sourceDocType,
        int sourceDocId,
        CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            EXEC dbo.usp_Stock_Post
                @WarehouseType = {warehouseType},
                @MaterialId = {materialId},
                @OrderLineId = {null},
                @ProductComponentMaterialId = {null},
                @StockBalanceId = {null},
                @SourceDocType = {sourceDocType},
                @SourceDocId = {sourceDocId},
                @SourceLineId = {null},
                @QtyChange = {qtyChange},
                @TransactionDate = {_t};
            """, ct);
    }
}
