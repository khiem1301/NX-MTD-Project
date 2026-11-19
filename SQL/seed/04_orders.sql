/* =====================================================================================
   04. ĐƠN HÀNG: 10 đơn ở đủ trạng thái (NEW x2, IN_PRODUCTION x2, IN_DELIVERY, PARTIALLY_DELIVERED, DELIVERED, COMPLETED, STOPPED, CANCELLED),
       dòng đơn (màu × size), tổ phụ trách, NVL của đơn (khách cấp / phải mua), lịch giao NVL của khách, bìa cắt.
   Bảng tạm #Cons, #Ord, #Ln dùng lại ở các file sau (cùng một phiên sqlcmd).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);

/* Định mức tiêu hao theo 1 sản phẩm: vải (m) cho chi tiết vải, số lượng cho phụ liệu */
IF OBJECT_ID('tempdb..#Cons') IS NOT NULL DROP TABLE #Cons;
CREATE TABLE #Cons (Prod VARCHAR(30) COLLATE DATABASE_DEFAULT, Comp NVARCHAR(100) COLLATE DATABASE_DEFAULT, PerGarment DECIMAL(9,4));
INSERT #Cons VALUES
 ('AS-0412', N'Thân trước', .55), ('AS-0412', N'Thân sau', .38), ('AS-0412', N'Tay áo', .38), ('AS-0412', N'Cổ áo', .10),
 ('AS-0412', N'Nút', 9), ('AS-0412', N'Chỉ may', .03), ('AS-0412', N'Nhãn dệt', 1),
 ('QK-0308', N'Ống quần', .85), ('QK-0308', N'Túi sau', .12), ('QK-0308', N'Lưng quần', .18),
 ('QK-0308', N'Khóa', 1), ('QK-0308', N'Nút', 1), ('QK-0308', N'Chỉ may', .04), ('QK-0308', N'Nhãn dệt', 1),
 ('AK-0522', N'Thân trước', .75), ('AK-0522', N'Thân sau', .55), ('AK-0522', N'Tay áo', .55), ('AK-0522', N'Mũ', .25),
 ('AK-0522', N'Thân lót', 1.0), ('AK-0522', N'Khóa', 1), ('AK-0522', N'Chỉ may', .05), ('AK-0522', N'Nhãn dệt', 1),
 ('AP-0715', N'Thân trước', .38), ('AP-0715', N'Thân sau', .28), ('AP-0715', N'Tay áo', .15), ('AP-0715', N'Bo cổ', .06),
 ('AP-0715', N'Nút', 3), ('AP-0715', N'Chỉ may', .02), ('AP-0715', N'Nhãn dệt', 1);

/* Hồ sơ đơn. Off = số ngày so với hôm nay. */
IF OBJECT_ID('tempdb..#Ord') IS NOT NULL DROP TABLE #Ord;
CREATE TABLE #Ord (OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT PRIMARY KEY, Cust INT, Prod VARCHAR(30) COLLATE DATABASE_DEFAULT, RecvOff INT, DueOff INT,
                   Status VARCHAR(25) COLLATE DATABASE_DEFAULT, MatStatus VARCHAR(10) COLLATE DATABASE_DEFAULT,
                   Price DECIMAL(18,2), StopReason NVARCHAR(500) COLLATE DATABASE_DEFAULT, Note NVARCHAR(1000) COLLATE DATABASE_DEFAULT, RiskOff INT NULL,
                   -- tiến độ sản xuất (0..1): cắt, tổ may nhận BTP, may xong (công đoạn cuối), kiểm đạt, nhập kho TP, xuất kho
                   CutF DECIMAL(5,3), IssueF DECIMAL(5,3), SewF DECIMAL(5,3), QcF DECIMAL(5,3), FgF DECIMAL(5,3), DispF DECIMAL(5,3),
                   StartOff INT NULL);   -- ngày bắt đầu sản xuất (so với hôm nay)
