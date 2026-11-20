/* =====================================================================================
   10. ĐƠN TỪ (đủ 12 loại và các trạng thái), CHẤM BÙ, KPI TỔ MAY, THÔNG BÁO NHANH, THÔNG BÁO NGƯỜI DÙNG
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @ketoan INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @codien INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CODIEN'),
        @ttcat INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTCAT'),
        @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @ttmay2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY2'),
        @tpmay2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TPMAY2'),
        @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @may2 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY2'),
        @vp INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP'),
        @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2510-003'),
        @o4 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2510-004'),
        @o5 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2511-005'),
        @o6 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2511-006'),
        @o8 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2511-008');

/* ---------- Đơn từ ---------- */
IF OBJECT_ID('tempdb..#Rq') IS NOT NULL DROP TABLE #Rq;
CREATE TABLE #Rq (K VARCHAR(20) COLLATE DATABASE_DEFAULT PRIMARY KEY, Type VARCHAR(30) COLLATE DATABASE_DEFAULT, Status VARCHAR(20) COLLATE DATABASE_DEFAULT, OrderId INT NULL,
                  WorkOrderId INT NULL, TargetOff INT NULL, Title NVARCHAR(200) COLLATE DATABASE_DEFAULT, Content NVARCHAR(2000) COLLATE DATABASE_DEFAULT,
                  CreatedBy INT, TeamId INT NULL, CreatedOff INT, DecisionNote NVARCHAR(1000) COLLATE DATABASE_DEFAULT NULL, ResultNote NVARCHAR(1000) COLLATE DATABASE_DEFAULT NULL,
                  Shortage VARCHAR(20) COLLATE DATABASE_DEFAULT NULL, HRole VARCHAR(30) COLLATE DATABASE_DEFAULT NULL, HTeam INT NULL, RequestId INT NULL);
DECLARE @wo4 INT = (SELECT TOP 1 WorkOrderId FROM dbo.WorkOrder WHERE OrderId = @o4 AND Status = 'COMPLETED'),
        @wo5 INT = (SELECT TOP 1 WorkOrderId FROM dbo.WorkOrder WHERE OrderId = @o5 AND Status = 'CUTTING'),
        @wo3 INT = (SELECT TOP 1 WorkOrderId FROM dbo.WorkOrder WHERE OrderId = @o3 AND Status = 'COMPLETED');
