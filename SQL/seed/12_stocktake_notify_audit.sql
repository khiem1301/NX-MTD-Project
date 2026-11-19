/* =====================================================================================
   12. KIỂM KÊ KHO, THÔNG BÁO, NHẬT KÝ THAY ĐỔI, ẢNH ĐÍNH KÈM
   Kiểm kê: APPROVED (có chênh lệch, điều chỉnh tồn qua usp_Stock_Post) / PENDING_APPROVAL / RETURNED.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC');

/* ---------- Kiểm kê ---------- */
IF OBJECT_ID('tempdb..#Sp4') IS NOT NULL DROP TABLE #Sp4;
CREATE TABLE #Sp4 (StockBalanceId INT, BalanceAfter DECIMAL(18,3));

-- K1: kho NVL đã duyệt, 8 mặt hàng, 3 dòng lệch (mất / dư / thiếu)
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, CreatedAt, ApprovedAt)
VALUES (CONCAT('KK-', FORMAT(DATEADD(DAY, -16, @today), 'yyyyMMdd'), '-001'), 'MATERIAL', N'Kho NVL - kiểm cuối tháng 10 (vải và phụ liệu chính)',
        DATEADD(DAY, -16, @today), 'APPROVED', DATEADD(HOUR, 9, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), DATEADD(HOUR, 16, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)));
DECLARE @k1 INT = SCOPE_IDENTITY();
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT TOP 8 @k1, sb.StockBalanceId, sb.OnHandQty,
       CASE ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) WHEN 2 THEN sb.OnHandQty - 3.5 WHEN 4 THEN sb.OnHandQty + 2 WHEN 7 THEN IIF(sb.OnHandQty >= 12, sb.OnHandQty - 12, 0) ELSE sb.OnHandQty END,
       CASE ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) WHEN 2 THEN N'Thiếu 3,5 m, cuộn vải đầu bị cắt thử' WHEN 4 THEN N'Dư 2 do nhập thiếu phiếu cũ' WHEN 7 THEN N'Thiếu 12, đã báo Quản đốc' END
FROM dbo.StockBalance sb WHERE sb.WarehouseType = 'MATERIAL' AND sb.OnHandQty > 0 ORDER BY sb.StockBalanceId;
DECLARE @sbid INT, @sl INT, @diff DECIMAL(18,3), @dk1 DATE = DATEADD(DAY, -15, @today), @dk2 DATE = DATEADD(DAY, -12, @today);
DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT StockBalanceId, StocktakeLineId, CountedQty - BookQty FROM dbo.StocktakeLine WHERE StocktakeId = @k1 AND CountedQty <> BookQty;
OPEN c1; FETCH NEXT FROM c1 INTO @sbid, @sl, @diff;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT #Sp4 EXEC dbo.usp_Stock_Post @StockBalanceId = @sbid, @SourceDocType = 'STOCKTAKE', @SourceDocId = @k1, @SourceLineId = @sl, @QtyChange = @diff,
                                        @TransactionDate = @dk1;
    FETCH NEXT FROM c1 INTO @sbid, @sl, @diff;
END; CLOSE c1; DEALLOCATE c1;

-- K2: kho thành phẩm đã duyệt, lệch 2 sản phẩm
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, CreatedAt, ApprovedAt)
VALUES (CONCAT('KK-', FORMAT(DATEADD(DAY, -12, @today), 'yyyyMMdd'), '-001'), 'FINISHED', N'Kho thành phẩm - kiểm trước đợt giao áo sơ mi',
        DATEADD(DAY, -12, @today), 'APPROVED', DATEADD(HOUR, 9, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), DATEADD(HOUR, 15, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)));
DECLARE @k2 INT = SCOPE_IDENTITY();
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT TOP 6 @k2, sb.StockBalanceId, sb.OnHandQty, IIF(ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) = 3, sb.OnHandQty - 2, sb.OnHandQty),
       IIF(ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) = 3, N'Thiếu 2 sản phẩm, nghi giao nhầm lô', NULL)
