/* =====================================================================================
   19.7 Dữ liệu demo để test đủ các chức năng (02/11/2026)
   ---------------------------------------------------------------------------------
   Bù các loại và trạng thái mà 19.1–19.6 chưa có, xem bảng tra trong docs/DEMO_SCRIPT.md:
   - Danh mục: khách hàng, NVL, mã hàng ngừng dùng; mã hàng chưa khai công đoạn; tổ ngừng; nhân viên đã nghỉ, tài khoản bị khóa;
     thêm 12 công nhân tổ may 2 (thử danh sách dài ở Phân công công đoạn).
   - Đơn hàng DEMO-DH005…013 ở mọi trạng thái; đơn đã giao có đủ chuỗi cắt → may → QC → kho → xuất, tồn kho ghi qua usp_Stock_Post.
   - Đủ 11 loại đơn từ và 6 trạng thái; phiếu nhập, cấp, trả, bàn giao, xuất, kiểm kê ở các trạng thái còn thiếu.
   - Chấm công (lượt quẹt, phiên đồng bộ, chấm hộ), suất ăn, bậc thợ, bảng lương, khóa tháng, chi phí, hòa vốn, thông báo.
   Chỉ thêm dữ liệu, không xóa. Chạy lại không nhân đôi (bỏ qua nếu đã có đơn DEMO-DH005).
   Ảnh và file đính kèm không có ở đây: Attachment, ProductImage cần file thật, tải lên trên giao diện.
   Tài khoản mới dùng placeholder mật khẩu: cần khởi động lại backend để băm mật khẩu 123456.
   ===================================================================================== */
USE NXMTD;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH005')
BEGIN
    PRINT N'Dữ liệu demo 19.7 đã có (DEMO-DH005), bỏ qua.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.Team WHERE TeamCode = 'MAY2')
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.6 (MAY2), bỏ qua 19.7.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @may2 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY2'),
        @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @ttcat INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTCAT'),
        @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @ttmay2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY2'),
        @qaqc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1'),
        @codien INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CODIEN'),
        @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4'),
        @w5 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY5'),
        @w6 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY6'),
        @w7 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY7'),
        @w8 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY8'),
        @rWorker INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'WORKER'),
        @rDeputy INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'TEAM_DEPUTY'),
        @rLeader INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'TEAM_LEADER'),
        @pAo INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @pQu INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-QU02'),
        @cusA INT = (SELECT CustomerId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o1 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o2 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH002'),
        @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003'),
        @wo2 INT = (SELECT WorkOrderId FROM dbo.WorkOrder WHERE WorkOrderCode = 'LC-DEMO-002'),
        @sup1 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Công ty Vải Sợi Thành Công%'),
        @sup3 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Cửa hàng Kim máy Minh Phát%'),
        @mVaiDen INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mVaiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI02'),
        @mVaiTrang INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI03'),
        @mVaiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI04'),
        @mChiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI03'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01');

/* ---------- 1. Danh mục: đủ trạng thái đang dùng / ngừng dùng ---------- */
INSERT dbo.Customer (CustomerName, Phone, Email, DefaultAddress, Status, DiscontinueReason) VALUES
 (N'Công ty B (DEMO)', '0281234567', 'lienhe@congtyb.demo', N'12 Lê Lợi, Quận 1, TP.HCM', 'ACTIVE', NULL),
 (N'Công ty C (DEMO - ngừng)', '0287654321', 'lienhe@congtyc.demo', N'45 Hai Bà Trưng, Quận 3, TP.HCM', 'DISCONTINUED', N'Khách đã ngừng đặt hàng từ tháng 9');
DECLARE @cusB INT = (SELECT CustomerId FROM dbo.Customer WHERE CustomerName = N'Công ty B (DEMO)');

INSERT dbo.Material (MaterialCode, MaterialName, Color, Category, Unit, WidthCm, Status) VALUES
 ('DEMO-CUC01', N'Cúc nhựa 4 lỗ DEMO', N'Trắng', 'BUTTON', N'cái', NULL, 'ACTIVE'),
 ('DEMO-LOT01', N'Vải lót lưới DEMO', N'Đen', 'LINING', N'm', 150, 'ACTIVE'),
 ('DEMO-NGUNG01', N'Mex dựng cũ DEMO', N'', 'INTERLINING', N'm', 90, 'DISCONTINUED');

-- DEMO-SM03: có màu – size nhưng chưa khai định mức và công đoạn (thử cảnh báo chưa khai); DEMO-NGUNG04: mã hàng ngừng sản xuất
INSERT dbo.Product (ProductCode, ProductName, Description, Status) VALUES
 ('DEMO-SM03', N'Sơ mi DEMO', N'Mã hàng mới, chưa khai định mức và công đoạn', 'ACTIVE'),
 ('DEMO-NGUNG04', N'Áo thun cũ DEMO', N'Đã ngừng sản xuất', 'DISCONTINUED');
DECLARE @pSm INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-SM03');
INSERT dbo.ProductVariant (ProductId, Color, SizeCode) VALUES (@pSm, N'Trắng', 'M'), (@pSm, N'Trắng', 'L');

INSERT dbo.GradeLevelCode (GradeCode, GradeName, Status) VALUES ('E', N'Bậc E – Thử việc (ngừng dùng)', 'INACTIVE');
INSERT dbo.GradeLevel (GradeCode, MonthlyAmount, EffectiveDate) VALUES ('E', 0, '2026-01-01');

INSERT dbo.Team (TeamCode, TeamName, TeamType, Status, CreatedAt) VALUES ('MAY3', N'Tổ may 3 (ngừng)', 'SEWING', 'DISCONTINUED', '2026-06-01 08:00:00');
INSERT dbo.Vehicle (PlateNumber, VehicleType, FuelNormPer100Km, Status) VALUES ('51E-000.00', N'Xe tải cũ', 14, 'DISCONTINUED');

