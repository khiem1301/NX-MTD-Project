/* =====================================================================================
   11. BẢNG LƯƠNG, PHIẾU LƯƠNG, CHI PHÍ THÁNG, HÒA VỐN
   Bảng lương: tháng 8 REJECTED, tháng 9 APPROVED, tháng 10 PENDING_APPROVAL, tháng 11 DRAFT. Số liệu lấy từ bảng công và sản lượng thật ở trên.
   (Hệ thống không tính lương chính thức: đây là số Kế toán nhập từ Excel, ở đây dựng sẵn để xem phiếu lương.)
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @ketoan INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN');

/* ---------- Bảng lương ---------- */
INSERT dbo.Payroll (PeriodId, Status, SubmittedAt, ApprovedAt, RejectReason)
SELECT p.PeriodId, v.Status, v.Sub, v.App, v.Rej
FROM (VALUES
 ('2026-08', 'REJECTED',         '2026-09-04 09:00', NULL, N'Thiếu phụ cấp chuyên cần của tổ may 2, Kế toán tính lại rồi trình lại'),
 ('2026-09', 'APPROVED',         '2026-10-05 10:30', '2026-10-06 14:00', NULL),
 ('2026-10', 'PENDING_APPROVAL', DATEADD(DAY, -13, @now), NULL, NULL),
 ('2026-11', 'DRAFT',            NULL, NULL, NULL)) v (Month, Status, Sub, App, Rej)
JOIN dbo.AccountingPeriod p ON p.PeriodMonth = v.Month;

IF OBJECT_ID('tempdb..#Ps') IS NOT NULL DROP TABLE #Ps;
SELECT pr.PayrollId, ap.PeriodMonth, ap.StartDate, ap.EndDate, e.EmployeeId, e.EmployeeCode, e.PayType, e.StartDate AS EmpStart, e.CurrentGradeCode,
       ISNULL(ts.Days, 0) AS Days, ISNULL(ts.Ot, 0) AS Ot, ISNULL(ts.SunDays, 0) AS SunDays,
       ISNULL(pc.Piece, 0) AS Piece,
       CASE e.EmployeeCode WHEN 'ADMIN' THEN 30000000 WHEN 'QUANDOC' THEN 18000000 WHEN 'KETOAN' THEN 14000000 WHEN 'THUKHO' THEN 11000000
                           WHEN 'CODIEN' THEN 10000000 ELSE CASE r.RoleCode WHEN 'TEAM_LEADER' THEN 12000000 WHEN 'TEAM_DEPUTY' THEN 9500000 ELSE 0 END END AS MonthlyBase,
       CASE WHEN e.PayType = 'DAILY' THEN CASE WHEN r.RoleCode = 'QAQC' THEN 300000 ELSE 340000 END ELSE 0 END AS DailyRate
INTO #Ps
FROM dbo.Payroll pr
JOIN dbo.AccountingPeriod ap ON ap.PeriodId = pr.PeriodId
JOIN dbo.Employee e ON e.StartDate <= ap.EndDate AND (e.ResignDate IS NULL OR e.ResignDate >= ap.StartDate)
JOIN dbo.Role r ON r.RoleId = e.RoleId
OUTER APPLY (SELECT SUM(t.WorkdayValue) AS Days, SUM(t.OvertimeHours) AS Ot, SUM(IIF(t.IsSunday = 1, t.WorkdayValue, 0)) AS SunDays
             FROM dbo.DailyTimesheet t WHERE t.EmployeeId = e.EmployeeId AND t.WorkDate BETWEEN ap.StartDate AND ap.EndDate) ts
OUTER APPLY (SELECT SUM(o.Quantity * rt.Rate) AS Piece
             FROM dbo.OutputRecord o JOIN dbo.PieceRate rt ON rt.PieceRateId = o.PieceRateId
             WHERE o.WorkerId = e.EmployeeId AND o.WorkDate BETWEEN ap.StartDate AND ap.EndDate) pc