FROM dbo.StockBalance sb WHERE sb.WarehouseType = 'FINISHED' AND sb.OnHandQty > 5 ORDER BY sb.StockBalanceId;
DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR SELECT StockBalanceId, StocktakeLineId, CountedQty - BookQty FROM dbo.StocktakeLine WHERE StocktakeId = @k2 AND CountedQty <> BookQty;
OPEN c2; FETCH NEXT FROM c2 INTO @sbid, @sl, @diff;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT #Sp4 EXEC dbo.usp_Stock_Post @StockBalanceId = @sbid, @SourceDocType = 'STOCKTAKE', @SourceDocId = @k2, @SourceLineId = @sl, @QtyChange = @diff,
                                        @TransactionDate = @dk2;
    FETCH NEXT FROM c2 INTO @sbid, @sl, @diff;
END; CLOSE c2; DEALLOCATE c2;

-- K3: kho BTP chờ Quản đốc duyệt (đã đếm, có lệch)
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, CreatedAt)
VALUES (CONCAT('KK-', FORMAT(DATEADD(DAY, -1, @today), 'yyyyMMdd'), '-001'), 'SEMI_FINISHED', N'Kho BTP - kiểm giữa tháng 11', DATEADD(DAY, -1, @today), 'PENDING_APPROVAL',
        DATEADD(HOUR, 14, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)));
DECLARE @k3 INT = SCOPE_IDENTITY();
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT TOP 6 @k3, sb.StockBalanceId, sb.OnHandQty, IIF(ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) IN (2, 5), sb.OnHandQty - 1, sb.OnHandQty),
       IIF(ROW_NUMBER() OVER (ORDER BY sb.StockBalanceId) IN (2, 5), N'Thiếu 1 bộ, chờ đối chiếu với tổ may', NULL)
FROM dbo.StockBalance sb WHERE sb.WarehouseType = 'SEMI_FINISHED' AND sb.OnHandQty > 2 ORDER BY sb.StockBalanceId;

-- K4: kho NVL bị trả lại (số đếm lệch quá nhiều, đề nghị đếm lại)
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, ReturnReason, CreatedAt)
VALUES (CONCAT('KK-', FORMAT(DATEADD(DAY, -4, @today), 'yyyyMMdd'), '-001'), 'MATERIAL', N'Kho NVL - phụ liệu', DATEADD(DAY, -4, @today), 'RETURNED',
        N'Số đếm khóa kéo lệch 40% so với sổ, đề nghị đếm lại từng thùng rồi gửi lại',
        DATEADD(HOUR, 10, CAST(DATEADD(DAY, -4, @today) AS DATETIME2)));
DECLARE @k4 INT = SCOPE_IDENTITY();
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT TOP 5 @k4, sb.StockBalanceId, sb.OnHandQty, CAST(sb.OnHandQty * .6 AS DECIMAL(18,3)), N'Số đếm tạm, chưa kiểm hết thùng'
FROM dbo.StockBalance sb JOIN dbo.Material m ON m.MaterialId = sb.MaterialId AND m.MaterialType = 'TRIM'
WHERE sb.WarehouseType = 'MATERIAL' AND sb.OnHandQty > 10 ORDER BY sb.StockBalanceId DESC;

