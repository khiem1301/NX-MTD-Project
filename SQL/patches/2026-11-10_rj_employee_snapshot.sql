/* =====================================================================================
   RjEmployeeSnapshot: cache nhẹ danh sách NV từ Ronald Jack Pro (MaChamCong + tên).
   Agent đẩy replace-all mỗi chu kỳ; web dùng để gán mã / prefill tạo nhân viên.
   Không lưu mật khẩu, vân tay, ảnh. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-10_rj_employee_snapshot.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.RjEmployeeSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RjEmployeeSnapshot (
        AttendanceCode  INT            NOT NULL CONSTRAINT PK_RjEmployeeSnapshot PRIMARY KEY,
        FullName        NVARCHAR(200)  NOT NULL,
        RjEmployeeCode  NVARCHAR(50)   NULL,
        SyncedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_RjEmp_SyncedAt DEFAULT (SYSUTCDATETIME())
    );
END;
GO

PRINT N'Đã thêm bảng RjEmployeeSnapshot.';
GO
