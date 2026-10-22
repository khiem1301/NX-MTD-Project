-- MI · Minh · 22/10/2026 · Dữ liệu dùng chung của Minh: dữ liệu demo các luồng của Minh + công đoạn, đơn giá công đoạn nhập từ Excel.
--
-- Chạy sau khi đã dựng DB (CreateAndSeed.sql / SeedAuth.sql) và các migration trước (TH-12). Chạy lại nhiều lần không lỗi, không nhân đôi.
--   sqlcmd -S localhost -d NXMTD -E -C -f 65001 -b -i SQL/Migrations/20261022_01_MI_shared-data.sql
--
-- Gồm 3 phần:
--   0. Sửa thủ tục usp_Attendance_ApplyAdjustment: thủ tục bị tạo khi QUOTED_IDENTIFIER tắt nên "Kế toán chấm công hộ" báo lỗi
--      MERGE (1934). Tạo lại đúng nội dung cũ với QUOTED_IDENTIFIER bật. Không làm gì nếu thủ tục đã đúng.
--   1. Dữ liệu demo: mã hàng DEMO-AO01 / DEMO-QU02, đơn hàng, NVL, nhà cung cấp, đơn mua, phân công, sản lượng, QC, hàng hỏng,
--      KPI, chi phí tháng, bảng công, suất ăn, xe, bảng lương demo (3 công nhân may CNMAY2..4, mật khẩu giống KETOAN). Bỏ qua nếu đã có DEMO-AO01.
--   2. Công đoạn và đơn giá công đoạn của DEMO-AO01 / DEMO-QU02 (đã nhập từ file Excel quy trình công nghệ), khớp theo mã hàng + mã công đoạn:
--      công đoạn / đơn giá chưa có thì thêm, đã có thì cập nhật theo bản này.
-- Không có họ tên thật, số lương thật hay hash mật khẩu.

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* ===== 0. Sửa usp_Attendance_ApplyAdjustment (QUOTED_IDENTIFIER) ===== */
SET QUOTED_IDENTIFIER ON;
IF EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.usp_Attendance_ApplyAdjustment') AND uses_quoted_identifier = 0)
BEGIN
    DECLARE @def NVARCHAR(MAX) = (SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.usp_Attendance_ApplyAdjustment'));
    DECLARE @pos INT = CHARINDEX(N'CREATE PROCEDURE', @def);
    IF @pos > 0
    BEGIN
        SET @def = STUFF(@def, @pos, LEN(N'CREATE PROCEDURE'), N'ALTER PROCEDURE');
        EXEC sp_executesql @def;
        PRINT N'Đã tạo lại usp_Attendance_ApplyAdjustment với QUOTED_IDENTIFIER bật.';
    END;
END;
GO

/* ===== 1. Dữ liệu demo ===== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Product WHERE ProductCode = 'DEMO-AO01')
BEGIN
    PRINT N'Dữ liệu demo đã có (DEMO-AO01), bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @vp INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP'),
        @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');
DECLARE @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @ketoan INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @qaqc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1');

/* ---------- 1. Công nhân may thêm + bậc thợ ---------- */
UPDATE dbo.Employee SET CurrentGradeCode = 'B' WHERE EmployeeCode = 'CNMAY1' AND CurrentGradeCode IS NULL;

DECLARE @wr INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'WORKER');
INSERT dbo.Employee (EmployeeCode, FullName, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode, StartDate)
SELECT v.Code, v.FullName, @wr, @may, 'PIECE_RATE', v.Grade, v.AttCode, '2026-01-01'
FROM (VALUES ('CNMAY2', N'Nguyễn Thị Lan', 'C', 104), ('CNMAY3', N'Trần Văn Hùng', 'A', 105),
             ('CNMAY4', N'Lê Thị Mai', 'B', 106)) v(Code, FullName, Grade, AttCode)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.EmployeeCode = v.Code);

INSERT dbo.Account (EmployeeId, Username, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, e.EmployeeCode, k.PasswordHash, k.IsTemporaryPassword
FROM dbo.Employee e
CROSS JOIN (SELECT a.PasswordHash, a.IsTemporaryPassword FROM dbo.Account a
            JOIN dbo.Employee x ON x.EmployeeId = a.EmployeeId WHERE x.EmployeeCode = 'KETOAN') k
WHERE e.EmployeeCode IN ('CNMAY2', 'CNMAY3', 'CNMAY4')
  AND NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

DECLARE @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4');

/* ---------- 2. Mã hàng, NVL, nhà cung cấp, đơn hàng ---------- */
INSERT dbo.Product (ProductCode, ProductName, Description) VALUES
 ('DEMO-AO01', N'Áo khoác gió DEMO', N'Mã hàng demo có 5 công đoạn'),
 ('DEMO-QU02', N'Quần short DEMO', N'Mã hàng demo có 2 công đoạn');
DECLARE @p1 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @p2 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-QU02');

INSERT dbo.Material (MaterialCode, MaterialName, MaterialType, Category, Unit) VALUES
 ('DEMO-VAI01',  N'Vải gió 2 lớp DEMO', 'FABRIC', 'MAIN_FABRIC', N'm'),
 ('DEMO-CHI01',  N'Chỉ may 40/2 DEMO',  'TRIM',   'THREAD',      N'cuộn'),
 ('DEMO-KHOA01', N'Khóa kéo 60cm DEMO', 'TRIM',   'ZIPPER',      N'cái'),
 ('DEMO-NHAN01', N'Nhãn dệt DEMO',      'TRIM',   'LABEL',       N'cái'),
 ('DEMO-KIM01',  N'Kim máy 14 DEMO',    'TRIM',   'OTHER',       N'hộp');
DECLARE @mVai INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mChi INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI01'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01');

INSERT dbo.Supplier (SupplierName, Phone, Email, Address, MaterialTypes, Note, Status) VALUES
 (N'Công ty Vải Sợi Thành Công (DEMO)', '0901000001', 'vai@thanhcong.demo', N'Quận Tân Phú, TP.HCM', N'Vải chính, vải lót', NULL, 'ACTIVE'),
 (N'Phụ liệu may Hoàng Gia (DEMO)',     '0901000002', 'pl@hoanggia.demo',  N'Quận 10, TP.HCM',      N'Chỉ, khóa, nhãn',  N'Giao hàng trong 2 ngày', 'ACTIVE'),
 (N'Cửa hàng Kim máy Minh Phát (DEMO)', '0901000003', NULL,                N'Quận 5, TP.HCM',       N'Kim, phụ tùng',    NULL, 'ACTIVE'),
 (N'Vải Phương Nam (DEMO - ngừng)',     '0901000004', NULL,                N'Bình Dương',           N'Vải',              N'Đã ngừng hợp tác', 'DISCONTINUED');
DECLARE @sup1 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Công ty Vải Sợi Thành Công%'),
        @sup2 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Phụ liệu may Hoàng Gia%');

DECLARE @cus INT = (SELECT TOP 1 CustomerId FROM dbo.Customer ORDER BY CustomerId);
INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, Note) VALUES
 ('DEMO-DH001', @cus, '2026-09-20', '2026-11-10', 'PARTIAL', 'IN_PRODUCTION', N'Đơn demo đang sản xuất'),
 ('DEMO-DH002', @cus, '2026-10-05', '2026-12-01', 'AWAITING', 'NEW',           N'Đơn demo mới nhận');
DECLARE @o1 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o2 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH002');