/* ---------- Ảnh đính kèm: một ảnh mẫu dùng chung (be/App_Data/uploads/seed/placeholder.png) ---------- */
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'DISPATCH_NOTE', n.DispatchNoteId, 'SIGNED_ISSUE_SLIP', 'seed/placeholder.png', N'phieu-xuat-da-ky.png', 'image/png', 1, DATEADD(HOUR, 7, CAST(n.DeliveryDate AS DATETIME2))
FROM dbo.DispatchNote n WHERE n.Status IN ('IN_DELIVERY', 'DELIVERED');
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'DISPATCH_NOTE', n.DispatchNoteId, 'SIGNED_DELIVERY_NOTE', 'seed/placeholder.png', N'bien-ban-giao-hang.png', 'image/png', 1, DATEADD(HOUR, 11, CAST(n.CustomerReceivedDate AS DATETIME2))
FROM dbo.DispatchNote n WHERE n.Status = 'DELIVERED';
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'PURCHASE_ORDER', po.PurchaseOrderId, 'PAYMENT_PROOF', 'seed/placeholder.png', N'uy-nhiem-chi.png', 'image/png', 1, DATEADD(HOUR, 10, CAST(po.PaymentDate AS DATETIME2))
FROM dbo.PurchaseOrder po WHERE po.PaymentStatus IN ('PAID', 'PARTIALLY_PAID') AND po.PaymentDate IS NOT NULL;
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT TOP 8 'MATERIAL_RECEIPT', mr.MaterialReceiptId, 'DOCUMENT_PHOTO', 'seed/placeholder.png', N'phieu-giao-hang-ncc.png', 'image/png', 1, DATEADD(HOUR, 15, CAST(mr.ReceiptDate AS DATETIME2))
FROM dbo.MaterialReceipt mr WHERE mr.PurchaseOrderId IS NOT NULL AND mr.Status = 'CONFIRMED' ORDER BY mr.ReceiptDate DESC;
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'VEHICLE_TRIP', t.VehicleTripId, 'FUEL_RECEIPT', 'seed/placeholder.png', N'hoa-don-xang.png', 'image/png', 1, DATEADD(HOUR, 17, CAST(t.TripDate AS DATETIME2))
FROM dbo.VehicleTrip t WHERE t.Status = 'COMPLETED' AND t.FuelCost IS NOT NULL;
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'DEFECT_RECORD', d.DefectRecordId, 'PHOTO', 'seed/placeholder.png', N'anh-loi.png', 'image/png', 1, d.RecordedAt
FROM dbo.DefectRecord d WHERE d.DefectType <> 'OUTPUT_ERR';
INSERT dbo.Attachment (OwnerType, OwnerId, Purpose, FileUrl, FileName, ContentType, IsCurrent, UploadedAt)
SELECT 'REQUEST', r.RequestId, 'PHOTO', 'seed/placeholder.png', N'anh-minh-hoa.png', 'image/png', 1, r.CreatedAt
FROM dbo.Request r WHERE r.RequestTypeCode IN ('DEFECTIVE_MATERIAL', 'SPARE_PARTS', 'NEW_MACHINE');

/* ---------- Ảnh mẫu mã hàng ---------- */
INSERT dbo.ProductImage (ProductId, ImageKey, SortOrder)
SELECT p.ProductId, 'seed/placeholder.png', n.i
FROM dbo.Product p CROSS APPLY (VALUES (1), (2), (3)) n (i)
WHERE p.ProductCode IN ('AS-0412', 'QK-0308', 'AK-0522') AND (n.i = 1 OR p.ProductCode = 'AS-0412')
UNION ALL
SELECT p.ProductId, 'seed/placeholder.png', 1 FROM dbo.Product p WHERE p.ProductCode = 'AP-0715';
UPDATE dbo.Product SET SampleImageUrl = 'seed/placeholder.png' WHERE ProductCode IN ('AS-0412', 'QK-0308', 'AK-0522', 'AP-0715');

/* ---------- Thông báo người dùng ---------- */
INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, LinkUrl, ReadAt, PushStatus, CreatedAt)
SELECT e.EmployeeId, n.Src, n.Ref, NULL, n.Title, n.Msg, NULL,
       CASE WHEN n.ReadHrs IS NULL THEN NULL ELSE DATEADD(HOUR, n.ReadHrs, DATEADD(HOUR, n.AgoHrs, @now)) END,
       n.Push, DATEADD(HOUR, n.AgoHrs, @now)