INSERT #Rq (K, Type, Status, OrderId, WorkOrderId, TargetOff, Title, Content, CreatedBy, TeamId, CreatedOff, DecisionNote, ResultNote, Shortage, HRole, HTeam) VALUES
 -- Cơ điện
 ('SP1', 'SPARE_PARTS',      'PENDING_APPROVAL',   NULL, NULL, 5,  N'Dây curoa máy may số 7 và số 12',               N'Dây curoa mòn sắp đứt, xin mua 4 dây loại A38 thay thế.', @codien, @vp, -1, NULL, NULL, NULL, NULL, NULL),
 ('SP2', 'SPARE_PARTS',      'COMPLETED',          NULL, NULL, -8, N'Bộ kim và thoi cho máy vắt sổ',                  N'Máy vắt sổ tổ may 2 hay đứt chỉ, xin bộ kim + thoi.', @codien, @vp, -14, N'Đồng ý, mua tại Minh Phát', N'Đã mua và lắp ngày hôm qua, máy chạy ổn', NULL, NULL, NULL),
 ('NM1', 'NEW_MACHINE',      'REJECTED',           NULL, NULL, 20, N'Máy may 1 kim điện tử mới cho tổ may 3',         N'Tổ may 3 đang thiếu 2 máy, xin mua 2 máy 1 kim điện tử.', @codien, @vp, -9, N'Chưa có ngân sách quý này, xem xét lại quý sau', NULL, NULL, NULL, NULL),
 ('NM2', 'NEW_MACHINE',      'APPROVED',           NULL, NULL, 15, N'Máy thùa khuy đính nút',                         N'Máy hiện tại hỏng nặng, sửa tốn hơn mua mới.', @codien, @vp, -6, N'Duyệt mua 1 máy thùa khuy cũ đã qua kiểm', NULL, NULL, NULL, NULL),
 ('MS1', 'MACHINE_SUPPLIES', 'COMPLETED',          NULL, NULL, -5, N'Dầu máy và kim số 14',                           N'Dầu máy và hộp kim 14 cho cả xưởng.', @codien, @vp, -10, NULL, N'Đã cấp 20 chai dầu, 3 hộp kim', NULL, NULL, NULL),
 ('MS2', 'MACHINE_SUPPLIES', 'PENDING_PROCESSING', NULL, NULL, 3,  N'Bóng đèn bàn may tổ 1',                         N'6 bóng đèn LED bàn may bị hỏng.', @codien, @vp, 0, NULL, NULL, NULL, NULL, NULL),
 -- Chấm công bù
 ('AC1', 'ATTENDANCE_CORRECTION', 'APPROVED',      NULL, NULL, -6, N'Bù công quên quẹt ra',                           N'Tôi quên quẹt vân tay lúc tan ca, xin bù giờ ra 16:40.', @ttmay, @may1, -5, N'Đã xác minh với tổ phó', NULL, NULL, NULL, NULL),
 ('AC2', 'ATTENDANCE_CORRECTION', 'PENDING_APPROVAL', NULL, NULL, -2, N'Bù công máy lỗi vân tay',                    N'Vân tay bị xước, máy không nhận 2 sáng liền, xin bù giờ vào.', @tpmay2, @may2, -1, NULL, NULL, NULL, NULL, NULL),
 ('AC3', 'ATTENDANCE_CORRECTION', 'REJECTED',      NULL, NULL, -20, N'Bù công nghỉ không phép',                       N'Xin bù công ngày nghỉ hôm thứ Hai.', @ttcat, @cat, -18, N'Không có chứng từ, không đủ điều kiện bù', NULL, NULL, NULL, NULL),
 -- Xin NVL / BTP
 ('MR1', 'MATERIAL_REQUISITION', 'COMPLETED',      @o4, @wo4, NULL, N'Xin vải đợt 2 lệnh cắt áo sơ mi',                  N'Xin cấp thêm vải kate cho 2 bàn cuối của lệnh.', @ttcat, @cat, -22, NULL, N'Đã cấp theo phiếu PC', NULL, NULL, NULL),
 ('MR2', 'MATERIAL_REQUISITION', 'PENDING_PROCESSING', @o5, @wo5, NULL, N'Xin vải thun hồng cho bàn cắt tiếp',          N'Bàn cắt kế tiếp cần 120m vải thun hồng.', @ttcat, @cat, 0, NULL, NULL, NULL, NULL, NULL),
 ('MR3', 'MATERIAL_REQUISITION', 'CANCELLED',      @o5, @wo5, NULL, N'Xin vải (nhập nhầm đơn)',                          N'Nhập nhầm đơn hàng, hủy.', @ttcat, @cat, -3, NULL, NULL, NULL, NULL, NULL),
 ('SR1', 'SEMI_FINISHED_REQUISITION', 'COMPLETED', @o4, NULL, NULL, N'Xin BTP áo sơ mi lần 2 cho tổ may 1',             N'Tổ may 1 hết BTP, xin cấp tiếp phần còn lại của đơn.', @ttmay, @may1, -20, NULL, N'Đã cấp BTP', NULL, NULL, NULL),
 ('SR2', 'SEMI_FINISHED_REQUISITION', 'PENDING_PROCESSING', @o5, NULL, NULL, N'Xin BTP áo polo hồng lần 2',                N'Xin cấp đợt 2 BTP áo polo hồng cho tổ may 2.', @ttmay2, @may2, 0, NULL, NULL, NULL, NULL, NULL),
 -- Cắt bù, vải lỗi, giao thiếu, bổ sung
 ('RC1', 'REPLACEMENT_CUTTING', 'PENDING_APPROVAL', @o3, @wo3, NULL, N'Cắt bù 12 thân sau áo khoác đen',               N'12 thân sau bị rách do dao cắt, xin cắt bù.', @ttcat, @cat, -1, NULL, NULL, NULL, NULL, NULL),
 ('RC2', 'REPLACEMENT_CUTTING', 'REJECTED',        @o3, @wo3, NULL, N'Cắt bù tay áo khoác',                            N'5 tay áo lệch canh sợi, xin cắt bù.', @ttcat, @cat, -12, N'Số lượng ít, dùng vải thừa của bàn cuối, không cần lập lệnh bù', NULL, NULL, NULL, NULL),
 ('RC3', 'REPLACEMENT_CUTTING', 'APPROVED',        @o3, @wo3, NULL, N'Cắt bù 8 thân trước áo khoác',                  N'8 thân trước dính dầu máy không giặt được, xin cắt bù.', @ttcat, @cat, -6, N'Duyệt, chờ Quản đốc lập lệnh cắt bù', NULL, NULL, NULL, NULL),
 ('RC4', 'REPLACEMENT_CUTTING', 'APPROVED',        @o3, @wo3, NULL, N'Cắt bù 6 tay áo khoác',                         N'6 tay áo bị lệch size, xin cắt bù.', @ttcat, @cat, -8, N'Duyệt, đã lập lệnh cắt bù', NULL, NULL, NULL, NULL),
 ('DM1', 'DEFECTIVE_MATERIAL',  'PENDING_APPROVAL', @o5, @wo5, NULL, N'Cuộn vải thun hồng bị loang màu',                N'Cuộn số 5 loang màu ở giữa, khoảng 18m không dùng được.', @ttcat, @cat, 0, NULL, NULL, NULL, NULL, NULL),
 ('DM2', 'DEFECTIVE_MATERIAL',  'APPROVED',        @o3, @wo3, NULL, N'Vải lót taffeta bị thủng',                         N'Phát hiện 25m vải lót thủng lỗ, xin đổi.', @ttcat, @cat, -28, N'Duyệt: đổi 25m, kho đủ tồn', NULL, NULL, NULL, NULL),
 ('DM3', 'DEFECTIVE_MATERIAL',  'NOT_COMPLETED',   @o6, NULL, NULL, N'Vải kate lệch tông',                              N'Cuộn vải giao lần 1 lệch tông so với mẫu.', @ttcat, @cat, -4, N'Duyệt mua bù', N'NCC không còn vải cùng lô, đã báo khách đổi lô', NULL, NULL, NULL),
 ('SD1', 'SHORT_DELIVERY',      'APPROVED',        @o6, NULL, NULL, N'NCC giao thiếu vải kate lần 1',                    N'Giao 680m thay vì 760m theo phiếu.', @thukho, @vp, -5, N'Yêu cầu NCC bù trong lần giao thứ 2', NULL, 'REPLENISH', NULL, NULL),
 ('SD2', 'SHORT_DELIVERY',      'COMPLETED',       (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DH-2510-002'), NULL, NULL, N'NCC giao thiếu vải kaki',   N'Thiếu 1% vải kaki so với đơn mua.', @thukho, @vp, -50, N'Không bù, chốt theo số đã nhận', N'Đã chốt đơn mua không bù', 'NO_REPLENISH', NULL, NULL),
 ('MP1', 'MATERIAL_PURCHASE',   'PENDING_PROCESSING', @o5, NULL, NULL, N'Mua bù vải thun hồng (vải lỗi không dùng được)', N'Hệ thống tạo: đơn NVL lỗi được duyệt nhưng kho không đủ tồn, Quản đốc lập đơn mua.', @ketoan, @vp, 0, NULL, NULL, NULL, NULL, NULL),
 ('MSU1', 'MATERIAL_SUPPLEMENT', 'PENDING_PROCESSING', @o4, NULL, NULL, N'Bổ sung 200 nút áo cho đơn sơ mi',              N'Đơn thiếu nút do hao hụt khi đính, xin cấp bổ sung.', @ttmay, @may1, -1, NULL, NULL, NULL, NULL, NULL),
 -- Đơn khác: gửi đúng người xử lý
 ('OT1', 'OTHER',  'PENDING_PROCESSING', NULL, NULL, 2,  N'Xin xe chở thùng carton',            N'Cần xe chở 100 thùng carton từ Đại Phát về xưởng.', @ttmay, @may1, -1, NULL, NULL, NULL, 'WAREHOUSE_KEEPER', NULL),
 ('OT2', 'OTHER',  'COMPLETED',          NULL, NULL, -10, N'Xác nhận bảng lương thử việc',     N'Xin xác nhận mức lương thử việc cho 2 công nhân mới.', @ttmay2, @may2, -15, NULL, N'Đã xác nhận và gửi lại tổ trưởng', NULL, 'ACCOUNTANT', NULL),
 ('OT3', 'OTHER',  'NOT_COMPLETED',      NULL, NULL, -7, N'Xin tăng ca ngày Chủ nhật',          N'Tổ cắt xin làm Chủ nhật tuần sau để kịp lô hàng.', @ttcat, @cat, -9, NULL, N'Chưa đủ nguyên liệu để cắt thêm nên không tăng ca', NULL, 'MANAGER', NULL),
 ('OT4', 'OTHER',  'PENDING_PROCESSING', NULL, NULL, 4,  N'Đề xuất khen thưởng tổ may 2',         N'Tổ may 2 hoàn thành đơn quần kaki trước hạn, đề nghị khen thưởng.', @ttmay2, @may2, -2, NULL, NULL, NULL, 'ADMIN', NULL),
 ('OT5', 'OTHER',  'PENDING_PROCESSING', NULL, NULL, 3,  N'Báo máy hút bụi tổ cắt hỏng',           N'Nhờ tổ trưởng tổ cắt kiểm tra máy hút bụi khu bàn cắt 2.', @ttmay, @may1, -1, NULL, NULL, NULL, 'TEAM_LEADER', @cat),
 ('OT6', 'OTHER',  'CANCELLED',          NULL, NULL, NULL, N'Xin ghế mới cho tổ may 1',         N'Xin 4 ghế mới, sau đó tự xử lý được nên hủy.', @ttmay, @may1, -11, NULL, NULL, NULL, 'WAREHOUSE_KEEPER', NULL);

INSERT dbo.Request (RequestCode, RequestTypeCode, Status, OrderId, WorkOrderId, TargetDate, Title, Content, CreatedBy, CreatedByTeamId, DecidedAt, DecisionNote,
                    ResultNote, CompletedAt, ShortageResolution, CreatedAt, UpdatedAt, HandlerRoleCode, HandlerTeamId)
SELECT CONCAT('DT-', FORMAT(DATEADD(DAY, r.CreatedOff, @today), 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY DATEADD(DAY, r.CreatedOff, @today) ORDER BY r.K)), 3)),
       r.Type, r.Status, r.OrderId, r.WorkOrderId, DATEADD(DAY, r.TargetOff, @today), r.Title, r.Content, r.CreatedBy, r.TeamId,
       CASE WHEN r.Status IN ('APPROVED', 'REJECTED', 'COMPLETED', 'NOT_COMPLETED') AND r.DecisionNote IS NOT NULL THEN DATEADD(HOUR, 10, DATEADD(DAY, r.CreatedOff + 1, CAST(@today AS DATETIME2))) END,
       r.DecisionNote, r.ResultNote,
       CASE WHEN r.Status IN ('COMPLETED', 'NOT_COMPLETED') THEN DATEADD(HOUR, 15, DATEADD(DAY, r.CreatedOff + 2, CAST(@today AS DATETIME2))) END,
       r.Shortage, DATEADD(HOUR, 9, DATEADD(DAY, r.CreatedOff, CAST(@today AS DATETIME2))), DATEADD(HOUR, 15, DATEADD(DAY, r.CreatedOff + 2, CAST(@today AS DATETIME2))),
       r.HRole, r.HTeam
