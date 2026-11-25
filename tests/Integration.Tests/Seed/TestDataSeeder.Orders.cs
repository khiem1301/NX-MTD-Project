using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedOrdersAsync(CancellationToken ct)
    {
        _ctx.OrderNew = Order("ORD-IT-NEW", OrderStatuses.New);
        _ctx.OrderInProduction = Order("ORD-IT-PROD", OrderStatuses.InProduction);
        _ctx.OrderInDelivery = Order("ORD-IT-INDLV", OrderStatuses.InDelivery);
        _ctx.OrderPartiallyDelivered = Order("ORD-IT-PART", OrderStatuses.PartiallyDelivered);
        _ctx.OrderDelivered = Order("ORD-IT-DLV", OrderStatuses.Delivered);
        _ctx.OrderCompleted = Order("ORD-IT-DONE", OrderStatuses.Completed, finalAmount: 10_000_000m, finalReason: "Chốt IT");
        _ctx.OrderStopped = Order("ORD-IT-STOP", OrderStatuses.Stopped, stopReason: "IT stop reason");
        _ctx.OrderCancelled = Order("ORD-IT-CANC", OrderStatuses.Cancelled);

        _db.CustomerOrders.AddRange(
            _ctx.OrderNew, _ctx.OrderInProduction, _ctx.OrderInDelivery, _ctx.OrderPartiallyDelivered,
            _ctx.OrderDelivered, _ctx.OrderCompleted, _ctx.OrderStopped, _ctx.OrderCancelled);
        await _db.SaveChangesAsync(ct);

        // 4 order lines trên ORD-IT-PROD
        var variants = new[]
        {
            _ctx.VariantBlackS, _ctx.VariantBlackM, _ctx.VariantWhiteS, _ctx.VariantWhiteM,
        };
        foreach (var v in variants)
        {
            var line = new OrderLine
            {
                OrderId = _ctx.OrderInProduction.OrderId,
                ProductVariantId = v.ProductVariantId,
                ProductId = _ctx.Product001.ProductId,
                OrderedQty = 100,
                CmtUnitPrice = 25_000m,
            };
            _db.OrderLines.Add(line);
            _ctx.OrderProdLines.Add(line);
        }

        await _db.SaveChangesAsync(ct);

        // 2 dòng → TEAM-SEW-A, 2 dòng → TEAM-SEW-B
        for (var i = 0; i < _ctx.OrderProdLines.Count; i++)
        {
            var teamId = i < 2 ? _ctx.TeamSewA.TeamId : _ctx.TeamSewB.TeamId;
            var olt = new OrderLineTeam
            {
                OrderLineId = _ctx.OrderProdLines[i].OrderLineId,
                SewingTeamId = teamId,
                AssignedAt = _now,
            };
            _db.OrderLineTeams.Add(olt);
            _ctx.OrderLineTeams.Add(olt);
        }

        _ctx.PaymentPartial = new OrderPayment
        {
            OrderId = _ctx.OrderDelivered.OrderId,
            PaymentDate = _t.AddDays(-5),
            Amount = 3_000_000m,
            PaymentMethod = PaymentMethods.BankTransfer,
            CreatedAt = _now,
        };
        _ctx.PaymentFull = new OrderPayment
        {
            OrderId = _ctx.OrderCompleted.OrderId,
            PaymentDate = _t.AddDays(-2),
            Amount = 10_000_000m,
            PaymentMethod = PaymentMethods.Cash,
            CreatedAt = _now,
        };
        _db.OrderPayments.AddRange(_ctx.PaymentPartial, _ctx.PaymentFull);
        await _db.SaveChangesAsync(ct);
    }

    private CustomerOrder Order(
        string code,
        string status,
        string? stopReason = null,
        decimal? finalAmount = null,
        string? finalReason = null) => new()
    {
        OrderCode = code,
        CustomerId = _ctx.CustomerParent.CustomerId,
        ReceivedDate = _t.AddDays(-14),
        DueDate = _t.AddDays(30),
        MaterialStatus = OrderMaterialStatuses.Partial,
        Status = status,
        StopReason = stopReason,
        FinalAmount = finalAmount,
        FinalAmountReason = finalReason,
        DeliveryAddress = "IT Delivery Address",
        CreatedAt = _now,
    };

    private async Task SeedOperationsAndRatesAsync(CancellationToken ct)
    {
        // EF scaffold map Product↔Operation thành WithOne (sai so với DB 1–n).
        // Insert từng dòng + Clear tracker để tránh MERGE/FK lỗi khi nhiều công đoạn cùng ProductId.
        _ctx.Op10 = await InsertOperationAsync("OP10", "May thân", 10, isFinal: false, OperationStatuses.Active, ct);
        _ctx.Op20 = await InsertOperationAsync("OP20", "Hoàn thiện", 20, isFinal: true, OperationStatuses.Active, ct);
        _ctx.Op30 = await InsertOperationAsync("OP30", "Công đoạn ngừng", 30, isFinal: false, OperationStatuses.Discontinued, ct);
        _ctx.Op40 = await InsertOperationAsync("OP40", "Công đoạn đơn giá T-30", 40, isFinal: false, OperationStatuses.Active, ct);

        _ctx.DraftPieceRate = new PieceRate
        {
            OperationId = _ctx.Op10.OperationId,
            Rate = 1_000m,
            EffectiveDate = _t,
            Status = PieceRateStatuses.Draft,
        };
        _ctx.PendingPieceRate = new PieceRate
        {
            OperationId = _ctx.Op10.OperationId,
            Rate = 1_100m,
            EffectiveDate = _t.AddDays(1),
            Status = PieceRateStatuses.PendingApproval,
            SubmittedAt = _now,
        };
        _ctx.ApprovedPieceRate = new PieceRate
        {
            OperationId = _ctx.Op10.OperationId,
            Rate = 1_200m,
            EffectiveDate = _t.AddDays(-30),
            Status = PieceRateStatuses.Approved,
            SubmittedAt = _now.AddDays(-31),
            ApprovedAt = _now.AddDays(-30),
            DecisionNote = "IT approved",
        };
        _ctx.RejectedPieceRate = new PieceRate
        {
            OperationId = _ctx.Op20.OperationId,
            Rate = 900m,
            EffectiveDate = _t,
            Status = PieceRateStatuses.Rejected,
            SubmittedAt = _now.AddDays(-2),
            DecisionNote = "IT rejected rate",
        };
        _db.PieceRates.AddRange(
            _ctx.DraftPieceRate, _ctx.PendingPieceRate, _ctx.ApprovedPieceRate, _ctx.RejectedPieceRate);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Operation> InsertOperationAsync(
        string code, string name, short seq, bool isFinal, string status, CancellationToken ct)
    {
        var op = new Operation
        {
            ProductId = _ctx.Product001.ProductId,
            OperationCode = code,
            OperationName = name,
            StationSequence = seq,
            TeamType = TeamTypes.Sewing,
            IsFinalStep = isFinal,
            Status = status,
        };
        _db.Operations.Add(op);
        await _db.SaveChangesAsync(ct);
        var id = op.OperationId;
        _db.ChangeTracker.Clear();
        return await _db.Operations.AsNoTracking().SingleAsync(o => o.OperationId == id, ct);
    }

    private async Task SeedMarkersAsync(CancellationToken ct)
    {
        // Trigger trg_ProductMarker_Check: mỗi (ProductId, MarkerNo) phải có ProductMarkerFabric trước.
        foreach (var markerNo in new byte[] { 1, 20 })
        {
            foreach (var color in new[] { _ctx.ColorBlack, _ctx.ColorWhite })
            {
                _db.ProductMarkerFabrics.Add(new ProductMarkerFabric
                {
                    ProductId = _ctx.Product001.ProductId,
                    MarkerNo = markerNo,
                    ProductColorId = color.ProductColorId,
                    MaterialId = _ctx.MatFab01.MaterialId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        _ctx.MarkerNo1 = new ProductMarker
        {
            OrderId = _ctx.OrderInProduction.OrderId,
            ProductId = _ctx.Product001.ProductId,
            MarkerNo = 1,
            MarkerCode = "1A",
            LengthM = 2.5m,
            WidthM = 1.5m,
            Status = RecordStatus.Active,
        };
        _ctx.MarkerNo20 = new ProductMarker
        {
            OrderId = _ctx.OrderInProduction.OrderId,
            ProductId = _ctx.Product001.ProductId,
            MarkerNo = 20,
            MarkerCode = "20A",
            LengthM = 2.5m,
            WidthM = 1.5m,
            Status = RecordStatus.Active,
        };
        _db.ProductMarkers.AddRange(_ctx.MarkerNo1, _ctx.MarkerNo20);
        await _db.SaveChangesAsync(ct);

        _db.ProductMarkerRatios.AddRange(
            new ProductMarkerRatio { ProductMarkerId = _ctx.MarkerNo1.ProductMarkerId, SizeCode = "S", PiecesPerPly = 2 },
            new ProductMarkerRatio { ProductMarkerId = _ctx.MarkerNo1.ProductMarkerId, SizeCode = "M", PiecesPerPly = 2 },
            new ProductMarkerRatio { ProductMarkerId = _ctx.MarkerNo20.ProductMarkerId, SizeCode = "S", PiecesPerPly = 1 },
            new ProductMarkerRatio { ProductMarkerId = _ctx.MarkerNo20.ProductMarkerId, SizeCode = "M", PiecesPerPly = 1 });
        await _db.SaveChangesAsync(ct);
    }
}
