/* =====================================================================================
   08. THÀNH PHẨM, XUẤT HÀNG, GIAO HÀNG, XE, THANH TOÁN ĐƠN HÀNG
   Bàn giao thành phẩm (CONFIRMED có lệch / PENDING / REJECTED / CANCELLED), phiếu xuất (PENDING / IN_DELIVERY / DELIVERED / REJECTED / CANCELLED,
   giao bằng xe công ty hoặc khách tự lấy), chuyến xe (STARTED / COMPLETED, cảnh báo km - xăng), giá trị chốt và thanh toán nhiều lần của đơn hàng.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @qaqc1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO');

/* ---------- Bàn giao thành phẩm QA/QC -> kho ---------- */
IF OBJECT_ID('tempdb..#Fg') IS NOT NULL DROP TABLE #Fg;
CREATE TABLE #Fg (FKey VARCHAR(20) COLLATE DATABASE_DEFAULT, OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, Seq INT, ShareFrom DECIMAL(4,2), ShareTo DECIMAL(4,2), FDate DATE,
                  Status VARCHAR(20) COLLATE DATABASE_DEFAULT, Reject NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL, FgHandoverId INT NULL);
INSERT #Fg (FKey, OrderCode, Seq, ShareFrom, ShareTo, FDate, Status)
SELECT CONCAT(o.OrderCode, '-F', s.n), o.OrderCode, s.n, IIF(s.n = 1, 0, .55), IIF(s.n = 1, .55, 1),
       CASE WHEN s.n = 1 THEN DATEADD(DAY, (DATEDIFF(DAY, q.Q0, q.Q1) * 7) / 10, q.Q0) ELSE DATEADD(DAY, 1, q.Q1) END, 'CONFIRMED'
FROM #Ord o JOIN #QcWin q ON q.OrderCode = o.OrderCode CROSS JOIN (VALUES (1), (2)) s (n)
WHERE EXISTS (SELECT 1 FROM #LnF l WHERE l.OrderCode = o.OrderCode AND l.FgQ > 0);
UPDATE #Fg SET FDate = @today WHERE FDate > @today;
DELETE FROM #Fg WHERE OrderCode = 'DH-2511-005' AND Seq = 2;   -- polo: mới bàn giao một lần
UPDATE #Fg SET ShareTo = 1 WHERE FKey = 'DH-2511-005-F1';
-- PENDING: QA/QC vừa lập phiếu bàn giao đợt tiếp của áo polo, chờ Thủ kho đếm
INSERT #Fg (FKey, OrderCode, Seq, ShareFrom, ShareTo, FDate, Status) VALUES ('DH-2511-005-F3', 'DH-2511-005', 3, 0, 0, @today, 'PENDING');
-- REJECTED / CANCELLED
INSERT #Fg (FKey, OrderCode, Seq, ShareFrom, ShareTo, FDate, Status, Reject) VALUES
 ('DH-2510-003-F4', 'DH-2510-003', 4, 0, 0, DATEADD(DAY, -9, @today), 'REJECTED', N'Đếm thực tế thiếu so với phiếu 12 sản phẩm, QA/QC kiểm lại rồi bàn giao lại'),
 ('DH-2510-004-F5', 'DH-2510-004', 5, 0, 0, DATEADD(DAY, -8, @today), 'CANCELLED', NULL);

INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt, RejectReason)
SELECT CONCAT('TP-', FORMAT(f.FDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY f.FDate ORDER BY f.FKey)), 3)),
       co.OrderId, p.ProductId, @qaqc1, DATEADD(HOUR, 16, CAST(f.FDate AS DATETIME2)), f.Status,
       CASE WHEN f.Status = 'CONFIRMED' THEN @thukho END, CASE WHEN f.Status = 'CONFIRMED' THEN DATEADD(HOUR, 17, CAST(f.FDate AS DATETIME2)) END, f.Reject
FROM #Fg f JOIN dbo.CustomerOrder co ON co.OrderCode = f.OrderCode JOIN #Ord o ON o.OrderCode = f.OrderCode JOIN dbo.Product p ON p.ProductCode = o.Prod;
UPDATE f SET FgHandoverId = h.FgHandoverId
FROM #Fg f JOIN dbo.CustomerOrder co ON co.OrderCode = f.OrderCode
JOIN dbo.FgHandover h ON h.OrderId = co.OrderId AND CAST(h.HandedAt AS DATE) = f.FDate AND h.Status = f.Status AND ISNULL(h.RejectReason, '') = ISNULL(f.Reject, '');