INSERT dbo.OrderLine (OrderId, ProductId, Color, SizeCode, OrderedQty, CmtUnitPrice) VALUES
 (@o1, @p1, N'Đen',  'S', 300, 28000), (@o1, @p1, N'Đen',  'M', 600, 28000), (@o1, @p1, N'Đen', 'L', 500, 28000),
 (@o1, @p1, N'Xanh', 'M', 300, 28000), (@o1, @p1, N'Xanh', 'L', 200, 28000),
 (@o2, @p2, N'Be',   'M', 400, 18000), (@o2, @p2, N'Be',   'L', 400, 18000);
DECLARE @lS INT = (SELECT OrderLineId FROM dbo.OrderLine WHERE OrderId = @o1 AND Color = N'Đen' AND SizeCode = 'S'),
        @lM INT = (SELECT OrderLineId FROM dbo.OrderLine WHERE OrderId = @o1 AND Color = N'Đen' AND SizeCode = 'M'),
        @lL INT = (SELECT OrderLineId FROM dbo.OrderLine WHERE OrderId = @o1 AND Color = N'Đen' AND SizeCode = 'L');

INSERT dbo.OrderMaterial (OrderId, MaterialId, MaterialColor, RequiredQty, CustomerQty, CustomerExpectedDate) VALUES
 (@o1, @mVai,  N'Đen', 2000, 0, NULL), (@o1, @mChi, N'Đen', 200, 0, NULL),
 (@o1, @mKhoa, N'',    1900, 400, '2026-10-30'), (@o1, @mNhan, N'', 1900, 1900, '2026-10-25'),
 (@o2, @mVai,  N'Be',  800, 0, NULL), (@o2, @mChi, N'Be', 100, 0, NULL);
DECLARE @om1Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mVai),
        @om1Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mChi),
        @om1Khoa INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mKhoa),
        @om2Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mVai),
        @om2Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mChi);

/* ---------- 3. MI-07: đơn mua ở đủ các trạng thái ---------- */
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, GoodsStatus, PaymentStatus, RejectReason, PaymentReturnReason,
                          PaymentDate, PaidAmount, PaymentMethod, PaymentNote, PaidAt, Note, OrderedAt) VALUES
 ('PO-DEMO-001', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Quản đốc chốt nhà cung cấp và đơn giá', NULL),
 ('PO-DEMO-002', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Mua thêm khóa kéo', NULL),
 ('PO-DEMO-003', @o2, @sup1, '2026-10-19', 'ORDERED',              'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Kế toán xác nhận thanh toán', '2026-10-19 09:30:00'),
 ('PO-DEMO-004', @o1, @sup2, '2026-10-16', 'ORDERED',              'RETURNED', NULL, N'Đơn giá khóa kéo cao hơn báo giá, đề nghị kiểm tra lại', NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-16 14:00:00'),
 ('PO-DEMO-005', @o1, @sup1, '2026-10-05', 'PARTIALLY_RECEIVED',  'PAID',     NULL, NULL, '2026-10-12', 44000000, 'BANK_TRANSFER', N'Chuyển khoản đợt 1', '2026-10-12 10:15:00', NULL, '2026-10-05 08:45:00'),
 ('PO-DEMO-006', @o1, NULL,  NULL,         'REJECTED',             'UNPAID',   N'Khách đổi màu vải, hủy đơn mua cũ', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
-- Đơn mua tồn kho dùng chung (TH-12): không gắn đơn hàng
INSERT dbo.PurchaseOrder (PoCode, OrderId, GoodsStatus, PaymentStatus, Note)
VALUES ('PO-DEMO-007', NULL, 'PENDING_FINALISATION', 'UNPAID', N'Kim máy dùng chung cho nhiều đơn');

DECLARE @po1 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-001'),
        @po2 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-002'),
        @po3 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-003'),
        @po4 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-004'),
        @po5 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-005'),
        @po6 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-006'),
        @po7 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-007');

INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice) VALUES
 (@po1, @om1Vai, 1000, 0), (@po1, @om1Chi, 200, 0),
 (@po2, @om1Khoa, 1500, 0),
 (@po3, @om2Vai, 800, 45000), (@po3, @om2Chi, 100, 12000),
 (@po4, @om1Khoa, 1500, 3500),
 (@po5, @om1Vai, 1000, 44000),
 (@po6, @om1Vai, 500, 0);
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, MaterialId, MaterialColor, OrderedQty, UnitPrice)
VALUES (@po7, NULL, @mKim, N'', 20, 0);

-- Phiếu nhập đã xác nhận 600/1000 của PO-DEMO-005 (còn phải nhận 400)
INSERT dbo.MaterialReceipt (ReceiptCode, ReceiptType, OrderId, Source, PurchaseOrderId, ReceiptDate, Status, ConfirmedBy, ConfirmedAt)
VALUES ('PN-DEMO-001', 'NEW', @o1, 'SUPPLIER', @po5, '2026-10-08', 'CONFIRMED', @thukho, '2026-10-08 15:00:00');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty)
SELECT (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-001'), @om1Vai,
       (SELECT PurchaseOrderLineId FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = @po5), 600;

/* ---------- 4. Công đoạn + đơn giá (KI-01) ---------- */
INSERT dbo.Operation (ProductId, OperationCode, OperationName, StationSequence, SkillLevel, StandardTimeSec, TeamType, IsFinalStep) VALUES
 (@p1, 'OP01', N'Ráp thân',      1, 2, 95,  'SEWING', 0), (@p1, 'OP02', N'May tay',        2, 2, 120, 'SEWING', 0),
 (@p1, 'OP03', N'Tra cổ',        3, 3, 80,  'SEWING', 0), (@p1, 'OP04', N'Vắt sổ',         4, 1, 60,  'SEWING', 0),
 (@p1, 'OP05', N'Hoàn thiện',    5, 2, 150, 'SEWING', 1),
 (@p2, 'OP01', N'May ống quần',  1, 2, 100, 'SEWING', 0), (@p2, 'OP02', N'Hoàn thiện quần', 2, 2, 130, 'SEWING', 1);
DECLARE @a1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP01'),
        @a2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP02'),
        @a3 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP03'),
        @a4 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP04'),
        @a5 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP05'),
        @b1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p2 AND OperationCode = 'OP01'),
        @b2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p2 AND OperationCode = 'OP02');

INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, ApprovalNote, RejectReason) VALUES
 (@a1, 1200, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', N'Duyệt theo bảng giá quý 3', NULL),
 (@a2, 1500, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL, NULL),
 (@a3, 1000, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL, NULL),
 (@a4,  800, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL, NULL),
 (@a5, 2000, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL, NULL),
 (@a3, 1100, '2026-10-25', 'PENDING_APPROVAL', '2026-10-20 15:00:00', NULL, NULL, NULL),
 (@a4,  900, '2026-11-01', 'DRAFT', NULL, NULL, NULL, NULL),
 (@a2, 1700, '2026-10-22', 'REJECTED', '2026-10-18 11:00:00', NULL, NULL, N'Mức tăng 13% vượt khung, đề nghị trình lại'),
 (@b1, 1300, '2026-10-01', 'APPROVED', '2026-09-28 10:00:00', '2026-09-29 09:00:00', NULL, NULL),
 (@b2, 1800, '2026-10-01', 'PENDING_APPROVAL', '2026-10-20 16:00:00', NULL, NULL, NULL);

/* ---------- 5. Phân công công đoạn (KI-03) ---------- */
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo) VALUES
 (@may, @o1, @a1, @w1, '2026-10-01', NULL), (@may, @o1, @a2, @w1, '2026-10-01', NULL),
 (@may, @o1, @a3, @w2, '2026-10-01', NULL), (@may, @o1, @a4, @w3, '2026-10-01', NULL),
 (@may, @o1, @a5, @w4, '2026-10-01', NULL),
 (@may, @o1, @a3, @w1, '2026-09-21', '2026-09-30');   -- lịch sử: CNMAY1 từng làm Tra cổ

