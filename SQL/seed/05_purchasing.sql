/* =====================================================================================
   05. MUA HÀNG VÀ NHẬP KHO NVL
   Đơn mua ở đủ trạng thái hàng (PENDING_FINALISATION, ORDERED, PARTIALLY_RECEIVED, RECEIVED, CLOSED_SHORT, REJECTED, CUSTOMER_SUPPLIED)
   và thanh toán (UNPAID, RETURNED, PARTIALLY_PAID, PAID) - thanh toán nhiều lần, có lần đã sửa trong 24 giờ; đơn mua dùng chung.
   Phiếu nhập: CONFIRMED / PENDING / RETURNED, NVL khách cấp; số tồn NVL ghi qua usp_Stock_Post.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @thukho  INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO');

/* ---------- Đơn giá mua theo NVL ---------- */
IF OBJECT_ID('tempdb..#Price') IS NOT NULL DROP TABLE #Price;
CREATE TABLE #Price (Mat VARCHAR(30) COLLATE DATABASE_DEFAULT PRIMARY KEY, Price DECIMAL(18,2));
INSERT #Price VALUES
 ('V-KATE-TR', 56000), ('V-KATE-XN', 58000), ('V-KAKI-BE', 69000), ('V-KAKI-DEN', 69000), ('V-GIO-DEN', 52000), ('V-GIO-NAVY', 54000),
 ('V-LOT-DEN', 24000), ('V-POLO-HG', 78000), ('V-POLO-TR', 76000),
 ('T-CHI-TR', 14000), ('T-CHI-XN', 14000), ('T-CHI-BE', 14000), ('T-CHI-DEN', 14000), ('T-CHI-NAVY', 14500), ('T-CHI-HG', 14000),
 ('T-NUT-4L', 180), ('T-NUT-KAKI', 1500), ('T-KHOA-18', 2200), ('T-KHOA-60D', 5800), ('T-KHOA-60N', 6200), ('T-NHAN-DET', 450), ('T-KIM-14', 38000);

/* ---------- Kế hoạch đơn mua ----------
   K | Đơn | NCC | Nhóm NVL (FAB vải chính / LIN vải lót / TRM phụ liệu) | Đặt cách hôm nay | Hàng | Thanh toán | Số lần giao dự kiến
   | % giao lần 1 | % giao lần 2 | Ghi chú */
IF OBJECT_ID('tempdb..#Po') IS NOT NULL DROP TABLE #Po;
CREATE TABLE #Po (K VARCHAR(20) COLLATE DATABASE_DEFAULT PRIMARY KEY, OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, Supplier NVARCHAR(200) COLLATE DATABASE_DEFAULT,
                  Grp VARCHAR(10) COLLATE DATABASE_DEFAULT, OrdOff INT, Goods VARCHAR(25) COLLATE DATABASE_DEFAULT, Pay VARCHAR(20) COLLATE DATABASE_DEFAULT,
                  ExpCnt SMALLINT, D1 DECIMAL(4,2), D2 DECIMAL(4,2), Note NVARCHAR(500) COLLATE DATABASE_DEFAULT, PoCode VARCHAR(30) COLLATE DATABASE_DEFAULT NULL);
