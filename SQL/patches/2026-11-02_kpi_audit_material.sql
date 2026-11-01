/* =====================================================================================
   Gộp các patch 31/10 - 02/11/2026 chưa đẩy lên remote: AuditLog.Summary; lý do ngừng hợp tác;
   bỏ Material.LengthCm; KPI tổ may. Chạy sau các patch 28/10 - 31/10 đã có trên remote.
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-02_kpi_audit_material.sql
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
