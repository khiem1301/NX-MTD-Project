/* =====================================================================================
   07. TỔ MAY: phân công công đoạn (kể cả lịch sử), giao / thu hồi việc với công nhân (CONFIRMED / PENDING / REJECTED, đủ cách xác nhận
       và lý do thu hồi), sản lượng công đoạn theo ngày, hàng lỗi (Rework / Scrap / Output_Err đủ trạng thái), đợt kiểm QA/QC.
   Trần chuỗi do trigger của DB kiểm: giao ≤ BTP tổ nhận; sản lượng ≤ số lần giao.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @qaqc1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1'),
        @qaqc2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC2');

/* ---------- Ngày làm việc (bỏ Chủ nhật) ---------- */
IF OBJECT_ID('tempdb..#WD') IS NOT NULL DROP TABLE #WD;
SELECT DATEADD(DAY, -n.i, @today) AS D INTO #WD
FROM (SELECT TOP 130 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS i FROM sys.all_objects) n
WHERE DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, -n.i, @today)) % 7 <> 0;

/* ---------- Khung ngày sản lượng theo đơn: bắt đầu 1 ngày sau lần cấp BTP đầu, kéo dài Span ngày làm ---------- */
IF OBJECT_ID('tempdb..#Win') IS NOT NULL DROP TABLE #Win;
CREATE TABLE #Win (OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT PRIMARY KEY, W0 DATE, W1 DATE, Span INT);
INSERT #Win (OrderCode, W0, Span)
SELECT o.OrderCode, DATEADD(DAY, o.StartOff + 5, @today), v.Span
FROM #Ord o JOIN (VALUES ('DH-2509-001', 17), ('DH-2510-002', 14), ('DH-2510-003', 16), ('DH-2510-004', 12), ('DH-2511-005', 9),
                         ('DH-2511-006', 1), ('DH-2510-009', 6)) v (Code, Span) ON v.Code = o.OrderCode;
