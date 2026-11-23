/* =====================================================================================
   DEV-ALL-169: đơn từ loại MACHINE_REPAIR (Sửa máy). Tổ trưởng / tổ phó tổ cắt và tổ may báo máy hỏng, Cơ điện xử lý.
   Chỉ thêm giá trị vào CK_Request_Type, không đổi cột. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-24_request_machine_repair.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_Request_Type' AND parent_object_id = OBJECT_ID(N'dbo.Request')
                 AND definition LIKE N'%MACHINE_REPAIR%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Request_Type' AND parent_object_id = OBJECT_ID(N'dbo.Request'))
        ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Type;
    ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Type CHECK (RequestTypeCode IN ('SPARE_PARTS','NEW_MACHINE','MACHINE_SUPPLIES','ATTENDANCE_CORRECTION',
        'MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION','SEMI_FINISHED_REQUISITION','MATERIAL_PURCHASE','OTHER',
        'MACHINE_REPAIR'));
END
GO

IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Request') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = N'Đơn từ (13 loại, gồm yêu cầu NVL / BTP / sửa máy)',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Request';
GO
