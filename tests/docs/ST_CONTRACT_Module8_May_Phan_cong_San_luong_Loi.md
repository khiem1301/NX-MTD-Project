# ST_CONTRACT — Module 8: May — Phân công, Sản lượng & Lỗi

> Nguồn: `fe/src/features/production/{routes,pages,components,api,constants}` (assignment/output/defect), `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/operation-assignments` | OperationAssignmentsPage ("Phân công công đoạn") | TeamLeader, TeamDeputy (tổ may) | Chọn đơn hàng → mã hàng → giao công đoạn cho công nhân (hàng loạt / theo công nhân) / Chép từ đơn khác / Bỏ phân công |
| `/output-records` | OutputEntryPage ("Sản lượng cuối ca") | TeamLeader, TeamDeputy | Chọn ngày, ca, dòng đơn → nhập sản lượng theo công nhân → Lưu sản lượng |
| `/defect-records` | DefectRecordsPage ("Phiếu lỗi / điều chỉnh") | TeamLeader, TeamDeputy | Ghi phiếu → Sửa / Hủy phiếu; từ phiếu hàng hủy → Đơn cắt bù |
| `/my-defects` | MyDefectsPage ("Phiếu lỗi / điều chỉnh của tôi") | Worker | Xem phiếu của mình ("Bạn chưa có phiếu nào." khi trống) |
| `/manager/output-records` | ManagerOutputPage ("Sửa sản lượng sau hạn") | Admin, Manager | Sửa sản lượng sau hạn, bắt buộc lý do |
| `/manager/defect-records` | ManagerDefectsPage ("Phiếu lỗi / điều chỉnh của các tổ") | Admin, Manager | Sửa (bắt buộc lý do) / Hủy phiếu của mọi tổ |

