/* =====================================================================================
   03. CÔNG ĐOẠN, NHÓM CÔNG ĐOẠN, CHI TIẾT LÀM, ĐƠN GIÁ (đủ DRAFT / PENDING_APPROVAL / APPROVED / REJECTED, giá cũ - mới - tương lai)
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);

INSERT dbo.OperationGroup (ProductId, GroupName)
SELECT p.ProductId, g.GroupName
FROM (VALUES ('AS-0412', N'Thân áo'), ('AS-0412', N'Cổ và tay'), ('AS-0412', N'Hoàn thiện'),
             ('QK-0308', N'Thân quần'), ('QK-0308', N'Hoàn thiện'),
             ('AK-0522', N'Lớp ngoài'), ('AK-0522', N'Lớp lót'), ('AK-0522', N'Hoàn thiện'),
             ('AP-0715', N'Thân áo'), ('AP-0715', N'Hoàn thiện')) g (Code, GroupName)
JOIN dbo.Product p ON p.ProductCode = g.Code;

-- Prod | Mã CĐ | Tên | Thứ tự | Bậc khó | Giây chuẩn | Loại tổ | Cuối chuyền | Nhóm | Trạng thái | Chi tiết làm (ngăn bằng ;) | Giá gốc
DECLARE @op TABLE (Prod VARCHAR(30), Code VARCHAR(30), Name NVARCHAR(200), Seq SMALLINT, Skill TINYINT, Sec DECIMAL(9,2), TeamType VARCHAR(10),
                   IsFinal BIT, GroupName NVARCHAR(200), Status VARCHAR(20), Comps NVARCHAR(300), Rate DECIMAL(18,2));
INSERT @op VALUES
 ('AS-0412', 'OP01', N'Ráp vai',                  1, 3,  45, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Thân trước;Thân sau', 1300),
 ('AS-0412', 'OP02', N'May cổ áo',                2, 5, 130, 'SEWING', 0, N'Cổ và tay',  'ACTIVE', N'Cổ áo', 3200),
 ('AS-0412', 'OP03', N'May tay áo',               3, 3,  85, 'SEWING', 0, N'Cổ và tay',  'ACTIVE', N'Tay áo', 2100),
 ('AS-0412', 'OP04', N'Tra tay vào thân',         4, 4, 110, 'SEWING', 0, N'Cổ và tay',  'ACTIVE', N'Tay áo;Thân trước', 2700),
 ('AS-0412', 'OP05', N'Ráp sườn',                 5, 3,  75, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Thân trước;Thân sau', 1900),
 ('AS-0412', 'OP06', N'Vắt sổ',                   6, 2,  60, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Thân trước;Thân sau', 1500),
 ('AS-0412', 'OP07', N'Làm cửa tay',              7, 3,  70, 'SEWING', 0, N'Cổ và tay',  'ACTIVE', N'Tay áo', 1800),
 ('AS-0412', 'OP08', N'Thùa khuy, đính nút',      8, 3,  95, 'SEWING', 0, N'Hoàn thiện', 'ACTIVE', N'', 2300),
 ('AS-0412', 'OP09', N'Ủi, kiểm, đóng gói',       9, 2, 150, 'SEWING', 1, N'Hoàn thiện', 'ACTIVE', N'', 3500),
 ('AS-0412', 'CUT01', N'Cắt chi tiết theo bìa',  20, 3, 200, 'CUTTING', 0, NULL,         'ACTIVE', N'', NULL),
 ('AS-0412', 'OP10', N'May túi ngực (bỏ từ 2026)', 10, 3, 60, 'SEWING', 0, N'Thân áo',   'DISCONTINUED', N'', 1200),
 ('QK-0308', 'OP01', N'May túi sau',              1, 3,  90, 'SEWING', 0, N'Thân quần',  'ACTIVE', N'Túi sau', 1800),
 ('QK-0308', 'OP02', N'Ráp sườn, đũng',           2, 3, 120, 'SEWING', 0, N'Thân quần',  'ACTIVE', N'Ống quần', 2200),
 ('QK-0308', 'OP03', N'May lưng quần',            3, 4, 140, 'SEWING', 0, N'Thân quần',  'ACTIVE', N'Lưng quần', 2600),
 ('QK-0308', 'OP04', N'Tra khóa quần',            4, 4, 130, 'SEWING', 0, N'Thân quần',  'ACTIVE', N'Ống quần', 2400),
 ('QK-0308', 'OP05', N'Vắt sổ',                   5, 2,  70, 'SEWING', 0, N'Thân quần',  'ACTIVE', N'Ống quần;Túi sau', 1400),
 ('QK-0308', 'OP06', N'Thùa khuy, đính nút',      6, 3,  80, 'SEWING', 0, N'Hoàn thiện', 'ACTIVE', N'', 1700),
 ('QK-0308', 'OP07', N'Ủi, kiểm, đóng gói',       7, 2, 140, 'SEWING', 1, N'Hoàn thiện', 'ACTIVE', N'', 3000),
 ('AK-0522', 'OP01', N'Ráp thân ngoài',           1, 3, 100, 'SEWING', 0, N'Lớp ngoài',  'ACTIVE', N'Thân trước;Thân sau', 2200),
 ('AK-0522', 'OP02', N'May mũ',                   2, 4, 115, 'SEWING', 0, N'Lớp ngoài',  'ACTIVE', N'Mũ', 2500),
 ('AK-0522', 'OP03', N'May tay ngoài',            3, 3, 105, 'SEWING', 0, N'Lớp ngoài',  'ACTIVE', N'Tay áo', 2400),
 ('AK-0522', 'OP04', N'Tra khóa kéo toàn thân',   4, 5, 170, 'SEWING', 0, N'Lớp ngoài',  'ACTIVE', N'Thân trước', 3800),
 ('AK-0522', 'OP05', N'Ráp lót',                  5, 3, 130, 'SEWING', 0, N'Lớp lót',    'ACTIVE', N'Thân lót', 3000),
 ('AK-0522', 'OP06', N'Tra tay lót',              6, 4, 115, 'SEWING', 0, N'Lớp lót',    'ACTIVE', N'Thân lót;Tay áo', 2600),
 ('AK-0522', 'OP07', N'Vắt sổ',                   7, 2,  65, 'SEWING', 0, N'Hoàn thiện', 'ACTIVE', N'', 1500),
 ('AK-0522', 'OP08', N'Ủi, kiểm, đóng gói',       8, 2, 180, 'SEWING', 1, N'Hoàn thiện', 'ACTIVE', N'', 4000),
 ('AP-0715', 'OP01', N'Ráp vai',                  1, 3,  40, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Thân trước;Thân sau', 1200),
 ('AP-0715', 'OP02', N'Tra bo cổ',                2, 5, 105, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Bo cổ', 2400),
 ('AP-0715', 'OP03', N'May tay áo',               3, 3,  80, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Tay áo', 1900),
 ('AP-0715', 'OP04', N'Ráp sườn',                 4, 3,  70, 'SEWING', 0, N'Thân áo',    'ACTIVE', N'Thân trước;Thân sau', 1600),
 ('AP-0715', 'OP05', N'Đính nút',                 5, 2,  50, 'SEWING', 0, N'Hoàn thiện', 'ACTIVE', N'', 1200),
 ('AP-0715', 'OP06', N'Ủi, kiểm, đóng gói',       6, 2, 140, 'SEWING', 1, N'Hoàn thiện', 'ACTIVE', N'', 2800);

INSERT dbo.Operation (ProductId, OperationGroupId, OperationCode, OperationName, StationSequence, SkillLevel, StandardTimeSec, TeamType, IsFinalStep, Status)
SELECT p.ProductId, g.OperationGroupId, o.Code, o.Name, o.Seq, o.Skill, o.Sec, o.TeamType, o.IsFinal, o.Status
FROM @op o
JOIN dbo.Product p ON p.ProductCode = o.Prod
LEFT JOIN dbo.OperationGroup g ON g.ProductId = p.ProductId AND g.GroupName = o.GroupName;

INSERT dbo.OperationComponent (OperationId, ProductComponentId, ProductId)
SELECT op.OperationId, pc.ProductComponentId, op.ProductId
FROM @op o
JOIN dbo.Product p ON p.ProductCode = o.Prod
JOIN dbo.Operation op ON op.ProductId = p.ProductId AND op.OperationCode = o.Code
CROSS APPLY STRING_SPLIT(o.Comps, ';') s
JOIN dbo.ProductComponent pc ON pc.ProductId = p.ProductId AND pc.ComponentName = LTRIM(RTRIM(s.value))
WHERE s.value <> N'';

/* ---- Đơn giá ---- */
-- Giá gốc đã duyệt (hiệu lực từ 120 ngày trước), ngay trước đợt tăng giá
INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote)
SELECT op.OperationId, o.Rate, DATEADD(DAY, -120, @today), 'APPROVED',
       DATEADD(DAY, -125, CAST(@today AS DATETIME2)), DATEADD(DAY, -122, CAST(@today AS DATETIME2)), N'Bảng giá quý 3'