-- Nhân viên: tổ phó, công nhân cắt, người đã nghỉ, tài khoản bị khóa, và công nhân tổ may 2 để danh sách đủ dài
INSERT dbo.Employee (EmployeeCode, FullName, Gender, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode, StartDate, ResignDate, Status)
SELECT v.Code, v.FullName, v.Gender, v.RoleId, v.TeamId, v.PayType, v.Grade, v.AttCode, v.StartDate, v.ResignDate, v.Status
FROM (VALUES
 ('TPCAT',   N'Tổ phó tổ cắt',      'M', @rDeputy, @cat,  'MONTHLY',    NULL, 113, '2026-01-05', NULL, 'ACTIVE'),
 ('CNCAT2',  N'Đặng Văn Tài',       'M', @rWorker, @cat,  'DAILY',      NULL, 114, '2026-03-02', NULL, 'ACTIVE'),
 ('TPMAY2',  N'Ngô Thị Hạnh',       'F', @rDeputy, @may2, 'PIECE_RATE', 'B',  115, '2026-10-26', NULL, 'ACTIVE'),
 ('NVNGHI1', N'Lý Văn Sơn',         'M', @rWorker, NULL,  'PIECE_RATE', 'C',  116, '2025-06-01', '2026-09-30', 'RESIGNED'),
 ('NVKHOA1', N'Tạ Thị Yến',         'F', @rWorker, @may1, 'PIECE_RATE', 'C',  117, '2026-02-02', NULL, 'ACTIVE'),
 ('CNMAY9',  N'Dương Thị Ánh',      'F', @rWorker, @may2, 'PIECE_RATE', 'B',  118, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY10', N'Hồ Văn Khoa',        'M', @rWorker, @may2, 'PIECE_RATE', 'C',  119, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY11', N'Mai Thị Dung',       'F', @rWorker, @may2, 'PIECE_RATE', 'A',  120, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY12', N'Cao Văn Phúc',       'M', @rWorker, @may2, 'PIECE_RATE', 'C',  121, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY13', N'Đinh Thị Hiền',      'F', @rWorker, @may2, 'PIECE_RATE', 'B',  122, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY14', N'Tô Văn Đức',         'M', @rWorker, @may2, 'PIECE_RATE', 'C',  123, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY15', N'La Thị Nga',         'F', @rWorker, @may2, 'PIECE_RATE', 'B',  124, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY16', N'Kiều Văn Tín',       'M', @rWorker, @may2, 'PIECE_RATE', 'D',  125, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY17', N'Phan Thị Trang',     'F', @rWorker, @may2, 'PIECE_RATE', 'C',  126, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY18', N'Lưu Văn Quân',       'M', @rWorker, @may2, 'PIECE_RATE', 'B',  127, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY19', N'Trịnh Thị Oanh',     'F', @rWorker, @may2, 'PIECE_RATE', 'C',  128, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY20', N'Vương Văn Hải',      'M', @rWorker, @may2, 'PIECE_RATE', 'A',  129, '2026-10-26', NULL, 'ACTIVE'))
 v(Code, FullName, Gender, RoleId, TeamId, PayType, Grade, AttCode, StartDate, ResignDate, Status);
INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword, Status, LockedAt, LockReason)
SELECT e.EmployeeId, N'__SET_BY_BACKEND_SEED__', 1,
       IIF(e.EmployeeCode IN ('NVKHOA1', 'NVNGHI1'), 'LOCKED', 'ACTIVE'),
       IIF(e.EmployeeCode IN ('NVKHOA1', 'NVNGHI1'), CAST('2026-10-30 09:00:00' AS DATETIME2(0)), NULL),
       CASE e.EmployeeCode WHEN 'NVKHOA1' THEN N'Nhập sai mật khẩu nhiều lần' WHEN 'NVNGHI1' THEN N'Nhân viên đã nghỉ việc' END
FROM dbo.Employee e
WHERE NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

/* ---------- 2. Đơn hàng ở mọi trạng thái ---------- */
INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, StopReason, DelayRiskSince, Note, CreatedAt) VALUES
 ('DEMO-DH005', @cusB, '2026-10-30', '2026-12-15', 'AWAITING', 'NEW', NULL, NULL, N'Chờ khai báo NVL; mã hàng chưa có công đoạn', '2026-10-30 08:00:00'),
 ('DEMO-DH006', @cusB, '2026-10-28', '2026-12-10', 'COMPLETE', 'NEW', NULL, NULL, N'Khách đã cấp đủ NVL, sẵn sàng tạo lệnh cắt', '2026-10-28 08:00:00'),
 ('DEMO-DH007', @cusA, '2026-10-10', '2026-11-25', 'AWAITING', 'CANCELLED', NULL, NULL, N'Khách hủy đơn trước khi sản xuất', '2026-10-10 08:00:00'),
 ('DEMO-DH008', @cusB, '2026-10-12', '2026-12-01', 'COMPLETE', 'STOPPED', N'Khách tạm dừng để đổi mẫu', NULL, N'Đơn đang dừng', '2026-10-12 08:00:00'),
 ('DEMO-DH009', @cusB, '2026-10-20', '2026-11-06', 'COMPLETE', 'IN_DELIVERY', NULL, NULL, N'Đang giao, chờ Kế toán xác nhận khách nhận', '2026-10-20 08:00:00'),
 ('DEMO-DH010', @cusA, '2026-10-20', '2026-11-05', 'COMPLETE', 'PARTIALLY_DELIVERED', NULL, '2026-11-01', N'Đã giao một phần, còn hàng trong kho thành phẩm', '2026-10-20 08:30:00'),
 ('DEMO-DH011', @cusB, '2026-10-20', '2026-11-10', 'COMPLETE', 'DELIVERED', NULL, NULL, N'Đã giao đủ', '2026-10-20 09:00:00'),
 ('DEMO-DH012', @cusA, '2026-10-20', '2026-11-10', 'COMPLETE', 'COMPLETED', NULL, NULL, N'Đã hoàn tất', '2026-10-20 09:30:00'),
 ('DEMO-DH013', @cusB, '2026-10-25', '2026-11-25', 'PARTIAL', 'NEW', NULL, NULL, N'Nhà cung cấp giao thiếu vải, chờ Quản đốc quyết định', '2026-10-25 08:00:00');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT o.OrderId, pv.ProductVariantId, pv.ProductId, v.Qty, v.Price
FROM (VALUES
 ('DEMO-DH005', 'DEMO-SM03', N'Trắng', 'M', 200, 22000), ('DEMO-DH005', 'DEMO-SM03', N'Trắng', 'L', 150, 22000),
 ('DEMO-DH006', 'DEMO-QU02', N'Be', 'M', 100, 18000),    ('DEMO-DH006', 'DEMO-QU02', N'Be', 'L', 100, 18000),
 ('DEMO-DH007', 'DEMO-AO01', N'Đen', 'M', 50, 28000),
 ('DEMO-DH008', 'DEMO-AO01', N'Xanh', 'L', 80, 28000),
 ('DEMO-DH009', 'DEMO-QU02', N'Be', 'M', 60, 18000),
 ('DEMO-DH010', 'DEMO-QU02', N'Be', 'M', 100, 18000),    ('DEMO-DH010', 'DEMO-QU02', N'Be', 'L', 60, 18000),
 ('DEMO-DH011', 'DEMO-QU02', N'Be', 'M', 50, 18000),     ('DEMO-DH011', 'DEMO-QU02', N'Be', 'L', 50, 18000),
 ('DEMO-DH012', 'DEMO-QU02', N'Be', 'M', 40, 18000),
 ('DEMO-DH013', 'DEMO-QU02', N'Be', 'M', 50, 18000),     ('DEMO-DH013', 'DEMO-QU02', N'Be', 'L', 50, 18000))
 v(OrderCode, ProductCode, Color, SizeCode, Qty, Price)
JOIN dbo.CustomerOrder o ON o.OrderCode = v.OrderCode
JOIN dbo.Product p ON p.ProductCode = v.ProductCode
JOIN dbo.ProductVariant pv ON pv.ProductId = p.ProductId AND pv.Color = v.Color AND pv.SizeCode = v.SizeCode;

-- DEMO-DH005 đã chia cho tổ may 2: màn Phân công báo mã hàng chưa khai công đoạn
INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT l.OrderLineId, @may2 FROM dbo.OrderLine l JOIN dbo.CustomerOrder o ON o.OrderId = l.OrderId WHERE o.OrderCode = 'DEMO-DH005';

-- NVL của đơn: DH006 và các đơn đã giao do khách cấp; DH008, DH013 mua ngoài
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount)
SELECT o.OrderId, @mVaiBe, q.Qty, q.Qty, 1
FROM dbo.CustomerOrder o
CROSS APPLY (SELECT SUM(l.OrderedQty) Qty FROM dbo.OrderLine l WHERE l.OrderId = o.OrderId) q
WHERE o.OrderCode IN ('DEMO-DH006', 'DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount)
SELECT OrderId, @mChiBe, 20, 20, 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH006';
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty)
SELECT OrderId, @mVaiXanh, 100, 0 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH008';
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty)
SELECT OrderId, @mVaiBe, 100, 0 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH013';

-- Phiếu nhập NVL khách cấp đã xác nhận
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, ReceiptDate, Status, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT 'PN-DEMO-' + RIGHT(o.OrderCode, 3), o.OrderId, '2026-10-24', 'CONFIRMED', N'Khách cấp đủ một đợt', '2026-10-24 09:00:00', @quandoc, '2026-10-24 10:00:00'
FROM dbo.CustomerOrder o WHERE o.OrderCode IN ('DEMO-DH006', 'DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, AcceptedQty)
SELECT r.MaterialReceiptId, m.OrderMaterialId, m.CustomerQty
FROM dbo.MaterialReceipt r JOIN dbo.OrderMaterial m ON m.OrderId = r.OrderId
WHERE r.ReceiptCode IN ('PN-DEMO-006', 'PN-DEMO-009', 'PN-DEMO-010', 'PN-DEMO-011', 'PN-DEMO-012');

/* ---------- 3. Đơn đã giao DH009…012: đủ chuỗi cắt → may → QC → nhập kho → xuất (dùng tổ may 2, công nhân CNMAY5) ---------- */
-- Công đoạn cuối cũ của DEMO-QU02 đã ngừng dùng: đặt công đoạn đang dùng có thứ tự lớn nhất làm công đoạn cuối (Kết thúc đơn cần có)
DECLARE @opFinal INT = (SELECT TOP (1) OperationId FROM dbo.Operation WHERE ProductId = @pQu AND Status = 'ACTIVE' ORDER BY StationSequence DESC, OperationId DESC);
UPDATE dbo.Operation SET IsFinalStep = 0 WHERE ProductId = @pQu AND IsFinalStep = 1 AND OperationId <> @opFinal;
UPDATE dbo.Operation SET IsFinalStep = 1 WHERE OperationId = @opFinal;
-- Cần đơn giá đã duyệt thì mới ghi được sản lượng
IF NOT EXISTS (SELECT 1 FROM dbo.PieceRate WHERE OperationId = @opFinal AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-29')
    INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt)
    VALUES (@opFinal, 1500, '2026-10-01', 'APPROVED', '2026-09-28 09:00:00', '2026-09-29 09:00:00');
DECLARE @rateFinal INT = (SELECT TOP (1) PieceRateId FROM dbo.PieceRate
                          WHERE OperationId = @opFinal AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-29' ORDER BY EffectiveDate DESC);

SELECT o.OrderId, o.OrderCode, RIGHT(o.OrderCode, 3) AS N INTO #Ch
FROM dbo.CustomerOrder o WHERE o.OrderCode IN ('DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');

INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt, CompletedAt)
SELECT 'LC-DEMO-' + c.N, c.OrderId, @pQu, @cat, '2026-10-27', 'COMPLETED', N'Lệnh cắt đã hoàn thành', '2026-10-25 08:00:00', '2026-10-27 16:00:00' FROM #Ch c;
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WorkOrderId, l.OrderLineId, l.OrderedQty
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Cấp vải cho tổ cắt (1 m / sản phẩm)
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt)
SELECT 'PC-DEMO-1' + c.N, 'MATERIAL', c.OrderId, w.WorkOrderId, @cat, '2026-10-25', @thukho, 'RECEIVED', @ttcat, '2026-10-25 10:00:00', '2026-10-25 09:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, @mVaiBe, m.RequiredQty, m.RequiredQty
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-1' + c.N JOIN dbo.OrderMaterial m ON m.OrderId = c.OrderId;