UPDATE w SET W1 = (SELECT MAX(D) FROM (SELECT TOP (w.Span) d.D FROM #WD d WHERE d.D >= w.W0 AND d.D < @today ORDER BY d.D) x)
FROM #Win w;

IF OBJECT_ID('tempdb..#Dd') IS NOT NULL DROP TABLE #Dd;
SELECT w.OrderCode, ROW_NUMBER() OVER (PARTITION BY w.OrderCode ORDER BY d.D) AS i, COUNT(*) OVER (PARTITION BY w.OrderCode) AS Cnt, d.D
INTO #Dd FROM #Win w JOIN #WD d ON d.D BETWEEN w.W0 AND w.W1;

/* ---------- Công đoạn may, công nhân từng tổ ---------- */
IF OBJECT_ID('tempdb..#Ops') IS NOT NULL DROP TABLE #Ops;
SELECT op.ProductId, p.ProductCode, op.OperationId, op.IsFinalStep,
       ROW_NUMBER() OVER (PARTITION BY op.ProductId ORDER BY op.StationSequence) AS k,
       COUNT(*) OVER (PARTITION BY op.ProductId) AS n
INTO #Ops FROM dbo.Operation op JOIN dbo.Product p ON p.ProductId = op.ProductId
WHERE op.Status = 'ACTIVE' AND op.TeamType = 'SEWING';

IF OBJECT_ID('tempdb..#TW') IS NOT NULL DROP TABLE #TW;
SELECT e.TeamId, e.EmployeeId, ROW_NUMBER() OVER (PARTITION BY e.TeamId ORDER BY e.EmployeeId) AS idx, COUNT(*) OVER (PARTITION BY e.TeamId) AS n
INTO #TW FROM dbo.Employee e JOIN dbo.Role r ON r.RoleId = e.RoleId AND r.RoleCode = 'WORKER'
WHERE e.Status = 'ACTIVE' AND e.PayType = 'PIECE_RATE' AND e.TeamId IS NOT NULL;

/* ---------- BTP tổ may thực nhận theo dòng đơn ---------- */
IF OBJECT_ID('tempdb..#Recv') IS NOT NULL DROP TABLE #Recv;
SELECT l.OrderLineId, mi.ReceivingTeamId AS TeamId, SUM(l.ConfirmedQty) AS Recv, MIN(mi.IssueDate) AS D1, MAX(mi.IssueDate) AS D2
INTO #Recv
FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = l.MaterialIssueId
WHERE mi.WarehouseType = 'SEMI_FINISHED' AND mi.Status = 'RECEIVED' AND l.OrderLineId IS NOT NULL
GROUP BY l.OrderLineId, mi.ReceivingTeamId;

/* ---------- Phân công công đoạn cho từng đơn × tổ ---------- */
IF OBJECT_ID('tempdb..#Cmb') IS NOT NULL DROP TABLE #Cmb;
SELECT lf.OrderCode, lf.OrderId, lf.OrderLineId, lf.TeamId, o.OperationId, o.k, o.n, o.IsFinalStep, rc.Recv, rc.D1, rc.D2,
       w.WorkerId, w.Part, w.NParts, lf.SewF, lf.IssueF,
       CAST(CASE WHEN lf.IssueF > 0 THEN lf.SewF / lf.IssueF ELSE 0 END AS DECIMAL(9,6)) AS Ratio
INTO #Cmb
FROM #LnF lf
JOIN #Recv rc ON rc.OrderLineId = lf.OrderLineId AND rc.TeamId = lf.TeamId
JOIN #Ops o ON o.ProductCode = lf.ProdCode
CROSS APPLY (SELECT t.EmployeeId AS WorkerId, 1 AS Part, IIF(o.k % 3 = 2 AND t.n > 1, 2, 1) AS NParts
             FROM #TW t WHERE t.TeamId = lf.TeamId AND t.idx = ((o.k - 1) % t.n) + 1
             UNION ALL
             SELECT t.EmployeeId, 2, 2
             FROM #TW t WHERE t.TeamId = lf.TeamId AND o.k % 3 = 2 AND t.n > 1 AND t.idx = (o.k % t.n) + 1) w;

IF OBJECT_ID('tempdb..#Cmb2') IS NOT NULL DROP TABLE #Cmb2;
SELECT c.*,
       CASE WHEN c.NParts = 1 THEN c.Recv WHEN c.Part = 1 THEN CAST(ROUND(c.Recv * .6, 0) AS INT) ELSE c.Recv - CAST(ROUND(c.Recv * .6, 0) AS INT) END AS Alloc,
       CASE WHEN c.NParts = 1 THEN x.OutAll WHEN c.Part = 1 THEN CAST(ROUND(x.OutAll * .6, 0) AS INT) ELSE x.OutAll - CAST(ROUND(x.OutAll * .6, 0) AS INT) END AS Out
INTO #Cmb2
FROM #Cmb c
CROSS APPLY (SELECT CAST(ROUND(c.Recv * (1 - (1 - c.Ratio) * (c.k - 1) / NULLIF(c.n - 1, 0)), 0) AS INT) AS OutAll) x;

-- Khớp lại số hàng sau may của từng dòng với công đoạn cuối, rồi chốt kiểm đạt / nhập kho TP / xuất kho theo đó
UPDATE l SET SewQ = f.FinalOut
FROM #LnF l JOIN (SELECT OrderLineId, SUM(Out) AS FinalOut FROM #Cmb2 WHERE IsFinalStep = 1 GROUP BY OrderLineId) f ON f.OrderLineId = l.OrderLineId;
UPDATE #LnF SET QcQ = IIF(QcQ > SewQ, SewQ, QcQ);
UPDATE #LnF SET FgQ = IIF(FgQ > QcQ, QcQ, FgQ);
UPDATE #LnF SET DispQ = IIF(DispQ > FgQ, FgQ, DispQ);

-- Phân công: mỗi (đơn, tổ, công đoạn, công nhân); ValidFrom trước ngày tổ nhận BTP đầu tiên
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
SELECT x.TeamId, x.OrderId, x.OperationId, x.WorkerId, DATEADD(DAY, -1, x.D1), NULL, DATEADD(HOUR, 8, CAST(DATEADD(DAY, -1, x.D1) AS DATETIME2))
FROM (SELECT DISTINCT TeamId, OrderId, OperationId, WorkerId, MIN(D1) OVER (PARTITION BY OrderId, TeamId) AS D1 FROM #Cmb2) x;
-- Lịch sử: công nhân cũ làm công đoạn thứ 2 của áo khoác gió (tổ may 1) đến 12 ngày trước, rồi chuyển người khác
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
SELECT a.SewingTeamId, a.OrderId, a.OperationId, (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY8'),
       DATEADD(DAY, -8, a.ValidFrom), DATEADD(DAY, -2, a.ValidFrom), DATEADD(DAY, -8, a.AssignedAt)
FROM dbo.OperationAssignment a
JOIN dbo.CustomerOrder co ON co.OrderId = a.OrderId AND co.OrderCode = 'DH-2510-003'
JOIN #Ops o ON o.OperationId = a.OperationId AND o.k = 2
WHERE a.SewingTeamId = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');

/* ---------- Giao việc cho công nhân (HANDOVER) ---------- */
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, OnBehalfReason,
                           Status, CreatedAt, ConfirmedAt, WorkDate)
SELECT 'HANDOVER', c.TeamId, c.WorkerId, c.OrderLineId, c.OperationId, h.Qty, h.Qty,
       m.Mode, CASE WHEN m.Mode = 'ON_BEHALF' THEN N'Công nhân đang đi khám bệnh, tổ trưởng xác nhận thay' END,
       'CONFIRMED', DATEADD(HOUR, 13, CAST(h.D AS DATETIME2)), DATEADD(MINUTE, IIF(m.Mode = 'SYSTEM', 24 * 60 + 5, 15), DATEADD(HOUR, 13, CAST(h.D AS DATETIME2))), h.D
FROM #Cmb2 c
CROSS APPLY (VALUES (1, c.D1, CASE WHEN c.D2 > c.D1 THEN CAST(ROUND(c.Alloc / 2.0, 0) AS INT) ELSE c.Alloc END),
                    (2, c.D2, CASE WHEN c.D2 > c.D1 THEN c.Alloc - CAST(ROUND(c.Alloc / 2.0, 0) AS INT) ELSE 0 END)) h (Seq, D, Qty)
CROSS APPLY (SELECT CASE (ABS(CHECKSUM(c.OrderLineId, c.OperationId, c.WorkerId, h.Seq)) % 20)
                         WHEN 0 THEN 'ON_BEHALF' WHEN 1 THEN 'SYSTEM' WHEN 2 THEN 'SYSTEM' WHEN 3 THEN 'LEADER' WHEN 4 THEN 'LEADER' WHEN 5 THEN 'LEADER'
                         ELSE 'WORKER' END AS Mode) m
WHERE h.Qty > 0;

-- Công nhân từ chối nhận (số BTP không khớp): không tính vào trần
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmMode, Status, RejectReason, CreatedAt, WorkDate)
SELECT TOP 2 'HANDOVER', c.TeamId, c.WorkerId, c.OrderLineId, c.OperationId, 5, 'WORKER', 'REJECTED',
       N'Số bộ ghi trên phiếu giao nhiều hơn số thực nhận, đã báo tổ trưởng kiểm lại',
       DATEADD(HOUR, 14, CAST(DATEADD(DAY, -3, @today) AS DATETIME2)), DATEADD(DAY, -3, @today)
FROM #Cmb2 c WHERE c.OrderCode = 'DH-2510-003' AND c.Part = 1 AND c.IsFinalStep = 0 ORDER BY c.OrderLineId, c.k;

-- Giao chờ xác nhận (hôm nay): lấy 3 bộ của công đoạn cuối áo polo hồng ra khỏi phần đã xác nhận
UPDATE a SET Quantity = a.Quantity - 3, ConfirmedQty = a.ConfirmedQty - 3
FROM dbo.WorkAllocation a
WHERE a.WorkAllocationId = (SELECT TOP 1 a2.WorkAllocationId
                            FROM dbo.WorkAllocation a2
                            JOIN #Cmb2 c ON c.OrderLineId = a2.OrderLineId AND c.OperationId = a2.OperationId AND c.WorkerId = a2.WorkerId
                            WHERE c.OrderCode = 'DH-2511-005' AND c.IsFinalStep = 1 AND a2.MovementType = 'HANDOVER' AND a2.Quantity > 10
                            ORDER BY a2.WorkAllocationId DESC);
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmMode, Status, CreatedAt, WorkDate)
SELECT TOP 1 'HANDOVER', a.SewingTeamId, a.WorkerId, a.OrderLineId, a.OperationId, 3, 'WORKER', 'PENDING', @now, @today
FROM dbo.WorkAllocation a
JOIN #Cmb2 c ON c.OrderLineId = a.OrderLineId AND c.OperationId = a.OperationId AND c.WorkerId = a.WorkerId
WHERE c.OrderCode = 'DH-2511-005' AND c.IsFinalStep = 1 AND a.MovementType = 'HANDOVER' AND a.Status = 'CONFIRMED'
ORDER BY a.WorkAllocationId DESC;

-- Thu hồi: phần chưa làm. Đơn tạm dừng thu hồi toàn bộ; đơn khác thu hồi nhỏ ở công đoạn cuối với đủ các lý do
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, DiscrepancyReason, RetrievalReason,
                           RetrievalNote, ConfirmMode, Status, CreatedAt, ConfirmedAt, WorkDate)
SELECT 'RETRIEVAL', c.TeamId, c.WorkerId, c.OrderLineId, c.OperationId, r.Qty, r.Qty, NULL, r.Reason,
       CASE r.Reason WHEN 'STYLE_FINISHED' THEN N'Khách tạm dừng đơn, thu hồi BTP chưa may' WHEN 'WORKER_ABSENT' THEN N'Công nhân nghỉ ốm, giao người khác'
                     WHEN 'OPERATION_CHANGED' THEN N'Đổi công đoạn cho công nhân' WHEN 'ADJUSTED' THEN N'Điều chỉnh số lượng giao theo thực tế'
                     ELSE N'Hết ca, thu BTP chưa làm' END,
       IIF(r.Reason = 'STYLE_FINISHED', 'LEADER', 'WORKER'), 'CONFIRMED',
       DATEADD(HOUR, 16, CAST(r.D AS DATETIME2)), DATEADD(HOUR, 17, CAST(r.D AS DATETIME2)), r.D
FROM #Cmb2 c
CROSS APPLY (SELECT CASE WHEN c.OrderCode = 'DH-2510-009' THEN c.Alloc - c.Out ELSE IIF(c.Alloc - c.Out >= 2, 2, 0) END AS Qty,
                    CASE WHEN c.OrderCode = 'DH-2510-009' THEN 'STYLE_FINISHED'
                         ELSE CHOOSE(ABS(CHECKSUM(c.OrderLineId, c.OperationId)) % 4 + 1, 'END_OF_DAY', 'WORKER_ABSENT', 'OPERATION_CHANGED', 'ADJUSTED') END AS Reason,
                    CASE WHEN c.OrderCode = 'DH-2510-009' THEN DATEADD(DAY, 1, (SELECT W1 FROM #Win WHERE OrderCode = c.OrderCode)) ELSE DATEADD(DAY, -3, @today) END AS D) r
WHERE r.Qty > 0 AND c.OrderCode IN ('DH-2510-003', 'DH-2510-009')
  AND (c.OrderCode = 'DH-2510-009' OR (c.IsFinalStep = 1 OR c.k >= c.n - 1));

/* ---------- Sản lượng công đoạn theo ngày ---------- */
INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId, IsOutsideAssignment,
                         EditDeadline, Status, EnteredAt)
SELECT d.D,
       CASE WHEN DATEDIFF(DAY, '2000-01-01', d.D) % 6 = 0 THEN 'OVERTIME' WHEN d.i % 2 = 0 THEN 'AFTERNOON' ELSE 'MORNING' END,
       c.TeamId, c.WorkerId, c.OperationId, c.OrderLineId,
       (c.Out * d.i) / d.Cnt - (c.Out * (d.i - 1)) / d.Cnt,
       pr.PieceRateId,
       CASE WHEN ABS(CHECKSUM(c.OrderLineId, c.OperationId, d.i)) % 97 = 0 THEN 1 ELSE 0 END,
       DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, d.D) AS DATETIME2)),
       'SAVED', DATEADD(HOUR, 17, CAST(d.D AS DATETIME2))
