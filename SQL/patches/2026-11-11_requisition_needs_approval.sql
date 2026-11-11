/* =====================================================================================
   UC07, UC40 · Claude (AI) · 11/11/2026 · DEV-ALL-98
   Mọi đơn từ tạo tay đều cần duyệt: Vật tư máy, Xin NVL, Xin BTP thêm bước duyệt.
   Đơn của ba loại này đang Chờ xử lý (tạo trước khi đổi) được chuyển về Chờ duyệt để người xử lý duyệt.
   Thêm trạng thái NOT_COMPLETED vào CK_Request_Status (+ CK_Request_NotComplete). Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_requisition_needs_approval.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE dbo.Request
SET Status = 'PENDING_APPROVAL', UpdatedAt = dbo.fn_VnNow()
WHERE Status = 'PENDING_PROCESSING'
  AND RequestTypeCode IN ('MACHINE_SUPPLIES', 'MATERIAL_REQUISITION', 'SEMI_FINISHED_REQUISITION');

COMMIT TRANSACTION;

/* ---- DEV-ALL-98: trạng thái NOT_COMPLETED (Không hoàn thành): đã duyệt nhưng người xử lý không làm được, bắt buộc có lý do ở ResultNote ---- */
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Request_Status')
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Status;
ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Status CHECK (Status IN
    ('PENDING_APPROVAL','PENDING_PROCESSING','APPROVED','REJECTED','COMPLETED','CANCELLED','NOT_COMPLETED'));

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Request_NotComplete')
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_NotComplete;
ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_NotComplete CHECK (Status <> 'NOT_COMPLETED' OR ResultNote IS NOT NULL);

COMMIT TRANSACTION;
