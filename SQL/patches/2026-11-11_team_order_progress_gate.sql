/* =====================================================================================
   TEAM_ORDER_PROGRESS_GATE_PCT: ngưỡng % nhập kho TP / SL đặt trước khi chia đơn mới cho tổ may.
   DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_team_order_progress_gate.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = N'TEAM_ORDER_PROGRESS_GATE_PCT')
BEGIN
    INSERT dbo.SystemSetting (SettingKey, SettingValue, Description)
    VALUES (N'TEAM_ORDER_PROGRESS_GATE_PCT', N'90',
            N'Tổ may chỉ nhận đơn mới khi đơn đang làm đạt % nhập kho TP / SL đặt');
END;
GO

PRINT N'Đã thêm SystemSetting TEAM_ORDER_PROGRESS_GATE_PCT.';
GO