FROM #Cmb2 c
JOIN #Dd d ON d.OrderCode = c.OrderCode
CROSS APPLY (SELECT TOP 1 r.PieceRateId FROM dbo.PieceRate r WHERE r.OperationId = c.OperationId AND r.Status = 'APPROVED' AND r.EffectiveDate <= d.D
             ORDER BY r.EffectiveDate DESC) pr
WHERE c.Out > 0 AND (c.Out * d.i) / d.Cnt - (c.Out * (d.i - 1)) / d.Cnt > 0;

/* ---------- Hàng lỗi ---------- */
IF OBJECT_ID('tempdb..#Def') IS NOT NULL DROP TABLE #Def;
SELECT TOP 22 ROW_NUMBER() OVER (ORDER BY ABS(CHECKSUM(c.OrderLineId, c.OperationId, c.WorkerId))) AS rn,
       c.TeamId, c.WorkerId, c.OrderLineId, c.OperationId, c.OrderCode,
       (SELECT MAX(o.WorkDate) FROM dbo.OutputRecord o WHERE o.WorkerId = c.WorkerId AND o.OrderLineId = c.OrderLineId AND o.OperationId = c.OperationId) AS WorkDate
INTO #Def
FROM #Cmb2 c
WHERE c.Out >= 12 AND c.OrderCode IN ('DH-2510-003', 'DH-2510-004', 'DH-2511-005', 'DH-2510-002', 'DH-2509-001')
ORDER BY ABS(CHECKSUM(c.OrderLineId, c.OperationId, c.WorkerId));

INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity,
                         DefectReasonCode, ReasonNote, PieceRateId, WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt)
SELECT CONCAT('LO-', FORMAT(f.WorkDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY f.WorkDate ORDER BY f.rn)), 3)),
       f.WorkDate, 'MORNING', f.TeamId, f.OrderLineId, f.OperationId, f.WorkerId,
       t.Type, t.Qty, t.Reason, CASE WHEN t.Reason = 'OTHER' THEN N'Lỗi không thuộc các nhóm có sẵn: vải bị lỗi dệt ở mép' END,
       CASE WHEN t.Type = 'REWORK' THEN NULL ELSE pr.PieceRateId END,
       DATEADD(HOUR, 10, CAST(f.WorkDate AS DATETIME2)),
       CASE WHEN f.rn IN (4, 11) THEN N'Em làm đúng mẫu, lỗi do máy bị lệch kim sáng nay' END,
       CASE WHEN f.rn IN (4, 11) THEN DATEADD(HOUR, 14, CAST(f.WorkDate AS DATETIME2)) END,
       DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, f.WorkDate) AS DATETIME2)), t.Status, @qaqc1, DATEADD(HOUR, 9, CAST(f.WorkDate AS DATETIME2))
FROM #Def f
CROSS APPLY (SELECT CHOOSE((f.rn - 1) % 10 + 1, 'REWORK', 'REWORK', 'SCRAP', 'REWORK', 'OUTPUT_ERR', 'REWORK', 'SCRAP', 'REWORK', 'OUTPUT_ERR', 'REWORK') AS Type,
                    CHOOSE((f.rn - 1) % 6 + 1, 'SKIPPED_STITCH', 'CROOKED_SEAM', 'STAINED', 'TORN', 'WRONG_SIZE', 'OTHER') AS Reason,
                    1 + (f.rn % 3) AS Qty,
                    CHOOSE((f.rn - 1) % 5 + 1, 'RECORDED', 'CONFIRMED', 'CONFIRMED', 'RECORDED', 'CANCELLED') AS Status) t