/* ---------- 6. BTP tổ may đã nhận từ kho (cấp trần sản lượng) ---------- */
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt)
VALUES ('PC-DEMO-001', 'SEMI_FINISHED', @o1, @may, '2026-09-23', @thukho, 'RECEIVED', @ttmay, '2026-09-23 10:00:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty)
SELECT (SELECT MaterialIssueId FROM dbo.MaterialIssue WHERE IssueCode = 'PC-DEMO-001'), ol.OrderLineId, ol.OrderedQty, ol.OrderedQty
FROM dbo.OrderLine ol WHERE ol.OrderId = @o1;

/* ---------- 7. Chấm công (bảng công ngày từ 01/09 đến 20/10) ---------- */
CREATE TABLE #Days (D DATE PRIMARY KEY);
;WITH d AS (SELECT CAST('2026-09-01' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-10-20')
INSERT #Days SELECT D FROM d
WHERE DATEDIFF(DAY, 0, D) % 7 <> 6 OR D IN ('2026-10-11', '2026-10-18');   -- Chủ nhật chỉ giữ 11/10 và 18/10 (sản xuất ngày Chủ nhật)

INSERT dbo.DailyTimesheet (EmployeeId, WorkDate, CheckIn, CheckOut, WorkedHours, WorkdayValue, OvertimeHours, IsMissingPunch, Source)
SELECT e.EmployeeId, d.D,
       DATEADD(MINUTE, 7 * 60 + 20 + ABS(CHECKSUM(e.EmployeeId, d.D)) % 10, CAST(d.D AS DATETIME2(0))),
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN NULL
            ELSE DATEADD(MINUTE, 16 * 60 + 30 + IIF(DATEDIFF(DAY, 0, d.D) % 3 = 0 AND e.PayType <> 'MONTHLY', 60, 0), CAST(d.D AS DATETIME2(0))) END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0 ELSE 8 END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0 ELSE 1 END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0
            WHEN DATEDIFF(DAY, 0, d.D) % 3 = 0 AND e.PayType <> 'MONTHLY' THEN 1 ELSE 0 END,
       IIF(e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14', 1, 0),
       'FINGERPRINT'
FROM dbo.Employee e
CROSS JOIN #Days d
WHERE e.EmployeeCode IN ('CNMAY1', 'CNMAY2', 'CNMAY3', 'CNMAY4', 'CNCAT1', 'TTMAY', 'TPMAY', 'TTCAT', 'KETOAN',
                         'QUANDOC', 'THUKHO', 'QAQC1', 'CODIEN')
  AND (DATEDIFF(DAY, 0, d.D) % 7 <> 6 OR (d.D = '2026-10-11' AND e.EmployeeCode IN ('CNMAY1', 'CNMAY2', 'CNMAY3', 'CNMAY4')))
  AND NOT EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = e.EmployeeId AND t.WorkDate = d.D);

-- Hai mã quẹt chưa gắn nhân viên (để thử màn "Mã chấm công chưa map")
DECLARE @sess INT = (SELECT MAX(SyncSessionId) FROM dbo.AttendanceSyncSession);
IF @sess IS NOT NULL
    INSERT dbo.AttendanceLog (AttendanceCode, PunchTime, EmployeeId, SyncSessionId)
    SELECT v.Code, v.T, NULL, @sess
    FROM (VALUES (999, CAST('2026-10-20 07:41:00' AS DATETIME2(0))), (999, CAST('2026-10-20 16:35:00' AS DATETIME2(0))),
                 (998, CAST('2026-10-20 07:52:00' AS DATETIME2(0)))) v(Code, T)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.AttendanceLog l WHERE l.AttendanceCode = v.Code AND l.PunchTime = v.T);

-- Đơn xin chấm công bù: một đơn đã duyệt (Kế toán chấm hộ CNMAY2 ngày 14/10), một đơn đang chờ duyệt
INSERT dbo.Request (RequestCode, RequestTypeCode, Status, TargetDate, Title, Content, CreatedBy, CreatedByTeamId, DecidedAt, DecisionNote)
VALUES ('YC261015-901', 'ATTENDANCE_CORRECTION', 'APPROVED', '2026-10-14', N'Xin chấm công bù cho Nguyễn Thị Lan',
        N'Công nhân quên quẹt thẻ ra ngày 14/10, có làm đủ ca.', @ttmay, @may, '2026-10-15 09:00:00', N'Đồng ý'),
       ('YC261020-902', 'ATTENDANCE_CORRECTION', 'PENDING_APPROVAL', '2026-10-19', N'Xin chấm công bù cho Lê Thị Mai',
        N'Máy chấm công không nhận vân tay ngày 19/10.', @ttmay, @may, NULL, NULL);

/* ---------- 8. Suất ăn 01/09..20/10 (đã chốt) ---------- */
INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, TeamTotal, MealPrice, GuestNote)
SELECT d.D, 'OPEN', 5, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, 3, 0), 18, 30000, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, N'Khách của công ty mẹ', NULL)
FROM #Days d
WHERE DATEDIFF(DAY, 0, d.D) % 7 <> 6
  AND NOT EXISTS (SELECT 1 FROM dbo.DailyMeal m WHERE m.MealDate = d.D);

INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
SELECT m.DailyMealId, t.TeamId, t.Cnt, t.Cnt, 'CONFIRMED', DATEADD(MINUTE, 8 * 60 + 5, CAST(m.MealDate AS DATETIME2(0)))
FROM dbo.DailyMeal m
CROSS JOIN (VALUES (@cat, 8), (@may, 10)) t(TeamId, Cnt)
WHERE m.Status = 'OPEN' AND m.MealDate <= '2026-10-20'
  AND NOT EXISTS (SELECT 1 FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = m.DailyMealId AND c.TeamId = t.TeamId);

UPDATE dbo.DailyMeal SET Status = 'CLOSED', ClosedAt = DATEADD(MINUTE, 8 * 60 + 40, CAST(MealDate AS DATETIME2(0)))
WHERE Status = 'OPEN' AND MealDate <= '2026-10-20';

/* ---------- 9. Sản lượng cuối ca 05/10..20/10 (KI-04) ---------- */
CREATE TABLE #Assign (WorkerId INT, OperationId INT);
INSERT #Assign VALUES (@w1, @a1), (@w1, @a2), (@w2, @a3), (@w3, @a4), (@w4, @a5);

INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId, UnitRate,
                         IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT d.D, 'AFTERNOON', @may, a.WorkerId, a.OperationId,
       IIF(DAY(d.D) % 2 = 0, @lL, @lM),
       25 + ABS(CHECKSUM(d.D, a.WorkerId, a.OperationId)) % 20,
       pr.PieceRateId, pr.Rate,
       0,
       IIF(EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = a.WorkerId AND t.WorkDate = d.D AND t.CheckIn IS NOT NULL), 0, 1),
       dbo.fn_NextWorkingDayEnd(d.D), 'SAVED'
