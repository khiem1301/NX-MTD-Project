/* =====================================================================================
   NX-MTD – dữ liệu mẫu cho màn "Đơn mua NVL" (22/10/2026)
   Không đổi cấu trúc bảng; chỉ thêm dòng dữ liệu để xem thử.
   Chạy trên database đã có sẵn dữ liệu nền (khách hàng, mã hàng TEST-MH01, NVL).
   Chạy lại nhiều lần an toàn: mỗi khối chỉ chèn khi chưa có.
   Đơn giá trong đơn mua là GIÁ MẪU, không phải giá thật.
   Dùng: sqlcmd -S localhost -d NXMTD -E -C -I -i SQL/DemoData_KH-06-10_purchase-orders.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Đơn hàng mẫu DH-MAU-02: 2 dòng màu x size của mã hàng TEST-MH01.
DECLARE @customerId INT = (SELECT TOP 1 CustomerId FROM Customer ORDER BY CustomerId);
DECLARE @productId  INT = (SELECT ProductId FROM Product WHERE ProductCode = 'TEST-MH01');
IF @customerId IS NULL OR @productId IS NULL
    THROW 50001, N'Thiếu khách hàng hoặc mã hàng TEST-MH01; hãy chạy dữ liệu nền trước.', 1;

IF NOT EXISTS (SELECT 1 FROM CustomerOrder WHERE OrderCode = 'DH-MAU-02')
BEGIN
    INSERT CustomerOrder(OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, IsDelayRisk, CreatedAt)
    VALUES ('DH-MAU-02', @customerId, '2026-10-21', '2026-12-10', 'AWAITING', 'NEW', 0, SYSDATETIME());
    DECLARE @o INT = SCOPE_IDENTITY();
    INSERT OrderLine(OrderId, ProductId, Color, SizeCode, OrderedQty, CmtUnitPrice)
    VALUES (@o, @productId, N'Trang', 'M', 100, 12000), (@o, @productId, N'Trang', 'L', 80, 12000);
END
DECLARE @mau INT = (SELECT OrderId FROM CustomerOrder WHERE OrderCode = 'DH-MAU-02');

-- 2) NVL của DH-MAU-02: vải V-0002 (khách cấp 50, phải mua 150) và chỉ PL-0001 (phải mua 30).
--    Source và PurchaseQty là cột tính, không chèn.
DECLARE @vai INT = (SELECT MaterialId FROM Material WHERE MaterialCode = 'V-0002');
DECLARE @chi INT = (SELECT MaterialId FROM Material WHERE MaterialCode = 'PL-0001');
IF @vai IS NOT NULL AND NOT EXISTS (SELECT 1 FROM OrderMaterial WHERE OrderId = @mau AND MaterialId = @vai AND MaterialColor = '')
    INSERT OrderMaterial(OrderId, MaterialId, MaterialColor, RequiredQty, CustomerQty, CustomerExpectedDate)
    VALUES (@mau, @vai, '', 200, 50, '2026-11-05');
IF @chi IS NOT NULL AND NOT EXISTS (SELECT 1 FROM OrderMaterial WHERE OrderId = @mau AND MaterialId = @chi AND MaterialColor = '')
    INSERT OrderMaterial(OrderId, MaterialId, MaterialColor, RequiredQty, CustomerQty)
    VALUES (@mau, @chi, '', 30, 0);

-- 3) Đơn mua gắn đơn hàng: mỗi đơn lấy phần "phải mua" của NVL trong đơn.
--    (PoCode, mã đơn hàng, ghi chú) -> dòng lấy từ OrderMaterial của đơn đó.
DECLARE @po TABLE (PoCode VARCHAR(30), OrderCode VARCHAR(30), Note NVARCHAR(200), Price0 DECIMAL(18,2));
INSERT @po VALUES
  ('DM-20261021-003', 'TEST-DH01',  N'Dữ liệu mẫu: mua NVL cho TEST-DH01',  15000),
  ('DM-20261021-004', 'FPT001-PL',  N'Dữ liệu mẫu: mua NVL cho FPT001-PL',  2000),
  ('DM-20261021-005', 'DH-MAU-02',  N'Dữ liệu mẫu: mua NVL cho DH-MAU-02',  0);

INSERT PurchaseOrder(PoCode, OrderId, GoodsStatus, PaymentStatus, Note, CreatedAt)
SELECT p.PoCode, o.OrderId, 'PENDING_FINALISATION', 'UNPAID', p.Note, SYSDATETIME()
FROM @po p JOIN CustomerOrder o ON o.OrderCode = p.OrderCode
WHERE NOT EXISTS (SELECT 1 FROM PurchaseOrder x WHERE x.PoCode = p.PoCode);

-- Giá mẫu theo NVL: vải V-0002 = 18.000, chỉ PL-0001 = 3.500, còn lại theo Price0 của đơn mua.
INSERT PurchaseOrderLine(PurchaseOrderId, OrderMaterialId, OrderedQty, NotReplenishedQty, UnitPrice)
SELECT po.PurchaseOrderId, om.OrderMaterialId, om.PurchaseQty, 0,
       CASE m.MaterialCode WHEN 'V-0002' THEN 18000 WHEN 'PL-0001' THEN 3500 ELSE p.Price0 END
FROM @po p
JOIN PurchaseOrder po ON po.PoCode = p.PoCode
JOIN OrderMaterial om ON om.OrderId = po.OrderId AND om.PurchaseQty > 0
JOIN Material m ON m.MaterialId = om.MaterialId
WHERE NOT EXISTS (SELECT 1 FROM PurchaseOrderLine l WHERE l.PurchaseOrderId = po.PurchaseOrderId);

COMMIT;

SELECT po.PoCode, o.OrderCode, COUNT(l.PurchaseOrderLineId) AS Dong, SUM(l.Amount) AS TongTien
FROM PurchaseOrder po LEFT JOIN CustomerOrder o ON o.OrderId = po.OrderId
LEFT JOIN PurchaseOrderLine l ON l.PurchaseOrderId = po.PurchaseOrderId
GROUP BY po.PoCode, o.OrderCode ORDER BY po.PoCode;