FROM #Rq r;
UPDATE r SET RequestId = q.RequestId FROM #Rq r JOIN dbo.Request q ON q.Title = r.Title COLLATE DATABASE_DEFAULT;

-- Dòng chi tiết đơn từ
INSERT dbo.RequestLine (RequestId, MaterialId, OrderLineId, ItemDescription, Quantity, ReplenishQty)
SELECT r.RequestId, m.MaterialId, NULL, NULL, v.Qty, NULL
FROM (VALUES ('MR1', 'V-KATE-TR', 180), ('MR1', 'V-KATE-XN', 120), ('MR2', 'V-POLO-HG', 120), ('MR2', 'V-POLO-TR', 25), ('MR3', 'V-POLO-HG', 60),
             ('DM1', 'V-POLO-HG', 18), ('DM2', 'V-LOT-DEN', 25), ('DM3', 'V-KATE-TR', 40), ('MP1', 'V-POLO-HG', 18), ('MSU1', 'T-NUT-4L', 200)) v (K, Mat, Qty)
JOIN #Rq r ON r.K = v.K JOIN dbo.Material m ON m.MaterialName = v.Mat;
INSERT dbo.RequestLine (RequestId, OrderLineId, Quantity)
SELECT r.RequestId, MIN(ol.OrderLineId), v.Qty
FROM (VALUES ('SR1', 120), ('SR2', 60)) v (K, Qty)
JOIN #Rq r ON r.K = v.K JOIN dbo.OrderLine ol ON ol.OrderId = r.OrderId GROUP BY r.RequestId, v.Qty;
INSERT dbo.RequestLine (RequestId, ItemDescription, Quantity)
SELECT r.RequestId, v.Item, v.Qty
FROM (VALUES ('SP1', N'Dây curoa A38', 4), ('SP2', N'Bộ kim vắt sổ 4 chỉ', 2), ('SP2', N'Thoi vắt sổ', 2), ('NM1', N'Máy may 1 kim điện tử', 2), ('NM2', N'Máy thùa khuy đính nút', 1),
             ('MS1', N'Dầu máy may (chai 500ml)', 20), ('MS1', N'Kim máy số 14 (hộp 100 cây)', 3), ('MS2', N'Bóng đèn LED bàn may', 6), ('OT1', N'Thùng carton 5 lớp', 100)) v (K, Item, Qty)