FROM #Days d
CROSS JOIN #Assign a
CROSS APPLY (SELECT TOP (1) PieceRateId, Rate FROM dbo.PieceRate
             WHERE OperationId = a.OperationId AND Status = 'APPROVED' AND EffectiveDate <= d.D
             ORDER BY EffectiveDate DESC) pr
WHERE d.D BETWEEN '2026-10-05' AND '2026-10-20';

/* ---------- 10. QC (KI-06), bàn giao thành phẩm (doanh thu) ---------- */
INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline) VALUES
 ('2026-09-24', 'AFTERNOON', '16:00', @may, @lM, 150, @qaqc, '2026-09-24 16:30:00'),
 ('2026-09-25', 'AFTERNOON', '15:30', @may, @lM, 150, @qaqc, '2026-09-25 16:30:00'),
 ('2026-09-26', 'MORNING',   '10:30', @may, @lL, 120, @qaqc, '2026-09-26 11:30:00'),
 ('2026-09-28', 'AFTERNOON', '15:00', @may, @lL, 130, @qaqc, '2026-09-28 16:30:00'),
 ('2026-10-13', 'MORNING',   '10:45', @may, @lM,  60, @qaqc, '2026-10-13 11:30:00'),
 ('2026-10-14', 'AFTERNOON', '15:30', @may, @lM,  90, @qaqc, '2026-10-14 16:30:00'),
 ('2026-10-16', 'AFTERNOON', '15:45', @may, @lL, 100, @qaqc, '2026-10-16 16:30:00'),
 ('2026-10-19', 'AFTERNOON', '15:50', @may, @lM,  30, @qaqc, '2026-10-19 16:30:00'),
 ('2026-10-20', 'AFTERNOON', '15:40', @may, @lM,  30, @qaqc, '2026-10-20 16:30:00');

INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt) VALUES
 ('FG-DEMO-001', @o1, @p1, @qaqc, '2026-09-25 16:00:00', 'CONFIRMED', @thukho, '2026-09-25 16:20:00'),
 ('FG-DEMO-002', @o1, @p1, @qaqc, '2026-09-28 16:00:00', 'CONFIRMED', @thukho, '2026-09-28 16:20:00'),
 ('FG-DEMO-003', @o1, @p1, @qaqc, '2026-10-14 16:00:00', 'CONFIRMED', @thukho, '2026-10-14 16:20:00');
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.FgHandoverId, v.OrderLineId, v.Qty, v.Qty
FROM (VALUES ('FG-DEMO-001', @lM, 300), ('FG-DEMO-002', @lL, 250), ('FG-DEMO-003', @lM, 150), ('FG-DEMO-003', @lL, 100)) v(Code, OrderLineId, Qty)
JOIN dbo.FgHandover h ON h.HandoverCode = v.Code;

/* ---------- 11. Hàng hỏng (KI-08) + thông báo cho công nhân ---------- */
INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity,
                         DefectReasonCode, ReasonNote, ScrapUnitRate, WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt) VALUES
 ('MAY1-261012-001', '2026-10-12', 'AFTERNOON', @may, @lL, @a1, @w1, 'REWORK', 2, 'SKIPPED_STITCH', NULL, NULL, '2026-10-12 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-12'), 'CANCELLED', @ttmay, '2026-10-12 16:10:00'),
 ('MAY1-261014-001', '2026-10-14', 'AFTERNOON', @may, @lL, @a2, @w1, 'REWORK', 3, 'SKIPPED_STITCH', NULL, NULL, '2026-10-14 16:05:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-14'), 'RECORDED', @ttmay, '2026-10-14 16:05:00'),
 ('MAY1-261015-001', '2026-10-15', 'AFTERNOON', @may, @lM, @a3, @w2, 'SCRAP',  2, 'STAINED',        NULL, 1000, '2026-10-15 16:20:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-15'), 'RECORDED', @ttmay, '2026-10-15 16:20:00'),
 ('MAY1-261016-001', '2026-10-16', 'AFTERNOON', @may, @lL, @a4, @w3, 'SCRAP',  1, 'TORN',           NULL,  800, '2026-10-16 16:15:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-16'), 'RECORDED', @ttmay, '2026-10-16 16:15:00'),
 ('MAY1-261019-001', '2026-10-19', 'AFTERNOON', @may, @lM, @a5, @w4, 'REWORK', 4, 'CROOKED_SEAM',   NULL, NULL, '2026-10-19 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-19'), 'RECORDED', @ttmay, '2026-10-19 16:10:00'),
 ('MAY1-261020-001', '2026-10-20', 'AFTERNOON', @may, @lL, @a2, @w1, 'SCRAP',  1, 'OTHER',          N'Cháy vải do là quá nhiệt', 1500, '2026-10-20 16:25:00',
  N'Hôm đó em không làm công đoạn này, đề nghị kiểm tra lại', '2026-10-20 18:00:00', dbo.fn_NextWorkingDayEnd('2026-10-20'), 'RECORDED', @ttmay, '2026-10-20 16:25:00');

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, IsRead, PushStatus)
SELECT d.ResponsibleWorkerId, 'PRODUCTION', 'DefectRecord', d.DefectRecordId, N'Có phiếu hàng hỏng ghi nhận cho bạn',
       N'Phiếu ' + d.DefectCode + N' ghi nhận ' + CAST(d.Quantity AS NVARCHAR(10)) + N' sản phẩm '
       + IIF(d.DefectType = 'SCRAP', N'bị hủy', N'cần sửa lại') + N'. Nếu ghi sai, bạn có thể phản hồi.',
       0, 'SENT'
FROM dbo.DefectRecord d WHERE d.DefectCode LIKE 'MAY1-26%' AND d.Status <> 'CANCELLED' AND d.WorkerNotifiedAt IS NOT NULL;

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, IsRead, PushStatus)
VALUES (@ketoan, 'SYSTEM', 'PurchaseOrder', @po3, N'Đơn mua chờ thanh toán', N'Đơn mua PO-DEMO-003 đã đặt hàng, chờ Kế toán xác nhận thanh toán.', 0, 'SENT'),
       (@quandoc, 'SYSTEM', 'PurchaseOrder', @po4, N'Đơn mua bị trả lại', N'Kế toán trả lại đơn mua PO-DEMO-004: đơn giá khóa kéo cao hơn báo giá.', 0, 'SENT');

/* ---------- 12. KPI tổ may (MI-16/17/18) ---------- */
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result) VALUES
 (@o1, @p1, @may, '2026-10-01', '2026-10-31', 1200, 22, 'ACTIVE', NULL),
 (@o1, @p1, @may, '2026-09-21', '2026-09-30',  500,  8, 'CLOSED', 'ACHIEVED'),
 (@o1, @p1, @may, '2026-09-10', '2026-09-19',  300,  8, 'CANCELLED', NULL),
 (@o2, @p2, @may, '2026-10-26', '2026-11-30',  800, 25, 'ACTIVE', NULL);

/* ---------- 13. Chi phí tháng 09/2026 (MI-12, MI-13, MI-19) ---------- */
DECLARE @sep INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-09'),
        @aug INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-08');
