using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Nhân viên
/// </summary>
public partial class Employee
{
    public int EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public int RoleId { get; set; }

    public int? TeamId { get; set; }

    public string PayType { get; set; } = null!;

    public string? CurrentGradeCode { get; set; }

    public int? AttendanceCode { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? ResignDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Account? Account { get; set; }

    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    public virtual ICollection<AttendanceAdjustment> AttendanceAdjustments { get; set; } = new List<AttendanceAdjustment>();

    public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<CutHandover> CutHandovers { get; set; } = new List<CutHandover>();

    public virtual ICollection<DailyTimesheet> DailyTimesheets { get; set; } = new List<DailyTimesheet>();

    public virtual ICollection<DefectRecord> DefectRecordRecordedByNavigations { get; set; } = new List<DefectRecord>();

    public virtual ICollection<DefectRecord> DefectRecordResponsibleWorkers { get; set; } = new List<DefectRecord>();

    public virtual ICollection<DispatchNote> DispatchNotes { get; set; } = new List<DispatchNote>();

    public virtual ICollection<FgHandover> FgHandoverConfirmedByNavigations { get; set; } = new List<FgHandover>();

    public virtual ICollection<FgHandover> FgHandoverInspectors { get; set; } = new List<FgHandover>();

    public virtual ICollection<GradeEvaluation> GradeEvaluationProposedByNavigations { get; set; } = new List<GradeEvaluation>();

    public virtual ICollection<GradeEvaluation> GradeEvaluationWorkers { get; set; } = new List<GradeEvaluation>();

    public virtual ICollection<MaterialIssue> MaterialIssueIssuedByNavigations { get; set; } = new List<MaterialIssue>();

    public virtual ICollection<MaterialIssue> MaterialIssueReceivedByNavigations { get; set; } = new List<MaterialIssue>();

    public virtual ICollection<MaterialReceipt> MaterialReceipts { get; set; } = new List<MaterialReceipt>();

    public virtual ICollection<MaterialReturn> MaterialReturns { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OperationAssignment> OperationAssignments { get; set; } = new List<OperationAssignment>();

    public virtual ICollection<OutputRecord> OutputRecords { get; set; } = new List<OutputRecord>();

    public virtual ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();

    public virtual ICollection<QcBatch> QcBatches { get; set; } = new List<QcBatch>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual Role Role { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual ICollection<VehicleTrip> VehicleTrips { get; set; } = new List<VehicleTrip>();

    public virtual ICollection<WorkerPieceMovement> WorkerPieceMovements { get; set; } = new List<WorkerPieceMovement>();
}
