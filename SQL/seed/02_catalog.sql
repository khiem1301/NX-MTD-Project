/* =====================================================================================
   02. DANH MỤC: khách hàng, nhà cung cấp, xe, NVL, mã hàng (màu, size, thành phần, định mức, nhóm bìa),
       công đoạn, đơn giá công đoạn (đủ trạng thái).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);

INSERT dbo.Customer (CustomerName, Phone, Email, DefaultAddress, Status, DiscontinueReason) VALUES
 (N'Công ty CP Thời trang An Phát (công ty mẹ)', '02838123456', 'donhang@anphat-fashion.vn', N'125 Nguyễn Văn Linh, Quận 7, TP.HCM', 'ACTIVE', NULL),
 (N'Công ty TNHH Xuất nhập khẩu Bình Minh',      '02838654321', 'mua.hang@binhminh-ex.vn',  N'56 Lê Văn Việt, TP. Thủ Đức, TP.HCM', 'ACTIVE', NULL),
 (N'Cửa hàng thời trang Hoa Mai',                '0988777666',  NULL,                       N'12 Phan Đăng Lưu, Quận Phú Nhuận, TP.HCM', 'DISCONTINUED', N'Khách đóng cửa hàng, không còn đặt gia công');

INSERT dbo.Supplier (SupplierName, Phone, Email, Address, MaterialTypes, Note, Status, DiscontinueReason) VALUES
 (N'Công ty Vải Sợi Thành Công',       '02838111222', 'kinhdoanh@vaisoithanhcong.vn', N'Quận Tân Phú, TP.HCM',  N'Vải chính, vải lót', N'Giao hàng trong 3 ngày, thanh toán 30 ngày', 'ACTIVE', NULL),
 (N'Phụ liệu may Hoàng Gia',           '02838333444', 'pl@hoanggia-may.vn',          N'Quận 10, TP.HCM',       N'Chỉ, khóa, nút, nhãn', N'Giao hàng trong 2 ngày', 'ACTIVE', NULL),
 (N'Cửa hàng Kim máy Minh Phát',       '0907555123',  NULL,                          N'Quận 5, TP.HCM',        N'Kim, phụ tùng máy may', NULL, 'ACTIVE', NULL),
 (N'Công ty Bao bì Đại Phát',          '02837777888', 'sales@baobidaiphat.vn',       N'KCN Tân Bình, TP.HCM',  N'Túi PE, thẻ treo, thùng carton', NULL, 'ACTIVE', NULL),
 (N'Vải Phương Nam',                   '0918222333',  NULL,                          N'TP. Thuận An, Bình Dương', N'Vải kaki', N'Giá cao, giao chậm', 'DISCONTINUED', N'Giao chậm nhiều lần, ngừng hợp tác');

INSERT dbo.Vehicle (PlateNumber, VehicleType, FuelNormPer100Km, Status) VALUES
 ('51C-123.45', N'Xe tải 1,25 tấn', 12.50, 'ACTIVE'),
 ('51D-678.90', N'Xe van 5 chỗ',     9.00, 'ACTIVE'),
 ('59F-246.80', N'Xe bán tải (đã thanh lý)', 11.00, 'DISCONTINUED');

/* ---------------- NVL ---------------- */
INSERT dbo.Material (MaterialCode, MaterialName, Color, MaterialType, Unit, WidthM, Status) VALUES
 ('V-KATE-TR',  N'Vải kate Hàn 2 chiều',   N'Trắng',      'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-KATE-XN',  N'Vải kate Hàn 2 chiều',   N'Xanh nhạt',  'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-KAKI-BE',  N'Vải kaki co giãn',       N'Be',         'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-KAKI-DEN', N'Vải kaki co giãn',       N'Đen',        'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-GIO-DEN',  N'Vải dù gió 2 lớp',       N'Đen',        'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-GIO-NAVY', N'Vải dù gió 2 lớp',       N'Xanh navy',  'FABRIC', N'm', 1.50, 'ACTIVE'),
 ('V-LOT-DEN',  N'Vải lót taffeta',        N'Đen',        'FABRIC', N'm', 1.40, 'ACTIVE'),
 ('V-POLO-HG',  N'Vải thun cá sấu',        N'Hồng',       'FABRIC', N'm', 1.60, 'ACTIVE'),
 ('V-POLO-TR',  N'Vải thun cá sấu',        N'Trắng',      'FABRIC', N'm', 1.60, 'ACTIVE'),
 ('V-CANIFA',   N'Vải canifa (ngừng nhập)', N'Xám',       'FABRIC', N'm', 1.50, 'DISCONTINUED'),
 ('T-CHI-TR',   N'Chỉ may 40/2',           N'Trắng',      'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-CHI-XN',   N'Chỉ may 40/2',           N'Xanh nhạt',  'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-CHI-BE',   N'Chỉ may 40/2',           N'Be',         'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-CHI-DEN',  N'Chỉ may 40/2',           N'Đen',        'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-CHI-NAVY', N'Chỉ may 40/2',           N'Xanh navy',  'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-CHI-HG',   N'Chỉ may 40/2',           N'Hồng',       'TRIM',   N'cuộn', NULL, 'ACTIVE'),
 ('T-NUT-4L',   N'Nút 4 lỗ 11mm',          N'Trắng',      'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-NUT-KAKI', N'Nút quần kim loại',      N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-KHOA-18',  N'Khóa quần 18cm',         N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-KHOA-60D', N'Khóa áo khoác 60cm',     N'Đen',        'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-KHOA-60N', N'Khóa áo khoác 60cm',     N'Xanh navy',  'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-NHAN-DET', N'Nhãn dệt thương hiệu',   N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-NHAN-SZ',  N'Nhãn size',              N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-THE-TREO', N'Thẻ treo giấy',          N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-TUI-PE',   N'Túi PE đóng gói',        N'',           'TRIM',   N'cái', NULL, 'ACTIVE'),
 ('T-KIM-14',   N'Kim máy may số 14',      N'',           'TRIM',   N'hộp', NULL, 'ACTIVE'),
 ('S-VAI-VUN',  N'Vải vụn',                N'',           'SCRAP',  N'kg',  NULL, 'ACTIVE'),
 ('S-KIM-GAY',  N'Kim gãy',                N'',           'SCRAP',  N'cái', NULL, 'ACTIVE');

/* ---------------- Mã hàng ---------------- */
INSERT dbo.Product (ProductCode, ProductName, Description, Status) VALUES
 ('AS-0412', N'Áo sơ mi nam dài tay',   N'Áo sơ mi công sở, cổ đức, tay dài, 9 công đoạn may', 'ACTIVE'),
 ('QK-0308', N'Quần kaki nam ống đứng', N'Quần kaki co giãn, 2 túi trước, 2 túi sau',           'ACTIVE'),
 ('AK-0522', N'Áo khoác gió 2 lớp',     N'Áo khoác gió có mũ, lót taffeta, khóa kéo toàn thân', 'ACTIVE'),
 ('AP-0715', N'Áo polo nữ cá sấu',      N'Áo polo nữ cổ bẻ, tay ngắn',                           'ACTIVE'),
 ('VD-0101', N'Váy đầm công sở (ngừng sản xuất)', N'Mã hàng cũ khách không đặt nữa',            'DISCONTINUED');

-- Màu áo
INSERT dbo.ProductColor (ProductId, Color)
SELECT p.ProductId, c.Color
FROM (VALUES ('AS-0412', N'Trắng'), ('AS-0412', N'Xanh nhạt'), ('QK-0308', N'Be'), ('QK-0308', N'Đen'),
             ('AK-0522', N'Đen'), ('AK-0522', N'Xanh navy'), ('AP-0715', N'Hồng'), ('AP-0715', N'Trắng'),
             ('VD-0101', N'Trắng')) c (Code, Color)
JOIN dbo.Product p ON p.ProductCode = c.Code;

-- Biến thể: màu × size
INSERT dbo.ProductVariant (ProductId, ProductColorId, SizeCode, Status)
SELECT pc.ProductId, pc.ProductColorId, s.SizeCode, 'ACTIVE'
FROM dbo.ProductColor pc
JOIN dbo.Product p ON p.ProductId = pc.ProductId
JOIN (VALUES ('AS-0412', 'S'), ('AS-0412', 'M'), ('AS-0412', 'L'), ('AS-0412', 'XL'),
             ('QK-0308', '29'), ('QK-0308', '30'), ('QK-0308', '31'), ('QK-0308', '32'), ('QK-0308', '33'),
             ('AK-0522', 'M'), ('AK-0522', 'L'), ('AK-0522', 'XL'),
             ('AP-0715', 'S'), ('AP-0715', 'M'), ('AP-0715', 'L'),
             ('VD-0101', 'S'), ('VD-0101', 'M')) s (Code, SizeCode) ON s.Code = p.ProductCode;
UPDATE pv SET Status = 'DISCONTINUED' FROM dbo.ProductVariant pv JOIN dbo.Product p ON p.ProductId = pv.ProductId WHERE p.ProductCode = 'VD-0101';
-- Một size đã ngừng ở mã còn bán (áo khoác size M không còn nhận đơn mới)
UPDATE pv SET Status = 'DISCONTINUED' FROM dbo.ProductVariant pv
JOIN dbo.Product p ON p.ProductId = pv.ProductId JOIN dbo.ProductColor c ON c.ProductColorId = pv.ProductColorId
WHERE p.ProductCode = 'AP-0715' AND c.Color = N'Trắng' AND pv.SizeCode = 'L';

-- Thành phần (chi tiết vải PANEL + phụ liệu TRIM)
INSERT dbo.ProductComponent (ProductId, ComponentName, ComponentType, PiecesPerGarment)
SELECT p.ProductId, c.Name, c.Type, c.Pieces
FROM (VALUES
 ('AS-0412', N'Thân trước', 'PANEL', 2), ('AS-0412', N'Thân sau', 'PANEL', 1), ('AS-0412', N'Tay áo', 'PANEL', 2),
 ('AS-0412', N'Cổ áo', 'PANEL', 2), ('AS-0412', N'Nút', 'TRIM', 9), ('AS-0412', N'Chỉ may', 'TRIM', 1), ('AS-0412', N'Nhãn dệt', 'TRIM', 1),
 ('QK-0308', N'Ống quần', 'PANEL', 2), ('QK-0308', N'Túi sau', 'PANEL', 2), ('QK-0308', N'Lưng quần', 'PANEL', 1),
 ('QK-0308', N'Khóa', 'TRIM', 1), ('QK-0308', N'Nút', 'TRIM', 1), ('QK-0308', N'Chỉ may', 'TRIM', 1), ('QK-0308', N'Nhãn dệt', 'TRIM', 1),
 ('AK-0522', N'Thân trước', 'PANEL', 2), ('AK-0522', N'Thân sau', 'PANEL', 1), ('AK-0522', N'Tay áo', 'PANEL', 2), ('AK-0522', N'Mũ', 'PANEL', 2),
 ('AK-0522', N'Thân lót', 'PANEL', 3), ('AK-0522', N'Khóa', 'TRIM', 1), ('AK-0522', N'Chỉ may', 'TRIM', 1), ('AK-0522', N'Nhãn dệt', 'TRIM', 1),
 ('AP-0715', N'Thân trước', 'PANEL', 2), ('AP-0715', N'Thân sau', 'PANEL', 1), ('AP-0715', N'Tay áo', 'PANEL', 2), ('AP-0715', N'Bo cổ', 'PANEL', 1),
 ('AP-0715', N'Nút', 'TRIM', 3), ('AP-0715', N'Chỉ may', 'TRIM', 1), ('AP-0715', N'Nhãn dệt', 'TRIM', 1),
 ('VD-0101', N'Thân váy', 'PANEL', 4), ('VD-0101', N'Chỉ may', 'TRIM', 1)) c (Code, Name, Type, Pieces)
JOIN dbo.Product p ON p.ProductCode = c.Code;

-- Định mức: vải / phụ liệu theo màu áo × thành phần (đã gồm màu NVL)
DECLARE @pcm TABLE (Prod VARCHAR(30), Color NVARCHAR(50), Comp NVARCHAR(100), Mat VARCHAR(30), Note NVARCHAR(200));
INSERT @pcm VALUES
 -- Áo sơ mi: vải theo màu áo; nút trắng, nhãn dùng chung, chỉ theo màu
 ('AS-0412', N'Trắng', N'Thân trước', 'V-KATE-TR', NULL), ('AS-0412', N'Trắng', N'Thân sau', 'V-KATE-TR', NULL),
 ('AS-0412', N'Trắng', N'Tay áo', 'V-KATE-TR', NULL), ('AS-0412', N'Trắng', N'Cổ áo', 'V-KATE-TR', N'Cổ cắt dọc canh sợi'),
 ('AS-0412', N'Trắng', N'Nút', 'T-NUT-4L', NULL), ('AS-0412', N'Trắng', N'Chỉ may', 'T-CHI-TR', NULL), ('AS-0412', N'Trắng', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 ('AS-0412', N'Xanh nhạt', N'Thân trước', 'V-KATE-XN', NULL), ('AS-0412', N'Xanh nhạt', N'Thân sau', 'V-KATE-XN', NULL),
 ('AS-0412', N'Xanh nhạt', N'Tay áo', 'V-KATE-XN', NULL), ('AS-0412', N'Xanh nhạt', N'Cổ áo', 'V-KATE-TR', N'Cổ phối vải trắng'),
 ('AS-0412', N'Xanh nhạt', N'Nút', 'T-NUT-4L', NULL), ('AS-0412', N'Xanh nhạt', N'Chỉ may', 'T-CHI-XN', NULL), ('AS-0412', N'Xanh nhạt', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 -- Quần kaki
 ('QK-0308', N'Be', N'Ống quần', 'V-KAKI-BE', NULL), ('QK-0308', N'Be', N'Túi sau', 'V-KAKI-BE', NULL), ('QK-0308', N'Be', N'Lưng quần', 'V-KAKI-BE', NULL),
 ('QK-0308', N'Be', N'Khóa', 'T-KHOA-18', NULL), ('QK-0308', N'Be', N'Nút', 'T-NUT-KAKI', NULL), ('QK-0308', N'Be', N'Chỉ may', 'T-CHI-BE', NULL), ('QK-0308', N'Be', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 ('QK-0308', N'Đen', N'Ống quần', 'V-KAKI-DEN', NULL), ('QK-0308', N'Đen', N'Túi sau', 'V-KAKI-DEN', NULL), ('QK-0308', N'Đen', N'Lưng quần', 'V-KAKI-DEN', NULL),
 ('QK-0308', N'Đen', N'Khóa', 'T-KHOA-18', NULL), ('QK-0308', N'Đen', N'Nút', 'T-NUT-KAKI', NULL), ('QK-0308', N'Đen', N'Chỉ may', 'T-CHI-DEN', NULL), ('QK-0308', N'Đen', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 -- Áo khoác gió: lớp ngoài theo màu, lớp lót taffeta đen chung
 ('AK-0522', N'Đen', N'Thân trước', 'V-GIO-DEN', NULL), ('AK-0522', N'Đen', N'Thân sau', 'V-GIO-DEN', NULL), ('AK-0522', N'Đen', N'Tay áo', 'V-GIO-DEN', NULL),
 ('AK-0522', N'Đen', N'Mũ', 'V-GIO-DEN', NULL), ('AK-0522', N'Đen', N'Thân lót', 'V-LOT-DEN', NULL),
 ('AK-0522', N'Đen', N'Khóa', 'T-KHOA-60D', NULL), ('AK-0522', N'Đen', N'Chỉ may', 'T-CHI-DEN', NULL), ('AK-0522', N'Đen', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 ('AK-0522', N'Xanh navy', N'Thân trước', 'V-GIO-NAVY', NULL), ('AK-0522', N'Xanh navy', N'Thân sau', 'V-GIO-NAVY', NULL), ('AK-0522', N'Xanh navy', N'Tay áo', 'V-GIO-NAVY', NULL),
 ('AK-0522', N'Xanh navy', N'Mũ', 'V-GIO-NAVY', NULL), ('AK-0522', N'Xanh navy', N'Thân lót', 'V-LOT-DEN', NULL),
 ('AK-0522', N'Xanh navy', N'Khóa', 'T-KHOA-60N', NULL), ('AK-0522', N'Xanh navy', N'Chỉ may', 'T-CHI-NAVY', NULL), ('AK-0522', N'Xanh navy', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 -- Áo polo nữ
 ('AP-0715', N'Hồng', N'Thân trước', 'V-POLO-HG', NULL), ('AP-0715', N'Hồng', N'Thân sau', 'V-POLO-HG', NULL), ('AP-0715', N'Hồng', N'Tay áo', 'V-POLO-HG', NULL),
 ('AP-0715', N'Hồng', N'Bo cổ', 'V-POLO-TR', N'Bo cổ phối trắng'), ('AP-0715', N'Hồng', N'Nút', 'T-NUT-4L', NULL), ('AP-0715', N'Hồng', N'Chỉ may', 'T-CHI-HG', NULL), ('AP-0715', N'Hồng', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 ('AP-0715', N'Trắng', N'Thân trước', 'V-POLO-TR', NULL), ('AP-0715', N'Trắng', N'Thân sau', 'V-POLO-TR', NULL), ('AP-0715', N'Trắng', N'Tay áo', 'V-POLO-TR', NULL),
 ('AP-0715', N'Trắng', N'Bo cổ', 'V-POLO-TR', NULL), ('AP-0715', N'Trắng', N'Nút', 'T-NUT-4L', NULL), ('AP-0715', N'Trắng', N'Chỉ may', 'T-CHI-TR', NULL), ('AP-0715', N'Trắng', N'Nhãn dệt', 'T-NHAN-DET', NULL),
 ('VD-0101', N'Trắng', N'Thân váy', 'V-CANIFA', NULL), ('VD-0101', N'Trắng', N'Chỉ may', 'T-CHI-TR', NULL);
INSERT dbo.ProductComponentMaterial (ProductId, ProductColorId, ProductComponentId, MaterialId, SpecNote, Status)
SELECT p.ProductId, pcol.ProductColorId, pc.ProductComponentId, m.MaterialId, x.Note, 'ACTIVE'
FROM @pcm x
JOIN dbo.Product p          ON p.ProductCode = x.Prod
JOIN dbo.ProductColor pcol  ON pcol.ProductId = p.ProductId AND pcol.Color = x.Color
JOIN dbo.ProductComponent pc ON pc.ProductId = p.ProductId AND pc.ComponentName = x.Comp
JOIN dbo.Material m         ON m.MaterialCode = x.Mat;

-- Nhóm bìa: mỗi nhóm một vải ở mỗi màu áo
DECLARE @mk TABLE (Prod VARCHAR(30), MarkerNo TINYINT, Comp NVARCHAR(100));
INSERT @mk VALUES
 ('AS-0412', 1, N'Thân trước'), ('AS-0412', 1, N'Thân sau'), ('AS-0412', 2, N'Tay áo'), ('AS-0412', 3, N'Cổ áo'),
 ('QK-0308', 1, N'Ống quần'), ('QK-0308', 1, N'Túi sau'), ('QK-0308', 1, N'Lưng quần'),
 ('AK-0522', 1, N'Thân trước'), ('AK-0522', 1, N'Thân sau'), ('AK-0522', 1, N'Tay áo'), ('AK-0522', 1, N'Mũ'), ('AK-0522', 2, N'Thân lót'),
 ('AP-0715', 1, N'Thân trước'), ('AP-0715', 1, N'Thân sau'), ('AP-0715', 1, N'Tay áo'), ('AP-0715', 2, N'Bo cổ');
INSERT dbo.ProductMarkerComponent (ProductId, MarkerNo, ProductComponentId)
SELECT p.ProductId, x.MarkerNo, pc.ProductComponentId
FROM @mk x JOIN dbo.Product p ON p.ProductCode = x.Prod
JOIN dbo.ProductComponent pc ON pc.ProductId = p.ProductId AND pc.ComponentName = x.Comp;
-- vải của nhóm = vải của chi tiết đầu tiên trong nhóm, theo từng màu áo
INSERT dbo.ProductMarkerFabric (ProductId, MarkerNo, MaterialId, ProductColorId)
SELECT mc.ProductId, mc.MarkerNo, pcm.MaterialId, pcm.ProductColorId
FROM (SELECT ProductId, MarkerNo, MIN(ProductComponentId) AS ProductComponentId FROM dbo.ProductMarkerComponent GROUP BY ProductId, MarkerNo) mc
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentId = mc.ProductComponentId;

PRINT N'02: danh mục, mã hàng, định mức xong.';
