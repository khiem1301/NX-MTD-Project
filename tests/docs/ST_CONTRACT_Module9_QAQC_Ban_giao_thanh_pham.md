# ST_CONTRACT — Module 9: QA/QC & Bàn giao thành phẩm

> Nguồn: `fe/src/features/production/{pages/QcBatchesPage,ManagerQcBatchesPage,components/Qc*,constants/outputText}`, `features/fg-handover/*`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/qc-batches` | QcBatchesPage ("Hàng hoàn thiện (QC)") | QaQc | Lọc theo ngày → Thêm đợt kiểm → Sửa số hàng đạt / Xóa |
| `/manager/qc-batches` | ManagerQcBatchesPage ("Sửa số hàng đạt QC") | Admin, Manager | Sửa hoặc xóa đợt QC kể cả sau hết ca (bắt buộc lý do) |
| `/warehouse/fg-handovers` | FgHandoverPage ("Bàn giao thành phẩm") + FgHandoverModal | QaQc, WarehouseKeeper | QA/QC: Lập phiếu bàn giao / Rút phiếu; Thủ kho: Nhận vào kho / Từ chối |

- Role sai → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Lọc theo ngày | DatePicker, placeholder "Lọc theo ngày" | `GetByPlaceholder("Lọc theo ngày")` |
| Thêm đợt kiểm | Button primary → modal "Thêm đợt kiểm hoàn thiện" | `GetByRole(Button, "Thêm đợt kiểm")` |
| Ngày / Ca / Giờ kiểm | Ngày, Ca, Giờ kiểm: required | `GetByLabel("Ngày")`, `GetByLabel("Ca")`, `GetByLabel("Giờ kiểm")` |
| Tổ may / Chọn đơn hàng / Dòng đơn | Select required | `GetByLabel("Tổ may")`, `GetByLabel("Dòng đơn")` |
| Số hàng đạt | required, ≥ 1 ("Số hàng đạt phải lớn hơn 0."), ≤ 1.000.000 ("Số hàng đạt tối đa 1.000.000.") | `GetByLabel("Số hàng đạt")` |
| Hàng trên chuyền | Thông tin "Nhận {a} - đạt {b} - hỏng hủy {c} = còn {d}" | `GetByText("Hàng trên chuyền")` |
| Sửa số hàng đạt (QcEditModal) | Số hàng đạt như trên; Lý do (bắt buộc khi sửa sau hết ca): required+whitespace, max `REASON_MAX_LENGTH` ("Vui lòng nhập lý do.") | `GetByRole(Button, "Sửa")`, `GetByLabel("Lý do (bắt buộc khi sửa sau hết ca)")` |
| Xóa đợt kiểm | Modal "Xóa đợt kiểm" + lý do | `GetByRole(Button, "Xóa")` |
| Toast QC | "Đã thêm đợt kiểm." / "Đã cập nhật đợt kiểm." | `AssertToastAsync("Đã thêm đợt kiểm.")` |
| Lập phiếu bàn giao | Button "Lập phiếu bàn giao"; Chọn đơn hàng → mã hàng; bảng Màu - size / QC đạt / Đã giao / Còn giao được / Giao lần này (0–còn giao được, disable khi còn 0); Modal.confirm `confirmCreate` | `GetByRole(Button, "Lập phiếu bàn giao")`, `GetByRole(Button, "Xác nhận")` |
| Rút phiếu (QA/QC) | Popconfirm "Rút phiếu" | `GetByRole(Button, "Rút phiếu")` |
| Nhận vào kho (Thủ kho) | "Số kho đếm" required, 0–số giao; "Lý do lệch" bắt buộc khi lệch | `GetByRole(Button, "Nhận vào kho")`, `GetByLabel("Số kho đếm")`, `GetByLabel("Lý do lệch")` |
| Từ chối (Thủ kho) | modal "Từ chối phiếu bàn giao thành phẩm" + "Lý do từ chối" | `GetByRole(Button, "Từ chối")`, `GetByLabel("Lý do từ chối")` |
| Tìm / Lọc trạng thái | "Tìm theo mã, tên..."; Select Trạng thái | `GetByPlaceholder("Tìm theo mã, tên...")` |
| Bảng phiếu | Mã phiếu, Đơn hàng, Mã hàng, QA/QC giao, Số giao, Giờ giao, Trạng thái, Thao tác | `GetByRole(Row, new{Name=<mã phiếu>})` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách đợt QC | `GET /api/v1/{qa-qc\|manager}/qc-batches` | `QcBatchQuery` (ngày, …) | Bảng đợt kiểm |
| Hàng trên chuyền | `GET /api/v1/qa-qc/qc-batches/on-line` | `sewingTeamId, orderLineId` | Dòng "Hàng trên chuyền" |
| Thêm đợt kiểm | `POST /api/v1/qa-qc/qc-batches` | `{workDate, shiftCode, batchTime, sewingTeamId, orderId, orderLineId, quantity}` | Toast "Đã thêm đợt kiểm." |
| Sửa đợt kiểm | `PUT /api/v1/{qa-qc\|manager}/qc-batches/{id}` | `{quantity, reason?}` | Toast "Đã cập nhật đợt kiểm." |
| Xóa đợt kiểm | `POST /api/v1/{qa-qc\|manager}/qc-batches/{id}/delete` | `{reason}` | Đợt biến mất khỏi bảng |
| Danh sách phiếu FG | `GET /api/v1/{qa-qc\|warehouse-keeper}/fg-handovers` (+ `/{id}`) | `FgHandoverQuery` | Bảng + modal chi tiết |
| Dòng giao được | `GET /api/v1/qa-qc/fg-handovers/handoverable?orderId` | — | Bảng "Giao lần này" |
| Lập phiếu | `POST /api/v1/qa-qc/fg-handovers` | `{orderId, productId, lines[{orderLineId, qty}]}` | Phiếu "Chờ xác nhận" (PENDING) |
| Rút phiếu | `POST /api/v1/qa-qc/fg-handovers/{id}/cancel` | — | Trạng thái CANCELLED |
| Nhận / từ chối | `POST /api/v1/warehouse-keeper/fg-handovers/{id}/confirm` (lines số kho đếm + lý do lệch); `.../{id}/reject` `{reason}` | — | CONFIRMED / REJECTED |

## 4. Điểm nghi vấn lệch pha

- QA/QC không có ô nhập số lượng tối đa theo "Hàng trên chuyền": FE chỉ hiển thị thông tin, không chặn Số hàng đạt vượt số còn trên chuyền (kiểm ở BE).
- Lý do khi sửa/xóa đợt QC "bắt buộc khi sửa sau hết ca" nhưng form QC luôn hiển thị ô lý do cho Quản đốc; QA/QC sửa trong ca có bắt buộc hay không không thể suy ra từ FE.
- Ngày/giờ kiểm không chặn ngày tương lai.
- Phiếu bàn giao: `maxQtyRule(remainingQty)` dựa trên số tải trước; thay đổi đồng thời chỉ biết khi API lỗi. Dòng `remainingQty=0` bị disable nên không test được nhập 0 > 0.
- `Modal.confirm` khi lập phiếu gọi `form.submit()` trong `onOk`; lỗi form validate không đóng/hiện ở modal confirm (người dùng thấy lỗi ở form phía sau).
- Trạng thái dùng khóa PENDING/CONFIRMED/REJECTED/CANCELLED — nhãn tiếng Việt lấy từ `FG_STATUS_LABEL` (chưa xác minh chữ chính xác).
- `/warehouse/fg-handovers` đặt dưới `/warehouse` nhưng QA/QC cũng vào được → menu hiển thị theo role, URL thì chung.
- Nút icon (Sửa/Xóa đợt kiểm) có thể chỉ là IconAction không tên → khó dùng Semantic Locator.
