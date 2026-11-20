/* =====================================================================================
   06. TỔ CẮT VÀ KHO: lệnh cắt (OPEN / CUTTING / COMPLETED / CANCELLED), bàn cắt (RECORDED / LOCKED) + vải dùng + số cắt theo size,
       cấp vải cho tổ cắt, bàn giao BTP (CONFIRMED có lệch / PENDING / REJECTED / CANCELLED), cấp BTP và phụ liệu cho tổ may,
       trả vải thừa + phế liệu (CONFIRMED / PENDING / REJECTED / CANCELLED), thẻ kho qua usp_Stock_Post.
   Bảng tạm #Frac, #LnF, #Wo, #Mv dùng ở các file sau.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @ttcat INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTCAT'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC');

/* ---------- Tiến độ theo (đơn, màu): cắt, đã bàn giao BTP vào kho, BTP cấp tổ may, may xong, kiểm đạt, nhập kho TP, xuất kho ---------- */
IF OBJECT_ID('tempdb..#Frac') IS NOT NULL DROP TABLE #Frac;
CREATE TABLE #Frac (OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, Color NVARCHAR(50) COLLATE DATABASE_DEFAULT NULL, WoKey VARCHAR(10) COLLATE DATABASE_DEFAULT,
                    CutF DECIMAL(5,3), HF DECIMAL(5,3), IssueF DECIMAL(5,3), SewF DECIMAL(5,3), QcF DECIMAL(5,3), FgF DECIMAL(5,3), DispF DECIMAL(5,3));
INSERT #Frac VALUES
 ('DH-2509-001', NULL,        'O1',  1,    1,    1,    1,    1,    1,    1),
 ('DH-2510-002', NULL,        'O2',  1,    1,    1,    1,    1,    1,    1),
 ('DH-2510-003', NULL,        'O3',  1,    1,    1,    .96,  .95,  .92,  .55),
 ('DH-2510-004', NULL,        'O4',  1,    1,    1,    1,    1,    1,    .72),
 ('DH-2511-005', N'Hồng',     'O5A', .80,  .70,  .60,  .42,  .30,  .18,  0),
 ('DH-2511-005', N'Trắng',    'O5B', 0,    0,    0,    0,    0,    0,    0),
 ('DH-2511-006', N'Trắng',    'O6A', .70,  .40,  .40,  .12,  0,    0,    0),
 ('DH-2511-006', N'Xanh nhạt','O6B', 0,    0,    0,    0,    0,    0,    0),
 ('DH-2510-009', NULL,        'O9',  .60,  .60,  .40,  .15,  .10,  0,    0);

IF OBJECT_ID('tempdb..#LnF') IS NOT NULL DROP TABLE #LnF;
SELECT ol.OrderLineId, ol.OrderId, co.OrderCode, o.Prod AS ProdCode, pc.Color, pv.SizeCode AS Size, olt.SewingTeamId AS TeamId, t.TeamCode, ol.OrderedQty AS Planned,
       f.WoKey,
       CAST(ROUND(ol.OrderedQty * f.CutF, 0) AS INT)   AS CutQ,
       CAST(ROUND(ol.OrderedQty * f.HF, 0) AS INT)     AS HQ,
       CAST(ROUND(ol.OrderedQty * f.IssueF, 0) AS INT) AS IssueQ,
       CAST(ROUND(ol.OrderedQty * f.SewF, 0) AS INT)   AS SewQ,
       CAST(ROUND(ol.OrderedQty * f.QcF, 0) AS INT)    AS QcQ,
       CAST(ROUND(ol.OrderedQty * f.FgF, 0) AS INT)    AS FgQ,
       CAST(ROUND(ol.OrderedQty * f.DispF, 0) AS INT)  AS DispQ,
       f.IssueF, f.SewF
INTO #LnF
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder co ON co.OrderId = ol.OrderId
JOIN #Ord o ON o.OrderCode = co.OrderCode
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN dbo.ProductColor pc ON pc.ProductColorId = pv.ProductColorId
JOIN #Frac f ON f.OrderCode = co.OrderCode AND (f.Color IS NULL OR f.Color = pc.Color)
LEFT JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId
LEFT JOIN dbo.Team t ON t.TeamId = olt.SewingTeamId;

/* ---------- Lệnh cắt ---------- */
IF OBJECT_ID('tempdb..#Wo') IS NOT NULL DROP TABLE #Wo;
CREATE TABLE #Wo (WoKey VARCHAR(10) COLLATE DATABASE_DEFAULT PRIMARY KEY, OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, StartOff INT, NLays INT,
                  Status VARCHAR(20) COLLATE DATABASE_DEFAULT, DueOff INT, Note NVARCHAR(500) COLLATE DATABASE_DEFAULT, Tol DECIMAL(5,2), WoId INT NULL, WoCode VARCHAR(30) COLLATE DATABASE_DEFAULT NULL);