OUTER APPLY (SELECT TOP 1 r.PieceRateId FROM dbo.PieceRate r WHERE r.OperationId = f.OperationId AND r.Status = 'APPROVED' AND r.EffectiveDate <= f.WorkDate
             ORDER BY r.EffectiveDate DESC) pr
WHERE f.WorkDate IS NOT NULL;

/* ---------- Đợt kiểm hoàn thiện QA/QC: số đạt của dòng đơn chia theo ngày sau ngày may ---------- */
IF OBJECT_ID('tempdb..#QcWin') IS NOT NULL DROP TABLE #QcWin;
SELECT w.OrderCode, DATEADD(DAY, 2, w.W0) AS Q0, IIF(DATEADD(DAY, 2, w.W1) > @today, @today, DATEADD(DAY, 2, w.W1)) AS Q1 INTO #QcWin FROM #Win w;
IF OBJECT_ID('tempdb..#Qd') IS NOT NULL DROP TABLE #Qd;
SELECT q.OrderCode, ROW_NUMBER() OVER (PARTITION BY q.OrderCode ORDER BY d.D) AS i, COUNT(*) OVER (PARTITION BY q.OrderCode) AS Cnt, d.D
INTO #Qd FROM #QcWin q JOIN #WD d ON d.D BETWEEN q.Q0 AND q.Q1;

INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline, CreatedAt)
SELECT d.D, IIF(d.i % 2 = 0, 'AFTERNOON', 'MORNING'), IIF(d.i % 2 = 0, '15:30', '10:30'), l.TeamId, l.OrderLineId,
       (l.QcQ * d.i) / d.Cnt - (l.QcQ * (d.i - 1)) / d.Cnt,
       IIF(d.i % 3 = 0, @qaqc2, @qaqc1), DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, d.D) AS DATETIME2)), DATEADD(HOUR, IIF(d.i % 2 = 0, 15, 10), CAST(d.D AS DATETIME2))
FROM #LnF l JOIN #Qd d ON d.OrderCode = l.OrderCode
WHERE l.QcQ > 0 AND l.TeamId IS NOT NULL AND (l.QcQ * d.i) / d.Cnt - (l.QcQ * (d.i - 1)) / d.Cnt > 0;

-- Đảm bảo có xác nhận thay (ON_BEHALF) và tự xác nhận (SYSTEM) để test các cách xác nhận
UPDATE a SET ConfirmMode = 'ON_BEHALF', OnBehalfReason = N'Công nhân xin về sớm, tổ trưởng xác nhận thay'
FROM dbo.WorkAllocation a
WHERE a.WorkAllocationId IN (SELECT TOP 6 WorkAllocationId FROM dbo.WorkAllocation WHERE MovementType = 'HANDOVER' AND Status = 'CONFIRMED' AND ConfirmMode = 'LEADER' ORDER BY WorkAllocationId DESC);

PRINT N'07: phân công, giao việc, sản lượng, hàng lỗi, kiểm QA/QC xong.';