JOIN #Rq r ON r.K = v.K;
-- Đơn giao thiếu: dòng đơn mua + phần bù
INSERT dbo.RequestLine (RequestId, MaterialId, PurchaseOrderLineId, Quantity, ReplenishQty)
SELECT r.RequestId, (SELECT om.MaterialId FROM dbo.OrderMaterial om WHERE om.OrderMaterialId = pol.OrderMaterialId), pol.PurchaseOrderLineId, 80, 80
FROM #Rq r JOIN dbo.PurchaseOrder po ON po.OrderId = r.OrderId AND po.GoodsStatus = 'PARTIALLY_RECEIVED'
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
WHERE r.K = 'SD1' AND pol.PurchaseOrderLineId = (SELECT MIN(PurchaseOrderLineId) FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = po.PurchaseOrderId);
INSERT dbo.RequestLine (RequestId, MaterialId, PurchaseOrderLineId, Quantity, ReplenishQty)
SELECT r.RequestId, (SELECT om.MaterialId FROM dbo.OrderMaterial om WHERE om.OrderMaterialId = pol.OrderMaterialId), pol.PurchaseOrderLineId, 8, 0
FROM #Rq r JOIN dbo.PurchaseOrder po ON po.OrderId = r.OrderId AND po.GoodsStatus = 'CLOSED_SHORT'
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
WHERE r.K = 'SD2' AND pol.PurchaseOrderLineId = (SELECT MIN(PurchaseOrderLineId) FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = po.PurchaseOrderId);

