/* =====================================================================================
   DEV-ALL-149: bỏ Tên tổ (Team.TeamName) và Tên bậc (GradeLevelCode.GradeName). Tổ chỉ còn Mã tổ (TeamCode),
   bậc chỉ còn Mã bậc (GradeCode); view vw_PayrollExportData trả TeamCode thay cho TeamName.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-20_drop_team_grade_name.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- View lương đọc TeamName: đổi trước khi bỏ cột
CREATE OR ALTER VIEW dbo.vw_PayrollExportData
AS
SELECT  p.PeriodMonth, e.EmployeeId, e.EmployeeCode, e.FullName, r.RoleName, e.PayType, t.TeamCode,
        ISNULL(ts.WorkDays, 0)        AS WorkDays,             -- tổng công, gồm cả Chủ nhật
        ISNULL(ts.SundayWorkDays, 0)  AS SundayWorkDays,       -- phần công Chủ nhật (lương x SUNDAY_PAY_MULTIPLIER)
        ISNULL(ts.OvertimeHours, 0)   AS OvertimeHours,
        ISNULL(ts.SundayOvertimeHours, 0) AS SundayOvertimeHours,
        ISNULL(pay.OutputAmount, 0)   AS PieceRateAmount,
        ISNULL(pay.SundayBonus, 0)    AS SundayPieceRateBonus, -- phần cộng thêm cho sản phẩm làm Chủ nhật
        ISNULL(pay.ScrapAmount, 0)    AS ScrapDeduction,
        g.GradeCode,
        g.MonthlyAmount               AS GradeAllowance
FROM dbo.AccountingPeriod p
CROSS JOIN dbo.Employee e
JOIN dbo.Role r ON r.RoleId = e.RoleId
LEFT JOIN dbo.Team t ON t.TeamId = e.TeamId
OUTER APPLY (SELECT SUM(WorkdayValue) WorkDays, SUM(OvertimeHours) OvertimeHours,
                    SUM(IIF(IsSunday = 1, WorkdayValue, 0)) SundayWorkDays, SUM(IIF(IsSunday = 1, OvertimeHours, 0)) SundayOvertimeHours
             FROM dbo.DailyTimesheet WHERE EmployeeId = e.EmployeeId AND WorkDate BETWEEN p.StartDate AND p.EndDate) ts
OUTER APPLY (SELECT SUM(OutputAmount) OutputAmount, SUM(SundayBonus) SundayBonus, SUM(ScrapAmount) ScrapAmount
             FROM dbo.vw_WorkerDailyEstimatedPay WHERE WorkerId = e.EmployeeId AND WorkDate BETWEEN p.StartDate AND p.EndDate) pay
OUTER APPLY (SELECT TOP (1) h.ProposedGradeCode AS GradeCode    -- lần đổi bậc gần nhất có hiệu lực trước cuối tháng
             FROM dbo.GradeEvaluation h WHERE h.WorkerId = e.EmployeeId AND h.Status = 'APPLIED' AND h.EffectiveDate <= p.EndDate
             ORDER BY h.EffectiveDate DESC, h.GradeEvaluationId DESC) gh
OUTER APPLY (SELECT TOP (1) gr.GradeCode, gr.MonthlyAmount
             FROM dbo.GradeLevel gr WHERE gr.GradeCode = COALESCE(gh.GradeCode, e.CurrentGradeCode) AND gr.EffectiveDate <= p.EndDate
             ORDER BY gr.EffectiveDate DESC) g     -- bậc và tiền bậc hiệu lực ngày cuối tháng
WHERE e.StartDate <= p.EndDate AND (e.ResignDate IS NULL OR e.ResignDate >= p.StartDate);
GO

IF OBJECT_ID(N'dbo.UQ_Team_Name', N'UQ') IS NOT NULL
    ALTER TABLE dbo.Team DROP CONSTRAINT UQ_Team_Name;
IF COL_LENGTH(N'dbo.Team', N'TeamName') IS NOT NULL
    ALTER TABLE dbo.Team DROP COLUMN TeamName;
IF COL_LENGTH(N'dbo.GradeLevelCode', N'GradeName') IS NOT NULL
    ALTER TABLE dbo.GradeLevelCode DROP COLUMN GradeName;
GO