INSERT #Wo (WoKey, OrderCode, StartOff, NLays, Status, DueOff, Note, Tol) VALUES
 ('O1',  'DH-2509-001', -70, 6, 'COMPLETED', -62, N'Cắt đủ 960 bộ, đã bàn giao hết BTP', 3),
 ('O2',  'DH-2510-002', -52, 5, 'COMPLETED', -45, N'Quần kaki cắt hai màu Be / Đen', 3),
 ('O3',  'DH-2510-003', -34, 6, 'COMPLETED', -26, N'Áo khoác gió cắt lớp ngoài và lớp lót', 3),
 ('O4',  'DH-2510-004', -24, 4, 'COMPLETED', -19, N'Áo sơ mi đợt cuối', 3),
 ('O5A', 'DH-2511-005', -15, 4, 'CUTTING',   -3,  N'Cắt áo polo hồng', 3),
 ('O5B', 'DH-2511-005', -2,  0, 'OPEN',      8,   N'Áo polo trắng, chờ vải trắng về đủ', 3),
 ('O6A', 'DH-2511-006', -6,  3, 'CUTTING',   10,  N'Cắt áo sơ mi trắng', 3),
 ('O6B', 'DH-2511-006', -1,  0, 'OPEN',      14,  N'Áo sơ mi xanh nhạt, chưa cắt', 3),
 ('O9',  'DH-2510-009', -20, 3, 'COMPLETED', -14, N'Đã cắt 60% trước khi khách tạm dừng đơn', 3),
 ('O3X', 'DH-2510-003', -33, 0, 'CANCELLED', -25, N'Lập nhầm lệnh cắt (trùng lệnh LC chính), hủy', 3);

INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt, CompletedAt, FabricTolerancePercent)
SELECT CONCAT('LC-', FORMAT(DATEADD(DAY, w.StartOff - 1, @today), 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY DATEADD(DAY, w.StartOff - 1, @today) ORDER BY w.WoKey)), 3)),
       co.OrderId, p.ProductId, @cat, DATEADD(DAY, w.DueOff, @today), w.Status, w.Note,
       DATEADD(HOUR, 8, CAST(DATEADD(DAY, w.StartOff - 1, @today) AS DATETIME2)),
       CASE WHEN w.Status = 'COMPLETED' THEN DATEADD(HOUR, 17, CAST(DATEADD(DAY, w.StartOff + w.NLays + 2, @today) AS DATETIME2)) END,
       w.Tol
FROM #Wo w
JOIN dbo.CustomerOrder co ON co.OrderCode = w.OrderCode
JOIN #Ord o ON o.OrderCode = w.OrderCode
JOIN dbo.Product p ON p.ProductCode = o.Prod;
UPDATE w SET WoId = wo.WorkOrderId, WoCode = wo.WorkOrderCode
FROM #Wo w JOIN dbo.WorkOrder wo ON wo.Note = w.Note COLLATE DATABASE_DEFAULT;

-- Dòng lệnh: số dự kiến cắt = số đặt của dòng đơn (O3X chỉ một dòng, lập nhầm)
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WoId, l.OrderLineId, l.Planned FROM #LnF l JOIN #Wo w ON w.WoKey = l.WoKey;
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WoId, MIN(l.OrderLineId), MIN(l.Planned) FROM #Wo w JOIN #LnF l ON l.OrderCode = w.OrderCode WHERE w.WoKey = 'O3X' GROUP BY w.WoId;

/* ---------- Bàn cắt ---------- */
IF OBJECT_ID('tempdb..#Lay') IS NOT NULL DROP TABLE #Lay;
SELECT w.WoKey, w.WoId, n.i AS Seq, DATEADD(DAY, w.StartOff + n.i - 1, @today) AS LayDate, w.NLays,
       CASE WHEN DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, w.StartOff + n.i - 1, @today)) % 7 = 0 THEN 'OVERTIME'
            WHEN n.i % 3 = 0 THEN 'AFTERNOON' ELSE 'MORNING' END AS Shift,
       CASE WHEN w.Status IN ('COMPLETED') THEN 'LOCKED'
            WHEN w.WoKey = 'O5A' AND n.i <= 2 THEN 'LOCKED'
            WHEN w.WoKey = 'O6A' AND n.i = 1 THEN 'LOCKED'
            ELSE 'RECORDED' END AS LayStatus,
       CAST(NULL AS INT) AS CuttingLayId
INTO #Lay
FROM #Wo w
CROSS APPLY (SELECT TOP (w.NLays) ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS i FROM sys.all_objects) n
WHERE w.NLays > 0;

INSERT dbo.CuttingLay (WorkOrderId, CuttingTeamId, WorkDate, ShiftCode, LaySequence, Note, Status, CreatedAt)
SELECT WoId, @cat, LayDate, Shift, Seq,
       CASE WHEN Seq = 1 THEN N'Trải vải, cắt theo bìa nhóm 1' WHEN Seq = NLays THEN N'Bàn cuối, cắt nốt số còn thiếu' END,
       LayStatus, DATEADD(HOUR, IIF(Shift = 'AFTERNOON', 15, 11), CAST(LayDate AS DATETIME2))
