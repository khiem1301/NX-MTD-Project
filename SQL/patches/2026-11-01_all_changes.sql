/* =====================================================================================
   Gộp các patch 31/10 - 01/11/2026 chưa đẩy lên remote, chạy sau các patch 28/10 - 31/10 đã có trên remote:
     1. AuditLog.Summary            2. Lý do ngừng hợp tác          3. Bỏ Material.LengthCm        4. KPI tổ may
     5. Ảnh phụ của mã hàng         6. Attachment.RemovedAt         7. Bỏ ProductVariant.LengthCm, WidthCm
     8. Hàng hỏng: trạng thái CONFIRMED
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-01_all_changes.sql
   ===================================================================================== */

/* =========================== 1. AuditLog.Summary =========================== */
-- AuditLog.Summary: câu mô tả tiếng Việt của dòng nhật ký, vd. "Cập nhật đơn hàng DH-0012: Trạng thái Mới → Đang sản xuất".
-- Backend dựng lúc ghi (AuditText). Dòng cũ để NULL, giao diện tự hiện "{hành động} {đối tượng} #{Id}".
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.AuditLog', 'Summary') IS NULL
    ALTER TABLE dbo.AuditLog ADD Summary NVARCHAR(500) NULL;

COMMIT TRANSACTION;
PRINT N'Đã thêm AuditLog.Summary.';
GO

/* =========================== 2. Lý do ngừng hợp tác =========================== */
-- Supplier / Customer: lý do ngừng hợp tác (bắt buộc khi ngừng, xóa khi hợp tác lại). Dòng đã ngừng từ trước để NULL.
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Supplier', 'DiscontinueReason') IS NULL
    ALTER TABLE dbo.Supplier ADD DiscontinueReason NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Customer', 'DiscontinueReason') IS NULL
    ALTER TABLE dbo.Customer ADD DiscontinueReason NVARCHAR(500) NULL;

COMMIT TRANSACTION;
PRINT N'Đã thêm Supplier.DiscontinueReason và Customer.DiscontinueReason.';
GO

/* =========================== 3. Bỏ Material.LengthCm =========================== */
-- Material: bỏ cột LengthCm (chiều dài cuộn / tấm) vì không dùng ở đâu; giữ WidthCm (khổ vải) để ước tính vải cắt bù.
-- ProductVariant.LengthCm và ProductComponentMaterial.LengthCm (kích thước thành phẩm / chi tiết) vẫn giữ.
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Material_Size' AND parent_object_id = OBJECT_ID('dbo.Material'))
    ALTER TABLE dbo.Material DROP CONSTRAINT CK_Material_Size;

IF COL_LENGTH('dbo.Material', 'LengthCm') IS NOT NULL
    ALTER TABLE dbo.Material DROP COLUMN LengthCm;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Material_Size' AND parent_object_id = OBJECT_ID('dbo.Material'))
    ALTER TABLE dbo.Material ADD CONSTRAINT CK_Material_Size CHECK (WidthCm IS NULL OR WidthCm > 0);

COMMIT TRANSACTION;
PRINT N'Đã bỏ Material.LengthCm.';
GO

/* =========================== 4. KPI tổ may =========================== */
-- KPI tổ may: một đơn + mã hàng + tổ có nhiều chỉ tiêu theo các kỳ không trùng ngày (KpiService kiểm), chỉ tiêu đã hủy không tính;
-- KPI theo ngày; khóa sửa KPI ngày sau KPI_EDIT_CUTOFF_TIME; cảnh báo theo ngày giao KPI_DUE_WARNING_DAYS (DEV-ALL-23, 24, 25).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Kpi' AND parent_object_id = OBJECT_ID('dbo.KpiTarget'))
    ALTER TABLE dbo.KpiTarget DROP CONSTRAINT UQ_Kpi;