DECLARE @mealAmount DECIMAL(18,2) = (SELECT ISNULL(SUM(TotalAmount), 0) FROM dbo.DailyMeal WHERE MealDate BETWEEN '2026-09-01' AND '2026-09-30'),
        @mealQty INT = (SELECT ISNULL(SUM(TotalMeals), 0) FROM dbo.DailyMeal WHERE MealDate BETWEEN '2026-09-01' AND '2026-09-30');
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Quantity, Note) VALUES
 (@sep, 'MEALS',            'FIXED',    NULL, @mealAmount, @mealQty, N'Hóa đơn bếp tháng 9 (khớp tổng suất ăn đã chốt)'),
 (@sep, 'FUEL',             'FIXED',    NULL,  6500000, NULL, NULL),
 (@sep, 'UTILITIES',        'FIXED',    NULL, 18000000, NULL, N'Điện, nước'),
 (@sep, 'PREMISES',         'FIXED',    NULL, 25000000, NULL, N'Thuê mặt bằng'),
 (@sep, 'MACHINE_REPAIR',   'FIXED',    NULL,  4000000, NULL, NULL),
 (@sep, 'STATIONERY',       'FIXED',    NULL,  1200000, NULL, NULL),
 (@sep, 'MACHINE_SUPPLIES', 'VARIABLE', @p1,   3500000, NULL, N'Kim, dầu máy cho áo khoác'),
 (@sep, 'MATERIALS_BOUGHT', 'VARIABLE', @p1,   8000000, NULL, N'Phụ liệu mua ngoài'),
 (@sep, 'OTHER',            'VARIABLE', @p2,    900000, NULL, NULL);

/* ---------- 14. Bảng lương: 08/2026 đã duyệt, 09/2026 chờ Admin duyệt (MI-09, MI-10) ---------- */
CREATE TABLE #Staff (Code VARCHAR(20) COLLATE DATABASE_DEFAULT, Grade CHAR(1) COLLATE DATABASE_DEFAULT NULL, Lcb DECIMAL(18,2), Sp DECIMAL(18,2), Piece BIT, WorkDays DECIMAL(5,2));
INSERT #Staff VALUES
 ('CNMAY1', 'B', 4500000, 7800000, 1, 25.5), ('CNMAY2', 'C', 4500000, 6900000, 1, 25), ('CNMAY3', 'A', 4500000, 8600000, 1, 26),
 ('CNMAY4', 'B', 4500000, 7400000, 1, 25.5), ('CNCAT1', NULL, 4500000, 6200000, 0, 26),
 ('TTMAY', 'A', 6000000, 9800000, 0, 26), ('TPMAY', 'B', 5500000, 8500000, 0, 26), ('TTCAT', 'A', 6000000, 9100000, 0, 26),
 ('KETOAN', NULL, 8000000, 8000000, 0, 26), ('QUANDOC', NULL, 12000000, 12000000, 0, 26), ('THUKHO', NULL, 7000000, 7000000, 0, 26),
 ('QAQC1', NULL, 5500000, 6300000, 0, 26), ('CODIEN', NULL, 7000000, 7000000, 0, 26);

CREATE TABLE #Months (PeriodId INT, Factor DECIMAL(4,2), Status VARCHAR(20) COLLATE DATABASE_DEFAULT);
INSERT #Months VALUES (@aug, 1.00, 'APPROVED'), (@sep, 1.08, 'PENDING_APPROVAL');

INSERT dbo.Payroll (PeriodId, Status, ImportedAt, TotalAmount, SubmittedAt, ApprovedAt)
SELECT m.PeriodId, m.Status, IIF(m.Status = 'APPROVED', '2026-09-05 09:00:00', '2026-10-05 09:00:00'), 0,
       IIF(m.Status = 'APPROVED', '2026-09-06 08:30:00', '2026-10-06 08:30:00'), IIF(m.Status = 'APPROVED', '2026-09-07 10:00:00', NULL)
FROM #Months m
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payroll p WHERE p.PeriodId = m.PeriodId);

-- Tính sẵn từng khoản cho mỗi phiếu
SELECT pr.PayrollId, e.EmployeeId, s.*, m.Factor,
       CAST(ROUND(s.Sp * m.Factor, 0) AS DECIMAL(18,2))                                         AS SpAmt,
       CAST(300000 AS DECIMAL(18,2))                                                            AS Attend,
       CAST(IIF(s.Code IN ('TTMAY', 'TPMAY', 'TTCAT', 'QUANDOC', 'KETOAN', 'THUKHO'), 200000, 0) AS DECIMAL(18,2)) AS Seniority,
       CAST(CASE s.Grade WHEN 'A' THEN 600000 WHEN 'B' THEN 400000 WHEN 'C' THEN 200000 ELSE 0 END AS DECIMAL(18,2)) AS GradeBonus
INTO #Slip
FROM #Staff s
JOIN dbo.Employee e ON e.EmployeeCode = s.Code
CROSS JOIN #Months m
JOIN dbo.Payroll pr ON pr.PeriodId = m.PeriodId
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payslip x WHERE x.PayrollId = pr.PayrollId AND x.EmployeeId = e.EmployeeId);

ALTER TABLE #Slip ADD Total DECIMAL(18,2), Ins DECIMAL(18,2), Tax DECIMAL(18,2);
UPDATE #Slip SET Total = SpAmt + Attend + Seniority + GradeBonus, Ins = ROUND(Lcb * 0.105, 0);
UPDATE #Slip SET Tax = IIF(Total - Ins > 9500000, ROUND((Total - Ins - 9500000) * 0.05, 0), 0);

INSERT dbo.Payslip (PayrollId, EmployeeId, WorkDays, TotalAdditions, TotalDeductions, Insurance, NetPay)
SELECT PayrollId, EmployeeId, WorkDays, Total, Tax, Ins, Total - Ins - Tax FROM #Slip;

INSERT dbo.PayslipItem (PayslipId, ItemGroup, ItemName, Amount, SortOrder)
SELECT ps.PayslipId, i.G, i.N, i.A, i.S
FROM #Slip s
JOIN dbo.Payslip ps ON ps.PayrollId = s.PayrollId AND ps.EmployeeId = s.EmployeeId
CROSS APPLY (VALUES
    ('INFO',      N'LCB',                              s.Lcb,        10, 1),
    ('INFO',      N'Phân loại: ' + ISNULL(s.Grade, N'-'), CAST(0 AS DECIMAL(18,2)), 20, IIF(s.Grade IS NULL, 0, 1)),
    ('INFO',      IIF(s.Piece = 1, N'Ngày công SP', N'Số giờ công hành chính'),
                  IIF(s.Piece = 1, s.WorkDays, s.WorkDays * 8), 30, 1),
    ('INFO',      N'Số giờ làm thêm ngày thường',      CAST(IIF(s.Piece = 1, 12, 8) AS DECIMAL(18,2)), 40, 1),
    ('ADDITION',  IIF(s.Piece = 1, N'Lương sản phẩm theo hệ số', N'Lương ngày công phép'), s.SpAmt, 50, 1),
    ('ADDITION',  N'Chuyên cần + Xăng xe',             s.Attend,     60, 1),
    ('ADDITION',  N'Thâm niên',                        s.Seniority,  70, IIF(s.Seniority > 0, 1, 0)),
    ('ADDITION',  N'Thưởng PL',                        s.GradeBonus, 80, IIF(s.GradeBonus > 0, 1, 0)),
    ('INSURANCE', N'BHXH 10.5%',                       s.Ins,        90, 1),
    ('DEDUCTION', N'Thuế TNCN',                        s.Tax,       100, IIF(s.Tax > 0, 1, 0))
) i(G, N, A, S, Keep)
WHERE i.Keep = 1;