INSERT #Po (K, OrderCode, Supplier, Grp, OrdOff, Goods, Pay, ExpCnt, D1, D2, Note) VALUES
 ('O1-FAB', 'DH-2509-001', N'Công ty Vải Sợi Thành Công', 'FAB', -76, 'RECEIVED',           'PAID',          2, .55, .45, N'Vải kate cho đơn áo sơ mi'),
 ('O1-TRM', 'DH-2509-001', N'Phụ liệu may Hoàng Gia',     'TRM', -76, 'RECEIVED',           'PAID',          1, 1,   0,   N'Chỉ, nút cho đơn áo sơ mi (nhãn khách cấp)'),
 ('O2-FAB', 'DH-2510-002', N'Vải Phương Nam',             'FAB', -56, 'CLOSED_SHORT',       'PAID',          2, .62, .37, N'NCC giao thiếu 1%, không bù, chốt theo số đã nhận'),
 ('O2-TRM', 'DH-2510-002', N'Phụ liệu may Hoàng Gia',     'TRM', -56, 'RECEIVED',           'PAID',          1, 1,   0,   N'Khóa, nút, chỉ, nhãn quần kaki'),
 ('O3-FAB', 'DH-2510-003', N'Công ty Vải Sợi Thành Công', 'FAB', -39, 'RECEIVED',           'PAID',          2, .70, .30, N'Vải dù gió lớp ngoài'),
 ('O3-LIN', 'DH-2510-003', N'Công ty Vải Sợi Thành Công', 'LIN', -39, 'RECEIVED',           'PARTIALLY_PAID', 1, 1,  0,   N'Vải lót taffeta, trả trước một phần'),
 ('O3-TRM', 'DH-2510-003', N'Phụ liệu may Hoàng Gia',     'TRM', -38, 'RECEIVED',           'UNPAID',        1, 1,   0,   N'Khóa kéo, chỉ (khóa khách cấp 40%), chờ Kế toán thanh toán'),
 ('O4-FAB', 'DH-2510-004', N'Công ty Vải Sợi Thành Công', 'FAB', -27, 'RECEIVED',           'PAID',          1, 1,   0,   N'Vải kate'),
 ('O4-TRM', 'DH-2510-004', N'Phụ liệu may Hoàng Gia',     'TRM', -27, 'RECEIVED',           'RETURNED',      1, 1,   0,   N'Chỉ, nút (nhãn khách cấp)'),
 ('O5-FAB', 'DH-2511-005', N'Công ty Vải Sợi Thành Công', 'FAB', -18, 'PARTIALLY_RECEIVED', 'UNPAID',        2, .85, 0,   N'Vải thun cá sấu hồng + trắng, giao lần 2 còn chờ'),
 ('O5-TRM', 'DH-2511-005', N'Phụ liệu may Hoàng Gia',     'TRM', -18, 'ORDERED',            'UNPAID',        1, 0,   0,   N'Chỉ, nút áo polo, chưa giao'),
 ('O6-FAB', 'DH-2511-006', N'Công ty Vải Sợi Thành Công', 'FAB', -11, 'PARTIALLY_RECEIVED', 'PARTIALLY_PAID', 2, .70, 0,   N'Vải kate, giao lần 1'),
 ('O6-TRM', 'DH-2511-006', N'Phụ liệu may Hoàng Gia',     'TRM', -11, 'ORDERED',            'UNPAID',        1, 0,   0,   N'Chỉ, nút, nhãn'),
 ('O7-FAB', 'DH-2511-007', N'Công ty Vải Sợi Thành Công', 'FAB', 0,   'PENDING_FINALISATION', 'UNPAID',    NULL, 0, 0,  N'NCC dự kiến Công ty Vải Sợi Thành Công, Quản đốc chưa chốt đơn giá và ngày đặt'),
 ('O7-TRM', 'DH-2511-007', N'Phụ liệu may Hoàng Gia',     'TRM', 0,   'PENDING_FINALISATION', 'UNPAID',    NULL, 0, 0,  N'Khóa, nút, chỉ (khóa khách cấp), NCC dự kiến, chờ Quản đốc chốt'),
 ('O8-KH',  'DH-2511-008', NULL,                            'NONE', 0,  'CUSTOMER_SUPPLIED',  'UNPAID',        NULL, 0, 0,  N'Khách cấp toàn bộ NVL, không phải mua'),
 ('O9-FAB', 'DH-2510-009', N'Công ty Vải Sợi Thành Công', 'FAB', -25, 'RECEIVED',           'PAID',          1, 1,   0,   N'Vải dù gió navy'),
 ('O9-LIN', 'DH-2510-009', N'Công ty Vải Sợi Thành Công', 'LIN', -25, 'RECEIVED',           'PAID',          1, 1,   0,   N'Vải lót taffeta'),
 ('O9-TRM', 'DH-2510-009', N'Phụ liệu may Hoàng Gia',     'TRM', -25, 'REJECTED',           'UNPAID',        NULL, 0, 0,  N'Đơn mua phụ liệu bị hủy khi đơn hàng tạm dừng'),
 ('O10-FAB','DH-2510-010', N'Công ty Vải Sợi Thành Công', 'FAB', -44, 'REJECTED',           'UNPAID',        NULL, 0, 0,  N'Đơn mua vải hủy do khách hủy đơn'),
 ('SH-1',   NULL,          N'Cửa hàng Kim máy Minh Phát',  'KIM', 0,   'PENDING_FINALISATION', 'UNPAID',    NULL, 0, 0,  N'Kim máy dùng chung cho nhiều đơn, NCC dự kiến, chờ Quản đốc chốt'),
 ('SH-2',   NULL,          N'Cửa hàng Kim máy Minh Phát',  'KIM', -20, 'RECEIVED',           'PAID',          1, 1,   0,   N'Kim máy số 14 mua dự trữ');

