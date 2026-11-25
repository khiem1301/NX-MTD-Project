using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedAttendanceAsync(CancellationToken ct)
    {
        // Sync quá hạn: FinishedAt vượt ngưỡng ATTENDANCE_ALERT_MINUTES
        _ctx.SyncOverdue = new AttendanceSyncSession
        {
            Source = SyncSource.RjSync,
            StartedAt = _now.AddHours(-3),
            FinishedAt = _now.AddHours(-2),
            RecordsReceived = 10,
            RecordsInserted = 10,
            Status = SyncStatus.Succeeded,
        };
        _db.AttendanceSyncSessions.Add(_ctx.SyncOverdue);

        _ctx.TimesheetMissingPunch = new DailyTimesheet
        {
            EmployeeId = _ctx.WorkerSewA1.EmployeeId,
            WorkDate = _t.AddDays(-1),
            CheckIn = _now.AddDays(-1).Date.AddHours(8),
            CheckOut = null,
            WorkedHours = 0m,
            WorkdayValue = 0m,
            OvertimeHours = 0m,
            IsMissingPunch = true,
            HasExtraPunch = false,
            Source = TimesheetSource.Fingerprint,
            UpdatedAt = _now,
        };
        _db.DailyTimesheets.Add(_ctx.TimesheetMissingPunch);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedRequestsAsync(CancellationToken ct)
    {
        var statuses = new[]
        {
            RequestStatuses.PendingApproval,
            RequestStatuses.PendingProcessing,
            RequestStatuses.Approved,
            RequestStatuses.Rejected,
            RequestStatuses.Completed,
            RequestStatuses.NotCompleted,
            RequestStatuses.Cancelled,
        };

        for (var i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            var isMachine = i == 1; // PENDING_PROCESSING → MACHINE_REPAIR (electrician xử lý theo loại, không qua HandlerRoleCode)
            var req = new Request
            {
                RequestCode = $"REQ-IT-{i + 1:D2}",
                RequestTypeCode = isMachine ? RequestTypes.MachineRepair : RequestTypes.Other,
                Status = status,
                Title = $"IT request {status}",
                Content = isMachine
                    ? "IT machine repair for electrician_01"
                    : "IT seed request body",
                CreatedBy = _ctx.WorkerSewA1.EmployeeId,
                CreatedByTeamId = _ctx.TeamSewA.TeamId,
                // CK_Request_Handler: OTHER → MANAGER + TeamId NULL; khác OTHER → cả hai NULL
                HandlerRoleCode = isMachine ? null : RoleCodes.Manager,
                HandlerTeamId = null,
                CreatedAt = _now.AddHours(-i),
                DecisionNote = status is RequestStatuses.Rejected ? "IT reject note" : null,
                ResultNote = status is RequestStatuses.NotCompleted ? "IT not completed" : null,
                DecidedAt = status is RequestStatuses.Approved or RequestStatuses.Rejected or RequestStatuses.Completed
                    or RequestStatuses.NotCompleted
                    ? _now.AddHours(-i + 1)
                    : null,
                CompletedAt = status is RequestStatuses.Completed or RequestStatuses.NotCompleted
                    ? _now.AddHours(-i + 2)
                    : null,
            };
            _db.Requests.Add(req);
            _ctx.Requests.Add(req);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedPurchaseAndVehicleAsync(CancellationToken ct)
    {
        var supplier = new Supplier
        {
            SupplierName = "SUP-IT-01",
            Status = RecordStatus.Active,
            CreatedAt = _now,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(ct);

        _ctx.PurchasePendingFinalisation = new PurchaseOrder
        {
            PoCode = "PO-IT-01",
            OrderId = _ctx.OrderInProduction.OrderId,
            GoodsStatus = PurchaseOrderStatuses.PendingFinalisation,
            PaymentStatus = PurchaseOrderStatuses.Unpaid,
            CreatedAt = _now,
        };
        _ctx.PurchaseUnpaid = new PurchaseOrder
        {
            PoCode = "PO-IT-02",
            OrderId = _ctx.OrderInProduction.OrderId,
            SupplierId = supplier.SupplierId,
            GoodsStatus = PurchaseGoodsStatuses.Ordered,
            PaymentStatus = PurchaseOrderStatuses.Unpaid,
            OrderDate = _t.AddDays(-5),
            CreatedAt = _now.AddDays(-5),
        };
        _db.PurchaseOrders.AddRange(_ctx.PurchasePendingFinalisation, _ctx.PurchaseUnpaid);

        _ctx.VehicleBusy = new Vehicle
        {
            PlateNumber = "VEH-01",
            VehicleType = "Xe tải IT",
            FuelNormPer100Km = 12m,
            Status = RecordStatus.Active,
        };
        _ctx.VehicleIdle = new Vehicle
        {
            PlateNumber = "VEH-02",
            VehicleType = "Xe tải IT rảnh",
            FuelNormPer100Km = 12m,
            Status = RecordStatus.Active,
        };
        _db.Vehicles.AddRange(_ctx.VehicleBusy, _ctx.VehicleIdle);
        await _db.SaveChangesAsync(ct);

        _ctx.TripStarted = new VehicleTrip
        {
            VehicleId = _ctx.VehicleBusy.VehicleId,
            TripDate = _t,
            DriverName = "Tài xế công ty giao",
            Purpose = "công ty giao / ISS-IT-02",
            OdometerStart = 10_000,
            Status = TripStatuses.Started,
            RecordedAt = _now,
        };
        _ctx.TripCompleted = new VehicleTrip
        {
            VehicleId = _ctx.VehicleIdle.VehicleId,
            TripDate = _t.AddDays(-1),
            DriverEmployeeId = _ctx.Warehouse01.EmployeeId,
            Purpose = "IT completed trip",
            OdometerStart = 5_000,
            OdometerEnd = 5_080,
            FuelLiters = 12m,
            FuelCost = 300_000m,
            Status = TripStatuses.Completed,
            RecordedAt = _now.AddDays(-1),
        };
        // TripCompleted gắn VEH-02 rồi trả xe về rảnh: chuyến đã COMPLETED nên xe idle.
        // VEH-01 giữ TripStarted → đang có chuyến.
        _db.VehicleTrips.AddRange(_ctx.TripStarted, _ctx.TripCompleted);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        _ctx.FuelNormSetting = await UpsertSettingAsync(
            SettingKeys.FuelNormTolerancePercent, "15", "IT fuel norm tolerance %", ct);
        _ctx.AttendanceAlertSetting = await UpsertSettingAsync(
            SettingKeys.AttendanceAlertMinutes, "30", "IT attendance sync overdue threshold", ct);

        // Sync overdue: finished 2h ago với threshold 30 phút → vượt ngưỡng
        // (đã seed SyncOverdue.FinishedAt = Now-2h)
    }

    private async Task<SystemSetting> UpsertSettingAsync(
        string key, string value, string description, CancellationToken ct)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key, ct);
        if (setting is null)
        {
            setting = new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description,
                UpdatedAt = _now,
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.SettingValue = value;
            setting.UpdatedAt = _now;
        }

        await _db.SaveChangesAsync(ct);
        return setting;
    }
}