-- Dòng bàn giao: CONFIRMED chia theo tỉ lệ của FgQ; PENDING = số kiểm đạt chưa bàn giao (tối đa 10 / dòng); một dòng đếm lệch 1 sản phẩm
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty, DiscrepancyReason)
SELECT f.FgHandoverId, l.OrderLineId, q.Handed,
       CASE WHEN f.Status = 'CONFIRMED' THEN q.Handed - q.Short END,
       CASE WHEN f.Status = 'CONFIRMED' AND q.Short > 0 THEN N'Thủ kho đếm thiếu 1 sản phẩm so với phiếu, QA/QC đã xác nhận' END
FROM #Fg f JOIN #LnF l ON l.OrderCode = f.OrderCode
CROSS APPLY (SELECT CASE WHEN f.Status = 'CONFIRMED' THEN (l.FgQ * f.ShareTo * 100) / 100 - (l.FgQ * f.ShareFrom * 100) / 100
                         WHEN f.Status = 'PENDING' THEN IIF(l.QcQ - l.FgQ > 10, 10, l.QcQ - l.FgQ)
                         ELSE IIF(l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.OrderCode = l.OrderCode), 12, 0) END AS Handed) h
CROSS APPLY (SELECT CAST(FLOOR(h.Handed) AS INT) AS Handed,
                    IIF(f.FKey = 'DH-2510-003-F2' AND l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.OrderCode = l.OrderCode), 1, 0) AS Short) q
WHERE q.Handed > 0;
-- Phần làm tròn: bù vào lần 2 để tổng bàn giao đúng FgQ của từng dòng
UPDATE fl SET HandedQty = fl.HandedQty + d.Diff, ConfirmedQty = fl.ConfirmedQty + d.Diff
FROM dbo.FgHandoverLine fl
JOIN dbo.FgHandover h ON h.FgHandoverId = fl.FgHandoverId AND h.Status = 'CONFIRMED'
JOIN #Fg f ON f.FgHandoverId = h.FgHandoverId AND f.Seq = (SELECT MAX(f2.Seq) FROM #Fg f2 WHERE f2.OrderCode = f.OrderCode AND f2.Status = 'CONFIRMED')
JOIN #LnF l ON l.OrderLineId = fl.OrderLineId AND l.OrderCode = f.OrderCode
CROSS APPLY (SELECT l.FgQ - (SELECT SUM(fl2.HandedQty) FROM dbo.FgHandoverLine fl2 JOIN dbo.FgHandover h2 ON h2.FgHandoverId = fl2.FgHandoverId
                             WHERE fl2.OrderLineId = fl.OrderLineId AND h2.Status = 'CONFIRMED') AS Diff) d
WHERE d.Diff <> 0;

IF OBJECT_ID('tempdb..#Mv2') IS NOT NULL DROP TABLE #Mv2;
SELECT CAST('FINISHED' AS VARCHAR(15)) AS Wh, fl.OrderLineId, CAST('FG_HANDOVER' AS VARCHAR(30)) AS DocType, h.FgHandoverId AS DocId, fl.FgHandoverLineId AS LineId, CAST(fl.ConfirmedQty AS DECIMAL(18,3)) AS Qty,
       CAST(h.ConfirmedAt AS DATE) AS D, CAST(10 AS INT) AS Ord
INTO #Mv2
FROM dbo.FgHandoverLine fl JOIN dbo.FgHandover h ON h.FgHandoverId = fl.FgHandoverId
WHERE h.Status = 'CONFIRMED' AND fl.ConfirmedQty > 0;

/* ---------- Phiếu xuất kho / giao hàng ---------- */
IF OBJECT_ID('tempdb..#Dn') IS NOT NULL DROP TABLE #Dn;
CREATE TABLE #Dn (DKey VARCHAR(20) COLLATE DATABASE_DEFAULT, OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, Seq INT, ShareFrom DECIMAL(5,3), ShareTo DECIMAL(5,3),
                  Status VARCHAR(25) COLLATE DATABASE_DEFAULT, Dy INT, Method VARCHAR(20) COLLATE DATABASE_DEFAULT NULL, Reject NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL,
                  Remain BIT, DnId INT NULL);