-- Mã đơn mua theo định dạng của hệ thống: DM-yyyyMMdd-NNN
;WITH x AS (
    SELECT K, DATEADD(DAY, OrdOff, @today) AS D FROM #Po
), y AS (
    SELECT K, D, ROW_NUMBER() OVER (PARTITION BY D ORDER BY K) AS rn FROM x
)
UPDATE p SET PoCode = CONCAT('DM-', FORMAT(y.D, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', y.rn), 3))
FROM #Po p JOIN y ON y.K = p.K;

-- Trạng thái thanh toán PAID / PARTIALLY_PAID cần có ngày, số tiền, hình thức nên cập nhật sau khi ghi các lần thanh toán
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, ExpectedDeliveryCount, GoodsStatus, PaymentStatus, RejectReason, PaymentReturnReason, Note, CreatedAt)
SELECT p.PoCode, co.OrderId, s.SupplierId,
       CASE WHEN p.Goods IN ('PENDING_FINALISATION', 'REJECTED', 'CUSTOMER_SUPPLIED') THEN NULL ELSE DATEADD(DAY, p.OrdOff, @today) END,
       p.ExpCnt, p.Goods, IIF(p.Pay = 'RETURNED', 'RETURNED', 'UNPAID'),
       CASE WHEN p.Goods = 'REJECTED' THEN N'Đơn hàng đã dừng / hủy, không mua NVL nữa' END,
       CASE WHEN p.Pay = 'RETURNED' THEN N'Đơn giá chỉ ghi 14.000 nhưng hóa đơn NCC là 15.500, đề nghị Quản đốc kiểm tra lại đơn giá' END,
       p.Note,
       DATEADD(DAY, p.OrdOff - IIF(p.Goods IN ('PENDING_FINALISATION', 'CUSTOMER_SUPPLIED', 'REJECTED'), 0, 2), CAST(@today AS DATETIME2))
FROM #Po p
LEFT JOIN dbo.CustomerOrder co ON co.OrderCode = p.OrderCode
LEFT JOIN dbo.Supplier s ON s.SupplierName = p.Supplier;

/* ---------- Dòng đơn mua ---------- */
-- Đơn mua theo đơn hàng: lấy phần phải mua của NVL của đơn
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice)
SELECT po.PurchaseOrderId, om.OrderMaterialId, om.PurchaseQty,
       CASE WHEN p.Goods IN ('PENDING_FINALISATION', 'REJECTED') THEN 0 ELSE pr.Price END
FROM #Po p
JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
JOIN dbo.OrderMaterial om ON om.OrderId = po.OrderId
JOIN dbo.Material m ON m.MaterialId = om.MaterialId
JOIN #Price pr ON pr.Mat = m.MaterialCode
WHERE om.PurchaseQty > 0
  AND ((p.Grp = 'FAB' AND m.MaterialType = 'FABRIC' AND m.MaterialCode <> 'V-LOT-DEN')
    OR (p.Grp = 'LIN' AND m.MaterialCode = 'V-LOT-DEN')
    OR (p.Grp = 'TRM' AND m.MaterialType = 'TRIM'));
-- Đơn mua dùng chung (không gắn đơn hàng): ghi thẳng NVL
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, MaterialId, OrderedQty, UnitPrice)
SELECT po.PurchaseOrderId, NULL, m.MaterialId, v.Qty, IIF(p.Goods = 'PENDING_FINALISATION', 0, pr.Price)
FROM #Po p
JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
JOIN (VALUES ('SH-1', 'T-KIM-14', 30), ('SH-2', 'T-KIM-14', 40)) v (K, Mat, Qty) ON v.K = p.K
JOIN dbo.Material m ON m.MaterialCode = v.Mat
JOIN #Price pr ON pr.Mat = m.MaterialCode;