FROM #Lay ORDER BY WoKey, Seq;
UPDATE y SET CuttingLayId = cl.CuttingLayId FROM #Lay y JOIN dbo.CuttingLay cl ON cl.WorkOrderId = y.WoId AND cl.LaySequence = y.Seq;

-- Số cắt theo dòng đơn: chia đều theo số bàn
INSERT dbo.CuttingLayLine (CuttingLayId, OrderLineId, ActualQty)
SELECT y.CuttingLayId, l.OrderLineId, (l.CutQ * y.Seq) / y.NLays - (l.CutQ * (y.Seq - 1)) / y.NLays
FROM #Lay y JOIN #LnF l ON l.WoKey = y.WoKey
WHERE (l.CutQ * y.Seq) / y.NLays - (l.CutQ * (y.Seq - 1)) / y.NLays > 0;

-- Vải dùng theo bàn: số cắt × định mức vải của từng NVL
IF OBJECT_ID('tempdb..#LnFab') IS NOT NULL DROP TABLE #LnFab;
SELECT ol.OrderLineId, pcm.MaterialId, SUM(c.PerGarment) AS PerGarment
INTO #LnFab
FROM #LnF l
JOIN dbo.OrderLine ol ON ol.OrderLineId = l.OrderLineId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductId = ol.ProductId AND pcm.ProductColorId = pv.ProductColorId
JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pcm.ProductComponentId AND pc.ComponentType = 'PANEL'
JOIN #Cons c ON c.Prod = l.ProdCode AND c.Comp = pc.ComponentName
GROUP BY ol.OrderLineId, pcm.MaterialId;

IF OBJECT_ID('tempdb..#LayFab') IS NOT NULL DROP TABLE #LayFab;
SELECT y.CuttingLayId, y.WoKey, y.WoId, y.LayDate, lf.MaterialId,
       CAST(ROUND(SUM(((l.CutQ * y.Seq) / y.NLays - (l.CutQ * (y.Seq - 1)) / y.NLays) * lf.PerGarment) * 1.005, 2) AS DECIMAL(9,2)) AS UsedM
INTO #LayFab
FROM #Lay y JOIN #LnF l ON l.WoKey = y.WoKey JOIN #LnFab lf ON lf.OrderLineId = l.OrderLineId
GROUP BY y.CuttingLayId, y.WoKey, y.WoId, y.LayDate, lf.MaterialId
HAVING SUM(((l.CutQ * y.Seq) / y.NLays - (l.CutQ * (y.Seq - 1)) / y.NLays) * lf.PerGarment) > 0;
INSERT dbo.CuttingLayFabric (CuttingLayId, MaterialId, UsedM) SELECT CuttingLayId, MaterialId, UsedM FROM #LayFab WHERE UsedM > 0;

/* ---------- Danh sách thẻ kho cần ghi (ghi theo thứ tự thời gian ở cuối file) ---------- */
IF OBJECT_ID('tempdb..#Mv') IS NOT NULL DROP TABLE #Mv;
CREATE TABLE #Mv (Wh VARCHAR(15) COLLATE DATABASE_DEFAULT, MaterialId INT NULL, OrderLineId INT NULL, DocType VARCHAR(30) COLLATE DATABASE_DEFAULT, DocId INT, LineId INT,
                  Qty DECIMAL(18,3), D DATE, Ord INT);

/* ---------- Cấp vải cho tổ cắt (2 lần: 55% đầu lệnh, 45% giữa lệnh) ---------- */
IF OBJECT_ID('tempdb..#Iss') IS NOT NULL DROP TABLE #Iss;
CREATE TABLE #Iss (IssueKey VARCHAR(40) COLLATE DATABASE_DEFAULT, OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, WoKey VARCHAR(10) COLLATE DATABASE_DEFAULT NULL,
                   Wh VARCHAR(15) COLLATE DATABASE_DEFAULT, TeamId INT, IssueDate DATE, Status VARCHAR(20) COLLATE DATABASE_DEFAULT, Reject NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL, MaterialIssueId INT NULL);
INSERT #Iss (IssueKey, OrderCode, WoKey, Wh, TeamId, IssueDate, Status)
SELECT CONCAT(w.WoKey, '-F', s.n), w.OrderCode, w.WoKey, 'MATERIAL', @cat, DATEADD(DAY, w.StartOff + IIF(s.n = 1, 0, 4), @today), 'RECEIVED'
FROM #Wo w CROSS JOIN (VALUES (1), (2)) s (n)
WHERE w.NLays > 0 AND DATEADD(DAY, w.StartOff + IIF(s.n = 1, 0, 4), @today) <= @today
  AND (s.n = 1 OR w.NLays >= 3)
  AND EXISTS (SELECT 1 FROM #LayFab f WHERE f.WoKey = w.WoKey);
-- Cấp vải chờ tổ cắt xác nhận nhận (vừa lập hôm nay cho lô cắt tiếp theo của áo polo)
INSERT #Iss (IssueKey, OrderCode, WoKey, Wh, TeamId, IssueDate, Status) VALUES ('O5A-F3', 'DH-2511-005', 'O5A', 'MATERIAL', @cat, @today, 'PENDING_RECEIPT');

INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, RejectReason, CreatedAt)
SELECT CONCAT('PC-', FORMAT(i.IssueDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY i.IssueDate ORDER BY i.IssueKey)), 3)),
       'MATERIAL', co.OrderId, w.WoId, i.TeamId, i.IssueDate, @thukho, i.Status,
       CASE WHEN i.Status = 'RECEIVED' THEN @ttcat END,
       CASE WHEN i.Status = 'RECEIVED' THEN DATEADD(HOUR, 9, CAST(i.IssueDate AS DATETIME2)) END,
       i.Reject, DATEADD(HOUR, 8, CAST(i.IssueDate AS DATETIME2))