WHERE pr.Status <> 'DRAFT';
DELETE FROM #Ps WHERE Days = 0 AND Piece = 0;

IF OBJECT_ID('tempdb..#Calc') IS NOT NULL DROP TABLE #Calc;
SELECT s.*,
       CAST(CASE s.PayType WHEN 'MONTHLY' THEN ROUND(s.MonthlyBase * IIF(s.Days > 26, 26, s.Days) / 26.0, -3)
                           WHEN 'DAILY'   THEN s.DailyRate * (s.Days + s.SunDays) ELSE 0 END AS DECIMAL(18,2)) AS BasePay,
       -- thợ khoán: sản lượng; tháng chưa có sản lượng thì có mức tối thiểu 230.000 / công
       CAST(CASE WHEN s.PayType = 'PIECE_RATE' THEN IIF(s.Piece > 0, s.Piece, 230000 * s.Days) ELSE 0 END AS DECIMAL(18,2)) AS PiecePay,
       CAST(ISNULL(g.MonthlyAmount, 0) * IIF(s.Days > 26, 26, s.Days) / 26.0 AS DECIMAL(18,2)) AS GradePay,
       CAST(25000 * s.Days AS DECIMAL(18,2)) AS MealPay,
       CAST(s.Ot * IIF(s.PayType = 'MONTHLY', 55000, 40000) AS DECIMAL(18,2)) AS OtPay
INTO #Calc
FROM #Ps s
OUTER APPLY (SELECT TOP 1 gl.MonthlyAmount FROM dbo.GradeLevel gl WHERE gl.GradeCode = s.CurrentGradeCode AND gl.EffectiveDate <= s.EndDate ORDER BY gl.EffectiveDate DESC) g;

INSERT dbo.Payslip (PayrollId, EmployeeId, WorkDays, TotalAdditions, TotalDeductions, Insurance, NetPay)
SELECT c.PayrollId, c.EmployeeId, c.Days,
       c.BasePay + c.PiecePay + c.GradePay + c.MealPay + c.OtPay,
       ad.Adv,
       ins.Amt,
       c.BasePay + c.PiecePay + c.GradePay + c.MealPay + c.OtPay - ad.Adv - ins.Amt
FROM #Calc c
CROSS APPLY (SELECT CAST(IIF(ABS(CHECKSUM(c.EmployeeId, c.PeriodMonth)) % 7 = 0, 1000000, IIF(ABS(CHECKSUM(c.EmployeeId, c.PeriodMonth)) % 7 = 1, 500000, 0)) AS DECIMAL(18,2)) AS Adv) ad
CROSS APPLY (SELECT CAST(IIF(c.EmpStart <= DATEADD(DAY, -180, c.StartDate),
                             ROUND(0.105 * CASE c.PayType WHEN 'MONTHLY' THEN c.MonthlyBase WHEN 'DAILY' THEN c.DailyRate * 26 ELSE 5500000 END, -3), 0) AS DECIMAL(18,2)) AS Amt) ins;

-- Các khoản trên phiếu
INSERT dbo.PayslipItem (PayslipId, ItemGroup, ItemName, Amount, SortOrder)
SELECT ps.PayslipId, i.G, i.Name, i.Amt, i.So
FROM dbo.Payslip ps
JOIN #Calc c ON c.PayrollId = ps.PayrollId AND c.EmployeeId = ps.EmployeeId
CROSS APPLY (VALUES
   ('INFO',      N'Số ngày công',                      CAST(c.Days AS DECIMAL(18,2)), 1),
   ('INFO',      N'Số giờ tăng ca',                    CAST(c.Ot AS DECIMAL(18,2)), 2),
   ('ADDITION',  N'Lương theo ngày công',              c.BasePay, 10),
   ('ADDITION',  N'Lương sản phẩm',                    c.PiecePay, 11),
   ('ADDITION',  N'Tiền bậc thợ',                      c.GradePay, 12),
   ('ADDITION',  N'Phụ cấp ăn trưa',                   c.MealPay, 13),
   ('ADDITION',  N'Tiền tăng ca',                      c.OtPay, 14),
   ('DEDUCTION', N'Tạm ứng lương',                     ps.TotalDeductions, 20),
   ('INSURANCE', N'BHXH, BHYT, BHTN (10,5%)',          ps.Insurance, 30)) i (G, Name, Amt, So)
