/* =====================================================================================
   UC07, UC08 · Claude (AI) · 11/11/2026 · DEV-ALL-91
   Đơn từ loại OTHER (Khác): mọi người dùng tạo, người tạo chọn vai trò xử lý.
     - Request thêm HandlerRoleCode, HandlerTeamId (FK Team); gửi tổ trưởng thì HandlerTeamId = tổ của người tạo.
     - CK_Request_Type thêm 'OTHER'; CK_Request_Handler: chỉ OTHER có vai trò xử lý.
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_request_other.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Request', 'HandlerRoleCode') IS NULL
    ALTER TABLE dbo.Request ADD HandlerRoleCode VARCHAR(30) NULL;
IF COL_LENGTH('dbo.Request', 'HandlerTeamId') IS NULL
    ALTER TABLE dbo.Request ADD HandlerTeamId INT NULL;
IF OBJECT_ID(N'dbo.FK_Request_HandlerTeam', N'F') IS NULL
    EXEC (N'ALTER TABLE dbo.Request ADD CONSTRAINT FK_Request_HandlerTeam FOREIGN KEY (HandlerTeamId) REFERENCES dbo.Team(TeamId);');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Request_Type')
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Type;
ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Type CHECK (RequestTypeCode IN ('SPARE_PARTS','NEW_MACHINE','MACHINE_SUPPLIES',
    'ATTENDANCE_CORRECTION','MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION',
    'SEMI_FINISHED_REQUISITION','MATERIAL_PURCHASE','OTHER'));

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Request_Handler')
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Handler;
EXEC (N'ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Handler CHECK (
    (RequestTypeCode = ''OTHER'' AND ((ISNULL(HandlerRoleCode, '''') = ''TEAM_LEADER'' AND HandlerTeamId IS NOT NULL)
        OR (ISNULL(HandlerRoleCode, '''') IN (''ADMIN'',''MANAGER'',''ACCOUNTANT'',''WAREHOUSE_KEEPER'') AND HandlerTeamId IS NULL)))
    OR (RequestTypeCode <> ''OTHER'' AND HandlerRoleCode IS NULL AND HandlerTeamId IS NULL));');

COMMIT;
GO

PRINT N'Đã thêm đơn từ loại OTHER (Khác) và cột HandlerRoleCode, HandlerTeamId.';
GO
