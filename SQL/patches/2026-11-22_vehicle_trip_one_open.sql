/* =====================================================================================
   DEV-ALL-158: một xe chỉ có một chuyến đang chạy (STARTED) - chặn ở DB, không chỉ ở code.
   - Thêm chỉ mục unique có lọc UQ_VT_OneOpen (VehicleId) WHERE Status = 'STARTED'.
   - Nếu DB đang có xe với từ hai chuyến STARTED, patch dừng và in danh sách: Kế toán kết thúc hoặc sửa các chuyến đó
     trước (không tự xóa / tự đóng chuyến), rồi chạy lại.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-22_vehicle_trip_one_open.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleTrip') AND name = N'UQ_VT_OneOpen')
    PRINT N'UQ_VT_OneOpen đã có.';
ELSE IF EXISTS (SELECT 1 FROM dbo.VehicleTrip WHERE Status = 'STARTED' GROUP BY VehicleId HAVING COUNT(*) > 1)
BEGIN
    SELECT VehicleTripId, VehicleId, TripDate, OdometerStart, DispatchNoteId
    FROM dbo.VehicleTrip
    WHERE Status = 'STARTED' AND VehicleId IN (SELECT VehicleId FROM dbo.VehicleTrip WHERE Status = 'STARTED' GROUP BY VehicleId HAVING COUNT(*) > 1)
    ORDER BY VehicleId, VehicleTripId;
    THROW 50001, N'Có xe đang có từ hai chuyến STARTED (danh sách ở trên). Kết thúc hoặc sửa các chuyến thừa rồi chạy lại patch.', 1;
END
ELSE
    CREATE UNIQUE INDEX UQ_VT_OneOpen ON dbo.VehicleTrip (VehicleId) WHERE Status = 'STARTED';
GO