FROM #Iss i JOIN dbo.CustomerOrder co ON co.OrderCode = i.OrderCode JOIN #Wo w ON w.WoKey = i.WoKey
WHERE i.Wh = 'MATERIAL' AND i.IssueKey LIKE '%-F%';
UPDATE i SET MaterialIssueId = mi.MaterialIssueId
FROM #Iss i JOIN #Wo w ON w.WoKey = i.WoKey JOIN dbo.MaterialIssue mi ON mi.WorkOrderId = w.WoId AND mi.IssueDate = i.IssueDate AND mi.WarehouseType = 'MATERIAL' AND mi.ReceivingTeamId = @cat
WHERE i.IssueKey LIKE '%-F%';

-- Dòng cấp vải: tổng vải các bàn × hệ số dự phòng 1,5%, chia theo lần cấp
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty, ConfirmedQty, DiscrepancyReason)
SELECT i.MaterialIssueId, t.MaterialId, q.Issued,
       CASE WHEN i.Status = 'RECEIVED' THEN q.Issued - q.Short END,
       CASE WHEN i.Status = 'RECEIVED' AND q.Short > 0 THEN N'Cuộn vải cuối thiếu mét so với phiếu, đã báo thủ kho' END
FROM #Iss i
JOIN (SELECT WoKey, MaterialId, SUM(UsedM) AS Total FROM #LayFab GROUP BY WoKey, MaterialId) t ON t.WoKey = i.WoKey
CROSS APPLY (SELECT CAST(ROUND(t.Total * 1.005 * CASE WHEN i.IssueKey LIKE '%-F1' THEN .55 WHEN i.IssueKey LIKE '%-F2' THEN .45 ELSE .10 END, 1) AS DECIMAL(18,3)) AS Issued) a
CROSS APPLY (SELECT a.Issued,
                    CASE WHEN i.IssueKey = 'O3-F1' AND t.MaterialId = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'V-GIO-DEN') THEN 2.0 ELSE 0 END AS Short) q
WHERE i.Wh = 'MATERIAL' AND q.Issued > 0;
INSERT #Mv (Wh, MaterialId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'MATERIAL', l.MaterialId, 'MATERIAL_ISSUE', mi.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, mi.IssueDate, 20
FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = l.MaterialIssueId
WHERE mi.Status = 'RECEIVED' AND l.MaterialId IS NOT NULL AND l.ConfirmedQty > 0;

/* ---------- Bàn giao BTP từ tổ cắt vào kho ---------- */
IF OBJECT_ID('tempdb..#Ho') IS NOT NULL DROP TABLE #Ho;
CREATE TABLE #Ho (HKey VARCHAR(20) COLLATE DATABASE_DEFAULT, WoKey VARCHAR(10) COLLATE DATABASE_DEFAULT, Share DECIMAL(4,2), HDate DATE, Status VARCHAR(20) COLLATE DATABASE_DEFAULT,
                  Reject NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL, CutHandoverId INT NULL);
INSERT #Ho (HKey, WoKey, Share, HDate, Status, Reject)
SELECT CONCAT(w.WoKey, '-H', s.n), w.WoKey, IIF(s.n = 1, .5, .5), DATEADD(DAY, w.StartOff + IIF(s.n = 1, 2, w.NLays + 1), @today),
       CASE WHEN w.WoKey = 'O5A' AND s.n = 2 THEN 'PENDING'
            WHEN w.WoKey = 'O6A' AND s.n = 2 THEN 'CANCELLED'
            ELSE 'CONFIRMED' END, NULL