-- Bàn cắt: 10 lớp × tỉ lệ (số lượng dòng / 10)
INSERT dbo.CuttingLay (WorkOrderId, CuttingTeamId, WorkDate, ShiftCode, LaySequence, FabricMaterialId, PlyCount, LayLengthM, Status, CreatedAt)
SELECT w.WorkOrderId, @cat, '2026-10-26', 'AFTERNOON', CAST(c.N AS SMALLINT), @mVaiBe, 10, 2.0, 'RECORDED', '2026-10-26 15:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.CuttingLayLine (CuttingLayId, OrderLineId, RatioValue, CalculatedQty, ActualQty)
SELECT y.CuttingLayId, l.OrderLineId, l.OrderedQty / 10, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N
JOIN dbo.CuttingLay y ON y.WorkOrderId = w.WorkOrderId JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, ConfirmedBy, ConfirmedAt)
SELECT 'BG-DEMO-' + c.N, w.WorkOrderId, '2026-10-27 15:00:00', 'CONFIRMED', @thukho, '2026-10-27 16:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.CutHandoverId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.CutHandover h ON h.HandoverCode = 'BG-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Cấp BTP cho tổ may 2
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt)
SELECT 'PC-DEMO-2' + c.N, 'SEMI_FINISHED', c.OrderId, @may2, '2026-10-28', @thukho, 'RECEIVED', @ttmay2, '2026-10-28 08:00:00', '2026-10-28 07:30:00' FROM #Ch c;
INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-2' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT l.OrderLineId, @may2 FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
SELECT @may2, c.OrderId, @opFinal, @w5, '2026-10-28', '2026-10-31', '2026-10-28 08:00:00' FROM #Ch c;
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt)
SELECT 'HANDOVER', @may2, @w5, l.OrderLineId, @opFinal, l.OrderedQty, l.OrderedQty, 'WORKER', 'CONFIRMED', '2026-10-28 08:10:00', '2026-10-28 08:30:00'
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId, IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT '2026-10-29', 'AFTERNOON', @may2, @w5, @opFinal, l.OrderLineId, l.OrderedQty, @rateFinal, 0, 0, dbo.fn_NextWorkingDayEnd('2026-10-29'), 'SAVED'
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline)
SELECT '2026-10-30', 'MORNING', '09:00:00', @may2, l.OrderLineId, l.OrderedQty, @qaqc, dbo.fn_NextWorkingDayEnd('2026-10-30')
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt)
SELECT 'TP-DEMO-' + c.N, c.OrderId, @pQu, @qaqc, '2026-10-30 14:00:00', 'CONFIRMED', @thukho, '2026-10-30 15:00:00' FROM #Ch c;
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.FgHandoverId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.FgHandover h ON h.HandoverCode = 'TP-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Phiếu xuất đã xuất kho: DH009 đang giao; DH010 giao một phần (khách nhận thiếu 5 cái); DH011, DH012 giao đủ
INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, DispatchedBy, DispatchedAt, CustomerReceivedDate)
SELECT 'XH-DEMO-' + c.N, c.OrderId, v.DeliveryDate, N'Kho khách hàng, KCN Tân Bình, TP.HCM', v.Note, v.Status, '2026-10-30 16:00:00', @thukho, '2026-10-31 08:00:00', v.ReceivedDate
FROM #Ch c
JOIN (VALUES ('009', CAST('2026-11-03' AS DATE), N'Đang trên đường giao', 'IN_DELIVERY', CAST(NULL AS DATE)),
             ('010', '2026-10-31', N'Khách nhận thiếu 5 cái size M', 'PARTIALLY_DELIVERED', '2026-10-31'),
             ('011', '2026-10-31', NULL, 'DELIVERED', '2026-10-31'),
             ('012', '2026-10-31', NULL, 'DELIVERED', '2026-10-31')) v(N, DeliveryDate, Note, Status, ReceivedDate) ON v.N = c.N;
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty, ReceivedQty)
SELECT d.DispatchNoteId, l.OrderLineId,
       IIF(c.N = '010', IIF(pv.SizeCode = 'M', 60, 40), l.OrderedQty),
       CASE c.N WHEN '009' THEN NULL WHEN '010' THEN IIF(pv.SizeCode = 'M', 55, 40) ELSE l.OrderedQty END
FROM #Ch c JOIN dbo.DispatchNote d ON d.DispatchCode = 'XH-DEMO-' + c.N
JOIN dbo.OrderLine l ON l.OrderId = c.OrderId JOIN dbo.ProductVariant pv ON pv.ProductVariantId = l.ProductVariantId;

-- DH010 còn hàng trong kho: phiếu xuất chờ Thủ kho, bị từ chối, đã hủy; bàn giao thành phẩm bị từ chối, đã hủy
DECLARE @o10 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH010'),
        @l10M INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.CustomerOrder o ON o.OrderId = l.OrderId JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId
                     WHERE o.OrderCode = 'DEMO-DH010' AND v.SizeCode = 'M');
INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, RejectReason) VALUES
 ('XH-DEMO-210', @o10, '2026-11-04', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Giao nốt size M', 'PENDING', '2026-11-01 09:00:00', NULL),
 ('XH-DEMO-310', @o10, '2026-11-02', N'Kho khách hàng, KCN Tân Bình, TP.HCM', NULL, 'REJECTED', '2026-10-31 09:00:00', N'Sai địa chỉ giao hàng'),
 ('XH-DEMO-410', @o10, '2026-11-02', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Khách đổi lịch', 'CANCELLED', '2026-10-31 10:00:00', NULL);
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty)
SELECT DispatchNoteId, @l10M, 20 FROM dbo.DispatchNote WHERE DispatchCode IN ('XH-DEMO-210', 'XH-DEMO-310', 'XH-DEMO-410');
INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, RejectReason) VALUES
 ('TP-DEMO-210', @o10, @pQu, @qaqc, '2026-10-30 11:00:00', 'REJECTED', N'Đếm thiếu so với phiếu'),
 ('TP-DEMO-310', @o10, @pQu, @qaqc, '2026-10-30 12:00:00', 'CANCELLED', NULL);
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty)
SELECT FgHandoverId, @l10M, 5 FROM dbo.FgHandover WHERE HandoverCode IN ('TP-DEMO-210', 'TP-DEMO-310');

-- KPI đã chốt: đạt và không đạt
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result, CreatedAt)
SELECT c.OrderId, @pQu, @may2, '2026-10-28', '2026-10-31', IIF(c.N = '011', 100, 60), 4, 'CLOSED', IIF(c.N = '011', 'ACHIEVED', 'NOT_ACHIEVED'), '2026-10-27 16:00:00'
FROM #Ch c WHERE c.N IN ('011', '012');

-- Chuyến xe giao hàng của DH009
DECLARE @veh INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51D-678.90');
DECLARE @km INT = ISNULL((SELECT MAX(ISNULL(OdometerEnd, OdometerStart)) FROM dbo.VehicleTrip WHERE VehicleId = @veh), 10000);
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverEmployeeId, Purpose, DispatchNoteId, OdometerStart, OdometerEnd, FuelLiters, FuelCost, Status)
SELECT @veh, '2026-10-31', @codien, N'Giao hàng DEMO-DH009', DispatchNoteId, @km, @km + 40, 5, 110000, 'COMPLETED'
FROM dbo.DispatchNote WHERE DispatchCode = 'XH-DEMO-009';