/* ---------- Ngày giao dự kiến của NCC ---------- */
UPDATE po SET ExpectedDeliveryDate = DATEADD(DAY, p.OrdOff + 3, @today),
              ExpectedDeliveryDateTo = CASE WHEN p.ExpCnt >= 2 THEN DATEADD(DAY, p.OrdOff + 9, @today) END
FROM dbo.PurchaseOrder po JOIN #Po p ON p.PoCode = po.PoCode
WHERE p.ExpCnt IS NOT NULL;
INSERT dbo.DeliveryScheduleDate (PurchaseOrderId, SeqNo, DeliveryDate)
SELECT po.PurchaseOrderId, s.n, DATEADD(DAY, p.OrdOff + 3 + 6 * (s.n - 1), @today)
FROM #Po p JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
JOIN (VALUES (1), (2)) s (n) ON s.n <= p.ExpCnt
WHERE p.ExpCnt IS NOT NULL;

/* ---------- Phiếu nhập NVL theo từng lần giao của NCC ---------- */
IF OBJECT_ID('tempdb..#Rcpt') IS NOT NULL DROP TABLE #Rcpt;
CREATE TABLE #Rcpt (K VARCHAR(20) COLLATE DATABASE_DEFAULT, Seq INT, Frac DECIMAL(4,2), RDate DATE, MaterialReceiptId INT NULL);
INSERT #Rcpt (K, Seq, Frac, RDate)
SELECT p.K, d.Seq, d.Frac, DATEADD(DAY, p.OrdOff + 3 + 6 * (d.Seq - 1), @today)
FROM #Po p CROSS APPLY (VALUES (1, p.D1), (2, p.D2)) d (Seq, Frac)
WHERE d.Frac > 0 AND p.Goods IN ('PARTIALLY_RECEIVED', 'RECEIVED', 'CLOSED_SHORT');

INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT CONCAT('PN-', FORMAT(r.RDate, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', ROW_NUMBER() OVER (PARTITION BY r.RDate ORDER BY r.K, r.Seq)), 3)),
       po.OrderId, po.PurchaseOrderId, r.RDate, 'CONFIRMED',
       CONCAT(N'Giao lần ', r.Seq, N'/', p.ExpCnt),
       DATEADD(HOUR, 15, CAST(r.RDate AS DATETIME2)), @quandoc, DATEADD(HOUR, 17, CAST(r.RDate AS DATETIME2))
FROM #Rcpt r JOIN #Po p ON p.K = r.K JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
ORDER BY r.RDate, r.K, r.Seq;
UPDATE r SET MaterialReceiptId = mr.MaterialReceiptId
FROM #Rcpt r JOIN #Po p ON p.K = r.K JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
JOIN dbo.MaterialReceipt mr ON mr.PurchaseOrderId = po.PurchaseOrderId AND mr.Note = CONCAT(N'Giao lần ', r.Seq, N'/', p.ExpCnt);

INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, MaterialId, PurchaseOrderLineId, AcceptedQty)
SELECT r.MaterialReceiptId, pol.OrderMaterialId, IIF(pol.OrderMaterialId IS NULL, pol.MaterialId, NULL), pol.PurchaseOrderLineId,
       ROUND(pol.OrderedQty * r.Frac, 0)
FROM #Rcpt r JOIN #Po p ON p.K = r.K JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId;

-- Đơn Chốt không bù: phần chưa giao không bù lại
UPDATE pol SET NotReplenishedQty = pol.OrderedQty - ISNULL(rec.Qty, 0)
FROM dbo.PurchaseOrderLine pol
JOIN dbo.PurchaseOrder po ON po.PurchaseOrderId = pol.PurchaseOrderId AND po.GoodsStatus = 'CLOSED_SHORT'
OUTER APPLY (SELECT SUM(l.AcceptedQty) Qty FROM dbo.MaterialReceiptLine l WHERE l.PurchaseOrderLineId = pol.PurchaseOrderLineId) rec;
UPDATE dbo.PurchaseOrderLine SET NotReplenishedQty = 0 WHERE NotReplenishedQty IS NULL;

/* ---------- NVL khách cấp: phiếu nhập không có đơn mua ---------- */
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT CONCAT('PN-', FORMAT(DATEADD(DAY, o.RecvOff + 6, @today), 'yyyyMMdd'), '-', RIGHT(CONCAT('00', 50 + ROW_NUMBER() OVER (ORDER BY o.OrderCode)), 3)),
       co.OrderId, NULL, DATEADD(DAY, o.RecvOff + 6, @today), 'CONFIRMED', N'Khách giao NVL cấp cho đơn',
       DATEADD(HOUR, 10, CAST(DATEADD(DAY, o.RecvOff + 6, @today) AS DATETIME2)), @quandoc, DATEADD(HOUR, 11, CAST(DATEADD(DAY, o.RecvOff + 6, @today) AS DATETIME2))
FROM #Ord o JOIN dbo.CustomerOrder co ON co.OrderCode = o.OrderCode
WHERE o.OrderCode IN ('DH-2509-001', 'DH-2510-003', 'DH-2510-004');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, AcceptedQty)
SELECT mr.MaterialReceiptId, om.OrderMaterialId, om.CustomerQty
FROM dbo.MaterialReceipt mr
JOIN dbo.OrderMaterial om ON om.OrderId = mr.OrderId AND om.CustomerQty > 0
WHERE mr.PurchaseOrderId IS NULL;