FROM #Wo w CROSS JOIN (VALUES (1), (2)) s (n)
WHERE w.NLays > 0 AND EXISTS (SELECT 1 FROM #LnF l WHERE l.WoKey = w.WoKey AND l.HQ > 0)
  AND DATEADD(DAY, w.StartOff + IIF(s.n = 1, 2, w.NLays + 1), @today) <= @today;
-- Bàn giao bị thủ kho từ chối (đếm thiếu nhiều), tổ cắt đếm lại và bàn giao lại ở lần sau
INSERT #Ho (HKey, WoKey, Share, HDate, Status, Reject) VALUES ('O3-HR', 'O3', .10, DATEADD(DAY, -31, @today), 'REJECTED', N'Đếm thực tế thiếu 30 bộ so với phiếu, tổ cắt kiểm tra lại số bàn cắt');

INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, ConfirmedBy, ConfirmedAt, RejectReason)
SELECT CONCAT('BG-', FORMAT(h.HDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY h.HDate ORDER BY h.HKey)), 3)),
       w.WoId, DATEADD(HOUR, 16, CAST(h.HDate AS DATETIME2)), h.Status,
       CASE WHEN h.Status IN ('CONFIRMED') THEN @thukho END,
       CASE WHEN h.Status IN ('CONFIRMED') THEN DATEADD(HOUR, 17, CAST(h.HDate AS DATETIME2)) END, h.Reject
FROM #Ho h JOIN #Wo w ON w.WoKey = h.WoKey;
UPDATE h SET CutHandoverId = ch.CutHandoverId
FROM #Ho h JOIN #Wo w ON w.WoKey = h.WoKey
JOIN dbo.CutHandover ch ON ch.WorkOrderId = w.WoId AND CAST(ch.HandedAt AS DATE) = h.HDate
     AND ch.Status = h.Status AND ISNULL(ch.RejectReason, '') = ISNULL(h.Reject, '');

-- Dòng bàn giao: lần 1 = nửa số BTP, lần 2 = phần còn lại; một dòng đếm lệch (nhận thiếu) để có lý do chênh lệch
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty, ConfirmedQty, DiscrepancyReason)
SELECT h.CutHandoverId, l.OrderLineId, q.Handed,
       CASE WHEN h.Status = 'CONFIRMED' THEN q.Handed - q.Short END,
       CASE WHEN h.Status = 'CONFIRMED' AND q.Short > 0 THEN N'Thủ kho đếm thiếu 2 bộ so với phiếu, tổ cắt đã xác nhận' END
FROM #Ho h JOIN #LnF l ON l.WoKey = h.WoKey
CROSS APPLY (SELECT CASE WHEN h.HKey LIKE '%-H1' THEN l.HQ / 2
                         WHEN h.HKey LIKE '%-H2' THEN l.HQ - l.HQ / 2
                         ELSE IIF(l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.WoKey = h.WoKey), 30, 0) END AS Handed) a
CROSS APPLY (SELECT a.Handed, IIF(h.HKey = 'O5A-H1' AND l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.WoKey = h.WoKey), 2, 0) AS Short) q
WHERE q.Handed > 0;
-- thẻ kho BTP: chỉ lần xác nhận (số xác nhận trừ lệch)
INSERT #Mv (Wh, OrderLineId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'SEMI_FINISHED', l.OrderLineId, 'CUT_HANDOVER', h.CutHandoverId, l.CutHandoverLineId, l.ConfirmedQty, CAST(h.ConfirmedAt AS DATE), 10
FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
WHERE h.Status = 'CONFIRMED' AND l.ConfirmedQty > 0;

/* ---------- Cấp BTP cho tổ may (SEMI_FINISHED) và phụ liệu (MATERIAL) ---------- */
-- Lần 1 = nửa số BTP cấp, lần 2 = phần còn lại; lần 2 cách lần 1 vài ngày
INSERT #Iss (IssueKey, OrderCode, WoKey, Wh, TeamId, IssueDate, Status, Reject)
SELECT CONCAT(l.OrderCode, '-B', s.n, '-', l.TeamCode), l.OrderCode, NULL, 'SEMI_FINISHED', l.TeamId,
       DATEADD(DAY, ISNULL(o.StartOff, 0) + 4 + IIF(s.n = 1, 0, 4), @today),
       CASE WHEN l.OrderCode = 'DH-2511-005' AND s.n = 2 THEN 'PENDING_RECEIPT'
            WHEN l.OrderCode = 'DH-2510-003' AND s.n = 3 THEN 'REJECTED'
            ELSE 'RECEIVED' END,
       CASE WHEN l.OrderCode = 'DH-2510-003' AND s.n = 3 THEN N'Sai màu: phiếu ghi áo Đen nhưng BTP trong kho là navy, thủ kho cấp lại' END
