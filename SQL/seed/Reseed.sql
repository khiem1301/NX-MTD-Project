/* Chạy từ thư mục gốc dự án:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\seed\Reseed.sql
   Xóa sạch dữ liệu rồi tạo dữ liệu test mới (xem SQL/seed/README.md). Ngày tháng tính theo ngày chạy. */
:on error exit
:r SQL/seed/00_wipe.sql
GO
:r SQL/seed/01_people.sql
GO
:r SQL/seed/02_catalog.sql
GO
:r SQL/seed/03_operations.sql
GO
:r SQL/seed/04_orders.sql
GO
:r SQL/seed/05_purchasing.sql
GO
:r SQL/seed/06_cutting_warehouse.sql
GO
:r SQL/seed/07_sewing.sql
GO
:r SQL/seed/08_delivery.sql
GO
:r SQL/seed/09_attendance_meals.sql
GO
:r SQL/seed/10_requests_kpi.sql
GO
:r SQL/seed/11_payroll_costs.sql
GO
:r SQL/seed/12_stocktake_notify_audit.sql
GO
:r SQL/seed/99_finalize.sql
GO