INSERT #Ord VALUES
 ('DH-2509-001', 1, 'AS-0412', -80, -38, 'COMPLETED',          'COMPLETE', 38000, NULL, N'Đơn áo sơ mi mùa tựu trường, đã giao đủ và thanh toán xong', NULL, 1, 1, 1, 1, 1, 1, -70),
 ('DH-2510-002', 2, 'QK-0308', -60, -12, 'DELIVERED',          'COMPLETE', 42000, NULL, N'Quần kaki xuất khẩu nội địa, đã giao đủ, chờ quyết toán', NULL, 1, 1, 1, 1, 1, 1, -52),
 ('DH-2510-003', 1, 'AK-0522', -42,   2, 'PARTIALLY_DELIVERED','COMPLETE', 65000, NULL, N'Áo khoác gió đợt đông: đã giao đợt 1, còn đợt 2', -3, 1, 1, .96, .95, .92, .55, -34),
 ('DH-2510-004', 2, 'AS-0412', -30,   4, 'IN_DELIVERY',        'COMPLETE', 39000, NULL, N'Đang giao hàng lô cuối cho khách', -2, 1, 1, 1, 1, 1, .72, -24),
 ('DH-2511-005', 1, 'AP-0715', -21,  20, 'IN_PRODUCTION',      'PARTIAL',  34000, NULL, N'Áo polo nữ: vải trắng về chậm hơn vải hồng', -1, .75, .55, .38, .25, .15, 0, -15),
 ('DH-2511-006', 1, 'AS-0412', -12,  25, 'IN_PRODUCTION',      'PARTIAL',  38500, NULL, N'Vừa cắt xong lô đầu, tổ may mới nhận BTP', NULL, .45, .25, .08, .0, 0, 0, -6),
 ('DH-2511-007', 2, 'QK-0308',  -3,  32, 'NEW',                'AWAITING', 43000, NULL, N'Đơn mới, đang chờ chốt nhà cung cấp vải', NULL, 0, 0, 0, 0, 0, 0, NULL),
 ('DH-2511-008', 1, 'AK-0522',  -1,  30, 'NEW',                'COMPLETE', 66000, NULL, N'Khách cấp toàn bộ NVL, chờ khách giao hàng', NULL, 0, 0, 0, 0, 0, 0, NULL),
 ('DH-2510-009', 1, 'AK-0522', -28,   6, 'STOPPED',            'PARTIAL',  65000, N'Khách tạm dừng đơn do đổi mẫu mũ, chờ khách gửi mẫu mới', N'Dừng giữa chừng sau khi tổ may nhận BTP', NULL, .6, .4, .15, .1, 0, 0, -20),
 ('DH-2510-010', 3, 'AP-0715', -45,  -5, 'CANCELLED',          'AWAITING', 33000, NULL, N'Khách hủy đơn trước khi mua NVL', NULL, 0, 0, 0, 0, 0, 0, NULL);

INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, DelayRiskSince, StopReason, Note, CreatedAt)
SELECT o.OrderCode, o.Cust, DATEADD(DAY, o.RecvOff, @today), DATEADD(DAY, o.DueOff, @today), o.MatStatus, o.Status,
       CASE WHEN o.RiskOff IS NOT NULL THEN DATEADD(DAY, o.RiskOff, @today) END, o.StopReason, o.Note,
       DATEADD(DAY, o.RecvOff, CAST(@today AS DATETIME2))
FROM #Ord o;