-- Liên kết phiếu cấp với đơn xin NVL / BTP đã hoàn tất
UPDATE mi SET RequestId = r.RequestId
FROM dbo.MaterialIssue mi JOIN #Rq r ON r.K = 'MR1'
WHERE mi.MaterialIssueId = (SELECT TOP 1 MaterialIssueId FROM dbo.MaterialIssue WHERE WorkOrderId = @wo4 AND WarehouseType = 'MATERIAL' ORDER BY IssueDate DESC);
UPDATE r SET MaterialIssueId = mi.MaterialIssueId FROM dbo.Request r JOIN dbo.MaterialIssue mi ON mi.RequestId = r.RequestId;
UPDATE mi SET RequestId = r.RequestId
FROM dbo.MaterialIssue mi JOIN #Rq r ON r.K = 'SR1'
WHERE mi.MaterialIssueId = (SELECT TOP 1 MaterialIssueId FROM dbo.MaterialIssue WHERE OrderId = @o4 AND WarehouseType = 'SEMI_FINISHED' AND Status = 'RECEIVED' ORDER BY IssueDate DESC);
UPDATE r SET MaterialIssueId = mi.MaterialIssueId FROM dbo.Request r JOIN dbo.MaterialIssue mi ON mi.RequestId = r.RequestId;

/* ---------- Cắt bù: gắn phiếu hàng hỏng, dòng chi tiết cần cắt bù, lệnh cắt bù ---------- */
-- Mỗi đơn cắt bù gắn một phiếu hàng hỏng loại Hủy (đã xác nhận) của đơn DH-2510-003; tổ trưởng tổ may của phiếu lập đơn;
-- dòng cắt bù = một chi tiết PANEL (định mức) của đúng màu áo của phiếu, số lượng không vượt số hàng đã hủy.
IF OBJECT_ID('tempdb..#Rc') IS NOT NULL DROP TABLE #Rc;
SELECT d.DefectRecordId, d.OrderLineId, d.Quantity, d.SewingTeamId, ol.ProductId, pv.ProductColorId, v.K, v.I
INTO #Rc
FROM (VALUES ('RC1', 12, 1), ('RC2', 5, 2), ('RC3', 8, 3), ('RC4', 6, 4)) v (K, Qty, I)
JOIN dbo.DefectRecord d ON d.Quantity = v.Qty AND d.DefectType = 'SCRAP' AND d.Status = 'CONFIRMED' AND d.ReasonNote = N'Hàng hủy, cần cắt bù'
JOIN dbo.OrderLine ol ON ol.OrderLineId = d.OrderLineId AND ol.OrderId = @o3
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId;

UPDATE r SET DefectRecordId = c.DefectRecordId, CreatedBy = lead.EmployeeId, CreatedByTeamId = c.SewingTeamId
FROM dbo.Request r JOIN #Rq q ON q.RequestId = r.RequestId JOIN #Rc c ON c.K = q.K
CROSS APPLY (SELECT TOP 1 e.EmployeeId FROM dbo.Employee e JOIN dbo.Role ro ON ro.RoleId = e.RoleId
             WHERE e.TeamId = c.SewingTeamId AND ro.RoleCode = 'TEAM_LEADER' AND e.Status = 'ACTIVE' ORDER BY e.EmployeeId) lead;

