using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedProductionFloorAsync(CancellationToken ct)
    {
        var lineA = _ctx.OrderProdLines[0];
        var lineA2 = _ctx.OrderProdLines[1];
        var workerA1 = _ctx.WorkerSewA1.EmployeeId;
        var workerA2 = _ctx.WorkerSewA2.EmployeeId;
        var teamA = _ctx.TeamSewA.TeamId;
        var op10 = _ctx.Op10.OperationId;

        // trg_WorkAllocation_Check: Σ giao ≤ BTP SEMI_FINISHED RECEIVED của tổ × dòng đơn.
        var btpIssue = new MaterialIssue
        {
            IssueCode = "ISS-IT-BTP",
            WarehouseType = WarehouseTypes.SemiFinished,
            OrderId = _ctx.OrderInProduction.OrderId,
            ReceivingTeamId = teamA,
            IssueDate = _t.AddDays(-2),
            IssuedBy = _ctx.Warehouse01.EmployeeId,
            Status = IssueStatuses.Received,
            ReceivedBy = _ctx.LeaderSewA.EmployeeId,
            ReceivedAt = _now.AddDays(-2),
            CreatedAt = _now.AddDays(-2),
        };
        _db.MaterialIssues.Add(btpIssue);
        await _db.SaveChangesAsync(ct);
        _db.MaterialIssueLines.AddRange(
            new MaterialIssueLine
            {
                MaterialIssueId = btpIssue.MaterialIssueId,
                OrderLineId = lineA.OrderLineId,
                IssuedQty = 500m,
                ConfirmedQty = 500m,
            },
            new MaterialIssueLine
            {
                MaterialIssueId = btpIssue.MaterialIssueId,
                OrderLineId = lineA2.OrderLineId,
                IssuedQty = 500m,
                ConfirmedQty = 500m,
            });
        await _db.SaveChangesAsync(ct);

        // 4 handover statuses cho worker_sew_a1 — CANCELLED map sang REJECTED (CK_WA_Status)
        _ctx.HandoverPending = Wa(
            PieceMovementTypes.Handover, workerA1, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Pending, ConfirmModes.Worker,
            createdAt: _now.AddMinutes(-(AllocationAutoConfirmWindow.Minutes + 1)));
        _ctx.HandoverConfirmed = Wa(
            PieceMovementTypes.Handover, workerA1, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Confirmed, ConfirmModes.Worker,
            qty: 50, confirmedQty: 50, confirmedAt: _now.AddHours(-2));
        _ctx.HandoverRejected = Wa(
            PieceMovementTypes.Handover, workerA1, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Rejected, ConfirmModes.Worker,
            rejectReason: "IT reject handover");
        _ctx.HandoverCancelledAsRejected = Wa(
            PieceMovementTypes.Handover, workerA1, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Rejected, ConfirmModes.Leader,
            rejectReason: "IT cancelled-as-rejected (DB không có CANCELLED)");

        // Movement HANDOVER + RETRIEVAL cho worker_sew_a2 (ConfirmedQty = Quantity theo CK_WA_Confirmed)
        _ctx.MovementHandoverA2 = Wa(
            PieceMovementTypes.Handover, workerA2, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Confirmed, ConfirmModes.Leader,
            qty: 30, confirmedQty: 30, confirmedAt: _now.AddHours(-5));
        _ctx.MovementRetrievalA2 = Wa(
            PieceMovementTypes.Retrieval, workerA2, lineA.OrderLineId, op10, teamA,
            HandoverStatuses.Confirmed, ConfirmModes.System,
            qty: 10, confirmedQty: 10, confirmedAt: _now.AddHours(-1),
            retrievalReason: RetrievalReasons.EndOfDay);

        _db.WorkAllocations.AddRange(
            _ctx.HandoverPending, _ctx.HandoverConfirmed, _ctx.HandoverRejected,
            _ctx.HandoverCancelledAsRejected, _ctx.MovementHandoverA2, _ctx.MovementRetrievalA2);
        await _db.SaveChangesAsync(ct); // Trigger sản lượng cần phiếu giao CONFIRMED đã có trước.

        var prevMonthDay = new DateOnly(_t.Year, _t.Month, 1).AddDays(-1);

        // Handover CONFIRMED ngày PREV — trần cho OutputLocked.
        await InsertPrevHandoverAsync(workerA1, lineA.OrderLineId, op10, teamA, prevMonthDay, ct);

        _ctx.OutputSaved = new OutputRecord
        {
            WorkDate = _t,
            ShiftCode = ShiftCodes.Morning,
            SewingTeamId = teamA,
            WorkerId = workerA1,
            OperationId = op10,
            OrderLineId = lineA.OrderLineId,
            Quantity = 40,
            PieceRateId = _ctx.ApprovedPieceRate.PieceRateId,
            EditDeadline = _now.AddHours(24),
            Status = OutputStatuses.Saved,
            EnteredAt = _now,
        };
        _ctx.OutputLocked = new OutputRecord
        {
            WorkDate = prevMonthDay,
            ShiftCode = ShiftCodes.Morning,
            SewingTeamId = teamA,
            WorkerId = workerA1,
            OperationId = op10,
            OrderLineId = lineA.OrderLineId,
            Quantity = 35,
            PieceRateId = _ctx.ApprovedPieceRate.PieceRateId,
            EditDeadline = _now.AddMonths(-1),
            Status = OutputStatuses.Locked,
            EnteredAt = _now.AddMonths(-1),
        };
        _db.OutputRecords.AddRange(_ctx.OutputSaved, _ctx.OutputLocked);
        await _db.SaveChangesAsync(ct);

        var qaqc = _ctx.Qaqc01.EmployeeId;
        _ctx.DefectRework = Def("DEF-IT-RW", DefectTypes.Rework, DefectStatuses.Recorded, _t, qaqc);
        _ctx.DefectScrap = Def("DEF-IT-SC", DefectTypes.Scrap, DefectStatuses.Confirmed, _t, qaqc);
        _ctx.DefectOutputErr = Def("DEF-IT-OE", DefectTypes.OutputError, DefectStatuses.Cancelled, _t, qaqc);
        _ctx.DefectLocked = Def("DEF-IT-LK", DefectTypes.Rework, DefectStatuses.Locked, prevMonthDay, qaqc);
        _db.DefectRecords.AddRange(
            _ctx.DefectRework, _ctx.DefectScrap, _ctx.DefectOutputErr, _ctx.DefectLocked);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<WorkAllocation> InsertPrevHandoverAsync(
        int workerId, int orderLineId, int operationId, int teamId, DateOnly workDate, CancellationToken ct)
    {
        var wa = new WorkAllocation
        {
            WorkDate = workDate,
            MovementType = PieceMovementTypes.Handover,
            SewingTeamId = teamId,
            WorkerId = workerId,
            OrderLineId = orderLineId,
            OperationId = operationId,
            Quantity = 50,
            ConfirmedQty = 50,
            ConfirmMode = ConfirmModes.Leader,
            Status = HandoverStatuses.Confirmed,
            CreatedAt = _now.AddMonths(-1),
            ConfirmedAt = _now.AddMonths(-1),
        };
        _db.WorkAllocations.Add(wa);
        await _db.SaveChangesAsync(ct);
        return wa;
    }

    private WorkAllocation Wa(
        string movement,
        int workerId,
        int orderLineId,
        int operationId,
        int teamId,
        string status,
        string confirmMode,
        int qty = 50,
        int? confirmedQty = null,
        DateTime? createdAt = null,
        DateTime? confirmedAt = null,
        string? rejectReason = null,
        string? retrievalReason = null) => new()
    {
        WorkDate = _t,
        MovementType = movement,
        SewingTeamId = teamId,
        WorkerId = workerId,
        OrderLineId = orderLineId,
        OperationId = operationId,
        Quantity = qty,
        ConfirmedQty = confirmedQty,
        ConfirmMode = confirmMode,
        Status = status,
        RejectReason = rejectReason,
        RetrievalReason = retrievalReason,
        CreatedAt = createdAt ?? _now,
        ConfirmedAt = confirmedAt,
    };

    private DefectRecord Def(
        string code, string type, string status, DateOnly workDate, int recordedBy) => new()
    {
        DefectCode = code,
        WorkDate = workDate,
        ShiftCode = ShiftCodes.Morning,
        SewingTeamId = _ctx.TeamSewA.TeamId,
        OrderLineId = _ctx.OrderProdLines[0].OrderLineId,
        OperationId = _ctx.Op10.OperationId,
        ResponsibleWorkerId = _ctx.WorkerSewA1.EmployeeId,
        DefectType = type,
        Quantity = 2,
        DefectReasonCode = DefectReasons.SkippedStitch,
        PieceRateId = _ctx.ApprovedPieceRate.PieceRateId,
        EditDeadline = _now.AddHours(DefectEditWindow.Hours),
        Status = status,
        RecordedBy = recordedBy,
        RecordedAt = _now,
    };
}