UPDATE p SET TotalAmount = x.Total
FROM dbo.Payroll p
JOIN (SELECT PayrollId, SUM(NetPay) AS Total FROM dbo.Payslip GROUP BY PayrollId) x ON x.PayrollId = p.PayrollId
WHERE p.PeriodId IN (@aug, @sep);

/* ---------- 15. Xe và sổ xe (MI-19) ---------- */
INSERT dbo.Vehicle (PlateNumber, VehicleType, FuelNormPer100Km) VALUES
 ('51C-123.45', N'Xe tải 1.5 tấn (DEMO)', 14), ('51D-678.90', N'Xe bán tải (DEMO)', 11);
DECLARE @v1 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51C-123.45'),
        @v2 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51D-678.90');
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverName, Purpose, OdometerStart, OdometerEnd, FuelLiters, FuelCost, Status, KmGapWarning, FuelOverNormWarning) VALUES
 (@v1, '2026-09-15', N'Phạm Văn Tài', N'Giao hàng cho công ty mẹ',     12000, 12080, 11.5, 253000, 'COMPLETED', 0, 0),
 (@v1, '2026-09-22', N'Phạm Văn Tài', N'Lấy vải tại Tân Phú',         12080, 12150, 14.0, 308000, 'COMPLETED', 0, 1),
 (@v2, '2026-09-28', N'Võ Minh Quân', N'Đi mua phụ liệu',             5400,  5440,  4.2,  92400,  'COMPLETED', 0, 0),
 (@v1, '2026-10-14', N'Phạm Văn Tài', N'Giao thành phẩm đợt 3',       12300, 12388, 12.6, 277200, 'COMPLETED', 1, 0),
 (@v2, '2026-10-20', N'Võ Minh Quân', N'Mua kim máy',                 5500,  NULL,  NULL, NULL,   'STARTED',   0, 0);

COMMIT TRANSACTION;

DROP TABLE #Days, #Assign, #Staff, #Months, #Slip;
GO

/* ===== 2. Công đoạn và đơn giá công đoạn (nhập từ Excel quy trình công nghệ) ===== */
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

CREATE TABLE #P (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, ProductName NVARCHAR(200) COLLATE DATABASE_DEFAULT,
                 Description NVARCHAR(1000) COLLATE DATABASE_DEFAULT NULL);
INSERT #P (ProductCode, ProductName, Description) VALUES
 (N'DEMO-AO01', N'Áo khoác gió DEMO', N'Mã hàng demo có 5 công đoạn'),
 (N'DEMO-QU02', N'Quần short DEMO', N'Mã hàng demo có 2 công đoạn');

CREATE TABLE #O (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, OperationCode VARCHAR(30) COLLATE DATABASE_DEFAULT,
                 OperationName NVARCHAR(200) COLLATE DATABASE_DEFAULT, Seq SMALLINT, Skill TINYINT NULL, StdTime DECIMAL(9,2) NULL,
                 TeamType VARCHAR(10) COLLATE DATABASE_DEFAULT, IsFinal BIT, Status VARCHAR(20) COLLATE DATABASE_DEFAULT);
