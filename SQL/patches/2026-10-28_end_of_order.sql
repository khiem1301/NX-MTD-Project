/* =====================================================================================
   Patch 28/10/2026 - Kiểm cuối đơn và tăng ca khi KPI không đạt
   - DefectRecord: thêm lý do END_OF_ORDER_MISMATCH (lệch kiểm cuối đơn, hệ thống ghi cho từng công nhân)
   - Request: thêm loại đơn OVERTIME (tăng ca khi KPI không đạt)
   - SystemSetting: KPI_OT_AUTO_THRESHOLD_PCT (ngưỡng thiếu ít, mặc định 10%)
   Chạy lại không lỗi (idempotent).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Reason' AND definition LIKE '%END_OF_ORDER_MISMATCH%')
BEGIN
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Reason;
    ALTER TABLE dbo.DefectRecord ADD CONSTRAINT CK_DR_Reason CHECK (DefectReasonCode IN
        ('SKIPPED_STITCH','CROOKED_SEAM','WRONG_SIZE','TORN','STAINED','OTHER','END_OF_ORDER_MISMATCH'));
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Request_Type' AND definition LIKE '%OVERTIME%')
BEGIN
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Type;
    ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Type CHECK (RequestTypeCode IN ('SPARE_PARTS','NEW_MACHINE','MACHINE_SUPPLIES',
        'ATTENDANCE_CORRECTION','MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION',
        'SEMI_FINISHED_REQUISITION','OVERTIME'));
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = 'KPI_OT_AUTO_THRESHOLD_PCT')
    INSERT dbo.SystemSetting (SettingKey, SettingValue, Description)
    VALUES ('KPI_OT_AUTO_THRESHOLD_PCT', N'10', N'KPI không đạt thiếu không quá số % này: tự duyệt tăng ca; thiếu nhiều hơn: tổ trưởng quyết định');

PRINT N'Đã áp dụng patch kiểm cuối đơn và tăng ca.';