-- Dy: số ngày sau khi kiểm xong (cuối cửa sổ QA/QC); Remain=1: lấy phần thành phẩm còn tồn chưa xuất
INSERT #Dn (DKey, OrderCode, Seq, ShareFrom, ShareTo, Status, Dy, Method, Reject, Remain) VALUES
 ('O1-D1', 'DH-2509-001', 1, 0,    .5,   'DELIVERED',   2, 'COMPANY_VEHICLE', NULL, 0),
 ('O1-D2', 'DH-2509-001', 2, .5,   1,    'DELIVERED',   6, 'CUSTOMER_PICKUP', NULL, 0),
 ('O2-D1', 'DH-2510-002', 1, 0,    .4,   'DELIVERED',   2, 'COMPANY_VEHICLE', NULL, 0),
 ('O2-D2', 'DH-2510-002', 2, .4,   .75,  'DELIVERED',   4, 'COMPANY_VEHICLE', NULL, 0),
 ('O2-D3', 'DH-2510-002', 3, .75,  1,    'DELIVERED',   7, 'CUSTOMER_PICKUP', NULL, 0),
 ('O3-D1', 'DH-2510-003', 1, 0,    1,    'DELIVERED',   2, 'COMPANY_VEHICLE', NULL, 0),
 ('O3-D2', 'DH-2510-003', 2, 0,    0,    'PENDING',     0, NULL, NULL, 1),
 ('O3-D3', 'DH-2510-003', 3, 0,    0,    'REJECTED',    0, NULL, N'Phiếu ghi sai địa chỉ giao (kho Quận 7 thay vì Thủ Đức), lập lại phiếu', 0),
 ('O4-D1', 'DH-2510-004', 1, 0,    .45,  'DELIVERED',   1, 'COMPANY_VEHICLE', NULL, 0),
 ('O4-D2', 'DH-2510-004', 2, .45,  1,    'IN_DELIVERY', 99, 'COMPANY_VEHICLE', NULL, 0),
 ('O4-D3', 'DH-2510-004', 3, 0,    0,    'PENDING',     0, NULL, NULL, 1),
 ('O4-D4', 'DH-2510-004', 4, 0,    0,    'CANCELLED',   0, NULL, NULL, 0);

IF OBJECT_ID('tempdb..#DnDate') IS NOT NULL DROP TABLE #DnDate;
SELECT d.DKey, CASE WHEN d.Dy = 99 THEN @today
                    WHEN d.Status IN ('PENDING', 'REJECTED', 'CANCELLED') THEN DATEADD(DAY, -IIF(d.Status = 'PENDING', 0, 2), @today)
                    ELSE IIF(DATEADD(DAY, d.Dy, q.Q1) > @today, DATEADD(DAY, -1, @today), DATEADD(DAY, d.Dy, q.Q1)) END AS DDate
INTO #DnDate FROM #Dn d JOIN #QcWin q ON q.OrderCode = d.OrderCode;

INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, RejectReason, DispatchedBy, DispatchedAt,
                         CustomerReceivedDate, DeliveryMethod)
SELECT CONCAT('XH-', FORMAT(x.DDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY x.DDate ORDER BY d.DKey)), 3)),
       co.OrderId, x.DDate, c.DefaultAddress,
       CASE d.Status WHEN 'PENDING' THEN N'Lập phiếu xuất phần thành phẩm còn tồn, chờ Quản đốc ký và Thủ kho xuất'
                     WHEN 'IN_DELIVERY' THEN N'Giao lô cuối trong ngày' ELSE NULL END,
       d.Status, DATEADD(HOUR, -20, CAST(x.DDate AS DATETIME2)), d.Reject,
       CASE WHEN d.Status IN ('DELIVERED', 'IN_DELIVERY') THEN @thukho END,
       CASE WHEN d.Status IN ('DELIVERED', 'IN_DELIVERY') THEN DATEADD(HOUR, 8, CAST(x.DDate AS DATETIME2)) END,
       CASE WHEN d.Status = 'DELIVERED' THEN DATEADD(DAY, IIF(d.Method = 'COMPANY_VEHICLE', 1, 0), x.DDate) END,
       d.Method
