/* =====================================================================================
   DEV-ALL-74: họ tên nhân viên chỉ gồm chữ cái và khoảng trắng (không số, không ký tự đặc biệt).
     Đổi 5 tên nhân viên mẫu có số hoặc ký tự đặc biệt (TTMAY, TPMAY, CNMAY1, CNCAT1, QAQC1) sang chữ để còn sửa được sau quy tắc mới.
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi (chỉ đổi dòng còn tên cũ).
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-05_employee_name_letters_only.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

UPDATE e SET FullName = v.NewName
FROM dbo.Employee e
JOIN (VALUES ('TTMAY',  N'Tổ trưởng tổ may 1',  N'Tổ trưởng tổ may một'),
             ('TPMAY',  N'Tổ phó tổ may 1',     N'Tổ phó tổ may một'),
             ('CNMAY1', N'Công nhân may 1',     N'Công nhân may một'),
             ('CNCAT1', N'Công nhân cắt 1',     N'Công nhân cắt một'),
             ('QAQC1',  N'QA/QC mẫu',           N'Nhân viên kiểm hàng mẫu')) v(Code, OldName, NewName)
  ON v.Code = e.EmployeeCode AND e.FullName = v.OldName;
PRINT N'Đã đổi ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' tên nhân viên mẫu sang chữ.';
GO