IF EXISTS (SELECT 1 FROM sys.indexes i
           JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
           WHERE i.name = 'UQ_Kpi' AND i.object_id = OBJECT_ID('dbo.KpiTarget')
           GROUP BY i.name HAVING COUNT(*) = 3)
    DROP INDEX UQ_Kpi ON dbo.KpiTarget;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Kpi' AND object_id = OBJECT_ID('dbo.KpiTarget'))
    CREATE UNIQUE INDEX UQ_Kpi ON dbo.KpiTarget(OrderId, ProductId, SewingTeamId, StartDate) WHERE Status <> 'CANCELLED';

IF OBJECT_ID('dbo.KpiDailyTarget', 'U') IS NULL
    CREATE TABLE dbo.KpiDailyTarget (
        KpiDailyTargetId INT IDENTITY(1,1) CONSTRAINT PK_KpiDailyTarget PRIMARY KEY,
        KpiTargetId      INT  NOT NULL CONSTRAINT FK_KpiDaily_Kpi REFERENCES dbo.KpiTarget(KpiTargetId),
        WorkDate         DATE NOT NULL,
        TargetQty        INT  NOT NULL CONSTRAINT CK_KpiDaily_Qty CHECK (TargetQty > 0),
        CONSTRAINT UQ_KpiDaily UNIQUE (KpiTargetId, WorkDate)
    );

IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID('dbo.KpiDailyTarget') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'KPI tổ may theo từng ngày',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'KpiDailyTarget';

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = 'KPI_EDIT_CUTOFF_TIME')
    INSERT dbo.SystemSetting (SettingKey, SettingValue, Description)
    VALUES ('KPI_EDIT_CUTOFF_TIME', N'08:00', N'Sau giờ này không sửa được KPI theo ngày của chính ngày đó');

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = 'KPI_DUE_WARNING_DAYS')
    INSERT dbo.SystemSetting (SettingKey, SettingValue, Description)
    VALUES ('KPI_DUE_WARNING_DAYS', N'3', N'Cảnh báo khi kỳ KPI kết thúc cách ngày giao hàng không quá số ngày này');

COMMIT TRANSACTION;
GO