INSERT dbo.RequestLine (RequestId, OrderLineId, ProductComponentMaterialId, Quantity, ItemDescription)
SELECT q.RequestId, c.OrderLineId, n.ProductComponentMaterialId, c.Quantity, CONCAT(n.ComponentName, N' (', n.MaterialName, N')')
FROM #Rc c JOIN #Rq q ON q.K = c.K
CROSS APPLY (SELECT x.ProductComponentMaterialId, x.ComponentName, x.MaterialName
             FROM (SELECT pcm.ProductComponentMaterialId, pc.ComponentName, m.MaterialName,
                          ROW_NUMBER() OVER (ORDER BY pcm.ProductComponentMaterialId) AS Rk, COUNT(*) OVER () AS Cnt
                   FROM dbo.ProductComponentMaterial pcm
                   JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pcm.ProductComponentId AND pc.ComponentType = 'PANEL' AND pc.Status = 'ACTIVE'
                   JOIN dbo.Material m ON m.MaterialId = pcm.MaterialId
                   WHERE pcm.ProductId = c.ProductId AND pcm.ProductColorId = c.ProductColorId AND pcm.Status = 'ACTIVE') x
             WHERE x.Rk = (c.I - 1) % x.Cnt + 1) n;

-- Lệnh cắt bù đã lập cho đơn RC4 (RC3 đã duyệt, chờ Quản đốc lập lệnh)
INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt, SourceRequestId, FabricTolerancePercent)
SELECT CONCAT('LC-', FORMAT(DATEADD(DAY, -6, @today), 'yyyyMMdd'), '-901'), @o3, c.ProductId, @cat, DATEADD(DAY, 3, @today), 'OPEN',
       N'Lệnh cắt bù 6 tay áo khoác theo đơn cắt bù', DATEADD(HOUR, 9, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), q.RequestId, 0
FROM #Rc c JOIN #Rq q ON q.K = c.K WHERE c.K = 'RC4';
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty, ProductComponentMaterialId)
SELECT wo.WorkOrderId, rl.OrderLineId, rl.Quantity, rl.ProductComponentMaterialId
FROM dbo.WorkOrder wo JOIN dbo.RequestLine rl ON rl.RequestId = wo.SourceRequestId;

/* ---------- Giao thiếu: phiếu nhập gốc, và phiếu nhập bổ sung của đơn bù ---------- */
UPDATE rl SET MaterialReceiptLineId = mrl.MaterialReceiptLineId
FROM dbo.RequestLine rl JOIN dbo.Request r ON r.RequestId = rl.RequestId AND r.RequestTypeCode = 'SHORT_DELIVERY'
CROSS APPLY (SELECT TOP 1 l.MaterialReceiptLineId FROM dbo.MaterialReceiptLine l
             JOIN dbo.MaterialReceipt m ON m.MaterialReceiptId = l.MaterialReceiptId AND m.ShortageRequestId IS NULL AND m.Status = 'CONFIRMED'
             WHERE l.PurchaseOrderLineId = rl.PurchaseOrderLineId ORDER BY l.MaterialReceiptLineId DESC) mrl;
UPDATE r SET MaterialReceiptId = l.MaterialReceiptId
FROM dbo.Request r JOIN dbo.RequestLine rl ON rl.RequestId = r.RequestId JOIN dbo.MaterialReceiptLine l ON l.MaterialReceiptLineId = rl.MaterialReceiptLineId
WHERE r.RequestTypeCode = 'SHORT_DELIVERY';

-- SD1 (NCC bù trong lần giao sau): thủ kho đã lập phiếu nhập bổ sung, chờ Quản đốc xác nhận
INSERT dbo.MaterialReceipt (ReceiptCode, ShortageRequestId, OrderId, PurchaseOrderId, ReceiptDate, Status, Note, SubmittedAt)
SELECT CONCAT('PN-', FORMAT(@today, 'yyyyMMdd'), '-902'), q.RequestId, po.OrderId, po.PurchaseOrderId, @today, 'PENDING',
       N'NCC giao bù phần vải kate thiếu của lần giao 1', @now
FROM #Rq q JOIN dbo.RequestLine rl ON rl.RequestId = q.RequestId
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = rl.PurchaseOrderLineId JOIN dbo.PurchaseOrder po ON po.PurchaseOrderId = pol.PurchaseOrderId
WHERE q.K = 'SD1';
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, OriginalReceiptLineId, AcceptedQty)
SELECT mr.MaterialReceiptId, pol.OrderMaterialId, pol.PurchaseOrderLineId, rl.MaterialReceiptLineId, 80
FROM #Rq q JOIN dbo.MaterialReceipt mr ON mr.ShortageRequestId = q.RequestId
JOIN dbo.RequestLine rl ON rl.RequestId = q.RequestId JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = rl.PurchaseOrderLineId
WHERE q.K = 'SD1';

