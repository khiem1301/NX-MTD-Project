# Phạm vi ảnh hưởng Service (`fa1e8b8` → `85d50fd`)

Phạm vi lấy từ diff lần kéo `main` mới nhất (PR trang chủ theo vai trò, hòa vốn hai cách chia định phí, đơn Sửa máy, chốt sản lượng khi chuyển tổ, bắt ảnh hóa đơn xăng).

Working tree lúc ghi tài liệu: không còn thay đổi local (chỉ `.vscode/`). Nếu còn chỉnh chưa commit, đối chiếu lại diff hiện tại.

## 1. Service bị sửa trực tiếp

| File | Logic bị ảnh hưởng |
|---|---|
| `be/Application/Services/Attendance/AttendanceService.cs` | Thêm `AlertSyncOverdueAsync`: quá hạn đồng bộ RJ thì báo Admin một lần mỗi đợt |
| `be/Application/Services/Finance/FinanceService.cs` | Hòa vốn tính và lưu cả hai cách chia định phí; thêm `GetBreakEvenInputsAsync` kiểm dữ liệu đầu vào |
| `be/Application/Services/Finance/OrderPaymentService.cs` | Danh sách/chi tiết thanh toán đơn hàng thêm tổng tiền từ `OrderLines` |
| `be/Application/Services/Home/HomeDashboardService.cs` (+ các partial theo vai trò) | Service trang chủ mới cho 9 vai trò |
| `be/Application/Services/Hr/EmployeeService.cs` | Đổi tổ may: gọi chốt/thu hồi sản lượng tổ cũ trước khi gán `TeamId` mới |
| `be/Application/Services/Hr/TeamService.cs` | Chuyển người / giải tán tổ may: cùng bước chốt/thu hồi trước khi đổi tổ |
| `be/Application/Services/Production/DailyWorkAllocationService.cs` | Thêm `SettleOnTeamLeaveAsync`: từ chối phiếu giao chờ, xác nhận thu hồi chờ, thu hồi phần còn lại, đóng phân công |
| `be/Application/Services/Orders/ProductSpecService.cs` | Lưu định mức/bìa nhận cả màu đã có ở mã hàng nhưng chưa có size |
| `be/Application/Services/Orders/SpecImportService.cs` | Import Excel: tạo màu thiếu, hỗ trợ màu chỉ có định mức chưa có size |
| `be/Application/Services/Orders/SpecImportBuilder.cs` | Size tạm `"?"` cho màu chưa có biến thể |
| `be/Application/Services/Requests/NoLineRequestHandler.cs` | Base mới: đơn không có dòng, chặn ngày cần ở quá khứ |
| `be/Application/Services/Requests/MachineRepairRequestHandler.cs` | Handler mới loại `MACHINE_REPAIR` |
| `be/Application/Services/Requests/OtherRequestHandler.cs` | Đơn Khác chuyển sang kế thừa `NoLineRequestHandler` |
| `be/Application/Services/Vehicle/VehicleService.cs` | Kết thúc chuyến: có tiền xăng thì bắt buộc ảnh hóa đơn xăng |

### Partial của trang chủ (cùng `HomeDashboardService`)

- `be/Application/Services/Home/HomeDashboardService.Admin.cs`
- `be/Application/Services/Home/HomeDashboardService.Manager.cs`
- `be/Application/Services/Home/HomeDashboardService.Accountant.cs`
- `be/Application/Services/Home/HomeDashboardService.Warehouse.cs`
- `be/Application/Services/Home/HomeDashboardService.TeamLeader.cs`
- `be/Application/Services/Home/HomeDashboardService.Qaqc.cs`
- `be/Application/Services/Home/HomeDashboardService.Electrician.cs`
- `be/Application/Services/Home/HomeDashboardService.Worker.cs`

## 2. Service khác phụ thuộc / gọi vào phần vừa sửa

| File | Phụ thuộc gì |
|---|---|
| `be/Application/Services/Requests/RequestService.cs` | Qua `RequestTypeHandlers` gọi `ValidateAsync` / `AllowManualComplete` / `OnStatusChangedAsync` của handler Sửa máy và Đơn Khác |
| `be/Application/Services/Requests/RequestQueryService.cs` | Đọc `AllowManualComplete` của handler khi dựng quyền thao tác trên đơn |
| `be/Application/Services/Requests/RequestTypeHandlers.cs` | Đăng ký/lookup handler theo `RequestTypeCode` (gồm `MACHINE_REPAIR`) |
| `be/Application/Services/Orders/ExcelBundleService.cs` | Gọi `SpecImportService.CheckAsync` khi kiểm tra gói Excel (định mức/bìa theo màu) |

Hai chỗ gọi `SettleOnTeamLeaveAsync` là `EmployeeService` và `TeamService` (đã nằm ở mục 1).

`HomeDashboardService` chủ yếu **đọc** dữ liệu từ tiến độ đơn, chi phí, đơn từ, chấm công, NVL chờ nhận, lương tạm tính, suất ăn; không bị service khác trong Application gọi ngược lại.

## 3. Ngoài tầng Service (tham chiếu)

| File | Vai trò |
|---|---|
| `be/Infrastructure/Jobs/AttendanceSyncMonitorJob.cs` | Gọi `AlertSyncOverdueAsync` |
| `be/API/Controllers/*/HomeController.cs` | Gọi API trang chủ theo vai trò |
| `be/API/Controllers/Admin/BreakEvenController.cs` | Gọi `GetBreakEvenAsync` / `GetBreakEvenInputsAsync` |
| `be/API/Modules/HomeModule.cs` | Đăng ký `IHomeDashboardService` |
| `be/API/Modules/RequestModule.cs` | Đăng ký `MachineRepairRequestHandler` |
| `SQL/patches/2026-11-24_break_even_both_bases.sql` | Đổi khóa `UQ_BER` thêm `AllocationBasis` |
| `SQL/patches/2026-11-24_request_machine_repair.sql` | Thêm `MACHINE_REPAIR` vào `CK_Request_Type` |

## 4. Gợi ý khi viết / cập nhật unit test

Ưu tiên theo hàm mới hoặc đổi hợp đồng nghiệp vụ:

1. `DailyWorkAllocationService.SettleOnTeamLeaveAsync` (kèm luồng `EmployeeService` / `TeamService` đổi tổ may)
2. `FinanceService` hòa vốn hai basis + `GetBreakEvenInputsAsync`
3. `AttendanceService.AlertSyncOverdueAsync` (một lần báo / đợt quá hạn)
4. `VehicleService` kết thúc chuyến khi có `FuelCost` thiếu ảnh
5. `MachineRepairRequestHandler` / `NoLineRequestHandler` (validate không có dòng, ngày cần)
6. `SpecImportService` / `ProductSpecService` với màu chưa có size
7. `HomeDashboardService` theo từng vai trò (smoke / snapshot dữ liệu tổng hợp)
8. `OrderPaymentService` hiển thị tổng tiền từ dòng đơn