/* ---------- Phiếu nhập ở các trạng thái khác ---------- */
-- PENDING: thủ kho vừa lập, chờ Quản đốc xác nhận (khách giao một phần vải cấp đơn DH-2511-008; NCC giao lần 2 phụ liệu áo polo)
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, Note, SubmittedAt)
SELECT CONCAT('PN-', FORMAT(@today, 'yyyyMMdd'), '-', RIGHT(CONCAT('00', 900 + v.n), 3)), co.OrderId, NULL, @today, 'PENDING', v.Note, @now
FROM (VALUES (1, 'DH-2511-008', N'Khách giao đợt 1 vải dù gió đen, chờ Quản đốc xác nhận')) v (n, Code, Note)
JOIN dbo.CustomerOrder co ON co.OrderCode = v.Code;
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, AcceptedQty)
SELECT mr.MaterialReceiptId, om.OrderMaterialId, 300
FROM dbo.MaterialReceipt mr
JOIN dbo.OrderMaterial om ON om.OrderId = mr.OrderId
JOIN dbo.Material m ON m.MaterialId = om.MaterialId AND m.MaterialCode = 'V-GIO-DEN'
WHERE mr.Status = 'PENDING' AND mr.PurchaseOrderId IS NULL;

-- RETURNED: Quản đốc trả lại phiếu nhập vì sai số lượng; thủ kho lập lại
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, ReturnReason, Note, SubmittedAt)
SELECT CONCAT('PN-', FORMAT(DATEADD(DAY, -5, @today), 'yyyyMMdd'), '-', RIGHT(CONCAT('00', 800 + v.n), 3)), po.OrderId, po.PurchaseOrderId,
       DATEADD(DAY, -5, @today), 'RETURNED', N'Số lượng ghi 500m nhưng cuộn vải thực tế chỉ 420m, kiểm lại và lập phiếu mới',
       N'Lô vải giao lần 1', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))
FROM (VALUES (1, 'O6-FAB')) v (n, K)
JOIN #Po p ON p.K = v.K JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode;
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty)
SELECT mr.MaterialReceiptId, pol.OrderMaterialId, pol.PurchaseOrderLineId, 500
FROM dbo.MaterialReceipt mr JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = mr.PurchaseOrderId
WHERE mr.Status = 'RETURNED' AND pol.PurchaseOrderLineId = (SELECT MIN(PurchaseOrderLineId) FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = mr.PurchaseOrderId);