/* ---------- Chấm bù qua thủ tục (APPROVED_REQUEST gắn đơn AC1) ---------- */
DECLARE @acReq INT = (SELECT RequestId FROM #Rq WHERE K = 'AC1');
DECLARE @wTTMAY INT = @ttmay, @wCNMAY3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @wCNMAY7 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY7');
DECLARE @d1 DATE = DATEADD(DAY, -6, @today), @d2 DATE = DATEADD(DAY, -4, @today), @d3 DATE = DATEADD(DAY, -9, @today);
-- bảo đảm là ngày làm việc (không phải Chủ nhật)
IF DATEDIFF(DAY, '2000-01-02', @d1) % 7 = 0 SET @d1 = DATEADD(DAY, -1, @d1);
IF DATEDIFF(DAY, '2000-01-02', @d2) % 7 = 0 SET @d2 = DATEADD(DAY, -1, @d2);
IF DATEDIFF(DAY, '2000-01-02', @d3) % 7 = 0 SET @d3 = DATEADD(DAY, -1, @d3);
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @wTTMAY, @WorkDate = @d1, @AdjustmentType = 'APPROVED_REQUEST', @Reason = N'Quên quẹt vân tay lúc tan ca, đơn bù đã duyệt',
                                        @RequestId = @acReq, @CheckIn = '07:25', @CheckOut = '16:40', @ChangedBy = @ketoan;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @wCNMAY3, @WorkDate = @d2, @AdjustmentType = 'FORGOT_PUNCH', @Reason = N'Quên quẹt giờ ra, Kế toán chấm hộ theo xác nhận của tổ trưởng',
                                        @RequestId = NULL, @CheckIn = '07:28', @CheckOut = '16:35', @ChangedBy = @ketoan;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @wCNMAY7, @WorkDate = @d2, @AdjustmentType = 'FINGERPRINT_FAIL', @Reason = N'Vân tay không nhận cả ngày, tổ trưởng xác nhận có đi làm',
                                        @RequestId = NULL, @CheckIn = '07:30', @CheckOut = '16:30', @ChangedBy = @ketoan;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @ttcat, @WorkDate = @d3, @AdjustmentType = 'OFFSITE_WORK', @Reason = N'Đi mua vật tư ở Quận 5, làm việc ngoài xưởng',
                                        @RequestId = NULL, @CheckIn = '08:00', @CheckOut = '16:30', @ChangedBy = @ketoan;

/* ---------- KPI tổ may ---------- */
IF OBJECT_ID('tempdb..#K') IS NOT NULL DROP TABLE #K;
CREATE TABLE #K (OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, TeamCode VARCHAR(20) COLLATE DATABASE_DEFAULT, Status VARCHAR(20) COLLATE DATABASE_DEFAULT, Result VARCHAR(20) COLLATE DATABASE_DEFAULT NULL,
                 StartOff INT, EndOff INT, KpiId INT NULL);
INSERT #K (OrderCode, TeamCode, Status, Result, StartOff, EndOff) VALUES
 ('DH-2509-001', 'MAY1', 'CLOSED', 'ACHIEVED',     -65, -48),
 ('DH-2509-001', 'MAY2', 'CLOSED', 'ACHIEVED',     -65, -46),
 ('DH-2510-002', 'MAY2', 'CLOSED', 'ACHIEVED',     -47, -30),
 ('DH-2510-003', 'MAY1', 'CLOSED', 'NOT_ACHIEVED', -29, -12),
 ('DH-2510-003', 'MAY2', 'CLOSED', 'ACHIEVED',     -29, -12),
 ('DH-2510-004', 'MAY1', 'CLOSED', 'ACHIEVED',     -19, -7),
 ('DH-2511-005', 'MAY2', 'ACTIVE', NULL,           -10, 14),
 ('DH-2511-006', 'MAY1', 'ACTIVE', NULL,           -1,  18),
 ('DH-2510-009', 'MAY3', 'CANCELLED', NULL,        -15, -2);
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result, CreatedAt)
SELECT co.OrderId, p.ProductId, t.TeamId, DATEADD(DAY, k.StartOff, @today), DATEADD(DAY, k.EndOff, @today),
       (SELECT SUM(ol.OrderedQty) FROM dbo.OrderLine ol JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = t.TeamId WHERE ol.OrderId = co.OrderId),
       (SELECT COUNT(*) FROM (SELECT TOP 120 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n
        WHERE DATEADD(DAY, n.i, DATEADD(DAY, k.StartOff, @today)) <= DATEADD(DAY, k.EndOff, @today)
          AND DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, n.i, DATEADD(DAY, k.StartOff, @today))) % 7 <> 0),
       k.Status, k.Result, DATEADD(DAY, k.StartOff - 2, CAST(@today AS DATETIME2))