FROM (SELECT DISTINCT OrderCode, TeamId, TeamCode FROM #LnF WHERE IssueQ > 0 AND TeamId IS NOT NULL) l
JOIN #Ord o ON o.OrderCode = l.OrderCode
CROSS JOIN (VALUES (1), (2), (3)) s (n)
WHERE (s.n < 3 OR l.OrderCode = 'DH-2510-003')
  AND DATEADD(DAY, ISNULL(o.StartOff, 0) + 4 + IIF(s.n = 1, 0, 4), @today) <= @today;
UPDATE #Iss SET IssueDate = DATEADD(DAY, 6, IssueDate) WHERE IssueKey LIKE 'DH-2510-003-B3%';

INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, RejectReason, CreatedAt)
SELECT CONCAT('PC-', FORMAT(i.IssueDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', 100 + ROW_NUMBER() OVER (PARTITION BY i.IssueDate ORDER BY i.IssueKey)), 3)),
       'SEMI_FINISHED', co.OrderId, i.TeamId, i.IssueDate, @thukho, i.Status,
       CASE WHEN i.Status = 'RECEIVED' THEN tl.EmployeeId END,
       CASE WHEN i.Status = 'RECEIVED' THEN DATEADD(HOUR, 10, CAST(i.IssueDate AS DATETIME2)) END,
       i.Reject, DATEADD(HOUR, 8, CAST(i.IssueDate AS DATETIME2))
FROM #Iss i JOIN dbo.CustomerOrder co ON co.OrderCode = i.OrderCode
OUTER APPLY (SELECT TOP 1 e.EmployeeId FROM dbo.Employee e JOIN dbo.Role r ON r.RoleId = e.RoleId AND r.RoleCode = 'TEAM_LEADER' WHERE e.TeamId = i.TeamId AND e.Status = 'ACTIVE') tl
WHERE i.Wh = 'SEMI_FINISHED';
UPDATE i SET MaterialIssueId = mi.MaterialIssueId
FROM #Iss i JOIN dbo.CustomerOrder co ON co.OrderCode = i.OrderCode
JOIN dbo.MaterialIssue mi ON mi.OrderId = co.OrderId AND mi.WarehouseType = 'SEMI_FINISHED' AND mi.ReceivingTeamId = i.TeamId AND mi.IssueDate = i.IssueDate
WHERE i.Wh = 'SEMI_FINISHED';

INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty, DiscrepancyReason)
SELECT i.MaterialIssueId, l.OrderLineId, q.Issued,
       CASE WHEN i.Status = 'RECEIVED' THEN q.Issued - q.Short END,
       CASE WHEN i.Status = 'RECEIVED' AND q.Short > 0 THEN N'Tổ may đếm thiếu 1 bộ, thủ kho đã đối chiếu và chấp nhận' END
FROM #Iss i JOIN #LnF l ON l.OrderCode = i.OrderCode AND l.TeamId = i.TeamId
CROSS APPLY (SELECT CASE WHEN i.IssueKey LIKE '%-B1-%' THEN l.IssueQ / 2
                         WHEN i.IssueKey LIKE '%-B2-%' THEN l.IssueQ - l.IssueQ / 2
                         ELSE IIF(l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.OrderCode = l.OrderCode AND x.TeamId = l.TeamId), 20, 0) END AS Issued) a
