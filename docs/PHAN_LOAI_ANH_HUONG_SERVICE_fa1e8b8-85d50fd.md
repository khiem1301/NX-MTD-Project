# Phân loại ảnh hưởng Service cần unit test (`fa1e8b8` → `85d50fd`)

Phạm vi lấy từ `PHAM_VI_ANH_HUONG_SERVICE_fa1e8b8-85d50fd.md`.  
Chỉ giữ các file **cần viết / cập nhật unit test**. Phần wiring/DI/refactor không đổi hành vi và file **không diff** trong range đã bỏ (không liệt kê).

---

## A. Ảnh hưởng code cũ

Đổi hành vi hàm/luồng đã có → test cũ dễ lệch, cần **viết lại hoặc mở rộng** case hiện hữu.

| File | Thay đổi chính | Hướng test |
|---|---|---|
| `be/Application/Services/Finance/FinanceService.cs` | Hòa vốn luôn tính/lưu **hai** `AllocationBasis`; DTO/`List` đổi; thêm `GetBreakEvenInputsAsync` | Viết lại assert (không còn 1 basis; 2× dòng kết quả; field mới); thêm case inputs |
| `be/Application/Services/Finance/OrderPaymentService.cs` | List/detail lấy `Amount` từ tổng `OrderLines` (+ `Include`) | Viết lại expect DTO/mock theo tổng dòng đơn |
| `be/Application/Services/Hr/EmployeeService.cs` | Đổi tổ may: gọi chốt/thu hồi (`SettleOnTeamLeaveAsync`) trước khi gán `TeamId` | Mở rộng case đổi tổ; mock `IDailyWorkAllocationService` |
| `be/Application/Services/Hr/TeamService.cs` | Transfer/remove thành viên tổ may: bọc settle trong transaction | Viết lại transfer/remove; mock allocation |
| `be/Application/Services/Orders/ProductSpecService.cs` | `Save`/validate nhận màu đã có ở mã hàng nhưng **chưa có size** | Mở rộng case save/validate color-only |
| `be/Application/Services/Orders/SpecImportService.cs` | Import: tạo màu thiếu, hỗ trợ màu chỉ có định mức; preview/cross-check đổi | Viết lại import/preview/plan |
| `be/Application/Services/Orders/SpecImportBuilder.cs` | Payload builder: size tạm `"?"` / bỏ variant color-only khi commit | Điều chỉnh case builder nhỏ |
| `be/Application/Services/Vehicle/VehicleService.cs` | Kết thúc chuyến: có `FuelCost` thì bắt buộc ảnh hóa đơn xăng | Thêm/viết lại case complete trip + fuel |

---

## B. Sửa thêm (feature / hàm mới)

Hàm hoặc loại mới; luồng cũ gần như giữ → test cũ **additive**, không cần rewrite hàng loạt.

| File | Thay đổi chính | Hướng test |
|---|---|---|
| `be/Application/Services/Attendance/AttendanceService.cs` | Thêm `AlertSyncOverdueAsync` (báo Admin 1 lần / đợt quá hạn sync RJ) | Case mới: overdue → notify 1 lần; không overdue → im |
| `be/Application/Services/Production/DailyWorkAllocationService.cs` | Thêm `SettleOnTeamLeaveAsync` (`Save*` không đổi) | Case mới settle: reject pending, confirm retrieval, đóng phân công |
| `be/Application/Services/Home/HomeDashboardService.Admin.cs` | Snapshot trang chủ Admin | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Manager.cs` | Snapshot trang chủ Manager | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Accountant.cs` | Snapshot trang chủ Accountant | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Warehouse.cs` | Snapshot trang chủ Warehouse | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.TeamLeader.cs` | Snapshot trang chủ TeamLeader | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Qaqc.cs` | Snapshot trang chủ QAQC | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Electrician.cs` | Snapshot trang chủ Electrician | Smoke/snapshot theo vai trò |
| `be/Application/Services/Home/HomeDashboardService.Worker.cs` | Snapshot trang chủ Worker | Smoke/snapshot theo vai trò |
| `be/Application/Services/Requests/NoLineRequestHandler.cs` | Base mới: đơn không dòng, chặn ngày cần quá khứ | Case validate no-line + due date |
| `be/Application/Services/Requests/MachineRepairRequestHandler.cs` | Handler mới `MACHINE_REPAIR` | Case tạo/xử lý đơn Sửa máy |

---

## Thứ tự ưu tiên khi viết lại test (sau Clean Re-baseline)

1. **P0 — nhóm A:** `FinanceService` → `OrderPaymentService` → `SpecImportService` + `ProductSpecService` + `SpecImportBuilder` → `EmployeeService` + `TeamService` (+ `SettleOnTeamLeave` qua mock/call)
2. **P1 — nhóm B nghiệp vụ nặng:** `DailyWorkAllocationService.SettleOnTeamLeaveAsync` + `VehicleService` fuel
3. **P2 — nhóm B mới:** `AttendanceService.AlertSyncOverdueAsync`, `MachineRepairRequestHandler` / `NoLineRequestHandler`, smoke `HomeDashboardService` theo vai trò