FROM @op o
JOIN dbo.Product p ON p.ProductCode = o.Prod
JOIN dbo.Operation op ON op.ProductId = p.ProductId AND op.OperationCode = o.Code
WHERE o.Rate IS NOT NULL AND o.Status = 'ACTIVE';

-- Đợt tăng ~8% đã duyệt, hiệu lực từ 40 ngày trước (áo sơ mi + quần kaki): sản lượng cũ vẫn tính giá cũ
INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote)
SELECT op.OperationId, ROUND(o.Rate * 1.08 / 50, 0) * 50, DATEADD(DAY, -40, @today), 'APPROVED',
       DATEADD(DAY, -46, CAST(@today AS DATETIME2)), DATEADD(DAY, -44, CAST(@today AS DATETIME2)), N'Điều chỉnh giá quý 4 theo giá thị trường'
FROM @op o
JOIN dbo.Product p ON p.ProductCode = o.Prod AND p.ProductCode IN ('AS-0412', 'QK-0308')
JOIN dbo.Operation op ON op.ProductId = p.ProductId AND op.OperationCode = o.Code
WHERE o.Rate IS NOT NULL AND o.Status = 'ACTIVE';

-- Các trạng thái còn lại
DECLARE @r TABLE (Prod VARCHAR(30), Code VARCHAR(30), Rate DECIMAL(18,2), EffOff INT, Status VARCHAR(20), SubOff INT, DecOff INT, Note NVARCHAR(500));
INSERT @r VALUES
 ('AS-0412', 'OP02', 3600, 7,   'APPROVED',         -5, -3,   N'Tăng giá cổ áo do đổi kiểu cổ, áp dụng từ tuần sau'),   -- giá mới đã duyệt, hiệu lực tương lai
 ('AK-0522', 'OP04', 4200, 5,   'PENDING_APPROVAL', -2, NULL, NULL),
 ('AP-0715', 'OP02', 2650, 3,   'PENDING_APPROVAL', -1, NULL, NULL),
 ('QK-0308', 'OP03', 2900, 10,  'DRAFT',            NULL, NULL, NULL),
 ('AS-0412', 'OP01', 1500, -10, 'REJECTED',         -14, -12, N'Mức tăng 15% vượt khung quý, đề nghị trình lại'),
 ('AK-0522', 'OP08', 4300, -20, 'REJECTED',         -25, -22, N'Chưa có biên bản đo thời gian chuẩn');
INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote)
SELECT op.OperationId, r.Rate, DATEADD(DAY, r.EffOff, @today), r.Status,
       CASE WHEN r.SubOff IS NOT NULL THEN DATEADD(DAY, r.SubOff, CAST(@today AS DATETIME2)) END,
       CASE WHEN r.Status = 'APPROVED' THEN DATEADD(DAY, r.DecOff, CAST(@today AS DATETIME2)) END,
       r.Note
FROM @r r
JOIN dbo.Product p ON p.ProductCode = r.Prod
JOIN dbo.Operation op ON op.ProductId = p.ProductId AND op.OperationCode = r.Code;
UPDATE dbo.PieceRate SET DecisionNote = N'Mức tăng 15% vượt khung quý, đề nghị trình lại' WHERE Status = 'REJECTED' AND DecisionNote IS NULL;

PRINT N'03: công đoạn, đơn giá xong.';