/* Dòng đơn: màu × size × số lượng */
IF OBJECT_ID('tempdb..#Ln') IS NOT NULL DROP TABLE #Ln;
CREATE TABLE #Ln (OrderCode VARCHAR(30) COLLATE DATABASE_DEFAULT, Color NVARCHAR(50) COLLATE DATABASE_DEFAULT, Size VARCHAR(10) COLLATE DATABASE_DEFAULT, Qty INT, Team VARCHAR(20) COLLATE DATABASE_DEFAULT);
INSERT #Ln VALUES
 ('DH-2509-001', N'Trắng', 'S', 100, 'MAY1'), ('DH-2509-001', N'Trắng', 'M', 200, 'MAY1'), ('DH-2509-001', N'Trắng', 'L', 200, 'MAY1'), ('DH-2509-001', N'Trắng', 'XL', 100, 'MAY1'),
 ('DH-2509-001', N'Xanh nhạt', 'S', 60, 'MAY2'), ('DH-2509-001', N'Xanh nhạt', 'M', 120, 'MAY2'), ('DH-2509-001', N'Xanh nhạt', 'L', 120, 'MAY2'), ('DH-2509-001', N'Xanh nhạt', 'XL', 60, 'MAY2'),
 ('DH-2510-002', N'Be', '29', 60, 'MAY2'), ('DH-2510-002', N'Be', '30', 100, 'MAY2'), ('DH-2510-002', N'Be', '31', 120, 'MAY2'), ('DH-2510-002', N'Be', '32', 100, 'MAY2'), ('DH-2510-002', N'Be', '33', 60, 'MAY2'),
 ('DH-2510-002', N'Đen', '29', 40, 'MAY2'), ('DH-2510-002', N'Đen', '30', 80, 'MAY2'), ('DH-2510-002', N'Đen', '31', 100, 'MAY2'), ('DH-2510-002', N'Đen', '32', 80, 'MAY2'), ('DH-2510-002', N'Đen', '33', 40, 'MAY2'),
 ('DH-2510-003', N'Đen', 'M', 150, 'MAY1'), ('DH-2510-003', N'Đen', 'L', 250, 'MAY1'), ('DH-2510-003', N'Đen', 'XL', 150, 'MAY1'),
 ('DH-2510-003', N'Xanh navy', 'M', 100, 'MAY2'), ('DH-2510-003', N'Xanh navy', 'L', 150, 'MAY2'), ('DH-2510-003', N'Xanh navy', 'XL', 100, 'MAY2'),
 ('DH-2510-004', N'Trắng', 'M', 150, 'MAY1'), ('DH-2510-004', N'Trắng', 'L', 150, 'MAY1'), ('DH-2510-004', N'Xanh nhạt', 'M', 100, 'MAY1'), ('DH-2510-004', N'Xanh nhạt', 'L', 100, 'MAY1'),
 ('DH-2511-005', N'Hồng', 'S', 120, 'MAY2'), ('DH-2511-005', N'Hồng', 'M', 200, 'MAY2'), ('DH-2511-005', N'Hồng', 'L', 120, 'MAY2'),
 ('DH-2511-005', N'Trắng', 'S', 80, 'MAY3'), ('DH-2511-005', N'Trắng', 'M', 120, 'MAY3'),
 ('DH-2511-006', N'Trắng', 'S', 80, 'MAY1'), ('DH-2511-006', N'Trắng', 'M', 160, 'MAY1'), ('DH-2511-006', N'Trắng', 'L', 160, 'MAY1'), ('DH-2511-006', N'Trắng', 'XL', 80, 'MAY1'),
 ('DH-2511-006', N'Xanh nhạt', 'M', 100, 'MAY1'), ('DH-2511-006', N'Xanh nhạt', 'L', 100, 'MAY1'),
 ('DH-2511-007', N'Be', '30', 60, NULL), ('DH-2511-007', N'Be', '31', 80, NULL), ('DH-2511-007', N'Be', '32', 80, NULL),
 ('DH-2511-007', N'Đen', '30', 50, NULL), ('DH-2511-007', N'Đen', '31', 70, NULL), ('DH-2511-007', N'Đen', '32', 60, NULL),
 ('DH-2511-008', N'Đen', 'M', 80, NULL), ('DH-2511-008', N'Đen', 'L', 120, NULL), ('DH-2511-008', N'Đen', 'XL', 80, NULL),
 ('DH-2510-009', N'Xanh navy', 'M', 60, 'MAY3'), ('DH-2510-009', N'Xanh navy', 'L', 90, 'MAY3'),
 ('DH-2510-010', N'Hồng', 'S', 50, NULL), ('DH-2510-010', N'Hồng', 'M', 50, NULL);

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT co.OrderId, pv.ProductVariantId, pv.ProductId, l.Qty, o.Price
FROM #Ln l
JOIN #Ord o ON o.OrderCode = l.OrderCode
JOIN dbo.CustomerOrder co ON co.OrderCode = l.OrderCode
JOIN dbo.Product p ON p.ProductCode = o.Prod
JOIN dbo.ProductColor pc ON pc.ProductId = p.ProductId AND pc.Color = l.Color
JOIN dbo.ProductVariant pv ON pv.ProductColorId = pc.ProductColorId AND pv.SizeCode = l.Size
ORDER BY co.OrderId, pc.ProductColorId, pv.ProductVariantId;

INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT ol.OrderLineId, t.TeamId
FROM #Ln l
JOIN dbo.CustomerOrder co ON co.OrderCode = l.OrderCode
JOIN dbo.OrderLine ol ON ol.OrderId = co.OrderId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId AND pv.SizeCode = l.Size
JOIN dbo.ProductColor pc ON pc.ProductColorId = pv.ProductColorId AND pc.Color = l.Color
JOIN dbo.Team t ON t.TeamCode = l.Team
WHERE l.Team IS NOT NULL;

/* NVL của đơn: nhu cầu = Σ SL × định mức × hao hụt; phần khách cấp theo bảng dưới */
IF OBJECT_ID('tempdb..#Need') IS NOT NULL DROP TABLE #Need;
SELECT co.OrderId, o.OrderCode, m.MaterialId, m.MaterialCode, m.MaterialType,
       SUM(ol.OrderedQty * c.PerGarment) AS RawQty
INTO #Need
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder co ON co.OrderId = ol.OrderId
JOIN #Ord o ON o.OrderCode = co.OrderCode
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductId = ol.ProductId AND pcm.ProductColorId = pv.ProductColorId
JOIN dbo.ProductComponent pcmp ON pcmp.ProductComponentId = pcm.ProductComponentId
JOIN #Cons c ON c.Prod = o.Prod AND c.Comp = pcmp.ComponentName
JOIN dbo.Material m ON m.MaterialId = pcm.MaterialId
GROUP BY co.OrderId, o.OrderCode, m.MaterialId, m.MaterialCode, m.MaterialType;