FROM (VALUES
 ('QUANDOC', 'WAREHOUSE', 'MaterialReceipt', N'Phiếu nhập chờ xác nhận', N'Thủ kho vừa lập phiếu nhập NVL khách cấp đơn DH-2511-008, vui lòng kiểm tra và xác nhận.', -2, NULL, 'SENT'),
 ('QUANDOC', 'REQUEST',   'Request',         N'Đơn từ chờ duyệt', N'Có 4 đơn từ đang chờ Quản đốc duyệt: cắt bù áo khoác, vải lỗi áo polo, bù công, mua dây curoa.', -3, NULL, 'SENT'),
 ('QUANDOC', 'SYSTEM',    'CustomerOrder',   N'Đơn có nguy cơ trễ', N'Đơn DH-2510-003 còn 2 ngày đến hạn nhưng mới giao 55%.', -6, -5, 'SENT'),
 ('QUANDOC', 'WAREHOUSE', 'Stocktake',       N'Phiếu kiểm kê chờ duyệt', N'Phiếu kiểm kê kho BTP chờ Quản đốc duyệt.', -20, NULL, 'SENT'),
 ('QUANDOC', 'WAREHOUSE', 'PurchaseOrder',   N'Đơn mua bị trả lại', N'Kế toán trả lại đơn mua DM phụ liệu áo sơ mi: đề nghị kiểm tra đơn giá.', -40, -38, 'SENT'),
 ('KETOAN',  'WAREHOUSE', 'PurchaseOrder',   N'Đơn mua chờ thanh toán', N'Có 3 đơn mua đã nhận hàng đang chờ thanh toán.', -4, NULL, 'SENT'),
 ('KETOAN',  'REQUEST',   'Request',         N'Đơn bù công đã duyệt', N'Đơn bù công của tổ trưởng tổ may 1 đã được duyệt, cần chấm bù.', -50, -49, 'SENT'),
 ('KETOAN',  'SYSTEM',    'AttendanceSyncSession', N'Đồng bộ chấm công lỗi', N'Không đồng bộ được với Ronald Jack Pro quá 30 phút. Kiểm tra máy chấm công.', -72, -70, 'SENT'),
 ('KETOAN',  'SYSTEM',    'OrderPayment',    N'Khách thanh toán', N'Đơn DH-2510-003 vừa ghi nhận khoản thanh toán 50% giá trị chốt.', -70, -69, 'SENT'),
 ('THUKHO',  'PRODUCTION','CutHandover',     N'Chờ nhận BTP từ tổ cắt', N'Tổ cắt bàn giao BTP lệnh cắt áo polo hồng, vui lòng đếm và xác nhận.', -1, NULL, 'SENT'),
 ('THUKHO',  'PRODUCTION','FgHandover',      N'QA/QC bàn giao thành phẩm', N'Có phiếu bàn giao thành phẩm áo polo chờ nhận kho.', -2, NULL, 'SENT'),
 ('THUKHO',  'REQUEST',   'Request',         N'Yêu cầu cấp vải', N'Tổ cắt xin cấp vải thun hồng cho bàn cắt kế tiếp.', -5, NULL, 'SENT'),
 ('THUKHO',  'WAREHOUSE', 'Stocktake',       N'Phiếu kiểm kê bị trả lại', N'Phiếu kiểm kê kho NVL (phụ liệu) bị trả lại, đề nghị đếm lại.', -96, -90, 'SENT'),
 ('TTCAT',   'WAREHOUSE', 'MaterialIssue',   N'Chờ xác nhận nhận vải', N'Thủ kho vừa cấp vải cho lệnh cắt áo polo hồng, bấm xác nhận khi nhận đủ.', -1, NULL, 'SENT'),
 ('TTCAT',   'REQUEST',   'Request',         N'Đơn cắt bù chờ duyệt', N'Đơn cắt bù áo khoác đen đang chờ Quản đốc duyệt.', -24, -22, 'SENT'),
 ('TTMAY',   'WAREHOUSE', 'MaterialIssue',   N'Thủ kho cấp BTP cho tổ', N'Có phiếu cấp BTP áo sơ mi chờ tổ xác nhận nhận.', -2, NULL, 'SENT'),
 ('TTMAY',   'SYSTEM',    'Meal',            N'Nhắc xác nhận suất ăn', N'Chưa xác nhận suất ăn hôm nay. Hệ thống sẽ tự chốt theo chấm công lúc 08:30.', -6, -5, 'SENT'),
 ('TPMAY2',  'SYSTEM',    'Team',            N'Bạn đang thay tổ trưởng', N'Tổ trưởng tổ may 2 chưa có giờ vào hôm nay, bạn được thao tác thay.', -10, -9, 'SENT'),
 ('TTMAY2',  'PRODUCTION','Kpi',             N'KPI tổ có nguy cơ không đạt', N'KPI đơn áo polo của tổ may 2 còn 14 ngày, mới đạt 30% mục tiêu.', -30, NULL, 'SENT'),
 ('QAQC1',   'PRODUCTION','QcBatch',         N'Đợt kiểm sắp hết hạn sửa', N'Đợt kiểm sáng nay còn 2 giờ để chỉnh sửa số đạt.', -3, -2, 'SENT'),
 ('CNMAY1',  'PRODUCTION','WorkAllocation',  N'Có việc mới được giao', N'Tổ trưởng giao thêm 120 bộ công đoạn Ráp vai đơn áo polo, bấm xác nhận khi nhận.', -1, NULL, 'SENT'),
 ('CNMAY1',  'SYSTEM',    'Payslip',         N'Phiếu lương tháng 9', N'Phiếu lương tháng 9 đã có, bạn có thể xem chi tiết.', -1000, -990, 'SENT'),
 ('CNMAY9',  'PRODUCTION','DefectRecord',    N'Có phiếu lỗi ghi cho bạn', N'QA/QC ghi 2 sản phẩm lỗi đường may ở công đoạn Tra tay. Bạn có thể phản hồi trong 24 giờ.', -20, NULL, 'SENT'),
 ('CNMAY9',  'ANNOUNCEMENT', 'Announcement', N'Thông báo từ Quản đốc', N'KHẨN: Kiểm tra lại toàn bộ khóa kéo áo khoác gió trước khi đóng gói.', -5, -4, 'SENT'),
 ('ADMIN',   'SYSTEM',    'CustomerOrder',   N'Đơn hàng mới', N'Đơn DH-2511-008 (áo khoác gió, khách cấp NVL) vừa được tạo.', -26, -24, 'SENT'),
 ('ADMIN',   'SYSTEM',    'PieceRate',       N'Đơn giá chờ duyệt', N'Có 2 đơn giá công đoạn đang chờ Admin duyệt: Tra khóa kéo toàn thân, Tra bo cổ.', -40, NULL, 'SENT'),
 ('ADMIN',   'SYSTEM',    'Payroll',         N'Bảng lương tháng 10 chờ duyệt', N'Kế toán đã trình bảng lương tháng 10.', -300, NULL, 'SENT'),
 ('ADMIN',   'SYSTEM',    'Notification',    N'Gửi thông báo thất bại', N'Không gửi được thông báo đẩy tới 1 thiết bị (token hết hạn).', -48, -47, 'FAILED'),
 ('CODIEN',  'REQUEST',   'Request',         N'Đơn từ đã xử lý', N'Đơn xin bộ kim và thoi máy vắt sổ của bạn đã hoàn tất.', -72, -70, 'SENT'),
 ('CODIEN',  'REQUEST',   'Request',         N'Đơn từ bị từ chối', N'Đơn xin mua 2 máy may 1 kim điện tử bị từ chối: chưa có ngân sách quý này.', -200, -190, 'SKIPPED')) n (Code, Src, Ref, Title, Msg, AgoHrs, ReadHrs, Push)