/* ---------- 4. Mua hàng: đã nhận đủ, chốt thiếu, thiếu chờ quyết định, phiếu nhập bị trả ---------- */
DECLARE @o8 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH008'),
        @o13 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH013');
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, ExpectedDeliveryCount, GoodsStatus, PaymentStatus, PaymentDate, PaidAmount, PaymentMethod, PaymentNote, Note, CreatedAt) VALUES
 ('DM-DEMO-013', @o13, @sup1, '2026-10-25', 1, 'PARTIALLY_RECEIVED', 'UNPAID', NULL, NULL, NULL, NULL, N'NCC giao thiếu 30 m', '2026-10-25 09:00:00'),
 ('DM-DEMO-014', @o8, @sup1, '2026-10-13', 1, 'RECEIVED', 'PAID', '2026-10-20', 4500000, 'BANK_TRANSFER', N'Chuyển khoản đợt 1', N'Đã nhận đủ và thanh toán', '2026-10-13 09:00:00'),
 ('DM-DEMO-015', NULL, @sup3, '2026-10-05', 1, 'CLOSED_SHORT', 'PAID', '2026-10-15', 1500000, 'CASH', NULL, N'Mua kim dùng chung, chốt thiếu 5 hộp', '2026-10-05 09:00:00');
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice)
SELECT p.PurchaseOrderId, m.OrderMaterialId, 100, v.Price
FROM (VALUES ('DM-DEMO-013', @o13, 52000), ('DM-DEMO-014', @o8, 45000)) v(Code, OrderId, Price)
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Code
JOIN dbo.OrderMaterial m ON m.OrderId = v.OrderId;
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, MaterialId, OrderedQty, NotReplenishedQty, UnitPrice)
SELECT PurchaseOrderId, @mKim, 20, 5, 100000 FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-015';

INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, ReturnReason, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT v.Code, p.OrderId, p.PurchaseOrderId, v.D, v.Status, v.Reason, v.Note, v.SubmittedAt, IIF(v.Status = 'CONFIRMED', @quandoc, NULL), IIF(v.Status = 'CONFIRMED', v.ConfirmedAt, NULL)
FROM (VALUES
 ('PN-DEMO-013', 'DM-DEMO-013', CAST('2026-10-28' AS DATE), 'CONFIRMED', CAST(NULL AS NVARCHAR(500)), N'Đợt giao duy nhất, thiếu 30 m', CAST('2026-10-28 09:00:00' AS DATETIME2(0)), CAST('2026-10-28 10:00:00' AS DATETIME2(0))),
 ('PN-DEMO-014', 'DM-DEMO-014', '2026-10-16', 'CONFIRMED', NULL, NULL, '2026-10-16 09:00:00', '2026-10-16 10:00:00'),
 ('PN-DEMO-015', 'DM-DEMO-015', '2026-10-10', 'CONFIRMED', NULL, N'Thiếu 5 hộp, không bù', '2026-10-10 09:00:00', '2026-10-10 10:00:00'),
 ('PN-DEMO-016', 'DM-DEMO-003', '2026-10-29', 'RETURNED', N'Sai màu vải, trả lại Thủ kho lập lại', NULL, '2026-10-29 09:00:00', NULL)) v(Code, Po, D, Status, Reason, Note, SubmittedAt, ConfirmedAt)
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Po;
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty, MaterialId)
SELECT r.MaterialReceiptId, pl.OrderMaterialId, pl.PurchaseOrderLineId, v.Qty, pl.MaterialId
FROM (VALUES ('PN-DEMO-013', 'DM-DEMO-013', 70), ('PN-DEMO-014', 'DM-DEMO-014', 100), ('PN-DEMO-015', 'DM-DEMO-015', 15), ('PN-DEMO-016', 'DM-DEMO-003', 200)) v(Code, Po, Qty)
JOIN dbo.MaterialReceipt r ON r.ReceiptCode = v.Code
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Po
CROSS APPLY (SELECT TOP (1) * FROM dbo.PurchaseOrderLine x WHERE x.PurchaseOrderId = p.PurchaseOrderId ORDER BY x.PurchaseOrderLineId) pl;

/* ---------- 5. Phiếu cấp, trả vải, bàn giao BTP, lệnh cắt ở các trạng thái còn thiếu (lệnh LC-DEMO-002, đơn DH013) ---------- */
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, RejectReason, CreatedAt)
VALUES ('PC-DEMO-901', 'MATERIAL', @o3, @wo2, @cat, '2026-10-27', @thukho, 'REJECTED', N'Vải bị bẩn, tổ cắt từ chối nhận', '2026-10-27 08:00:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty)
SELECT MaterialIssueId, @mVaiDen, 30 FROM dbo.MaterialIssue WHERE IssueCode = 'PC-DEMO-901';

DECLARE @il3 INT = (SELECT l.MaterialIssueLineId FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId WHERE i.IssueCode = 'PC-DEMO-003');
INSERT dbo.MaterialReturn (ReturnCode, MaterialIssueId, Note, Status, ConfirmedBy, ConfirmedAt, RejectReason, CreatedAt)
SELECT v.Code, l.MaterialIssueId, v.Note, v.Status, IIF(v.Status = 'CONFIRMED', @thukho, NULL), IIF(v.Status = 'CONFIRMED', CAST('2026-10-27 16:30:00' AS DATETIME2(0)), NULL), v.Reason, '2026-10-27 16:00:00'
FROM (VALUES ('TV-DEMO-002', N'Vải thừa bàn cắt 2', 'CONFIRMED', CAST(NULL AS NVARCHAR(500))),
             ('TV-DEMO-003', N'Vải đầu tấm', 'REJECTED', N'Vải rách, không nhận lại kho'),
             ('TV-DEMO-004', N'Tổ cắt rút phiếu', 'CANCELLED', NULL)) v(Code, Note, Status, Reason)
JOIN dbo.MaterialIssueLine l ON l.MaterialIssueLineId = @il3;
INSERT dbo.MaterialReturnLine (MaterialReturnId, MaterialIssueLineId, ReturnedQty, ConfirmedQty)
SELECT r.MaterialReturnId, @il3, v.Qty, IIF(r.Status = 'CONFIRMED', v.Qty, NULL)
FROM (VALUES ('TV-DEMO-002', 5), ('TV-DEMO-003', 3), ('TV-DEMO-004', 2)) v(Code, Qty)
JOIN dbo.MaterialReturn r ON r.ReturnCode = v.Code;

INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, RejectReason) VALUES
 ('BG-DEMO-901', @wo2, '2026-10-27 15:00:00', 'REJECTED', N'Số bó không khớp phiếu'),
 ('BG-DEMO-902', @wo2, '2026-10-27 15:30:00', 'CANCELLED', NULL);
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty)
SELECT h.CutHandoverId, l.OrderLineId, 5 FROM dbo.CutHandover h JOIN dbo.OrderLine l ON l.OrderId = @o3 AND l.ProductId = @pAo
WHERE h.HandoverCode IN ('BG-DEMO-901', 'BG-DEMO-902');

INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt) VALUES
 ('LC-DEMO-013', @o13, @pQu, @cat, '2026-11-10', 'OPEN', N'Lệnh mới, tổ cắt chưa bắt đầu', '2026-10-30 08:00:00'),
 ('LC-DEMO-113', @o13, @pQu, @cat, '2026-11-08', 'CANCELLED', N'Lập nhầm, đã hủy', '2026-10-29 08:00:00');
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WorkOrderId, l.OrderLineId, l.OrderedQty FROM dbo.WorkOrder w JOIN dbo.OrderLine l ON l.OrderId = w.OrderId
WHERE w.WorkOrderCode IN ('LC-DEMO-013', 'LC-DEMO-113');

-- Giao việc: tổ trưởng xác nhận hộ, công nhân từ chối; phân công cũ đã đóng (giữ lịch sử)
DECLARE @op6 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP06'),
        @op7 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP07'),
        @op1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP01'),
        @op2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP02'),
        @l3M INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId WHERE l.OrderId = @o3 AND l.ProductId = @pAo AND v.SizeCode = 'M'),
        @l3L INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId WHERE l.OrderId = @o3 AND l.ProductId = @pAo AND v.SizeCode = 'L');
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, OnBehalfReason, Status, RejectReason, CreatedAt, ConfirmedAt) VALUES
 ('HANDOVER', @may1, @w2, @l3L, @op6, 5, 5, 'ON_BEHALF', N'Công nhân nghỉ phép, tổ trưởng xác nhận hộ', 'CONFIRMED', NULL, '2026-10-30 08:00:00', '2026-10-30 08:30:00'),
 ('HANDOVER', @may1, @w3, @l3L, @op7, 5, NULL, 'WORKER', NULL, 'REJECTED', N'Không đúng công đoạn của tôi', '2026-10-30 09:00:00', NULL);
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
VALUES (@may1, @o3, @op6, @w4, '2026-10-25', '2026-10-28', '2026-10-25 08:00:00');

-- Hàng hỏng: phiếu đang bị công nhân phản đối; phiếu hủy còn trống để Tổ trưởng may tạo đơn cắt bù trên giao diện
DECLARE @rateOp2 INT = (SELECT TOP (1) PieceRateId FROM dbo.PieceRate WHERE OperationId = @op2 AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-30' ORDER BY EffectiveDate DESC);
INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity, DefectReasonCode, ReasonNote, PieceRateId,
                         WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt) VALUES
 ('MAY1-261102-901', '2026-11-02', 'MORNING', @may1, @l3M, @op1, @w1, 'REWORK', 2, 'CROOKED_SEAM', NULL, NULL,
  '2026-11-02 09:00:00', N'Lỗi do máy, không phải do tôi', '2026-11-02 09:30:00', dbo.fn_NextWorkingDayEnd('2026-11-02'), 'RECORDED', @ttmay, '2026-11-02 09:00:00'),
 ('MAY1-261030-901', '2026-10-30', 'AFTERNOON', @may1, @l3L, @op2, @w1, 'SCRAP', 2, 'OTHER', N'Rách vải khi may', @rateOp2,
  '2026-10-30 15:00:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-30'), 'CONFIRMED', @ttmay, '2026-10-30 15:00:00');