-- Phần khách cấp: (đơn, mã NVL bắt đầu bằng, tỉ lệ)
DECLARE @cust TABLE (OrderCode VARCHAR(30), MatLike VARCHAR(30), Frac DECIMAL(4,2));
INSERT @cust VALUES
 ('DH-2509-001', 'T-NHAN%', 1), ('DH-2510-003', 'T-KHOA%', .40), ('DH-2510-004', 'T-NHAN%', 1),
 ('DH-2511-007', 'T-NHAN%', .50), ('DH-2511-007', 'T-KHOA%', 1),
 ('DH-2511-008', '%', 1);   -- khách cấp toàn bộ

INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, Note)
SELECT n.OrderId, n.MaterialId,
       CASE WHEN n.MaterialType = 'FABRIC' THEN CEILING(n.RawQty * 1.03) ELSE CEILING(n.RawQty * 1.02) END,
       ISNULL((SELECT CAST(MAX(c.Frac) AS DECIMAL(9,4)) FROM @cust c WHERE c.OrderCode = n.OrderCode AND n.MaterialCode LIKE c.MatLike), 0)
         * CASE WHEN n.MaterialType = 'FABRIC' THEN CEILING(n.RawQty * 1.03) ELSE CEILING(n.RawQty * 1.02) END,
       CASE WHEN n.MaterialType = 'FABRIC' THEN N'Đã gồm 3% hao hụt cắt' ELSE N'Đã gồm 2% hao hụt' END
FROM #Need n;
-- làm tròn phần khách cấp xuống số nguyên
UPDATE dbo.OrderMaterial SET CustomerQty = FLOOR(CustomerQty);

/* Khách giao NVL: số lần + lịch giao theo ngày */
UPDATE co SET CustomerDeliveryCount = 2, CustomerDeliveryDate = DATEADD(DAY, 3, @today), CustomerDeliveryDateTo = DATEADD(DAY, 7, @today)
FROM dbo.CustomerOrder co WHERE co.OrderCode = 'DH-2511-007';
UPDATE co SET CustomerDeliveryCount = 1, CustomerDeliveryDate = DATEADD(DAY, 2, @today)
FROM dbo.CustomerOrder co WHERE co.OrderCode = 'DH-2511-008';
INSERT dbo.DeliveryScheduleDate (OrderId, SeqNo, DeliveryDate)
SELECT co.OrderId, v.Seq, DATEADD(DAY, v.Dd, @today)
FROM (VALUES ('DH-2511-007', 1, 3), ('DH-2511-007', 2, 7), ('DH-2511-008', 1, 2)) v (Code, Seq, Dd)
JOIN dbo.CustomerOrder co ON co.OrderCode = v.Code;

/* Bìa cắt của đơn (ProductMarker + tỉ lệ size): mọi đơn đã vào sản xuất / cắt */
INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM, Status, WidthM)
SELECT co.OrderId, p.ProductId, mk.MarkerNo, CONCAT(mk.MarkerNo, 'A'),
       -- chiều dài bìa = Σ định mức vải của các chi tiết trong nhóm × số size trong bìa (mỗi size 1 cái)
       CAST(ROUND((SELECT SUM(c.PerGarment) FROM dbo.ProductMarkerComponent pmc
                   JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pmc.ProductComponentId
                   JOIN #Cons c ON c.Prod = o.Prod AND c.Comp = pc.ComponentName
                   WHERE pmc.ProductId = p.ProductId AND pmc.MarkerNo = mk.MarkerNo)
                  * (SELECT COUNT(DISTINCT l.Size) FROM #Ln l WHERE l.OrderCode = o.OrderCode), 2) AS DECIMAL(7,2)),
       'ACTIVE', 1.50
FROM #Ord o
JOIN dbo.CustomerOrder co ON co.OrderCode = o.OrderCode
JOIN dbo.Product p ON p.ProductCode = o.Prod
JOIN (SELECT DISTINCT ProductId, MarkerNo FROM dbo.ProductMarkerComponent) mk ON mk.ProductId = p.ProductId
WHERE o.Status NOT IN ('CANCELLED') AND NOT (o.OrderCode = 'DH-2511-007');
INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
SELECT pm.ProductMarkerId, s.Size, 1
FROM dbo.ProductMarker pm
JOIN dbo.CustomerOrder co ON co.OrderId = pm.OrderId
CROSS APPLY (SELECT DISTINCT l.Size FROM #Ln l WHERE l.OrderCode = co.OrderCode) s;

PRINT N'04: đơn hàng, dòng đơn, NVL, bìa cắt xong.';