WHERE i.Amt > 0 OR i.G = 'INFO' AND i.Name = N'Số ngày công';

/* ---------- Chi phí tháng (cố định theo tháng, biến đổi theo mã hàng) ---------- */
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Quantity, Note, ImportedAt)
SELECT p.PeriodId, v.Cat, 'FIXED', NULL, v.Amt, NULL, v.Note, DATEADD(DAY, 6, CAST(p.EndDate AS DATETIME2))
FROM (VALUES
 ('2026-09', 'PREMISES',         45000000, N'Thuê mặt bằng xưởng'),
 ('2026-09', 'UTILITIES',        21500000, N'Điện nước tháng 9'),
 ('2026-09', 'MACHINE_REPAIR',    3200000, N'Sửa máy may'),
 ('2026-09', 'MACHINE_SUPPLIES',  4100000, N'Kim, dầu máy'),
 ('2026-09', 'STATIONERY',        1200000, N'Văn phòng phẩm'),
 ('2026-09', 'FUEL',              2100000, N'Xăng xe giao hàng'),
 ('2026-09', 'MEALS',            38000000, N'Cơm trưa công nhân'),
 ('2026-09', 'OTHER',             2000000, N'Chi phí khác'),
 ('2026-10', 'PREMISES',         45000000, N'Thuê mặt bằng xưởng'),
 ('2026-10', 'UTILITIES',        23800000, N'Điện nước tháng 10'),
 ('2026-10', 'MACHINE_REPAIR',    6800000, N'Sửa 2 máy vắt sổ'),
 ('2026-10', 'MACHINE_SUPPLIES',  3600000, N'Kim, dầu máy'),
 ('2026-10', 'STATIONERY',        1400000, N'Văn phòng phẩm'),
 ('2026-10', 'FUEL',              2600000, N'Xăng xe giao hàng'),
 ('2026-10', 'MEALS',            41000000, N'Cơm trưa công nhân'),
 ('2026-10', 'OTHER',             2400000, N'Chi phí khác')) v (Month, Cat, Amt, Note)
JOIN dbo.AccountingPeriod p ON p.PeriodMonth = v.Month;

-- Chi phí biến đổi theo mã hàng: NVL mua (theo phiếu nhập trong tháng) và phế liệu; Quantity = số thành phẩm đạt trong tháng
IF OBJECT_ID('tempdb..#Fin') IS NOT NULL DROP TABLE #Fin;
SELECT CONVERT(CHAR(7), q.WorkDate) AS Month, ol.ProductId, SUM(q.FinishedQty) AS Qty, SUM(q.FinishedQty * ol.CmtUnitPrice) AS Revenue
INTO #Fin FROM dbo.QcBatch q JOIN dbo.OrderLine ol ON ol.OrderLineId = q.OrderLineId
WHERE q.WorkDate < DATEFROMPARTS(YEAR(@today), MONTH(@today), 1) GROUP BY CONVERT(CHAR(7), q.WorkDate), ol.ProductId;

INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Quantity, Note, ImportedAt)
SELECT p.PeriodId, 'MATERIALS_BOUGHT', 'VARIABLE', f.ProductId,
       ISNULL((SELECT SUM(l.AcceptedQty * pol.UnitPrice)
               FROM dbo.MaterialReceiptLine l JOIN dbo.MaterialReceipt mr ON mr.MaterialReceiptId = l.MaterialReceiptId AND mr.Status = 'CONFIRMED'
               JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = l.PurchaseOrderLineId
               JOIN dbo.CustomerOrder co ON co.OrderId = mr.OrderId
               JOIN dbo.OrderLine ol ON ol.OrderId = co.OrderId AND ol.ProductId = f.ProductId
               WHERE CONVERT(CHAR(7), mr.ReceiptDate) = f.Month AND ol.OrderLineId = (SELECT MIN(OrderLineId) FROM dbo.OrderLine WHERE OrderId = co.OrderId)), 0),
       f.Qty, N'Giá trị NVL mua nhập kho trong tháng', DATEADD(DAY, 6, CAST(p.EndDate AS DATETIME2))
FROM #Fin f JOIN dbo.AccountingPeriod p ON p.PeriodMonth = f.Month AND p.PeriodMonth IN ('2026-09', '2026-10');
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Quantity, Note, ImportedAt)
SELECT p.PeriodId, 'SCRAP', 'VARIABLE', f.ProductId, CAST(f.Qty * 150 AS DECIMAL(18,2)), f.Qty, N'Phế liệu, hàng hỏng không bán được', DATEADD(DAY, 6, CAST(p.EndDate AS DATETIME2))
FROM #Fin f JOIN dbo.AccountingPeriod p ON p.PeriodMonth = f.Month AND p.PeriodMonth IN ('2026-09', '2026-10');

/* ---------- Hòa vốn theo mã hàng (phân bổ chi phí cố định theo sản lượng) ---------- */
INSERT dbo.BreakEvenResult (PeriodId, ProductId, AllocationBasis, FinishedQty, CmtUnitPrice, Revenue, VariableCost, AllocatedFixedCost, BreakEvenQty,
                            PercentReached, SurplusValue, IsAtRisk, CalculatedAt)
SELECT p.PeriodId, f.ProductId, 'OUTPUT', f.Qty, ROUND(f.Revenue / f.Qty, 2), f.Revenue,
       vc.Var, fx.Fixed * f.Qty / tot.TotalQty,
       CASE WHEN f.Revenue / f.Qty - vc.Var / f.Qty > 0 THEN (fx.Fixed * f.Qty / tot.TotalQty) / (f.Revenue / f.Qty - vc.Var / f.Qty) END,
       CASE WHEN f.Revenue / f.Qty - vc.Var / f.Qty > 0
            THEN ROUND(f.Qty / NULLIF((fx.Fixed * f.Qty / tot.TotalQty) / (f.Revenue / f.Qty - vc.Var / f.Qty), 0) * 100, 2) END,
       f.Revenue - vc.Var - fx.Fixed * f.Qty / tot.TotalQty,
       IIF(f.Revenue - vc.Var - fx.Fixed * f.Qty / tot.TotalQty < 0, 1, 0),
       DATEADD(DAY, 7, CAST(p.EndDate AS DATETIME2))
FROM #Fin f
JOIN dbo.AccountingPeriod p ON p.PeriodMonth = f.Month AND p.PeriodMonth IN ('2026-09', '2026-10')
CROSS APPLY (SELECT ISNULL(SUM(c.Amount), 0) AS Var FROM dbo.MonthlyCost c WHERE c.PeriodId = p.PeriodId AND c.CostType = 'VARIABLE' AND c.ProductId = f.ProductId) vc
CROSS APPLY (SELECT SUM(c.Amount) AS Fixed FROM dbo.MonthlyCost c WHERE c.PeriodId = p.PeriodId AND c.CostType = 'FIXED') fx
CROSS APPLY (SELECT SUM(f2.Qty) AS TotalQty FROM #Fin f2 WHERE f2.Month = f.Month) tot
WHERE f.Qty > 0;

PRINT N'11: bảng lương, chi phí tháng, hòa vốn xong.';