INSERT #O (ProductCode, OperationCode, OperationName, Seq, Skill, StdTime, TeamType, IsFinal, Status) VALUES
 (N'DEMO-AO01', N'OP01', N'KT cơi lót túi sườn', 1, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP02', N'KT đáp mác', 2, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP03', N'KT nẹp ve', 3, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP04', N'Vắt sổ', 4, 1, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP05', N'KT thân trước lần', 5, 1, 5.00, 'SEWING', 1, 'ACTIVE'),
 (N'DEMO-AO01', N'OP06', N'KT TS lần', 6, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP07', N'KT tay to lần', 7, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP08', N'KT tay nhỏ lần', 8, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP09', N'KT bản cổ ngoài', 9, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP10', N'KT bản cổ trong', 10, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP12', N'Lộn túi sườn+ sửa đường may túi, đầu cơi + bổ cơi', 12, 2, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP13', N'Bấm ngạnh cổ', 13, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP14', N'Bấm ngạnh nẹp ve + Lộn', 14, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP15', N'Bấm ngạnh TT +gấu + Lộn', 15, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP16', N'Sửa lộn nẹp áo hc', 16, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP17', N'Sửa lộn bản cổ', 17, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP18', N'Quấn chân cúc(6)', 18, 2, 72.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP19', N'Đo+ cắt dây treo áo', 19, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP20', N'Lộn tay', 20, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP21', N'Đi lại+ lấy hàng phòng chần', 21, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP23', N'VSCN+ tẩy dầu+ chấm bụi hc', 23, 2, 197.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP24', N'Nhận hàng', 24, 2, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP25', N'Là dây treo', 25, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP26', N'Là cạnh mí đáp mác', 26, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP27', N'Là gập cơi túi sườn', 27, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP28', N'Là túi sườn+ ke lót túi sườn', 28, 3, 26.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP29', N'Là quay lộn nẹp', 29, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP30', N'Là ql bản cổ', 30, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP31', N'Là vai con', 31, 3, 12.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP32', N'Là sống lưng', 32, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP33', N'Là sườn áo', 33, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP34', N'Là sống tay lần', 34, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP35', N'Là bụng tay lần', 35, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP36', N'Là vòng nách', 36, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP37', N'Là gập gấu áo', 37, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP38', N'Là gập cửa tay trc chắp bụng tay', 38, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP39', N'Là tra cổ lần', 39, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP40', N'Là cạnh mí nẹp ve', 40, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP41', N'Là ht áo', 41, 3, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP43', N'đính cúc nẹp+ cổ(5)', 43, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP44', N'đính cúc nẹp ve phải(1)', 44, 3, 13.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP45', N'Đính cúc dự phòng trên+ dưới(1)', 45, 3, 51.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP46', N'thùa khuy nẹp phải(3)+ cổ(2)', 46, 3, 75.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP47', N'thùa khuy nẹp trái(1)', 47, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP48', N'SD cúc nẹp + cổ, cúc nẹp ve phải, khuy nẹp trái phải', 48, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP49', N'VS cạnh trong nẹp ve', 49, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP50', N'VS cạnh chân đáp mác', 50, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP51', N'VS chân cơi túi sườn', 51, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP52', N'VS gấu áo', 52, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP53', N'VS cửa tay + rút chỉ', 53, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP55', N'Ghim chân cổ lần lót+ ql bản cổ+ sd', 55, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP56', N'Ghim chập chân cổ để tra', 56, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP57', N'Ke dây treo', 57, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP58', N'Mí cạnh trong nẹp ve', 58, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP59', N'Đặt cơi, lót túi sườn+ ghim đường tra cổ trên thân, nẹp ve+ ql nẹp áo+ sd+ bổ túi sườn(báo hàng VC)', 59, 3, 430.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP60', N'Chặn ngạnh túi sườn hc', 60, 4, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP61', N'May chân cơi túi sườn', 61, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP62', N'Quay lót túi sườn', 62, 3, 113.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP63', N'Ke lót túi sườn', 63, 3, 105.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP64', N'Cắt nhét ke viền lót túi sườn', 64, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP66', N'Bóc số + Chắp sống tay', 66, 3, 70.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP67', N'Chắp bụng tay', 67, 3, 65.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP68', N'Vắt gấu cửa tay', 68, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP69', N'Chắp sống lưng', 69, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP70', N'Chắp vai con', 70, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP71', N'Chắp sườn áo', 71, 3, 75.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP72', N'Tra tay+ sd + Rút chỉ', 72, 4, 160.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP73', N'Tra cổ lần hc+ sd', 73, 4, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP74', N'Mí cạnh cong đáp mác', 74, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP75', N'Gắn dây treo', 75, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP76', N'Gắn mác cổ(2)', 76, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP77', N'Chắp vai con nẹp ve+ đáp mác', 77, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP78', N'Chặn rẽ đường may vai nẹp ve', 78, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP79', N'Kê mí nẹp ve gấu áo đoạn gấu', 79, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP80', N'Tra QL cổ lót hc, sd', 80, 4, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP81', N'Mí chân dây treo, mác cổ', 81, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP82', N'Vắt gấu gấu áo', 82, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP83', N'Ghim đính vai con nẹp ve(2)', 83, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP84', N'Ghim đính đáp mác TS(1)', 84, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP85', N'Chặn đính túi sườn nẹp ve(2*2)', 85, 2, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP86', N'gắn mác sườn', 86, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP87', N'Gắn nhãn thị trường', 87, 2, 8.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP88', N'Diễu nẹp+ cổ áo', 88, 4, 200.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP89', N'Gim chập mác sườn', 89, 1, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP90', N'Tở dây ke túi sườn', 90, 1, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP91', N'Ghim cạnh ngoài lót túi sau quay x2', 91, 2, 42.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP01', N'May ống quần', 1, 2, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP02', N'SD thân sau lót(điểm dán mác )', 2, 2, 15.00, 'SEWING', 1, 'DISCONTINUED'),
 (N'DEMO-QU02', N'OP07', N'Kt tay lần hc', 7, 1, 4.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP08', N'SD chun gấu(rút)', 8, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP09', N'KT cơi, đáp ,lót túi sườn', 9, 1, 6.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP11', N'Đo+Cắt rời dây khuy nắp túi tay', 11, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP12', N'Đo+Cắt rời dây khuy tay', 12, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP13', N'Đo+ cắt dây treo', 13, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP14', N'Đo+cắt tay kéo khoá nẹp', 14, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP15', N'Sửa lộn nắp túi tay + sd túi sườn', 15, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP16', N'Sửa lộn dây khuy tay', 16, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP17', N'Bấm ngạnh miệng túi sườn', 17, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP18', N'Sửa lộn áo qua nách+VS sạch sẽ', 18, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP19', N'Sửa đầu cổ +đầu gấu', 19, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP20', N'Lộn đầu cổ+đầu gấu', 20, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP21', N'Lộn tay lần+lồng đôi', 21, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP22', N'Xén sửa sống cổ', 22, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP23', N'Xén sửa họng cổ lần+bấm nhả', 23, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP24', N'Đi lại lấy hàng phòng chần', 24, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP25', N'VSCN +tẩy dầu+xịt bụi h/c', 25, 2, 200.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP26', N'Nhận hàng', 26, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP27', N'Là khoá nẹp', 27, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP28', N'Là dây treo', 28, 3, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP29', N'Là tay kéo khoá nẹp', 29, 3, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP30', N'Chạy ke dây khuy tay hc(chạy ke máy ep mex)', 30, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP31', N'Là dây khuy tay sau mí', 31, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP32', N'Là ql nắp túi', 32, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP33', N'Là Tp nắp túi', 33, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP34', N'Là chắp áo lót hc', 34, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP35', N'Là gập chân cổ lót', 35, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP36', N'Là gạt sống cổ', 36, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP37', N'Là chắp bụng tay', 37, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP38', N'Là gạt chắp miệng túi sườn', 38, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP40', N'Là chắp cầu vai lần', 40, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP41', N'Là chắp sườn áo trước', 41, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP42', N'Là chắp sườn áo sau', 42, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP43', N'Là chắp vai con', 43, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP44', N'Là vòng nách', 44, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP45', N'Là HT áo', 45, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP46', N'Là ly TS', 46, 3, 17.50, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP47', N'Bọ miệng túi sườn + SD (4)', 47, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP48', N'Bọ nắp túi tay (4)', 48, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP49', N'Lồng+chặn bọ tay kéo khoá nẹp(3)', 49, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP50', N'Ke dây treo', 50, 2, 12.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP51', N'May mác cỡ& mác chính', 51, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP52', N'May ly Ts lót+ghim chặn đầu', 52, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP53', N'May mác & cầu vai thân sau', 53, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP54', N'Gắn dây treo', 54, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP55', N'Ghim chập +gắn mác sườn', 55, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP56', N'Gắn mác TT(ghi tiền theo sl)', 56, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP57', N'Chắp vai con lót', 57, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP58', N'Chắp sườn áo lót(chỉ lót)', 58, 3, 85.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP59', N'Ke mí xq dây khuy nắp túi', 59, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP60', N'Lồng+chặn chân dây khuy nắp túi tay', 60, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP61', N'Quay lộn nắp túi+đặt dây khuy', 61, 3, 65.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP62', N'Mí diễu xq nắp túi', 62, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP63', N'Ghim TP nắp túi+đánh số', 63, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP64', N'Quay lộn 2 đầu dây khuy tay', 64, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP65', N'Kẹp mí xq dây khuy tay(chiết kim)', 65, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP66', N'Đặt BTP+ nắp túi+dây khuy tay= gắn nắp+gắn dây khuy & tay', 66, 3, 140.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP67', N'Mí diễu gáy nắp túi', 67, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP68', N'Chắp bụng tay lần', 68, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP69', N'May cửa tay', 69, 4, 140.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP70', N'Đặt cổ lần, lót= ghim chân, chắp sống+sd', 70, 3, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP71', N'May đáp túi sườn', 71, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP72', N'chắp miệng túi sườn(chữ U)+ vén lót chắp sườn 2 đầu (bỏ miệng túi)', 72, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP73', N'Mí diễu miệng túi sườn', 73, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP74', N'Đặt lót +quay lót túi cạnh trên-dưới', 74, 3, 70.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP75', N'Vào lót túi to', 75, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP76', N'Quay lót túi cạnh nẹp', 76, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP77', N'Ghim lót túi ra nẹp', 77, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP78', N'Chắp sống lưng lần + đặt giằng', 78, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP79', N'Mí diễu sống lưng lần', 79, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP80', N'Chắp cầu vai lần', 80, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP81', N'Diễu cầu vai lần', 81, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP82', N'Chắp vai con lần', 82, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP83', N'Mí vai con lần', 83, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP84', N'VS chắp sườn trước + đặt giằng 2 bên sườn', 84, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP85', N'Mí diễu sườn trước lần(nhấc kim)', 85, 3, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP86', N'Chắp sườn sau lần', 86, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP87', N'Tra cổ lần+sd', 87, 4, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP88', N'Chạy kê chun gấu+cắt', 88, 3, 130.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP89', N'Ke tay kéo khoá nẹp', 89, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP90', N'Gập đầu khoá nẹp', 90, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP91', N'Tra khoá nẹp+sd', 91, 4, 180.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP92', N'QL gấu lần, lót+sd', 92, 3, 125.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP93', N'QL nẹp+sd', 93, 3, 150.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP94', N'Chặn giằng trực tiếp gấu(5)', 94, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP95', N'Ghim chập họng cổ lần, lót', 95, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP96', N'Mí kẹp họng cổ', 96, 4, 180.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP97', N'QL đầu cổ+sd', 97, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP98', N'Ghim chập vòng nách lần,lót', 98, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP99', N'Tra tay +sd + bóc số (x7)', 99, 4, 205.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP100', N'Mí vòng nách', 100, 3, 170.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP101', N'Mí diễu xq nẹp+sống cổ', 101, 4, 220.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP102', N'Mí dây khoá', 102, 2, 4.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP103', N'Ép mex vai trước', 103, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP104', N'Ép mex vai sau', 104, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP105', N'Ép mex túi sườn', 105, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP106', N'Bù', 106, 3, 106.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP107', N'Gia cố túi sườn', 107, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP108', N'Ghim miệng túi sườn', 108, 2, 15.00, 'SEWING', 0, 'ACTIVE');

CREATE TABLE #R (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, OperationCode VARCHAR(30) COLLATE DATABASE_DEFAULT,
                 Rate DECIMAL(18,2), EffectiveDate DATE, Status VARCHAR(20) COLLATE DATABASE_DEFAULT, SubmittedAt DATETIME2(0) NULL,
                 ApprovedAt DATETIME2(0) NULL, ApprovalNote NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL,
                 RejectReason NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL);