FROM #K k JOIN dbo.CustomerOrder co ON co.OrderCode = k.OrderCode JOIN #Ord o ON o.OrderCode = k.OrderCode JOIN dbo.Product p ON p.ProductCode = o.Prod
JOIN dbo.Team t ON t.TeamCode = k.TeamCode;
UPDATE k SET KpiId = kt.KpiTargetId FROM #K k JOIN dbo.CustomerOrder co ON co.OrderCode = k.OrderCode JOIN dbo.Team t ON t.TeamCode = k.TeamCode
JOIN dbo.KpiTarget kt ON kt.OrderId = co.OrderId AND kt.SewingTeamId = t.TeamId;

-- Chỉ tiêu theo dòng đơn của tổ và theo ngày làm việc trong kỳ (tổng khớp chỉ tiêu)
INSERT dbo.KpiTargetLine (KpiTargetId, OrderLineId, TargetQty)
SELECT k.KpiId, ol.OrderLineId, ol.OrderedQty
FROM #K k JOIN dbo.CustomerOrder co ON co.OrderCode = k.OrderCode JOIN dbo.Team t ON t.TeamCode = k.TeamCode
JOIN dbo.OrderLine ol ON ol.OrderId = co.OrderId JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = t.TeamId;
IF OBJECT_ID('tempdb..#Kd') IS NOT NULL DROP TABLE #Kd;
SELECT kt.KpiTargetId, kt.TargetFinishedQty, kt.WorkingDays, f.D,
       ROW_NUMBER() OVER (PARTITION BY kt.KpiTargetId ORDER BY f.D) AS i
INTO #Kd
FROM dbo.KpiTarget kt
CROSS APPLY (SELECT DATEADD(DAY, n.i, kt.StartDate) AS D FROM (SELECT TOP 80 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n
             WHERE DATEADD(DAY, n.i, kt.StartDate) <= kt.EndDate AND DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, n.i, kt.StartDate)) % 7 <> 0) f;
INSERT dbo.KpiDailyTarget (KpiTargetId, WorkDate, TargetQty)
SELECT KpiTargetId, D, (TargetFinishedQty * i) / WorkingDays - (TargetFinishedQty * (i - 1)) / WorkingDays
FROM #Kd WHERE i <= WorkingDays AND (TargetFinishedQty * i) / WorkingDays - (TargetFinishedQty * (i - 1)) / WorkingDays > 0;
/* ---------- Thông báo nhanh ---------- */
INSERT dbo.Announcement (Content, Priority, TargetScope, SenderId, SentAt) VALUES
 (N'Từ thứ Hai tuần sau xưởng làm việc theo khung giờ mới: vào 07:30, nghỉ trưa 11:30-12:30, tan 16:30.', 'NORMAL', 'ALL', @quandoc, DATEADD(DAY, -3, @now)),
 (N'KHẨN: Kiểm tra lại toàn bộ khóa kéo áo khoác gió trước khi đóng gói, khách phản ánh lỗi khóa lô trước.', 'URGENT', 'TEAM', @quandoc, DATEADD(HOUR, -5, @now)),
 (N'Thủ kho chú ý: sáng mai nhà cung cấp phụ liệu giao hàng lúc 08:00, chuẩn bị chỗ xếp hàng.', 'NORMAL', 'ROLE', @quandoc, DATEADD(HOUR, -2, @now)),
 (N'Chúc mừng tổ may 2 hoàn thành đơn quần kaki trước hạn 2 ngày!', 'NORMAL', 'ALL', @admin, DATEADD(DAY, -10, @now));
INSERT dbo.AnnouncementTarget (AnnouncementId, TeamId, RoleId)
SELECT a.AnnouncementId, t.TeamId, NULL FROM dbo.Announcement a CROSS JOIN dbo.Team t WHERE a.TargetScope = 'TEAM' AND t.TeamCode IN ('MAY1', 'MAY2', 'MAY3');
INSERT dbo.AnnouncementTarget (AnnouncementId, TeamId, RoleId)
SELECT a.AnnouncementId, NULL, r.RoleId FROM dbo.Announcement a CROSS JOIN dbo.Role r WHERE a.TargetScope = 'ROLE' AND r.RoleCode = 'WAREHOUSE_KEEPER';

PRINT N'10: đơn từ, chấm bù, KPI, thông báo nhanh xong.';