/* ---------- Thanh toán đơn mua ---------- */
-- Đơn Chốt không bù: ActualAmount = giá trị hàng thực nhận
UPDATE po SET ActualAmount = (SELECT SUM((pol.OrderedQty - pol.NotReplenishedQty) * pol.UnitPrice) FROM dbo.PurchaseOrderLine pol WHERE pol.PurchaseOrderId = po.PurchaseOrderId)
FROM dbo.PurchaseOrder po WHERE po.GoodsStatus = 'CLOSED_SHORT';

IF OBJECT_ID('tempdb..#Pay') IS NOT NULL DROP TABLE #Pay;
CREATE TABLE #Pay (K VARCHAR(20) COLLATE DATABASE_DEFAULT, Seq INT, DayOff INT, Frac DECIMAL(5,3) NULL, Method VARCHAR(20) COLLATE DATABASE_DEFAULT, Note NVARCHAR(500) COLLATE DATABASE_DEFAULT, Edit NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL);
-- Frac NULL = phần còn lại (trả đủ)
INSERT #Pay VALUES
 ('O1-FAB', 1, -74, .50, 'BANK_TRANSFER', N'Chuyển khoản đợt 1 (50%)', NULL), ('O1-FAB', 2, -62, NULL, 'BANK_TRANSFER', N'Chuyển khoản phần còn lại', NULL),
 ('O1-TRM', 1, -72, NULL, 'CASH', N'Thanh toán tiền mặt khi nhận hàng', NULL),
 ('O2-FAB', 1, -50, .40, 'BANK_TRANSFER', N'Tạm ứng 40%', NULL), ('O2-FAB', 2, -35, NULL, 'BANK_TRANSFER', N'Thanh toán theo giá trị thực nhận (NCC giao thiếu)', NULL),
 ('O2-TRM', 1, -52, NULL, 'CASH', N'Thanh toán đủ', NULL),
 ('O3-FAB', 1, -37, .60, 'BANK_TRANSFER', N'Chuyển khoản lần 1', NULL), ('O3-FAB', 2, -20, NULL, 'BANK_TRANSFER', N'Chuyển khoản lần 2, hết nợ', N'Sửa số tiền do làm tròn lẻ ngàn'),
 ('O3-LIN', 1, -30, .40, 'BANK_TRANSFER', N'Trả trước 40%, phần còn lại sau khi đối chiếu số mét', NULL),
 ('O4-FAB', 1, -24, NULL, 'BANK_TRANSFER', N'Chuyển khoản đủ', NULL),
 ('O5-FAB', 1, -10, .30, 'BANK_TRANSFER', N'Tạm ứng 30% theo hợp đồng', NULL),
 ('O6-FAB', 1, -1,  .50, 'BANK_TRANSFER', N'Tạm ứng 50%', N'Nhập nhầm 40%, sửa thành 50% theo chứng từ'),
 ('O9-FAB', 1, -22, NULL, 'BANK_TRANSFER', N'Thanh toán đủ', NULL),
 ('O9-LIN', 1, -22, NULL, 'BANK_TRANSFER', N'Thanh toán đủ', NULL),
 ('SH-2',   1, -18, NULL, 'CASH', N'Mua kim tiền mặt', NULL);
-- O5-FAB: PARTIALLY_RECEIVED nhưng UNPAID theo kế hoạch trên -> đã tạm ứng nên là PARTIALLY_PAID
UPDATE #Po SET Pay = 'PARTIALLY_PAID' WHERE K = 'O5-FAB';

IF OBJECT_ID('tempdb..#PayAmt') IS NOT NULL DROP TABLE #PayAmt;
SELECT y.K, y.Seq, po.PurchaseOrderId, y.DayOff, y.Method, y.Note, y.Edit, tgt.T,
       CASE WHEN y.Frac IS NULL THEN NULL ELSE ROUND(y.Frac * tgt.T / 1000, 0) * 1000 END AS Fixed
INTO #PayAmt
FROM #Pay y
JOIN #Po p ON p.K = y.K
JOIN dbo.PurchaseOrder po ON po.PoCode = p.PoCode
CROSS APPLY (SELECT ISNULL(po.ActualAmount, (SELECT SUM(pol.OrderedQty * pol.UnitPrice) FROM dbo.PurchaseOrderLine pol WHERE pol.PurchaseOrderId = po.PurchaseOrderId)) AS T) tgt;