/* ---------- 6. Đơn từ: đủ 11 loại, đủ 6 trạng thái ---------- */
DECLARE @d3 INT = (SELECT DefectRecordId FROM dbo.DefectRecord WHERE DefectCode = 'MAY1-261015-001'),
        @d4 INT = (SELECT DefectRecordId FROM dbo.DefectRecord WHERE DefectCode = 'MAY1-261016-001');
DECLARE @r13 INT = (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-013'),
        @r15 INT = (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-015');

INSERT dbo.Request (RequestCode, RequestTypeCode, Status, OrderId, WorkOrderId, MaterialReceiptId, DefectRecordId, TargetDate, Title, Content, CreatedBy, CreatedByTeamId,
                    DecidedAt, DecisionNote, ResultNote, CompletedAt, ShortageResolution, CreatedAt) VALUES
 ('YC-DEMO-003', 'SPARE_PARTS', 'PENDING_APPROVAL', NULL, NULL, NULL, NULL, '2026-11-05', N'Thay dây curoa máy 1 kim', N'Máy số 12 tổ may 1 trượt dây', @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-11-01 08:00:00'),
 ('YC-DEMO-004', 'NEW_MACHINE', 'APPROVED', NULL, NULL, NULL, NULL, '2026-11-20', N'Mua thêm máy vắt sổ', N'Tổ may 2 thiếu máy', @codien, NULL, '2026-10-29 10:00:00', N'Đồng ý, mua trong tháng 11', NULL, NULL, NULL, '2026-10-28 08:00:00'),
 ('YC-DEMO-005', 'MACHINE_SUPPLIES', 'PENDING_PROCESSING', NULL, NULL, NULL, NULL, '2026-11-04', N'Xin dầu máy may', NULL, @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-11-01 08:30:00'),
 ('YC-DEMO-006', 'MACHINE_SUPPLIES', 'COMPLETED', NULL, NULL, NULL, NULL, '2026-10-20', N'Xin kim máy', NULL, @codien, NULL, NULL, NULL, N'Đã cấp đủ 2 hộp', '2026-10-19 15:00:00', NULL, '2026-10-18 08:00:00'),
 ('YC-DEMO-007', 'SPARE_PARTS', 'REJECTED', NULL, NULL, NULL, NULL, '2026-10-25', N'Thay bàn là hơi', NULL, @codien, NULL, '2026-10-22 09:00:00', N'Bàn là còn dùng được, sửa thay vì mua mới', NULL, NULL, NULL, '2026-10-21 08:00:00'),
 ('YC-DEMO-008', 'NEW_MACHINE', 'CANCELLED', NULL, NULL, NULL, NULL, '2026-10-30', N'Mua máy đính cúc', NULL, @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-20 08:00:00'),
 ('YC-DEMO-009', 'MATERIAL_SUPPLEMENT', 'PENDING_APPROVAL', @o3, NULL, NULL, NULL, '2026-11-06', N'Bổ sung khóa kéo', N'Khóa kéo lỗi nhiều hơn dự kiến', @ttmay, @may1, NULL, NULL, NULL, NULL, NULL, '2026-11-01 09:00:00'),
 ('YC-DEMO-010', 'MATERIAL_SUPPLEMENT', 'APPROVED', @o3, NULL, NULL, NULL, '2026-11-03', N'Bổ sung nhãn dệt', NULL, @thukho, NULL, '2026-10-31 10:00:00', N'Đồng ý', NULL, NULL, NULL, '2026-10-30 09:00:00'),
 ('YC-DEMO-011', 'DEFECTIVE_MATERIAL', 'PENDING_APPROVAL', @o3, @wo2, NULL, NULL, '2026-11-03', N'Vải đen bị lỗi dệt (kho đủ để cấp bù)', N'Duyệt đơn này: hệ thống tự tạo đơn Xin NVL', @ttcat, @cat, NULL, NULL, NULL, NULL, NULL, '2026-11-01 10:00:00'),
 ('YC-DEMO-012', 'DEFECTIVE_MATERIAL', 'PENDING_APPROVAL', @o1, NULL, NULL, NULL, '2026-11-03', N'Vải trắng bị ố (kho thiếu)', N'Duyệt đơn này: hệ thống tự tạo đơn Mua NVL cho phần thiếu', @ttcat, @cat, NULL, NULL, NULL, NULL, NULL, '2026-11-01 10:30:00'),
 ('YC-DEMO-013', 'ATTENDANCE_CORRECTION', 'REJECTED', NULL, NULL, NULL, NULL, '2026-10-22', N'Chấm công bù ngày 22/10', N'Quên quẹt thẻ', @ttmay, @may1, '2026-10-24 09:00:00', N'Không có xác nhận của tổ', NULL, NULL, NULL, '2026-10-23 08:00:00'),
 ('YC-DEMO-014', 'REPLACEMENT_CUTTING', 'PENDING_APPROVAL', @o1, NULL, NULL, @d3, '2026-11-05', N'Cắt bù thân trước', N'Theo phiếu hàng hủy', @ttmay, @may1, NULL, NULL, NULL, NULL, NULL, '2026-11-01 11:00:00'),
 ('YC-DEMO-015', 'REPLACEMENT_CUTTING', 'APPROVED', @o1, NULL, NULL, @d4, '2026-11-04', N'Cắt bù thân trước size L', N'Đã duyệt, chờ lập lệnh cắt bù', @ttmay, @may1, '2026-10-31 15:00:00', N'Đồng ý', NULL, NULL, NULL, '2026-10-31 08:00:00'),
 ('YC-DEMO-016', 'SHORT_DELIVERY', 'PENDING_PROCESSING', @o13, NULL, @r13, NULL, NULL, N'Thiếu NVL — phiếu PN-DEMO-013', N'Hệ thống tạo khi phiếu nhập còn thiếu', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-28 10:00:00'),
 ('YC-DEMO-017', 'SHORT_DELIVERY', 'COMPLETED', NULL, NULL, @r15, NULL, NULL, N'Thiếu NVL — phiếu PN-DEMO-015', N'Hệ thống tạo khi phiếu nhập còn thiếu', NULL, NULL, '2026-10-11 09:00:00', N'Không cần bù, kim còn đủ dùng', N'Chốt không bù', '2026-10-11 09:00:00', 'NO_REPLENISH', '2026-10-10 10:00:00'),
 ('YC-DEMO-018', 'MATERIAL_PURCHASE', 'PENDING_PROCESSING', @o1, NULL, NULL, NULL, NULL, N'Mua NVL bù hàng lỗi', N'Tồn kho không đủ, còn thiếu vải trắng. Vui lòng tạo đơn mua NVL.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-30 10:00:00'),
 ('YC-DEMO-019', 'MATERIAL_PURCHASE', 'COMPLETED', @o1, NULL, NULL, NULL, NULL, N'Mua NVL bù hàng lỗi (đã tạo đơn mua)', N'Tồn kho không đủ, còn thiếu vải trắng.', NULL, NULL, NULL, NULL, N'Đã tạo đơn mua DM-DEMO-016', '2026-10-27 09:00:00', NULL, '2026-10-26 10:00:00'),
 ('YC-DEMO-020', 'MATERIAL_REQUISITION', 'COMPLETED', @o10, NULL, NULL, NULL, '2026-10-25', N'Xin vải cho DEMO-DH010', NULL, @ttcat, @cat, NULL, NULL, N'Đã cấp theo PC-DEMO-110', '2026-10-25 09:00:00', NULL, '2026-10-24 15:00:00'),
 ('YC-DEMO-021', 'SEMI_FINISHED_REQUISITION', 'REJECTED', @o3, NULL, NULL, NULL, '2026-10-30', N'Xin BTP size M', NULL, @ttmay, @may1, '2026-10-29 09:00:00', N'Kho BTP chưa có hàng', NULL, NULL, NULL, '2026-10-28 15:00:00');

UPDATE i SET i.RequestId = r.RequestId
FROM dbo.MaterialIssue i JOIN dbo.Request r ON r.RequestCode = 'YC-DEMO-020' WHERE i.IssueCode = 'PC-DEMO-110';

INSERT dbo.RequestLine (RequestId, MaterialId, OrderLineId, ProductComponentMaterialId, ItemDescription, Quantity, ReplenishQty, MaterialReceiptLineId)
SELECT r.RequestId, v.MaterialId, v.OrderLineId, v.Pcm, v.Item, v.Qty, v.Replenish, v.ReceiptLine
FROM (VALUES
 ('YC-DEMO-003', CAST(NULL AS INT), CAST(NULL AS INT), CAST(NULL AS INT), N'Dây curoa máy 1 kim', CAST(5 AS DECIMAL(18,3)), CAST(NULL AS DECIMAL(18,3)), CAST(NULL AS INT)),
 ('YC-DEMO-004', NULL, NULL, NULL, N'Máy vắt sổ 5 chỉ', 1, NULL, NULL),
 ('YC-DEMO-005', NULL, NULL, NULL, N'Dầu máy may (lít)', 10, NULL, NULL),
 ('YC-DEMO-006', @mKim, NULL, NULL, NULL, 2, NULL, NULL),
 ('YC-DEMO-007', NULL, NULL, NULL, N'Bàn là hơi', 1, NULL, NULL),
 ('YC-DEMO-008', NULL, NULL, NULL, N'Máy đính cúc', 1, NULL, NULL),
 ('YC-DEMO-009', @mKhoa, NULL, NULL, NULL, 50, NULL, NULL),
 ('YC-DEMO-010', @mNhan, NULL, NULL, NULL, 30, NULL, NULL),
 ('YC-DEMO-011', @mVaiDen, NULL, NULL, NULL, 20, NULL, NULL),
 ('YC-DEMO-012', @mVaiTrang, NULL, NULL, NULL, 1000, NULL, NULL),
 ('YC-DEMO-018', @mVaiTrang, NULL, NULL, NULL, 200, NULL, NULL),
 ('YC-DEMO-019', @mVaiTrang, NULL, NULL, NULL, 100, NULL, NULL),
 ('YC-DEMO-020', @mVaiBe, NULL, NULL, NULL, 160, NULL, NULL),
 ('YC-DEMO-021', NULL, @l3M, NULL, NULL, 10, NULL, NULL)) v(Code, MaterialId, OrderLineId, Pcm, Item, Qty, Replenish, ReceiptLine)
JOIN dbo.Request r ON r.RequestCode = v.Code;

-- Cắt bù: dòng là chi tiết vải "Thân trước" đúng màu × size của phiếu hàng hủy
INSERT dbo.RequestLine (RequestId, OrderLineId, ProductComponentMaterialId, Quantity)
SELECT r.RequestId, d.OrderLineId, pcm.ProductComponentMaterialId, 1
FROM dbo.Request r
JOIN dbo.DefectRecord d ON d.DefectRecordId = r.DefectRecordId
JOIN dbo.OrderLine l ON l.OrderLineId = d.OrderLineId
JOIN dbo.ProductComponent pc ON pc.ProductId = l.ProductId AND pc.ComponentName = N'Thân trước'
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentId = pc.ProductComponentId AND pcm.ProductVariantId = l.ProductVariantId
WHERE r.RequestCode IN ('YC-DEMO-014', 'YC-DEMO-015');

-- Thiếu hàng khi nhập: dòng trỏ về dòng phiếu nhập gốc
INSERT dbo.RequestLine (RequestId, MaterialId, Quantity, ReplenishQty, MaterialReceiptLineId)
SELECT q.RequestId, COALESCE(m.MaterialId, rl.MaterialId), v.Qty, v.Replenish, rl.MaterialReceiptLineId
FROM (VALUES ('YC-DEMO-016', 30, CAST(NULL AS DECIMAL(18,3))), ('YC-DEMO-017', 5, 0)) v(Code, Qty, Replenish)
JOIN dbo.Request q ON q.RequestCode = v.Code
JOIN dbo.MaterialReceiptLine rl ON rl.MaterialReceiptId = q.MaterialReceiptId
LEFT JOIN dbo.OrderMaterial m ON m.OrderMaterialId = rl.OrderMaterialId;

-- Đơn mua sinh từ đơn Mua NVL, chờ Quản đốc chốt nhà cung cấp
INSERT dbo.PurchaseOrder (PoCode, OrderId, SourceRequestId, GoodsStatus, PaymentStatus, Note, CreatedAt)
SELECT 'DM-DEMO-016', @o1, RequestId, 'PENDING_FINALISATION', 'UNPAID', N'Tạo từ đơn Mua NVL YC-DEMO-019', '2026-10-27 09:00:00' FROM dbo.Request WHERE RequestCode = 'YC-DEMO-019';
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice)
SELECT p.PurchaseOrderId, m.OrderMaterialId, 100, 0
FROM dbo.PurchaseOrder p JOIN dbo.OrderMaterial m ON m.OrderId = @o1 AND m.MaterialId = @mVaiTrang WHERE p.PoCode = 'DM-DEMO-016';

/* ---------- 7. Ghi kho qua usp_Stock_Post, theo thứ tự thời gian ---------- */
CREATE TABLE #Mv (Seq INT IDENTITY PRIMARY KEY, WarehouseType VARCHAR(15), MaterialId INT, OrderLineId INT, DocType VARCHAR(30), DocId INT, LineId INT, Qty DECIMAL(18,3), TxDate DATE);

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', COALESCE(m.MaterialId, rl.MaterialId), NULL, 'MATERIAL_RECEIPT', r.MaterialReceiptId, rl.MaterialReceiptLineId, rl.AcceptedQty, r.ReceiptDate
FROM dbo.MaterialReceipt r
JOIN dbo.MaterialReceiptLine rl ON rl.MaterialReceiptId = r.MaterialReceiptId
LEFT JOIN dbo.OrderMaterial m ON m.OrderMaterialId = rl.OrderMaterialId
WHERE r.ReceiptCode IN ('PN-DEMO-006', 'PN-DEMO-009', 'PN-DEMO-010', 'PN-DEMO-011', 'PN-DEMO-012', 'PN-DEMO-013', 'PN-DEMO-014', 'PN-DEMO-015')
ORDER BY r.ReceiptDate, r.MaterialReceiptId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', il.MaterialId, NULL, 'MATERIAL_RETURN', r.MaterialReturnId, rl.MaterialReturnLineId, rl.ConfirmedQty, '2026-10-27'
FROM dbo.MaterialReturn r JOIN dbo.MaterialReturnLine rl ON rl.MaterialReturnId = r.MaterialReturnId
JOIN dbo.MaterialIssueLine il ON il.MaterialIssueLineId = rl.MaterialIssueLineId WHERE r.ReturnCode = 'TV-DEMO-002';

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', l.MaterialId, NULL, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-1' + c.N JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', NULL, l.OrderLineId, 'CUT_HANDOVER', h.CutHandoverId, l.CutHandoverLineId, l.ConfirmedQty, '2026-10-27'
FROM #Ch c JOIN dbo.CutHandover h ON h.HandoverCode = 'BG-DEMO-' + c.N JOIN dbo.CutHandoverLine l ON l.CutHandoverId = h.CutHandoverId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', NULL, l.OrderLineId, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-2' + c.N JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', NULL, l.OrderLineId, 'FG_HANDOVER', h.FgHandoverId, l.FgHandoverLineId, l.ConfirmedQty, '2026-10-30'
FROM #Ch c JOIN dbo.FgHandover h ON h.HandoverCode = 'TP-DEMO-' + c.N JOIN dbo.FgHandoverLine l ON l.FgHandoverId = h.FgHandoverId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', NULL, l.OrderLineId, 'DISPATCH_NOTE', d.DispatchNoteId, l.DispatchNoteLineId, -l.IssueQty, '2026-10-31'
FROM #Ch c JOIN dbo.DispatchNote d ON d.DispatchCode = 'XH-DEMO-' + c.N JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId;

CREATE TABLE #Posted (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @seq INT = 0, @wh VARCHAR(15), @mid INT, @olid INT, @dt VARCHAR(30), @did INT, @lid INT, @qty DECIMAL(18,3), @tx DATE;
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @seq = Seq, @wh = WarehouseType, @mid = MaterialId, @olid = OrderLineId, @dt = DocType, @did = DocId,
                   @lid = LineId, @qty = Qty, @tx = TxDate
    FROM #Mv WHERE Seq > @seq ORDER BY Seq;
    IF @@ROWCOUNT = 0 BREAK;
    INSERT #Posted EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @OrderLineId = @olid,
        @SourceDocType = @dt, @SourceDocId = @did, @SourceLineId = @lid, @QtyChange = @qty, @TransactionDate = @tx;
END;
DROP TABLE #Mv, #Posted;

-- Kiểm kê (ghi sau khi đã có tồn): phiếu bị trả lại; phiếu kho BTP và kho thành phẩm chờ duyệt
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, ReturnReason, CreatedAt) VALUES
 ('KK-DEMO-003', 'MATERIAL', N'Kiểm kê kim máy', '2026-10-31', 'RETURNED', N'Số đếm chưa khớp sổ tay, đếm lại', '2026-10-31 16:00:00'),
 ('KK-DEMO-004', 'SEMI_FINISHED', N'Kiểm kê BTP cuối tháng', '2026-10-31', 'PENDING_APPROVAL', NULL, '2026-10-31 16:30:00'),
 ('KK-DEMO-005', 'FINISHED', N'Kiểm kê thành phẩm cuối tháng', '2026-10-31', 'PENDING_APPROVAL', NULL, '2026-10-31 17:00:00');
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT s.StocktakeId, b.StockBalanceId, b.OnHandQty, b.OnHandQty + v.Diff, v.Note
FROM (VALUES ('KK-DEMO-003', 'MATERIAL', @mKim, CAST(NULL AS INT), -1, N'Thiếu 1 hộp'),
             ('KK-DEMO-004', 'SEMI_FINISHED', NULL, @l3M, -2, N'Lệch 2 bộ'),
             ('KK-DEMO-005', 'FINISHED', NULL, @l10M, 0, N'Khớp sổ')) v(Code, Wh, MaterialId, OrderLineId, Diff, Note)
JOIN dbo.Stocktake s ON s.StocktakeCode = v.Code
JOIN dbo.StockBalance b ON b.WarehouseType = v.Wh AND b.ProductComponentMaterialId IS NULL
 AND EXISTS (SELECT b.MaterialId, b.OrderLineId INTERSECT SELECT v.MaterialId, v.OrderLineId);

/* ---------- 8. Chấm công: lượt quẹt ngày 02/11 (đủ giờ, thiếu giờ ra, quẹt trùng, mã chưa gán nhân viên), phiên đồng bộ, chấm hộ ---------- */
DECLARE @punch dbo.AttendancePunchTableType;
INSERT @punch (SourceRecordId, AttendanceCode, PunchTime)
SELECT 900000 + ROW_NUMBER() OVER (ORDER BY v.Code, v.T), v.Code, v.T
FROM (VALUES (104, CAST('2026-11-02 07:25:00' AS DATETIME2(0))), (104, '2026-11-02 16:35:00'),
             (105, '2026-11-02 07:28:00'), (105, '2026-11-02 18:00:00'),
             (106, '2026-11-02 07:40:00'), (106, '2026-11-02 16:30:00'),
             (107, '2026-11-02 07:20:00'), (107, '2026-11-02 16:32:00'),
             (108, '2026-11-02 07:27:00'),
             (111, '2026-11-02 07:26:00'), (111, '2026-11-02 07:29:00'), (111, '2026-11-02 16:31:00'),
             (999, '2026-11-02 07:30:00'), (999, '2026-11-02 16:30:00')) v(Code, T);
EXEC dbo.usp_Attendance_IngestBatch @Rows = @punch, @Source = 'RJ_SYNC';
DELETE @punch;
INSERT @punch (SourceRecordId, AttendanceCode, PunchTime) VALUES (NULL, 112, '2026-11-02 07:30:00'), (NULL, 112, '2026-11-02 16:30:00');
EXEC dbo.usp_Attendance_IngestBatch @Rows = @punch, @Source = 'EXCEL_IMPORT', @FileName = N'cham-cong-02-11-demo.xlsx';
INSERT dbo.AttendanceSyncSession (Source, StartedAt, FinishedAt, RecordsReceived, RecordsInserted, Status, ErrorMessage)
VALUES ('RJ_SYNC', '2026-11-01 06:00:00', '2026-11-01 06:00:30', 0, 0, 'FAILED', N'Không kết nối được máy chấm công');

EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w6, @WorkDate = '2026-11-02', @AdjustmentType = 'FINGERPRINT_FAIL',
     @Reason = N'Máy không nhận vân tay', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w7, @WorkDate = '2026-11-02', @AdjustmentType = 'FORGOT_PUNCH',
     @Reason = N'Quên quẹt thẻ buổi sáng', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w8, @WorkDate = '2026-11-03', @AdjustmentType = 'OFFSITE_WORK',
     @Reason = N'Đi giao mẫu cho khách', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;

/* ---------- 9. Suất ăn: ngày đã báo bếp và ngày mở (tổ may 1 đã xác nhận, các tổ khác chưa) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.DailyMeal WHERE MealDate = '2026-11-01')
BEGIN
    INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, GuestNote, MealPrice) VALUES ('2026-11-01', 'OPEN', 8, 2, N'Khách kiểm hàng', 35000);
    INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
    SELECT m.DailyMealId, t.TeamId, 4, 4, 'CONFIRMED', '2026-11-01 08:00:00'
    FROM dbo.DailyMeal m CROSS JOIN dbo.Team t WHERE m.MealDate = '2026-11-01' AND t.TeamCode IN ('CAT', 'MAY1', 'MAY2');
    UPDATE dbo.DailyMeal SET Status = 'KITCHEN_NOTIFIED', ClosedAt = '2026-11-01 08:30:00', KitchenNotifiedAt = '2026-11-01 08:31:00' WHERE MealDate = '2026-11-01';
END;
IF NOT EXISTS (SELECT 1 FROM dbo.DailyMeal WHERE MealDate = '2026-11-03')
BEGIN
    INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, MealPrice) VALUES ('2026-11-03', 'OPEN', 8, 0, 35000);
    INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
    SELECT m.DailyMealId, @may1, 5, 5, 'CONFIRMED', '2026-11-03 07:45:00' FROM dbo.DailyMeal m WHERE m.MealDate = '2026-11-03';
END;

/* ---------- 10. Bậc thợ: đề xuất đang chờ, đã áp dụng, không áp dụng ---------- */
INSERT dbo.GradeEvaluation (WorkerId, ProposedBy, TeamId, CurrentGradeCode, ProposedGradeCode, Reason, RecentDefectCount, EvaluationDate, Status, EffectiveDate, ConsideredAt, DecisionNote, CreatedAt) VALUES
 (@w2, @ttmay, @may1, 'C', 'B', N'Tay nghề tiến bộ, ít lỗi trong 3 tháng', 0, '2026-10-30', 'SUBMITTED', NULL, NULL, NULL, '2026-10-30 09:00:00'),
 (@w4, @ttmay, @may1, 'C', 'B', N'Làm được công đoạn khó', 1, '2026-09-25', 'APPLIED', '2026-10-01', '2026-09-28 10:00:00', N'Đồng ý', '2026-09-25 09:00:00'),
 (@w6, @ttmay2, @may2, 'C', 'A', N'Đề xuất lên thẳng bậc A', 3, '2026-10-29', 'NOT_APPLIED', NULL, '2026-10-30 10:00:00', N'Chưa đủ điều kiện, còn nhiều lỗi', '2026-10-29 09:00:00');

/* ---------- 11. Lương, khóa tháng, chi phí, hòa vốn ----------
   Trạng thái theo tháng: 05 đã duyệt + khóa, 06 nháp, 07 bị trả lại, 08 đã duyệt, 09 chờ duyệt, 10 đã duyệt (đủ điều kiện tính hòa vốn). */
INSERT dbo.Payroll (PeriodId, Status, SubmittedAt, ApprovedAt, RejectReason)
SELECT a.PeriodId, v.Status, v.SubmittedAt, v.ApprovedAt, v.Reason
FROM (VALUES ('2026-05', 'APPROVED', CAST('2026-06-03 09:00:00' AS DATETIME2(0)), CAST('2026-06-04 09:00:00' AS DATETIME2(0)), CAST(NULL AS NVARCHAR(500))),
             ('2026-06', 'DRAFT', NULL, NULL, NULL),
             ('2026-07', 'REJECTED', '2026-08-03 09:00:00', NULL, N'Sai phụ cấp bậc thợ, sửa lại rồi gửi duyệt'),
             ('2026-10', 'APPROVED', '2026-11-01 09:00:00', '2026-11-01 15:00:00', NULL)) v(PeriodMonth, Status, SubmittedAt, ApprovedAt, Reason)
JOIN dbo.AccountingPeriod a ON a.PeriodMonth = v.PeriodMonth;

-- Phiếu lương các tháng mới chép từ tháng 09 (số liệu mẫu)
DECLARE @pay9 INT = (SELECT p.PayrollId FROM dbo.Payroll p JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId WHERE a.PeriodMonth = '2026-09');
INSERT dbo.Payslip (PayrollId, EmployeeId, WorkDays, TotalAdditions, TotalDeductions, Insurance, NetPay)
SELECT p.PayrollId, s.EmployeeId, s.WorkDays, s.TotalAdditions, s.TotalDeductions, s.Insurance, s.NetPay
FROM dbo.Payslip s
CROSS JOIN dbo.Payroll p JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId
WHERE s.PayrollId = @pay9 AND a.PeriodMonth IN ('2026-05', '2026-06', '2026-07', '2026-10');
INSERT dbo.PayslipItem (PayslipId, ItemGroup, ItemName, Amount, SortOrder)
SELECT n.PayslipId, i.ItemGroup, i.ItemName, i.Amount, i.SortOrder
FROM dbo.Payslip s
JOIN dbo.PayslipItem i ON i.PayslipId = s.PayslipId
JOIN dbo.Payslip n ON n.EmployeeId = s.EmployeeId AND n.PayslipId <> s.PayslipId
JOIN dbo.Payroll p ON p.PayrollId = n.PayrollId JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId
WHERE s.PayrollId = @pay9 AND a.PeriodMonth IN ('2026-05', '2026-06', '2026-07', '2026-10');

UPDATE dbo.AccountingPeriod SET Status = 'LOCKED', LockedAt = '2026-06-05 09:00:00' WHERE PeriodMonth = '2026-05';

DECLARE @p10 INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-10');
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Note) VALUES
 (@p10, 'MEALS', 'FIXED', NULL, 3000000, N'Tiền ăn'), (@p10, 'FUEL', 'FIXED', NULL, 2000000, N'Xăng xe'),
 (@p10, 'UTILITIES', 'FIXED', NULL, 3000000, N'Điện nước'), (@p10, 'PREMISES', 'FIXED', NULL, 3000000, N'Mặt bằng'),
 (@p10, 'MACHINE_REPAIR', 'FIXED', NULL, 1000000, N'Sửa máy'),
 (@p10, 'MACHINE_SUPPLIES', 'VARIABLE', @pAo, 1500000, N'Vật tư máy cho áo khoác'),
 (@p10, 'MATERIALS_BOUGHT', 'VARIABLE', @pAo, 2000000, N'NVL mua thêm cho áo khoác'),
 (@p10, 'OTHER', 'VARIABLE', @pQu, 5000000, N'Chi phí khác của quần short');

-- Một lần tính hòa vốn tháng 10 theo công thức của FinanceService (phân bổ theo sản lượng): áo khoác có lãi, quần short nguy cơ lỗ
;WITH rev AS (
    SELECT ProductId, SUM(FinishedQty) AS Qty, SUM(Revenue) AS Revenue FROM dbo.vw_MonthlyRevenue WHERE PeriodMonth = '2026-10' GROUP BY ProductId
), calc AS (
    SELECT r.ProductId, r.Qty, r.Revenue, ISNULL(c.Variable, 0) AS Variable,
           ROUND(r.Revenue / r.Qty, 2) AS Cmt,
           ROUND(f.Fixed * r.Qty / t.TotalQty, 2) AS Allocated
    FROM rev r
    CROSS JOIN (SELECT SUM(Qty) AS TotalQty FROM rev) t
    CROSS JOIN (SELECT SUM(Amount) AS Fixed FROM dbo.MonthlyCost WHERE PeriodId = @p10 AND CostType = 'FIXED') f
    OUTER APPLY (SELECT SUM(Amount) AS Variable FROM dbo.MonthlyCost WHERE PeriodId = @p10 AND CostType = 'VARIABLE' AND ProductId = r.ProductId) c
), be AS (
    SELECT *, CASE WHEN Cmt <= Variable / Qty THEN NULL ELSE ROUND(Allocated / (Cmt - Variable / Qty), 2) END AS BreakEvenQty FROM calc
)
INSERT dbo.BreakEvenResult (PeriodId, ProductId, AllocationBasis, FinishedQty, CmtUnitPrice, Revenue, VariableCost, AllocatedFixedCost, BreakEvenQty, PercentReached, SurplusValue, IsAtRisk, CalculatedAt)
SELECT @p10, ProductId, 'OUTPUT', Qty, Cmt, Revenue, Variable, Allocated, BreakEvenQty,
       CASE WHEN BreakEvenQty IS NULL THEN NULL WHEN BreakEvenQty = 0 THEN 100 ELSE ROUND(Qty / BreakEvenQty * 100, 2) END,
       ROUND(Revenue - Variable - Allocated, 2), IIF(Revenue - Variable - Allocated < 0, 1, 0), '2026-11-01 16:00:00'
FROM be;

/* ---------- 12. Thông báo: cấp trên gửi theo tổ, theo vai trò; hệ thống tự tạo đủ nguồn, có cái đã đọc ---------- */
INSERT dbo.Announcement (Content, Priority, TargetScope, SenderId, SentAt) VALUES
 (N'[DEMO] Tổ may 1 họp đầu ca sáng thứ Ba 03/11 để phổ biến quy cách đơn DEMO-DH003.', 'NORMAL', 'TEAM', @quandoc, '2026-11-02 10:00:00'),
 (N'[DEMO] Tổ trưởng kiểm tra lại phân công công đoạn trước khi nhận BTP đợt mới.', 'URGENT', 'ROLE', @admin, '2026-11-02 11:00:00');
DECLARE @aTeam INT = (SELECT AnnouncementId FROM dbo.Announcement WHERE TargetScope = 'TEAM' AND Content LIKE N'[[]DEMO] Tổ may 1%'),
        @aRole INT = (SELECT AnnouncementId FROM dbo.Announcement WHERE TargetScope = 'ROLE' AND Content LIKE N'[[]DEMO] Tổ trưởng%');
INSERT dbo.AnnouncementTarget (AnnouncementId, TeamId) VALUES (@aTeam, @may1);
INSERT dbo.AnnouncementTarget (AnnouncementId, RoleId) VALUES (@aRole, @rLeader);
INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, LinkUrl, PushStatus, CreatedAt)
SELECT e.EmployeeId, 'ANNOUNCEMENT', 'Announcement', a.AnnouncementId, IIF(a.Priority = 'URGENT', N'Thông báo khẩn', N'Thông báo'), a.Content, '/notifications', 'SKIPPED', a.SentAt
FROM dbo.Announcement a
JOIN dbo.Employee e ON e.Status = 'ACTIVE' AND e.EmployeeId <> a.SenderId
 AND ((a.AnnouncementId = @aTeam AND e.TeamId = @may1) OR (a.AnnouncementId = @aRole AND e.RoleId = @rLeader))
WHERE a.AnnouncementId IN (@aTeam, @aRole);

INSERT dbo.Notification (RecipientId, SourceType, Title, Message, LinkUrl, ReadAt, PushStatus, CreatedAt) VALUES
 (@thukho,  'WAREHOUSE',  N'Có phiếu xuất chờ xuất kho', N'Phiếu XH-DEMO-210 của đơn DEMO-DH010 đang chờ bạn xuất kho.', '/warehouse/dispatch-notes', NULL, 'SKIPPED', '2026-11-01 09:05:00'),
 (@thukho,  'REQUEST',    N'Có đơn mới cần xử lý', N'Đơn YC-DEMO-006 (Vật tư máy) đã hoàn thành.', '/requests/inbox', '2026-10-19 16:00:00', 'SKIPPED', '2026-10-19 15:00:00'),
 (@quandoc, 'REQUEST',    N'Có đơn mới cần xử lý', N'Đơn YC-DEMO-011 (NVL lỗi) chờ bạn duyệt.', '/requests/inbox', NULL, 'SKIPPED', '2026-11-01 10:00:00'),
 (@quandoc, 'WAREHOUSE',  N'Thiếu NVL khi nhập', N'Phiếu PN-DEMO-013 thiếu 30 m vải, hãy chọn giao bù hoặc không bù.', '/warehouse/short-deliveries', NULL, 'SKIPPED', '2026-10-28 10:00:00'),
 (@quandoc, 'SYSTEM',     N'Đơn hàng có nguy cơ trễ hạn', N'Đơn DEMO-DH010 có nguy cơ trễ hạn giao 05/11.', '/admin/orders', NULL, 'SKIPPED', '2026-11-01 17:00:00'),
 (@ttmay,   'PRODUCTION', N'Công nhân phản đối phiếu hàng hỏng', N'CNMAY1 phản đối phiếu MAY1-261102-901.', '/defect-records', NULL, 'SKIPPED', '2026-11-02 09:30:00'),
 (@ttmay,   'REQUEST',    N'Đơn đã được xử lý', N'Đơn YC-DEMO-013 bị từ chối.', '/requests', '2026-10-24 10:00:00', 'SKIPPED', '2026-10-24 09:00:00'),
 (@w1,      'PRODUCTION', N'Bạn có phiếu hàng hỏng', N'Phiếu MAY1-261030-901: 2 sản phẩm hủy ở công đoạn OP02.', '/my-defects', NULL, 'SKIPPED', '2026-10-30 15:00:00'),
 (@w2,      'PRODUCTION', N'Bạn được giao việc', N'Tổ trưởng ghi giao 5 lần công đoạn OP06 và đã xác nhận hộ.', '/my-piece-movements', '2026-10-30 09:00:00', 'SKIPPED', '2026-10-30 08:30:00'),
 (@admin,   'SYSTEM',     N'Có đề xuất bậc thợ chờ duyệt', N'Tổ trưởng đề xuất CNMAY2 lên bậc B.', '/hr/grades', NULL, 'SKIPPED', '2026-10-30 09:00:00');

DROP TABLE #Ch;
COMMIT TRANSACTION;
PRINT N'Đã thêm dữ liệu demo 19.7.';
GO