INSERT #R (ProductCode, OperationCode, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, ApprovalNote, RejectReason) VALUES
 (N'DEMO-AO01', N'OP01', 1200.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', N'Duyệt theo bảng giá quý 3', NULL),
 (N'DEMO-AO01', N'OP02', 1500.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP02', 1700.00, '2026-10-22', 'REJECTED', '2026-10-18T11:00:00', NULL, NULL, N'Mức tăng 13% vượt khung, đề nghị trình lại'),
 (N'DEMO-AO01', N'OP03', 1000.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP03', 1100.00, '2026-10-25', 'APPROVED', '2026-10-20T15:00:00', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-AO01', N'OP04', 800.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP04', 900.00, '2026-11-01', 'DRAFT', NULL, NULL, NULL, NULL),
 (N'DEMO-AO01', N'OP05', 2000.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-QU02', N'OP01', 1300.00, '2026-10-01', 'APPROVED', '2026-09-28T10:00:00', '2026-09-29T09:00:00', NULL, NULL),
 (N'DEMO-QU02', N'OP02', 1800.00, '2026-10-01', 'PENDING_APPROVAL', '2026-10-20T16:00:00', NULL, NULL, NULL),
 (N'DEMO-QU02', N'OP07', 21.27, '2026-10-22', 'PENDING_APPROVAL', '2026-10-22T08:23:08', NULL, NULL, NULL),
 (N'DEMO-QU02', N'OP08', 113.80, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP09', 31.91, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP100', 1030.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP101', 1415.63, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP102', 22.76, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP103', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP104', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP105', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP106', 642.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP107', 256.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP108', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP11', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP12', 113.80, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP13', 28.45, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP14', 28.45, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP15', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP16', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP17', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP18', 256.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP19', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP20', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP21', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP22', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP23', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP24', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP25', 1138.04, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP26', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP27', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP28', 18.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP29', 18.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP30', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP31', 42.44, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP32', 181.87, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP33', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP34', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP35', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP36', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP37', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP38', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP40', 60.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP41', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP42', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP43', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP44', 212.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP45', 666.87, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP46', 106.09, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP47', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP48', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP49', 212.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP50', 68.28, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP51', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP52', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP53', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP54', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP55', 199.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP56', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP57', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP58', 515.31, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP59', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP60', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP61', 394.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP62', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP63', 227.61, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP64', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP65', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP66', 848.74, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP67', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP68', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP69', 900.86, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP70', 606.24, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP71', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP72', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP73', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP74', 424.37, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP75', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP76', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP77', 199.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP78', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP79', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP80', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP81', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP82', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP83', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP84', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP85', 727.49, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP86', 545.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP87', 772.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP88', 788.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP89', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP90', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP91', 1158.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP92', 757.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP93', 909.37, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP94', 545.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP95', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP96', 1158.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP97', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP98', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP99', 1319.11, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL);

-- Mã hàng
INSERT dbo.Product (ProductCode, ProductName, Description)
SELECT s.ProductCode, s.ProductName, s.Description FROM #P s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product p WHERE p.ProductCode = s.ProductCode);

-- Công đoạn: bỏ cờ công đoạn cuối trước (chỉ được một công đoạn cuối đang dùng cho mỗi mã hàng), cập nhật, thêm, rồi đặt lại cờ
UPDATE o SET IsFinalStep = 0
FROM dbo.Operation o JOIN dbo.Product p ON p.ProductId = o.ProductId
WHERE p.ProductCode IN (SELECT ProductCode FROM #P) AND o.IsFinalStep = 1;

UPDATE o SET OperationName = s.OperationName, StationSequence = s.Seq, SkillLevel = s.Skill, StandardTimeSec = s.StdTime,
             TeamType = s.TeamType, Status = s.Status
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #O s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode;

INSERT dbo.Operation (ProductId, OperationCode, OperationName, StationSequence, SkillLevel, StandardTimeSec, TeamType, IsFinalStep, Status)
SELECT p.ProductId, s.OperationCode, s.OperationName, s.Seq, s.Skill, s.StdTime, s.TeamType, 0, s.Status
FROM #O s JOIN dbo.Product p ON p.ProductCode = s.ProductCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Operation o WHERE o.ProductId = p.ProductId AND o.OperationCode = s.OperationCode);

UPDATE o SET IsFinalStep = 1
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #O s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode
WHERE s.IsFinal = 1;   -- chỉ được một công đoạn cuối ĐANG DÙNG mỗi mã hàng (index lọc); công đoạn cuối đã ngừng dùng vẫn giữ cờ

-- Đơn giá công đoạn: khớp theo (mã hàng, mã công đoạn, ngày hiệu lực)
UPDATE r SET Rate = s.Rate, Status = s.Status, SubmittedAt = s.SubmittedAt, ApprovedAt = s.ApprovedAt,
             ApprovalNote = s.ApprovalNote, RejectReason = s.RejectReason
FROM dbo.PieceRate r
JOIN dbo.Operation o ON o.OperationId = r.OperationId
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #R s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode AND s.EffectiveDate = r.EffectiveDate;

INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, ApprovalNote, RejectReason)
SELECT o.OperationId, s.Rate, s.EffectiveDate, s.Status, s.SubmittedAt, s.ApprovedAt, s.ApprovalNote, s.RejectReason
FROM #R s
JOIN dbo.Product p ON p.ProductCode = s.ProductCode
JOIN dbo.Operation o ON o.ProductId = p.ProductId AND o.OperationCode = s.OperationCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.PieceRate r WHERE r.OperationId = o.OperationId AND r.EffectiveDate = s.EffectiveDate);

COMMIT TRANSACTION;

DROP TABLE #P, #O, #R;

SELECT p.ProductCode, COUNT(DISTINCT o.OperationId) AS Operations, COUNT(r.PieceRateId) AS PieceRates
FROM dbo.Product p
LEFT JOIN dbo.Operation o ON o.ProductId = p.ProductId
LEFT JOIN dbo.PieceRate r ON r.OperationId = o.OperationId
WHERE p.ProductCode LIKE 'DEMO-%'
GROUP BY p.ProductCode;
GO