INSERT dbo.PurchasePayment (PurchaseOrderId, PaymentDate, Amount, PaymentMethod, PaymentNote, CreatedAt, UpdatedAt, EditReason)
SELECT a.PurchaseOrderId, DATEADD(DAY, a.DayOff, @today),
       ISNULL(a.Fixed, a.T - ISNULL((SELECT SUM(b.Fixed) FROM #PayAmt b WHERE b.K = a.K AND b.Seq < a.Seq), 0)),
       a.Method, a.Note,
       DATEADD(HOUR, 9, CAST(DATEADD(DAY, a.DayOff, @today) AS DATETIME2)),
       CASE WHEN a.Edit IS NOT NULL THEN DATEADD(HOUR, 11, CAST(DATEADD(DAY, a.DayOff, @today) AS DATETIME2)) END,
       a.Edit
FROM #PayAmt a
ORDER BY a.K, a.Seq;

-- Tóm tắt trên đơn mua: tổng đã trả + thông tin lần trả gần nhất
UPDATE po SET PaidAmount = x.Paid, PaymentDate = x.LastDate, PaymentMethod = x.LastMethod, PaymentNote = x.LastNote
FROM dbo.PurchaseOrder po
CROSS APPLY (SELECT SUM(pp.Amount) AS Paid,
                    (SELECT TOP 1 PaymentDate FROM dbo.PurchasePayment WHERE PurchaseOrderId = po.PurchaseOrderId ORDER BY CreatedAt DESC, PurchasePaymentId DESC) AS LastDate,
                    (SELECT TOP 1 PaymentMethod FROM dbo.PurchasePayment WHERE PurchaseOrderId = po.PurchaseOrderId ORDER BY CreatedAt DESC, PurchasePaymentId DESC) AS LastMethod,
                    (SELECT TOP 1 PaymentNote FROM dbo.PurchasePayment WHERE PurchaseOrderId = po.PurchaseOrderId ORDER BY CreatedAt DESC, PurchasePaymentId DESC) AS LastNote
             FROM dbo.PurchasePayment pp WHERE pp.PurchaseOrderId = po.PurchaseOrderId) x
WHERE x.Paid IS NOT NULL;
UPDATE po SET PaymentStatus = p.Pay
FROM dbo.PurchaseOrder po JOIN #Po p ON p.PoCode = po.PoCode
WHERE p.Pay IN ('PAID', 'PARTIALLY_PAID') AND po.PaidAmount > 0;

/* ---------- Ghi tồn kho NVL qua usp_Stock_Post ---------- */
IF OBJECT_ID('tempdb..#sp') IS NOT NULL DROP TABLE #sp;
CREATE TABLE #sp (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @wh VARCHAR(15) = 'MATERIAL', @mid INT, @doc INT, @line INT, @q DECIMAL(18,3), @d DATE;
DECLARE st CURSOR LOCAL FAST_FORWARD FOR
    SELECT COALESCE(om.MaterialId, l.MaterialId), mr.MaterialReceiptId, l.MaterialReceiptLineId, l.AcceptedQty, mr.ReceiptDate
    FROM dbo.MaterialReceiptLine l
    JOIN dbo.MaterialReceipt mr ON mr.MaterialReceiptId = l.MaterialReceiptId AND mr.Status = 'CONFIRMED'
    LEFT JOIN dbo.OrderMaterial om ON om.OrderMaterialId = l.OrderMaterialId
    WHERE l.AcceptedQty > 0
    ORDER BY mr.ReceiptDate, mr.MaterialReceiptId, l.MaterialReceiptLineId;
OPEN st; FETCH NEXT FROM st INTO @mid, @doc, @line, @q, @d;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT #sp EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @SourceDocType = 'MATERIAL_RECEIPT', @SourceDocId = @doc,
                            @SourceLineId = @line, @QtyChange = @q, @TransactionDate = @d;
    FETCH NEXT FROM st INTO @mid, @doc, @line, @q, @d;
END;
CLOSE st; DEALLOCATE st;

PRINT N'05: đơn mua, phiếu nhập, thanh toán, tồn NVL xong.';
