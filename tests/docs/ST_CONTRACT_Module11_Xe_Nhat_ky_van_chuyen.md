# ST_CONTRACT — Module 11: Xe & Nhật ký vận chuyển

> Nguồn: `fe/src/features/vehicle-log/*`, `features/vehicle-status/*`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/accounting/vehicle-log` | VehicleLogPage ("Sổ xe và nhiên liệu": tab "Chuyến xe", tab "Danh mục xe") | Accountant | Tab Danh mục xe: Thêm/Sửa/Ngừng dùng/Dùng lại; tab Chuyến xe: Ghi chuyến mới → Kết thúc chuyến |
| `/admin/vehicles` | VehicleStatusPage ("Xe và trạng thái hoạt động") | Admin | Chỉ xem: Đang chạy / Rảnh, chuyến chưa kết thúc |

- Role sai → redirect `/`. Chuyến xe có thể tự sinh từ phiếu xuất hàng (Module 10: Thủ kho xuất hàng tạo chuyến "Đang chạy"; cột "Phiếu xuất"); Kế toán chỉ kết thúc chuyến.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tab Chuyến xe / Danh mục xe | Tabs | `GetByRole(Tab, "Chuyến xe")`, `GetByRole(Tab, "Danh mục xe")` |
| Tìm / lọc Trạng thái | "Tìm theo mã, tên..."; Select | `GetByPlaceholder("Tìm theo mã, tên...")` |
| Thêm xe | Button primary | `GetByRole(Button, "Thêm xe")` |
| Biển số | required+whitespace, max 20, pattern `^[1-9]\d[A-Z]{1,2}\d?-(\d{3}\.\d{2}\|\d{4,5})$` ("Biển số không đúng định dạng (ví dụ 51C-123.45 hoặc 30F-1234)."), placeholder `51C-123.45` | `GetByLabel("Biển số")` |
| Loại xe | required+whitespace, max 50 | `GetByLabel("Loại xe")` |
| Định mức (lít / 100 km) | required, 0.1–9999 | `GetByLabel("Định mức (lít / 100 km)")` |
| Ngừng dùng / Dùng lại | Popconfirm "Ngừng sử dụng xe này? Xe không được ghi thêm chuyến nhưng dữ liệu cũ vẫn giữ." | `row.GetByRole(Button, "Ngừng dùng")`, `"Dùng lại"` |
| Bảng xe | Biển số, Loại xe, Định mức, Trạng thái, Số chuyến, Km hiện tại | `GetByRole(Row, new{Name=<biển số>})` |
| Ghi chuyến mới | Button primary → modal | `GetByRole(Button, "Ghi chuyến mới")` |
| Xe | Select required, placeholder "Chọn xe" | `GetByLabel("Xe")` |
| Ngày | DatePicker required | `GetByLabel("Ngày")` |
| Tài xế | required+whitespace, max 100 | `GetByLabel("Tài xế")` |
| Mục đích | max 300 | `GetByLabel("Mục đích")` |
| Km đầu | required, 0–9.999.999 | `GetByLabel("Km đầu")` |
| Kết thúc chuyến | Km cuối required, > Km đầu ("Km cuối phải lớn hơn km đầu ({n})."), ≤ 9.999.999; Tiền xăng 1–100.000.000 (bước 1000); Ảnh phiếu xăng tối đa 5 (bắt buộc khi có Tiền xăng: "Nhập tiền xăng thì phải có ít nhất một ảnh hóa đơn xăng…") | `GetByRole(Button, "Kết thúc chuyến")`, `GetByLabel("Km cuối")`, `GetByLabel("Tiền xăng")`, `Locator("input[type=file]")` |
| Bảng chuyến | Ngày, Xe, Tài xế, Mục đích, Phiếu xuất, Km đầu/cuối, Quãng đường, Định mức (lít), Tiền xăng, Cảnh báo (Lệch km, Xăng vượt định mức) | `GetByRole(Row)`; lọc "Chỉ chuyến có cảnh báo" |
| Admin: lọc "Trạng thái chạy" | Select Đang chạy / Không chạy | `GetByPlaceholder("Trạng thái chạy")` |
| Admin: bảng trạng thái xe | Biển số, Loại xe, Tình trạng xe, Đang chạy/Rảnh, Tài xế, Phiếu xuất, Ngày chuyến, Km đầu chuyến, Km hiện tại; cảnh báo "chuyến chưa kết thúc" | `GetByText("Đang chạy")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách xe | `GET /api/v1/accountant/vehicles` | `VehicleQuery` | Bảng xe |
| Thêm / sửa xe | `POST /api/v1/accountant/vehicles`; `PUT .../vehicles/{id}` | `{plate, vehicleType, fuelNorm}` | Toast "Đã lưu xe." |
| Ngừng / Dùng lại | `POST .../vehicles/{id}/discontinue`, `.../reactivate` | — | Trạng thái đổi |
| Danh sách chuyến | `GET /api/v1/accountant/vehicle-trips` | `TripQuery` (lọc cảnh báo) | Bảng chuyến |
| Ghi chuyến | `POST /api/v1/accountant/vehicle-trips` | `{vehicleId, tripDate, driver, purpose?, odometerStart}` | Toast "Đã ghi chuyến, chờ kết thúc chuyến." |
| Kết thúc chuyến | `PUT/POST /api/v1/accountant/vehicle-trips/{id}/complete` (`completeTrip`) | `{odometerEnd, fuelCost?, photos}` | Toast "Đã kết thúc chuyến."; trạng thái "Đã kết thúc" |
| Trạng thái xe (Admin) | `GET /api/v1/admin/vehicles` | `VehicleLiveStatusQuery` | Bảng trạng thái |

## 4. Điểm nghi vấn lệch pha

- `odometerEndTooSmall` chỉ kiểm km cuối > km đầu của chính chuyến; "Lệch km" (so với km cuối chuyến trước) chỉ là cảnh báo hiển thị trong bảng, FE không chặn lúc nhập.
- Tiền xăng không bắt buộc ở FE, nhưng nếu nhập thì bắt buộc ≥ 1 ảnh (`fuelReceiptRequired`) — kiểm ảnh ở submit chứ không ngay tại ô.
- `Ngày` chuyến không chặn ngày tương lai.
- Biển số: pattern chặn theo định dạng ô tô; FE không tự viết hoa → nhập chữ thường bị coi là sai định dạng.
- Kế toán có "Ghi chuyến mới" nhưng chuyến cũng tự sinh từ phiếu xuất: FE không chặn Kế toán ghi chuyến cho xe đang có chuyến chưa kết thúc (kiểm ở BE).
- Chuyến xe không có nút Sửa/Xóa trên FE: sai thông tin chỉ có thể xử lý qua kết thúc chuyến.
- Admin `/admin/vehicles` chỉ xem; nhãn "Rảnh" lấy từ trạng thái trả về, không tính lại ở FE.
- Nút Ngừng/Dùng lại là IconAction → nên thêm `data-testid`.
