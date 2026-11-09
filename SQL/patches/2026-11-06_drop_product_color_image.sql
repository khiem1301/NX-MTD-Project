/* =====================================================================================
   DEV-ALL-80: bỏ ảnh theo màu của mã hàng. Xóa bảng ProductColorImage (ProductColor vẫn giữ, là nơi lưu tên màu).
     Dữ liệu ảnh theo màu bị xóa; file ảnh nằm trên ổ lưu trữ sẽ được job dọn ảnh mồ côi xóa sau 24 giờ.
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-06_drop_product_color_image.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ProductColorImage', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ProductColorImage;
    PRINT N'Đã xóa bảng ProductColorImage.';
END
ELSE PRINT N'Bảng ProductColorImage đã được xóa, bỏ qua.';
GO