FROM #Dn d JOIN #DnDate x ON x.DKey = d.DKey
JOIN dbo.CustomerOrder co ON co.OrderCode = d.OrderCode
JOIN dbo.Customer c ON c.CustomerId = co.CustomerId;
UPDATE d SET DnId = n.DispatchNoteId
FROM #Dn d JOIN #DnDate x ON x.DKey = d.DKey JOIN dbo.CustomerOrder co ON co.OrderCode = d.OrderCode
JOIN dbo.DispatchNote n ON n.OrderId = co.OrderId AND n.DeliveryDate = x.DDate AND n.Status = d.Status AND ISNULL(n.DeliveryMethod, '') = ISNULL(d.Method, '')
     AND ISNULL(n.RejectReason, '') = ISNULL(d.Reject, '');

-- Dòng xuất: phiếu đã xuất chia theo tỉ lệ của DispQ; PENDING = phần thành phẩm tồn (FgQ - DispQ, tối đa 40 / dòng); REJECTED / CANCELLED = số nhỏ
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty)
SELECT d.DnId, l.OrderLineId, q.Qty
FROM #Dn d JOIN #LnF l ON l.OrderCode = d.OrderCode
CROSS APPLY (SELECT CASE WHEN d.Status IN ('DELIVERED', 'IN_DELIVERY') THEN CAST(FLOOR(l.DispQ * d.ShareTo) AS INT) - CAST(FLOOR(l.DispQ * d.ShareFrom) AS INT)
                         WHEN d.Status = 'PENDING' THEN IIF(l.FgQ - l.DispQ > 40, 40, l.FgQ - l.DispQ)
                         ELSE IIF(l.OrderLineId = (SELECT MIN(OrderLineId) FROM #LnF x WHERE x.OrderCode = l.OrderCode), 25, 0) END AS Qty) q
WHERE q.Qty > 0;
-- phần làm tròn: bù vào phiếu cuối của mỗi dòng (phiếu có ShareTo = 1) để tổng xuất đúng DispQ
UPDATE dl SET IssueQty = dl.IssueQty + d2.Diff
FROM dbo.DispatchNoteLine dl
JOIN #Dn d ON d.DnId = dl.DispatchNoteId AND d.ShareTo = 1 AND d.Status IN ('DELIVERED', 'IN_DELIVERY')
JOIN #LnF l ON l.OrderLineId = dl.OrderLineId AND l.OrderCode = d.OrderCode
CROSS APPLY (SELECT l.DispQ - (SELECT SUM(x.IssueQty) FROM dbo.DispatchNoteLine x JOIN #Dn dd ON dd.DnId = x.DispatchNoteId AND dd.Status IN ('DELIVERED', 'IN_DELIVERY')
                              WHERE x.OrderLineId = dl.OrderLineId) AS Diff) d2
WHERE d2.Diff <> 0;
DELETE FROM dbo.DispatchNoteLine WHERE IssueQty <= 0;

INSERT #Mv2 (Wh, OrderLineId, DocType, DocId, LineId, Qty, D, Ord)
SELECT 'FINISHED', dl.OrderLineId, 'DISPATCH_NOTE', n.DispatchNoteId, dl.DispatchNoteLineId, -dl.IssueQty, CAST(n.DispatchedAt AS DATE), 20
FROM dbo.DispatchNoteLine dl JOIN dbo.DispatchNote n ON n.DispatchNoteId = dl.DispatchNoteId
WHERE n.Status IN ('DELIVERED', 'IN_DELIVERY');

IF OBJECT_ID('tempdb..#sp3') IS NOT NULL DROP TABLE #sp3;
CREATE TABLE #sp3 (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @wh VARCHAR(15), @lid INT, @dt VARCHAR(30), @doc INT, @line INT, @q DECIMAL(18,3), @dd DATE;
DECLARE st CURSOR LOCAL FAST_FORWARD FOR SELECT Wh, OrderLineId, DocType, DocId, LineId, Qty, D FROM #Mv2 ORDER BY D, Ord, DocId, LineId;
OPEN st; FETCH NEXT FROM st INTO @wh, @lid, @dt, @doc, @line, @q, @dd;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        INSERT #sp3 EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @OrderLineId = @lid, @SourceDocType = @dt, @SourceDocId = @doc, @SourceLineId = @line,
                                            @QtyChange = @q, @TransactionDate = @dd;
    END TRY
    BEGIN CATCH
        PRINT CONCAT(N'LỖI thẻ kho TP: line=', @lid, N' doc=', @dt, N'#', @doc, N' qty=', @q, N' ngày=', @dd);
        EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @OrderLineId = @lid, @SourceDocType = @dt, @SourceDocId = @doc, @SourceLineId = @line,
                                @QtyChange = @q, @TransactionDate = @dd;
    END CATCH;
    FETCH NEXT FROM st INTO @wh, @lid, @dt, @doc, @line, @q, @dd;
END;
CLOSE st; DEALLOCATE st;

/* ---------- Xe: chuyến giao hàng (gắn phiếu xuất) và chuyến khác ---------- */
DECLARE @v1 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51C-123.45'),
        @v2 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51D-678.90'),
        @codien INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CODIEN');
IF OBJECT_ID('tempdb..#Trip') IS NOT NULL DROP TABLE #Trip;
SELECT n.DispatchNoteId, n.DeliveryDate, n.Status, d.OrderCode, ROW_NUMBER() OVER (ORDER BY n.DeliveryDate, n.DispatchNoteId) AS rn
INTO #Trip FROM dbo.DispatchNote n JOIN #Dn d ON d.DnId = n.DispatchNoteId WHERE n.DeliveryMethod = 'COMPANY_VEHICLE';

-- UQ_VT_OneOpen: mỗi xe tối đa 1 chuyến STARTED — giao đang chạy gắn @v1; chuyến việc khác STARTED gắn @v2 bên dưới.
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverName, DriverEmployeeId, Purpose, DispatchNoteId, OdometerStart, OdometerEnd, FuelLiters, FuelCost,
                        Status, KmGapWarning, FuelOverNormWarning, RecordedAt)
SELECT IIF(t.Status = 'IN_DELIVERY', @v1, IIF(t.rn % 3 = 0, @v2, @v1)), t.DeliveryDate, IIF(t.rn = 3, NULL, N'Nguyễn Văn Tài (lái xe)'), IIF(t.rn = 3, @codien, NULL),
       CONCAT(N'Giao hàng đơn ', t.OrderCode), t.DispatchNoteId,
       10000 + t.rn * 160, IIF(t.Status = 'IN_DELIVERY', NULL, 10000 + t.rn * 160 + 38 + (t.rn % 4) * 12),
       IIF(t.Status = 'IN_DELIVERY', NULL, 6.0 + (t.rn % 4) * 1.2 + IIF(t.rn = 4, 6, 0)),
       IIF(t.Status = 'IN_DELIVERY', NULL, (6.0 + (t.rn % 4) * 1.2 + IIF(t.rn = 4, 6, 0)) * 24500),
       IIF(t.Status = 'IN_DELIVERY', 'STARTED', 'COMPLETED'),
       IIF(t.rn = 5, 1, 0), IIF(t.rn = 4, 1, 0), DATEADD(HOUR, 17, CAST(t.DeliveryDate AS DATETIME2))
FROM #Trip t;
-- Chuyến việc khác (không gắn phiếu xuất)
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverName, Purpose, OdometerStart, OdometerEnd, FuelLiters, FuelCost, Status, KmGapWarning, FuelOverNormWarning, RecordedAt)
VALUES
 (@v2, DATEADD(DAY, -12, @today), N'Nguyễn Văn Tài (lái xe)', N'Đi mua phụ liệu ở Quận 10 và Quận 5', 11800, 11846, 5.2, 127400, 'COMPLETED', 0, 0, DATEADD(DAY, -12, CAST(@today AS DATETIME2))),
 (@v1, DATEADD(DAY, -5, @today),  N'Nguyễn Văn Tài (lái xe)', N'Đi lấy vải ở Thành Công',                      10930, 10981, 7.9, 193500, 'COMPLETED', 0, 1, DATEADD(DAY, -5, CAST(@today AS DATETIME2))),
 (@v2, DATEADD(DAY, -1, @today),  N'Trần Văn Bình (lái xe thuê)', N'Đi ngân hàng và hải quan',                  11846, NULL, NULL, NULL,   'STARTED',   0, 0, DATEADD(DAY, -1, CAST(@today AS DATETIME2)));

/* ---------- Giá trị chốt và thanh toán đơn hàng ---------- */
UPDATE co SET FinalAmount = v.Final, FinalAmountReason = v.Reason
FROM dbo.CustomerOrder co
JOIN (SELECT co2.OrderCode, SUM(l.DispQ * o.Price) AS Delivered
      FROM #LnF l JOIN dbo.CustomerOrder co2 ON co2.OrderCode = l.OrderCode JOIN #Ord o ON o.OrderCode = l.OrderCode GROUP BY co2.OrderCode) t ON t.OrderCode = co.OrderCode
CROSS APPLY (SELECT CASE co.OrderCode WHEN 'DH-2509-001' THEN t.Delivered
                                      WHEN 'DH-2510-002' THEN t.Delivered - 1500000
                                      WHEN 'DH-2510-003' THEN (SELECT SUM(ol.OrderedQty * ol.CmtUnitPrice) FROM dbo.OrderLine ol WHERE ol.OrderId = co.OrderId) * .98 END AS Final,
                    CASE co.OrderCode WHEN 'DH-2509-001' THEN N'Chốt theo số lượng thực giao'
                                      WHEN 'DH-2510-002' THEN N'Khách trừ 1.500.000 đồng do 12 chiếc lệch màu chỉ, đã thỏa thuận ngày giao hàng'
                                      WHEN 'DH-2510-003' THEN N'Chốt tạm theo 98% số đặt (khách chưa nghiệm thu đợt 2)' END AS Reason) v
WHERE co.OrderCode IN ('DH-2509-001', 'DH-2510-002', 'DH-2510-003');

INSERT dbo.OrderPayment (OrderId, PaymentDate, Amount, PaymentMethod, PaymentNote, CreatedAt, UpdatedAt, EditReason)
SELECT co.OrderId, DATEADD(DAY, p.Dy, @today),
       CASE WHEN p.Frac IS NULL THEN p.Fixed ELSE ROUND(co.FinalAmount * p.Frac / 1000, 0) * 1000 END,
       p.Method, p.Note, DATEADD(HOUR, 10, CAST(DATEADD(DAY, p.Dy, @today) AS DATETIME2)),
       CASE WHEN p.Edit IS NOT NULL THEN DATEADD(HOUR, 14, CAST(DATEADD(DAY, p.Dy, @today) AS DATETIME2)) END, p.Edit
FROM (VALUES
 ('DH-2509-001', -50, .40,  NULL, 'BANK_TRANSFER', N'Khách chuyển khoản đợt 1 (40%)', NULL),
 ('DH-2509-001', -35, .40,  NULL, 'BANK_TRANSFER', N'Khách chuyển khoản đợt 2 (40%)', NULL),
 ('DH-2509-001', -22, .20,  NULL, 'CASH',          N'Khách thanh toán nốt bằng tiền mặt', N'Nhập sai ngày, sửa theo biên lai'),
 ('DH-2510-002', -20, .60,  NULL, 'BANK_TRANSFER', N'Khách thanh toán 60% khi nhận hàng đợt 1-2', NULL),
 ('DH-2510-002', -9,  .45,  NULL, 'BANK_TRANSFER', N'Khách thanh toán phần còn lại', NULL),
 ('DH-2510-002', -6,  NULL, -250000, 'CASH',       N'Hoàn trả khách số tiền thanh toán dư', NULL),
 ('DH-2510-003', -3,  .50,  NULL, 'BANK_TRANSFER', N'Khách ứng 50% theo giá trị chốt tạm', NULL)) p (Code, Dy, Frac, Fixed, Method, Note, Edit)
JOIN dbo.CustomerOrder co ON co.OrderCode = p.Code;

PRINT N'08: thành phẩm, xuất hàng, xe, thanh toán đơn hàng xong.';