- Role sai hoặc tổ không phải tổ may (TL/TD) → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Chọn đơn hàng (phân công) | Select "Chọn đơn hàng" | `GetByRole(Combobox)` theo placeholder / `GetByText("Chọn đơn hàng")` |
| Mã hàng | Select aria-label "Mã hàng" | `GetByLabel("Mã hàng")` |
| Tìm công đoạn / lọc nhóm / lọc bậc | search max `ASSIGNMENT_SEARCH_MAX_LENGTH` (100) | `GetByPlaceholder("Tìm mã, tên")` |
| Chọn công nhân (hàng loạt) | Select aria-label "Chọn công nhân" | `GetByLabel("Chọn công nhân")` |
| Giao / Xong (bulk bar) | Button; toast "Đã giao {n} công đoạn." / "…bỏ qua {m} công đoạn công nhân đang làm." / "Công nhân đã làm tất cả công đoạn đã chọn, không có gì thay đổi." | `GetByRole(Button, "Giao")` (xem `AssignmentBulkBar`) |
| Chép từ đơn khác | Modal: "Đơn nguồn" required, phải khác đơn đang chọn ("Đơn nguồn phải khác đơn đang chọn."); toast "Đã chép {n} phân công." / "Không có phân công nào để chép từ đơn này." | `GetByRole(Button, "Chép từ đơn khác")`, `GetByLabel("Đơn nguồn")` |
| Bỏ phân công | IconAction; toast "Đã bỏ phân công." | `row.GetByRole(Button, "Bỏ phân công")` (nếu có tên) |
| Sản lượng: Ngày làm việc / Ca / Tổ may | DatePicker, Select (có "Cả ngày", "Chọn ca để nhập sản lượng") | `GetByLabel("Ngày làm việc")`, `GetByLabel("Ca")` |
| Ô sản lượng | số theo công nhân/công đoạn; không có phân công → "Chưa có phân công nào cho dòng đơn này…" | `GetByRole(Spinbutton)` theo dòng |
| Lý do sửa (bắt buộc) | ô lý do aria-label "Lý do sửa (bắt buộc)", max `REASON_MAX_LENGTH`; Quản đốc: "Quản đốc sửa sản lượng phải ghi lý do." | `GetByLabel("Lý do sửa (bắt buộc)")` |
| Lưu sản lượng | Button "Lưu sản lượng (n)"; không đổi gì → "Chưa có thay đổi nào để lưu."; kết quả "Đã lưu: thêm {a}, sửa {b}, xóa {c}." | `GetByRole(Button, new{Name=new Regex("Lưu sản lượng")})` |
| Ghi phiếu | Button "Ghi phiếu" → modal "Ghi phiếu lỗi / điều chỉnh" | `GetByRole(Button, "Ghi phiếu")` |
| Phiếu: Ngày | DatePicker required | `GetByLabel("Ngày")` |
| Phiếu: Ca | Select (không bắt buộc) | `GetByLabel("Ca")` |
| Phiếu: Chọn đơn hàng / Dòng đơn / Công đoạn / Công nhân | Select required | `GetByLabel("Dòng đơn")`, `GetByLabel("Công đoạn")`, `GetByLabel("Công nhân")` |
| Phiếu: Loại phiếu | Select required: Sửa lại / Hàng hủy / Nhập sai sản lượng | `GetByLabel("Loại phiếu")` |
| Phiếu: Số lượng | required, ≥ 1 ("…phải lớn hơn 0") và ≤ `DEFECT_MAX_QTY` (1.000.000) | `GetByLabel("Số lượng")` |
| Phiếu: Lý do | Select required: Bỏ mũi, Đường may lệch, Sai size, Rách, Dơ bẩn, Khác | `GetByLabel("Lý do")` |
| Phiếu: Ghi chú lý do | bắt buộc khi loại "Nhập sai sản lượng" hoặc lý do "Khác" | `GetByLabel("Ghi chú lý do")` |
| Sửa / Hủy phiếu | Quản đốc: "Lý do sửa" required ("Quản đốc sửa phiếu phải nhập lý do."); không đổi được ngày và dòng đơn | `GetByRole(Button, "Sửa phiếu")`, `GetByRole(Button, "Hủy phiếu")` |
| Lọc phiếu | "Từ ngày"/"Đến ngày" (tối đa 366 ngày: "Khoảng ngày lọc tối đa 366 ngày."), Tổ, Loại phiếu, Phiếu; tìm "Tìm theo công nhân, mã hàng, tổ, mã phiếu" | `GetByPlaceholder("Tìm theo công nhân, mã hàng, tổ, mã phiếu")` |
| Đơn cắt bù | Button "Đơn cắt bù" → modal: Chi tiết cần cắt bù required ("Chọn chi tiết cần cắt bù."), Số lượng 1..số hàng hủy, Ngày cần, Ghi chú max 500; nút "Gửi đơn" | `GetByRole(Button, "Đơn cắt bù")`, `GetByRole(Button, "Thêm chi tiết")`, `GetByRole(Button, "Gửi đơn")` |
| Trạng thái phiếu | Còn hiệu lực / Đã hủy; Đã ghi / Đã chốt (phiếu cũ) / Phiếu đã hủy | `GetByText("Còn hiệu lực")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách phân công / công đoạn giao được | `GET /api/v1/team-leader/operation-assignments?orderId`; `GET .../operation-assignments/operations` | `orderId` | Bảng phân công |
| Giao công đoạn | `POST .../operation-assignments`; `POST .../operation-assignments/bulk` | `AssignPayload` / `AssignManyPayload` | Toast "Đã phân công." / "Đã giao {n} công đoạn." |
| Thay / bỏ phân công | `POST .../operation-assignments/{id}/replace`; `.../{id}/close` | — | Toast "Đã bỏ phân công." |
| Chép phân công | `POST .../operation-assignments/copy` | `{fromOrderId, toOrderId}` | Toast "Đã chép {n} phân công." |
| Lưới sản lượng | `GET /api/v1/{team-leader\|manager}/output-records/grid` | `OutputGridQuery` | Bảng nhập |
| Lưu sản lượng | `PUT/POST /api/v1/{team-leader\|manager}/output-records` | `SaveOutputPayload` (+ lý do khi Manager) | Toast "Đã lưu: thêm…, sửa…, xóa…." |
| Danh sách phiếu lỗi | `GET /api/v1/{team-leader\|manager}/defect-records` | `DefectQuery` | Bảng phiếu |
| Ghi / sửa / hủy phiếu | `POST /api/v1/team-leader/defect-records`; `PUT .../defect-records/{id}`; `POST .../{id}/cancel` | `CreateDefectPayload` / `UpdateDefectPayload` / `CancelDefectPayload` | Toast lưu; phiếu "Phiếu đã hủy" |
| Đơn cắt bù từ phiếu | (qua `ReplacementCuttingModal`) | `{defectId, panels[{name, qty}], targetDate?, content?}` | Toast "Đã gửi đơn cắt bù cho Quản đốc duyệt." |
| Phiếu của tôi | `GET /api/v1/common/my-defects` | `DefectQuery` | Bảng phiếu cá nhân |

## 4. Điểm nghi vấn lệch pha

- TL/TD thao tác trên 3 màn khác nhau (phân công / sản lượng / phiếu lỗi), nhưng nhiều ô chỉ có `aria-label` (Mã hàng, Chọn công nhân, Lý do sửa) → bắt buộc dùng `GetByLabel`, không có TestId.
- Toast bulk có 3 nhánh (`noneAdded` dùng `message.info`, hai nhánh còn lại `message.success`) → `AssertToastAsync` chỉ cần khớp text nhưng loại toast khác nhau.
- Sản lượng: FE chỉ báo "Chưa có thay đổi nào để lưu." và kết quả tổng; không có validate tổng sản lượng > số được giao (kiểm ở BE).
- Phiếu lỗi: "Loại Nhập sai sản lượng" hoặc lý do "Khác" bắt buộc ghi chú chỉ ở FE form sửa (`DefectEditModal`); cần kiểm form ghi mới có cùng rule.
- Số lượng hàng hỏng chỉ chặn ≤ 1.000.000 ở FE, không chặn vượt sản lượng thực tế.
- Khoảng ngày lọc tối đa 366 ngày là thông báo FE (hardcode) — lọc quá 366 ngày không gọi API.
- Quản đốc sửa sản lượng/phiếu "sau hạn" nhưng FE không hiển thị hạn sửa trên form; chỉ có cột "Hạn sửa".
- `OutputEntryPage`/`DefectRecordsPage` không import `Can`; chặn truy cập hoàn toàn dựa vào `ProtectedRoute` + loại tổ.
- Phiếu "Đã chốt (phiếu cũ)" là trạng thái legacy, FE vẫn hiển thị.
- Đơn cắt bù chỉ cho mỗi phiếu một lần (hint) nhưng FE không chặn gửi lại ngoài cột "Đơn cắt bù".
