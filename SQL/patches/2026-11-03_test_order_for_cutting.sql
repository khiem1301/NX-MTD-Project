/* =====================================================================================
   Đơn test lập lệnh cắt + ước tính vải (03/11/2026)
   ---------------------------------------------------------------------------------
   - Bù bìa cắt (ProductMarker / ProductMarkerFabric) cho DEMO-AO01, DEMO-QU02 nếu thiếu.
   - Tạo đơn TEST-CAT-001: mã áo DEMO-AO01 (Đen S/M/L + Xanh M/L), NVL đơn, MaterialStatus COMPLETE.
   Chạy lại không nhân đôi. Không đụng tồn kho (đã có DEMO-VAI*).
   sqlcmd -S localhost -E -C -I -f 65001 -d NXMTD -i SQL/patches/2026-11-03_test_order_for_cutting.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Product WHERE ProductCode = 'DEMO-AO01')
BEGIN
    PRINT N'Chưa có DEMO-AO01, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @pAo INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @pQu INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-QU02'),
        @mVaiDen INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mVaiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI02'),
        @mVaiTrang INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI03'),
        @mVaiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI04'),
        @mChiDen INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI01'),
        @mChiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI02'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @cus INT = (SELECT TOP 1 CustomerId FROM dbo.Customer WHERE Status = 'ACTIVE' ORDER BY CustomerId);

/* ---------- 1. Bìa DEMO-AO01 (thân + tay; Xanh phối tay vải Trắng) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ProductMarker WHERE ProductId = @pAo)
BEGIN
    INSERT dbo.ProductMarker (ProductId, MarkerNo, SizeCode, LengthCm, WidthCm) VALUES
     (@pAo, 1, 'S', 70, 90), (@pAo, 1, 'M', 72, 94), (@pAo, 1, 'L', 74, 98),
     (@pAo, 2, 'S', 60, 82), (@pAo, 2, 'M', 62, 84), (@pAo, 2, 'L', 64, 86);
    PRINT N'Đã thêm bìa DEMO-AO01.';
END
ELSE PRINT N'Bìa DEMO-AO01 đã có, bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric WHERE ProductId = @pAo)
BEGIN
    INSERT dbo.ProductMarkerFabric (ProductId, MarkerNo, Color, MaterialId) VALUES
     (@pAo, 1, N'Đen', @mVaiDen), (@pAo, 2, N'Đen', @mVaiDen),
     (@pAo, 1, N'Xanh', @mVaiXanh), (@pAo, 2, N'Xanh', @mVaiTrang);
    PRINT N'Đã thêm vải bìa DEMO-AO01.';
END
ELSE PRINT N'Vải bìa DEMO-AO01 đã có, bỏ qua.';

/* ---------- 2. Bìa DEMO-QU02 (một bìa, màu Be) ---------- */
IF @pQu IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarker WHERE ProductId = @pQu)
BEGIN
    INSERT dbo.ProductMarker (ProductId, MarkerNo, SizeCode, LengthCm, WidthCm) VALUES
     (@pQu, 1, 'M', 48, 58), (@pQu, 1, 'L', 50, 62);
    PRINT N'Đã thêm bìa DEMO-QU02.';
END;

IF @pQu IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric WHERE ProductId = @pQu)
BEGIN
    INSERT dbo.ProductMarkerFabric (ProductId, MarkerNo, Color, MaterialId) VALUES
     (@pQu, 1, N'Be', @mVaiBe);
    PRINT N'Đã thêm vải bìa DEMO-QU02.';
END;

/* ---------- 3. Đơn TEST-CAT-001 ---------- */
IF EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'TEST-CAT-001')
BEGIN
    PRINT N'Đơn TEST-CAT-001 đã có, bỏ qua phần đơn.';
    COMMIT;
    RETURN;
END;

INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, Note, CreatedAt)
VALUES ('TEST-CAT-001', @cus, '2026-11-01', '2026-12-20', 'COMPLETE', 'NEW',
        N'Đơn test lập lệnh cắt + xem ước tính vải (bìa DEMO-AO01)', '2026-11-01 08:00:00');

DECLARE @oid INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'TEST-CAT-001');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT @oid, pv.ProductVariantId, pv.ProductId, v.Qty, 28000
FROM (VALUES
 (N'Đen',  'S', 50),
 (N'Đen',  'M', 100),
 (N'Đen',  'L', 80),
 (N'Xanh', 'M', 60),
 (N'Xanh', 'L', 40))
 v(Color, SizeCode, Qty)
JOIN dbo.ProductVariant pv ON pv.ProductId = @pAo AND pv.Color = v.Color AND pv.SizeCode = v.SizeCode;

INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount) VALUES
 (@oid, @mVaiDen,   200, 200, 1),
 (@oid, @mVaiXanh,   80,  80, 1),
 (@oid, @mVaiTrang,  40,  40, 1),
 (@oid, @mChiDen,    30,  30, 1),
 (@oid, @mChiXanh,   15,  15, 1),
 (@oid, @mKhoa,     330, 330, 1),
 (@oid, @mNhan,     330, 330, 1);

COMMIT;

PRINT N'Đã tạo đơn TEST-CAT-001 (OrderId=' + CAST(@oid AS NVARCHAR(20)) + N').';
PRINT N'Đăng nhập QUANDOC → Lệnh cắt → Tạo → chọn TEST-CAT-001 / DEMO-AO01 / tổ CAT.';
GO