CROSS APPLY (SELECT a.Issued, 0 AS Short) q
WHERE i.Wh = 'SEMI_FINISHED' AND q.Issued > 0;
-- Lệch số lượng nhận ở một dòng: tổ may nhận thiếu 1 bộ
UPDATE l SET ConfirmedQty = l.IssuedQty - 1, DiscrepancyReason = N'Tổ may đếm thiếu 1 bộ, thủ kho đã đối chiếu và chấp nhận'
FROM dbo.MaterialIssueLine l
WHERE l.MaterialIssueLineId = (SELECT MIN(l2.MaterialIssueLineId) FROM dbo.MaterialIssueLine l2 JOIN dbo.MaterialIssue m2 ON m2.MaterialIssueId = l2.MaterialIssueId
                               WHERE m2.WarehouseType = 'SEMI_FINISHED' AND m2.Status = 'RECEIVED' AND l2.IssuedQty > 3
                                 AND m2.OrderId = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2511-005'));
INSERT #Mv (Wh, OrderLineId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'SEMI_FINISHED', l.OrderLineId, 'MATERIAL_ISSUE', mi.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, mi.IssueDate, 30
FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = l.MaterialIssueId
WHERE mi.Status = 'RECEIVED' AND mi.WarehouseType = 'SEMI_FINISHED' AND l.ConfirmedQty > 0;

-- Phụ liệu cấp cho tổ may (một phiếu cho mỗi đơn × tổ): theo số BTP tổ nhận lần 1; đơn chưa nhận phụ liệu từ NCC (polo, sơ mi mới) thì chưa cấp
INSERT #Iss (IssueKey, OrderCode, WoKey, Wh, TeamId, IssueDate, Status)
SELECT CONCAT(l.OrderCode, '-T-', l.TeamCode), l.OrderCode, NULL, 'MATERIAL', l.TeamId, DATEADD(DAY, o.StartOff + 5, @today), 'RECEIVED'
FROM (SELECT DISTINCT OrderCode, TeamId, TeamCode FROM #LnF WHERE IssueQ > 0 AND TeamId IS NOT NULL) l
JOIN #Ord o ON o.OrderCode = l.OrderCode
WHERE l.OrderCode IN ('DH-2509-001', 'DH-2510-002', 'DH-2510-003', 'DH-2510-004');
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt)
SELECT CONCAT('PC-', FORMAT(i.IssueDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', 200 + ROW_NUMBER() OVER (PARTITION BY i.IssueDate ORDER BY i.IssueKey)), 3)),
       'MATERIAL', co.OrderId, i.TeamId, i.IssueDate, @thukho, 'RECEIVED', tl.EmployeeId, DATEADD(HOUR, 10, CAST(i.IssueDate AS DATETIME2)), DATEADD(HOUR, 8, CAST(i.IssueDate AS DATETIME2))
FROM #Iss i JOIN dbo.CustomerOrder co ON co.OrderCode = i.OrderCode
OUTER APPLY (SELECT TOP 1 e.EmployeeId FROM dbo.Employee e JOIN dbo.Role r ON r.RoleId = e.RoleId AND r.RoleCode = 'TEAM_LEADER' WHERE e.TeamId = i.TeamId AND e.Status = 'ACTIVE') tl
WHERE i.IssueKey LIKE '%-T-%';
UPDATE i SET MaterialIssueId = mi.MaterialIssueId
FROM #Iss i JOIN dbo.CustomerOrder co ON co.OrderCode = i.OrderCode
JOIN dbo.MaterialIssue mi ON mi.OrderId = co.OrderId AND mi.WarehouseType = 'MATERIAL' AND mi.ReceivingTeamId = i.TeamId AND mi.IssueDate = i.IssueDate AND mi.WorkOrderId IS NULL
WHERE i.IssueKey LIKE '%-T-%';
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, t.MaterialId, t.Qty, t.Qty
FROM #Iss i
JOIN (SELECT l.OrderCode, l.TeamId, pcm.MaterialId, CEILING(SUM(l.IssueQ * c.PerGarment)) AS Qty
      FROM #LnF l
      JOIN dbo.OrderLine ol ON ol.OrderLineId = l.OrderLineId
      JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
      JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductId = ol.ProductId AND pcm.ProductColorId = pv.ProductColorId
      JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pcm.ProductComponentId AND pc.ComponentType = 'TRIM'
      JOIN #Cons c ON c.Prod = l.ProdCode AND c.Comp = pc.ComponentName
      GROUP BY l.OrderCode, l.TeamId, pcm.MaterialId) t ON t.OrderCode = i.OrderCode AND t.TeamId = i.TeamId
WHERE i.IssueKey LIKE '%-T-%' AND t.Qty > 0;
INSERT #Mv (Wh, MaterialId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'MATERIAL', l.MaterialId, 'MATERIAL_ISSUE', mi.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, mi.IssueDate, 20
FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = l.MaterialIssueId
WHERE mi.Status = 'RECEIVED' AND mi.WarehouseType = 'MATERIAL' AND mi.WorkOrderId IS NULL AND l.MaterialId IS NOT NULL AND l.ConfirmedQty > 0;

/* ---------- Trả vải thừa / phế liệu về kho (tổ cắt) và trả BTP thừa (tổ may) ---------- */
INSERT dbo.MaterialReturn (ReturnCode, MaterialIssueId, Note, Status, ConfirmedBy, ConfirmedAt, RejectReason, CreatedAt, ReturningTeamId, WarehouseType, OrderId, WorkOrderId)
SELECT CONCAT('TV-', FORMAT(r.D, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY r.D ORDER BY r.Code)), 3)),
       mi.MaterialIssueId, r.Note, r.Status,
       CASE WHEN r.Status = 'CONFIRMED' THEN @thukho END, CASE WHEN r.Status = 'CONFIRMED' THEN DATEADD(HOUR, 11, CAST(r.D AS DATETIME2)) END,
       r.Reject, DATEADD(HOUR, 9, CAST(r.D AS DATETIME2)), @cat, 'MATERIAL', co.OrderId, w.WoId
FROM (VALUES
 ('O1', 'DH-2509-001', 'O1', DATEADD(DAY, -62, @today), 'CONFIRMED', N'Trả vải thừa và vải vụn sau khi cắt xong lệnh', NULL),
 ('O2', 'DH-2510-002', 'O2', DATEADD(DAY, -45, @today), 'CONFIRMED', N'Trả vải kaki thừa', NULL),
 ('O3', 'DH-2510-003', 'O3', DATEADD(DAY, -27, @today), 'CONFIRMED', N'Trả vải dù và vải lót thừa', NULL),
 ('O4', 'DH-2510-004', 'O4', DATEADD(DAY, -19, @today), 'REJECTED',  N'Trả vải thừa', N'Số mét ghi trên phiếu lệch cuộn thực tế, tổ cắt đo lại rồi lập phiếu mới'),
 ('O4B','DH-2510-004', 'O4', DATEADD(DAY, -18, @today), 'CONFIRMED', N'Trả vải thừa (lập lại sau khi đo lại)', NULL),
 ('O9', 'DH-2510-009', 'O9', DATEADD(DAY, -14, @today), 'CANCELLED', N'Lập phiếu trả nhưng đơn tạm dừng nên hủy', NULL),
 ('O5', 'DH-2511-005', 'O5A', @today, 'PENDING', N'Trả vải thừa lô cắt đầu, chờ thủ kho nhận', NULL)) r (Code, OrderCode, WoKey, D, Status, Note, Reject)
JOIN dbo.CustomerOrder co ON co.OrderCode = r.OrderCode
JOIN #Wo w ON w.WoKey = r.WoKey
OUTER APPLY (SELECT TOP 1 MaterialIssueId FROM dbo.MaterialIssue x WHERE x.WorkOrderId = w.WoId AND x.WarehouseType = 'MATERIAL' ORDER BY x.IssueDate DESC) mi;

-- Dòng trả: vải thừa = 1,5% đã cấp dự phòng (trừ phần hao 0,5%); phiếu O1 trả cả vải vụn (phế liệu, kg)
INSERT dbo.MaterialReturnLine (MaterialReturnId, MaterialIssueLineId, MaterialId, ReturnedQty, ConfirmedQty, DiscrepancyReason)
SELECT mr.MaterialReturnId, NULL, f.MaterialId, q.Ret, CASE WHEN mr.Status = 'CONFIRMED' THEN q.Ret - q.Short END,
       CASE WHEN mr.Status = 'CONFIRMED' AND q.Short > 0 THEN N'Đo lại ở kho thiếu so với phiếu' END
FROM dbo.MaterialReturn mr
JOIN #Wo w ON w.WoId = mr.WorkOrderId
JOIN (SELECT WoKey, MaterialId, SUM(UsedM) AS Total FROM #LayFab GROUP BY WoKey, MaterialId) f ON f.WoKey = w.WoKey
CROSS APPLY (SELECT CAST(ROUND(f.Total * .004, 1) AS DECIMAL(18,3)) AS Ret, CAST(IIF(mr.ReturnCode LIKE '%' AND f.MaterialId % 5 = 0, 0.3, 0) AS DECIMAL(18,3)) AS Short) q
WHERE q.Ret > 0;
INSERT dbo.MaterialReturnLine (MaterialReturnId, MaterialId, ReturnedQty, ConfirmedQty)
SELECT mr.MaterialReturnId, m.MaterialId, v.Qty, IIF(mr.Status = 'CONFIRMED', v.Qty, NULL)
FROM dbo.MaterialReturn mr
JOIN dbo.CustomerOrder co ON co.OrderId = mr.OrderId
JOIN (VALUES ('DH-2509-001', 'S-VAI-VUN', 46.5), ('DH-2510-002', 'S-VAI-VUN', 21.0), ('DH-2510-003', 'S-VAI-VUN', 38.0), ('DH-2510-003', 'S-KIM-GAY', 12.0)) v (OrderCode, Mat, Qty)
     ON v.OrderCode = co.OrderCode
JOIN dbo.Material m ON m.MaterialCode = v.Mat
WHERE mr.Status = 'CONFIRMED' AND mr.Note NOT LIKE N'%lập lại%';
INSERT #Mv (Wh, MaterialId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'MATERIAL', l.MaterialId, 'MATERIAL_RETURN', mr.MaterialReturnId, l.MaterialReturnLineId, l.ConfirmedQty, CAST(mr.ConfirmedAt AS DATE), 40
FROM dbo.MaterialReturnLine l JOIN dbo.MaterialReturn mr ON mr.MaterialReturnId = l.MaterialReturnId
WHERE mr.Status = 'CONFIRMED' AND l.ConfirmedQty > 0 AND l.MaterialId IS NOT NULL;

/* ---------- Ghi thẻ kho theo thứ tự thời gian ---------- */
IF OBJECT_ID('tempdb..#sp2') IS NOT NULL DROP TABLE #sp2;
CREATE TABLE #sp2 (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @wh VARCHAR(15), @mid INT, @lid INT, @dt VARCHAR(30), @doc INT, @line INT, @q DECIMAL(18,3), @d DATE;
DECLARE st CURSOR LOCAL FAST_FORWARD FOR SELECT Wh, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, D FROM #Mv ORDER BY D, Ord, DocId, LineId;
OPEN st; FETCH NEXT FROM st INTO @wh, @mid, @lid, @dt, @doc, @line, @q, @d;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        INSERT #sp2 EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @OrderLineId = @lid, @SourceDocType = @dt, @SourceDocId = @doc,
                                            @SourceLineId = @line, @QtyChange = @q, @TransactionDate = @d;
    END TRY
    BEGIN CATCH
        PRINT CONCAT(N'LỖI thẻ kho: ', @wh, N' mat=', @mid, N' line=', @lid, N' doc=', @dt, N'#', @doc, N' qty=', @q, N' ngày=', @d);
        EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @OrderLineId = @lid, @SourceDocType = @dt, @SourceDocId = @doc,
                                @SourceLineId = @line, @QtyChange = @q, @TransactionDate = @d;
    END CATCH;
    FETCH NEXT FROM st INTO @wh, @mid, @lid, @dt, @doc, @line, @q, @d;
END;
CLOSE st; DEALLOCATE st;

PRINT N'06: lệnh cắt, bàn cắt, bàn giao BTP, cấp phát, trả vải, thẻ kho xong.';