-- Chỉ tiêu ngày lấy từ KpiDailyTarget; KPI chưa đặt ngày nào thì vẫn chia đều. Có dòng cho mọi ngày đã đặt KPI (chưa có hàng = 0).
CREATE OR ALTER VIEW dbo.vw_KpiDailyResult
AS
SELECT  k.KpiTargetId, k.SewingTeamId, k.OrderId, k.ProductId, dd.WorkDate,
        CAST(tg.DailyTarget AS DECIMAL(18,2)) AS DailyTarget,
        ISNULL(q.FinishedQty, 0) AS FinishedQty,
        ISNULL(d.DefectQty, 0) AS DefectQty,
        CASE WHEN ISNULL(q.FinishedQty, 0) + ISNULL(d.DefectQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * ISNULL(d.DefectQty, 0) / (ISNULL(q.FinishedQty, 0) + ISNULL(d.DefectQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct,
        CASE WHEN ISNULL(tg.DailyTarget, 0) = 0 THEN NULL
             ELSE CAST(100.0 * ISNULL(q.FinishedQty, 0) / tg.DailyTarget AS DECIMAL(9,2)) END AS PercentAchieved
FROM dbo.KpiTarget k
CROSS APPLY (SELECT x.WorkDate FROM dbo.KpiDailyTarget x WHERE x.KpiTargetId = k.KpiTargetId
             UNION
             SELECT b.WorkDate
             FROM dbo.QcBatch b JOIN dbo.OrderLine ol ON ol.OrderLineId = b.OrderLineId
             WHERE b.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND b.WorkDate BETWEEN k.StartDate AND k.EndDate) dd
LEFT JOIN dbo.KpiDailyTarget p ON p.KpiTargetId = k.KpiTargetId AND p.WorkDate = dd.WorkDate
CROSS APPLY (SELECT CASE WHEN p.TargetQty IS NOT NULL THEN 1.0 * p.TargetQty
                         WHEN EXISTS (SELECT 1 FROM dbo.KpiDailyTarget y WHERE y.KpiTargetId = k.KpiTargetId) THEN NULL
                         ELSE 1.0 * k.TargetFinishedQty / k.WorkingDays END AS DailyTarget) tg
OUTER APPLY (SELECT SUM(b.FinishedQty) AS FinishedQty
             FROM dbo.QcBatch b JOIN dbo.OrderLine ol ON ol.OrderLineId = b.OrderLineId
             WHERE b.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND b.WorkDate = dd.WorkDate) q
OUTER APPLY (SELECT SUM(dr.Quantity) AS DefectQty
             FROM dbo.DefectRecord dr JOIN dbo.OrderLine ol ON ol.OrderLineId = dr.OrderLineId
             WHERE dr.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND dr.WorkDate = dd.WorkDate AND dr.Status <> 'CANCELLED') d;
GO

PRINT N'Đã cập nhật KPI tổ may.';
GO
GO

/* =========================== 5. Ảnh phụ của mã hàng =========================== */
-- Mã hàng có một ảnh chính (Product.SampleImageUrl, giữ nguyên) và tối đa 10 ảnh phụ (bảng mới dbo.ProductImage).
-- SortOrder 1-10, mỗi vị trí một ảnh: database tự chặn ảnh phụ thứ 11.
-- Database đã chạy bản cũ (tối đa 5 ảnh) thì nới ràng buộc lên 10, dữ liệu giữ nguyên.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.ProductImage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductImage (
        ProductImageId  INT IDENTITY(1,1) CONSTRAINT PK_ProductImage PRIMARY KEY,
        ProductId       INT           NOT NULL CONSTRAINT FK_ProductImage_Product REFERENCES dbo.Product(ProductId),
        ImageKey        NVARCHAR(500) NOT NULL,   -- khóa ảnh tải từ máy, vd PRODUCT/2026/11/<guid>.jpg
        SortOrder       TINYINT       NOT NULL,   -- thứ tự hiển thị 1-10
        CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_ProductImage_CreatedAt DEFAULT (dbo.fn_VnNow()),
        CONSTRAINT CK_ProductImage_Sort CHECK (SortOrder BETWEEN 1 AND 10),
        CONSTRAINT UQ_ProductImage_Sort UNIQUE (ProductId, SortOrder)
    );
    PRINT N'Đã tạo bảng ProductImage.';
END
ELSE IF EXISTS (SELECT 1 FROM sys.check_constraints
                WHERE name = 'CK_ProductImage_Sort' AND parent_object_id = OBJECT_ID('dbo.ProductImage')
                  AND definition LIKE N'%(5)%')
BEGIN
    ALTER TABLE dbo.ProductImage DROP CONSTRAINT CK_ProductImage_Sort;
    ALTER TABLE dbo.ProductImage ADD CONSTRAINT CK_ProductImage_Sort CHECK (SortOrder BETWEEN 1 AND 10);
    PRINT N'Đã nới CK_ProductImage_Sort lên 10 ảnh phụ.';
END
ELSE
    PRINT N'ProductImage đã đúng, bỏ qua.';
GO

/* =========================== 6. Attachment.RemovedAt =========================== */
-- Ghi lúc gỡ ảnh (IsCurrent = 0) để job dọn tệp xóa file và dòng của ảnh đã gỡ sau 15 ngày.
-- Ảnh đã gỡ từ trước không biết gỡ lúc nào nên lấy giờ chạy patch làm mốc: được giữ thêm 15 ngày kể từ hôm nay.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.Attachment', 'RemovedAt') IS NOT NULL
BEGIN
    PRINT N'Attachment.RemovedAt đã có, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;
ALTER TABLE dbo.Attachment ADD RemovedAt DATETIME2(0) NULL;
-- Cột vừa thêm chưa dùng được trong cùng lô, nên cập nhật bằng EXEC.
EXEC(N'UPDATE dbo.Attachment SET RemovedAt = dbo.fn_VnNow() WHERE IsCurrent = 0 AND RemovedAt IS NULL');
COMMIT TRANSACTION;
PRINT N'Đã thêm Attachment.RemovedAt.';
GO

/* =========================== 7. Bỏ ProductVariant.LengthCm, WidthCm =========================== */
-- Số mét vải của lệnh cắt tính từ kích thước chi tiết (ProductComponentMaterial.LengthCm, WidthCm), không còn dùng kích thước theo màu – size.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.ProductVariant', 'LengthCm') IS NULL AND COL_LENGTH('dbo.ProductVariant', 'WidthCm') IS NULL
BEGIN
    PRINT N'ProductVariant đã bỏ LengthCm, WidthCm, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;
IF OBJECT_ID('dbo.CK_PV_Size', 'C') IS NOT NULL ALTER TABLE dbo.ProductVariant DROP CONSTRAINT CK_PV_Size;
IF COL_LENGTH('dbo.ProductVariant', 'LengthCm') IS NOT NULL ALTER TABLE dbo.ProductVariant DROP COLUMN LengthCm;
IF COL_LENGTH('dbo.ProductVariant', 'WidthCm') IS NOT NULL ALTER TABLE dbo.ProductVariant DROP COLUMN WidthCm;
COMMIT TRANSACTION;
PRINT N'Đã bỏ ProductVariant.LengthCm, WidthCm.';
GO
GO

/* =========================== 8. Hàng hỏng: trạng thái CONFIRMED =========================== */
-- Công nhân phản đối phiếu thì Quản đốc / Admin chấp nhận phiếu (Status = CONFIRMED) hoặc hủy phiếu (CANCELLED có sẵn).
-- Không thêm cột: giờ quyết định ở UpdatedAt, người và ghi chú ở AuditLog. Phiếu quá 48 giờ không bị phản đối cũng được job nền chuyển sang CONFIRMED.
-- Database đã chạy bản đầu (thêm 7 cột HandledAt, HandledBy, HandledNote, ObjectionResolution, ObjectionResolvedNote, ObjectionResolvedBy, ObjectionResolvedAt) thì bước này bỏ các cột và ràng buộc đó.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- Dọn bản đầu (nếu đã chạy): ràng buộc, khóa ngoại, rồi 7 cột.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Objection' AND parent_object_id = OBJECT_ID('dbo.DefectRecord'))
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Objection;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_ObjectionCols' AND parent_object_id = OBJECT_ID('dbo.DefectRecord'))
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_ObjectionCols;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Handled' AND parent_object_id = OBJECT_ID('dbo.DefectRecord'))
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Handled;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DR_ResolvedBy') ALTER TABLE dbo.DefectRecord DROP CONSTRAINT FK_DR_ResolvedBy;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DR_HandledBy')  ALTER TABLE dbo.DefectRecord DROP CONSTRAINT FK_DR_HandledBy;
IF COL_LENGTH('dbo.DefectRecord', 'HandledAt') IS NOT NULL
    ALTER TABLE dbo.DefectRecord DROP COLUMN ObjectionResolution, ObjectionResolvedNote, ObjectionResolvedBy, ObjectionResolvedAt,
                                             HandledAt, HandledBy, HandledNote;
GO

-- Status thêm giá trị CONFIRMED.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Status' AND parent_object_id = OBJECT_ID('dbo.DefectRecord')
               AND definition LIKE '%CONFIRMED%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Status' AND parent_object_id = OBJECT_ID('dbo.DefectRecord'))
        ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Status;
    ALTER TABLE dbo.DefectRecord ADD CONSTRAINT CK_DR_Status CHECK (Status IN ('RECORDED','CONFIRMED','CANCELLED','LOCKED'));
END
GO
PRINT N'Đã thêm trạng thái CONFIRMED cho phiếu hàng hỏng.';
GO