JOIN dbo.Employee e ON e.EmployeeCode = n.Code;

/* ---------- Nhật ký thay đổi (dữ liệu nhạy cảm) ---------- */
INSERT dbo.AuditLog (TableName, RecordId, Action, OldValues, NewValues, Reason, Summary, ChangedBy, ChangedAt)
SELECT x.T, x.Id, x.Act, x.Ov, x.Nv, x.Rs, x.Sm, x.[By], x.At
FROM (
    -- đơn hàng: tạo; chuyển trạng thái; dừng
    SELECT 'CustomerOrder' AS T, co.OrderId AS Id, 'INSERT' AS Act, CAST(NULL AS NVARCHAR(MAX)) AS Ov,
           CONCAT('{"OrderCode":"', co.OrderCode, '","Status":"NEW"}') AS Nv, CAST(NULL AS NVARCHAR(500)) AS Rs, CAST(N'Tạo đơn hàng' AS NVARCHAR(500)) AS Sm,
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN') AS [By], co.CreatedAt AS At
    FROM dbo.CustomerOrder co
    UNION ALL
    SELECT 'CustomerOrder', co.OrderId, 'UPDATE', '{"Status":"NEW"}', CONCAT('{"Status":"', co.Status, '"}'), co.StopReason, N'Đổi trạng thái đơn hàng',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'), DATEADD(DAY, 6, co.CreatedAt)
    FROM dbo.CustomerOrder co WHERE co.Status NOT IN ('NEW')
    -- đơn mua
    UNION ALL
    SELECT 'PurchaseOrder', po.PurchaseOrderId, 'INSERT', NULL, CONCAT('{"PoCode":"', po.PoCode, '","GoodsStatus":"PENDING_FINALISATION"}'), NULL, N'Tạo đơn mua',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), po.CreatedAt
    FROM dbo.PurchaseOrder po
    UNION ALL
    SELECT 'PurchaseOrder', po.PurchaseOrderId, 'UPDATE', '{"GoodsStatus":"PENDING_FINALISATION"}', CONCAT('{"GoodsStatus":"ORDERED","SupplierId":', po.SupplierId, '}'), NULL, N'Chốt NCC và đơn giá',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), DATEADD(DAY, 1, po.CreatedAt)
    FROM dbo.PurchaseOrder po WHERE po.SupplierId IS NOT NULL
    UNION ALL
    SELECT 'PurchaseOrder', po.PurchaseOrderId, 'CONFIRM', '{"PaymentStatus":"UNPAID"}', CONCAT('{"PaymentStatus":"', po.PaymentStatus, '","PaidAmount":', CAST(po.PaidAmount AS VARCHAR(30)), '}'), NULL, N'Kế toán xác nhận thanh toán',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN'), CAST(po.PaymentDate AS DATETIME2)
    FROM dbo.PurchaseOrder po WHERE po.PaymentStatus IN ('PAID', 'PARTIALLY_PAID')
    UNION ALL
    SELECT 'PurchaseOrder', po.PurchaseOrderId, 'REJECT', '{"PaymentStatus":"UNPAID"}', '{"PaymentStatus":"RETURNED"}', po.PaymentReturnReason, N'Kế toán trả lại đơn mua',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN'), DATEADD(DAY, 5, po.CreatedAt)
    FROM dbo.PurchaseOrder po WHERE po.PaymentStatus = 'RETURNED'
    -- phiếu kho
    UNION ALL
    SELECT 'MaterialReceipt', mr.MaterialReceiptId, 'CONFIRM', '{"Status":"PENDING"}', '{"Status":"CONFIRMED"}', NULL, N'Quản đốc xác nhận phiếu nhập',
           mr.ConfirmedBy, mr.ConfirmedAt
    FROM dbo.MaterialReceipt mr WHERE mr.Status = 'CONFIRMED'
    UNION ALL
    SELECT 'MaterialReceipt', mr.MaterialReceiptId, 'REJECT', '{"Status":"PENDING"}', '{"Status":"RETURNED"}', mr.ReturnReason, N'Quản đốc trả lại phiếu nhập',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), DATEADD(HOUR, 17, CAST(mr.ReceiptDate AS DATETIME2))
    FROM dbo.MaterialReceipt mr WHERE mr.Status = 'RETURNED'
    UNION ALL
    SELECT 'MaterialIssue', mi.MaterialIssueId, 'CONFIRM', '{"Status":"PENDING_RECEIPT"}', '{"Status":"RECEIVED"}', NULL, N'Tổ xác nhận nhận cấp phát', mi.ReceivedBy, mi.ReceivedAt
    FROM dbo.MaterialIssue mi WHERE mi.Status = 'RECEIVED'
    UNION ALL
    SELECT 'MaterialIssue', mi.MaterialIssueId, 'REJECT', '{"Status":"PENDING_RECEIPT"}', '{"Status":"REJECTED"}', mi.RejectReason, N'Tổ từ chối nhận BTP', mi.IssuedBy, DATEADD(HOUR, 11, CAST(mi.IssueDate AS DATETIME2))
    FROM dbo.MaterialIssue mi WHERE mi.Status = 'REJECTED'
    UNION ALL
    SELECT 'CutHandover', h.CutHandoverId, 'CONFIRM', '{"Status":"PENDING"}', '{"Status":"CONFIRMED"}', NULL, N'Thủ kho xác nhận bàn giao BTP', h.ConfirmedBy, h.ConfirmedAt
    FROM dbo.CutHandover h WHERE h.Status = 'CONFIRMED'
    UNION ALL
    SELECT 'FgHandover', f.FgHandoverId, 'CONFIRM', '{"Status":"PENDING"}', '{"Status":"CONFIRMED"}', NULL, N'Thủ kho xác nhận nhận thành phẩm', f.ConfirmedBy, f.ConfirmedAt
    FROM dbo.FgHandover f WHERE f.Status = 'CONFIRMED'
    UNION ALL
    SELECT 'DispatchNote', n.DispatchNoteId, 'CONFIRM', '{"Status":"PENDING"}', CONCAT('{"Status":"', n.Status, '","DeliveryMethod":"', n.DeliveryMethod, '"}'), NULL, N'Thủ kho xuất kho giao hàng',
           n.DispatchedBy, n.DispatchedAt
    FROM dbo.DispatchNote n WHERE n.Status IN ('IN_DELIVERY', 'DELIVERED')
    UNION ALL
    SELECT 'DispatchNote', n.DispatchNoteId, 'REJECT', '{"Status":"PENDING"}', '{"Status":"REJECTED"}', n.RejectReason, N'Quản đốc từ chối phiếu xuất',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), DATEADD(HOUR, 10, CAST(n.DeliveryDate AS DATETIME2))
    FROM dbo.DispatchNote n WHERE n.Status = 'REJECTED'
    -- đơn từ
    UNION ALL
    SELECT 'Request', r.RequestId, IIF(r.Status = 'REJECTED', 'REJECT', 'APPROVE'), '{"Status":"PENDING_APPROVAL"}', CONCAT('{"Status":"', r.Status, '"}'), r.DecisionNote, N'Quản đốc duyệt đơn từ',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), r.DecidedAt
    FROM dbo.Request r WHERE r.DecidedAt IS NOT NULL
    -- đơn giá công đoạn
    UNION ALL
    SELECT 'PieceRate', pr.PieceRateId, IIF(pr.Status = 'REJECTED', 'REJECT', 'APPROVE'), '{"Status":"PENDING_APPROVAL"}', CONCAT('{"Status":"', pr.Status, '","Rate":', CAST(pr.Rate AS VARCHAR(30)), '}'),
           pr.DecisionNote, N'Admin duyệt đơn giá công đoạn', (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'), ISNULL(pr.ApprovedAt, DATEADD(DAY, 1, pr.SubmittedAt))
    FROM dbo.PieceRate pr WHERE pr.Status IN ('APPROVED', 'REJECTED') AND pr.SubmittedAt IS NOT NULL
    -- bảng lương
    UNION ALL
    SELECT 'Payroll', p.PayrollId, IIF(p.Status = 'REJECTED', 'REJECT', 'APPROVE'), '{"Status":"PENDING_APPROVAL"}', CONCAT('{"Status":"', p.Status, '"}'), p.RejectReason, N'Admin duyệt bảng lương',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'), ISNULL(p.ApprovedAt, DATEADD(DAY, 1, p.SubmittedAt))
    FROM dbo.Payroll p WHERE p.Status IN ('APPROVED', 'REJECTED')
    -- bậc thợ
    UNION ALL
    SELECT 'GradeEvaluation', g.GradeEvaluationId, 'APPROVE', '{"Status":"SUBMITTED"}', CONCAT('{"Status":"', g.Status, '","ProposedGradeCode":"', g.ProposedGradeCode, '"}'), g.DecisionNote, N'Quản đốc xem xét đề xuất bậc',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'), g.ConsideredAt
    FROM dbo.GradeEvaluation g WHERE g.ConsideredAt IS NOT NULL
    -- đăng nhập
    UNION ALL
    SELECT 'Account', a.AccountId, 'LOGIN', NULL, '{"Result":"SUCCESS"}', NULL, N'Đăng nhập', a.EmployeeId, DATEADD(MINUTE, -(a.AccountId * 37), @now)
    FROM dbo.Account a WHERE a.Status = 'ACTIVE' AND a.AccountId <= 14
    UNION ALL
    SELECT 'Account', a.AccountId, 'LOCK', '{"Status":"ACTIVE"}', '{"Status":"LOCKED"}', a.LockReason, N'Khóa tài khoản nhân viên nghỉ việc',
           (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'), a.LockedAt
    FROM dbo.Account a WHERE a.Status = 'LOCKED'
) x;

PRINT N'12: kiểm kê, ảnh, thông báo, nhật ký xong.';
