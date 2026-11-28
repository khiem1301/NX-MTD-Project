# ST_CONTRACT — Module 12: Chấm công, Suất ăn & Đơn từ

> Nguồn: `fe/src/features/{attendance,meal,requests}/*`, `constants/{apiEndpoints,requestTypes,messages}.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/attendance` | AttendancePage (dữ liệu từ máy vân tay Ronald Jack Pro): tab "Bảng công ngày", "Nhân viên", "Chấm công hộ", "Đồng bộ" (Admin: gộp lịch sử đồng bộ + Import Excel) | Admin, Manager, Accountant | Gán mã chấm công cho mã chưa khớp; chấm công hộ từ đơn xin chấm công bù đã duyệt |
| `/team/attendance` | TeamAttendancePage | TeamLeader, TeamDeputy | Xem bảng công của tổ |
| `/my/attendance` | MyAttendancePage | Mọi role trừ Admin | Xem bảng công của mình |
| `/team/meals` | TeamMealPage ("Suất ăn của tổ") | TeamLeader, TeamDeputy | Rà soát danh sách → Xác nhận đã rà soát |
| `/my/meals` | MyMealPage ("Suất ăn của tôi") | Mọi role (trừ vai trò bị loại trong `roles`) | Bỏ tick nếu hôm nay không ăn (chỉ hiện khi đã quẹt vân tay) |
| `/meals/summary` | MealSummaryPage ("Suất ăn hôm nay") | Accountant | Nhập suất khách → xem tổng hợp theo tổ → nhập đơn giá suất ăn |
| `/admin/meal-settings` | MealSettingsPage ("Giờ chốt suất ăn") | Admin | Giờ nhắc xác nhận, Giờ chốt suất ăn |
| `/requests` | MyRequestsPage ("Đơn từ của tôi") | Mọi role (`REQUEST_CREATOR_ROLES`) | Tạo đơn → xem chi tiết → Sửa đơn / Hủy đơn |
| `/requests/inbox` | RequestInboxPage ("Đơn cần xử lý") | Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, Electrician | Duyệt / Từ chối; Hoàn thành / Không hoàn thành; "Xử lý ngay" mở màn tương ứng với `?fromRequest=<id>&requestType=<loại>` |

- Role sai → redirect `/`. Tổ trưởng/phó bị lọc loại tổ.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tabs chấm công | Bảng công ngày / Nhân viên / Chấm công hộ / Đồng bộ | `GetByRole(Tab, "Bảng công ngày")` ... |
| Lọc bảng công | ô tìm theo mã/tên, nút Đặt lại (reload) | `GetByPlaceholder(ATTENDANCE_TEXT.filter.keyword)`, `GetByRole(Button, "Đặt lại")` |
| Gán mã chấm công | Modal "Gán mã chấm công": Nhân viên required ("Vui lòng chọn nhân viên.") | `GetByLabel("Nhân viên")` |
| Chấm công hộ (modal) | Nhân viên, Ngày, Loại điều chỉnh, Đơn xin chấm công bù (required "…requestRequired"), Giờ vào / Giờ ra (bắt buộc), Lý do (required+whitespace) | `GetByLabel("Nhân viên")`, `GetByLabel("Lý do")`, `GetByPlaceholder("Giờ vào")` |
| Nhập file Excel dự phòng | Upload `.xlsx,.xls`, Xem trước (Số dòng, Số lần quẹt), Xác nhận (Modal.confirm) | `Locator("input[type=file]")` |
| Suất ăn tổ: Xác nhận đã rà soát | Button; bảng thành viên với tick ăn/không ăn | `GetByRole(Button, "Xác nhận đã rà soát")` |
| Suất ăn của tôi | Checkbox ăn/không ăn | `GetByRole(Checkbox)` |
| Số suất khách | required ("Vui lòng nhập số suất khách."), ≥ 0; Ghi chú max 300; disable khi chốt | `GetByLabel("Số suất khách")`, `GetByRole(Button, "Lưu suất khách")` |
| Đơn giá một suất | required ("Vui lòng nhập đơn giá."), ≥ 0; chỉ nhập khi đã chốt | `GetByLabel("Đơn giá một suất")`, `GetByRole(Button, "Lưu đơn giá")` |
| Giờ nhắc xác nhận / Giờ chốt suất ăn | TimePicker required; Giờ chốt trong khoảng cho phép (mặc định 08:30) | `GetByLabel("Giờ nhắc xác nhận")`, `GetByLabel("Giờ chốt suất ăn")`, `GetByRole(Button, "Lưu")` |
| Tạo đơn | Button primary "Tạo đơn" | `GetByRole(Button, "Tạo đơn")` |
| Loại đơn | Select required, aria-label "Loại đơn", placeholder "Chọn loại đơn" | `GetByLabel("Loại đơn")` |
| Người xử lý | Select required (một số loại), aria-label "Người xử lý", placeholder "Chọn người xử lý đơn" | `GetByLabel("Người xử lý")` |
| Tiêu đề | max 200 | `GetByLabel("Tiêu đề")` |
| Ngày cần | DatePicker | `GetByLabel("Ngày cần")` |
| Nội dung | TextArea (đơn Khác / Sửa máy chỉ có nội dung) | `GetByLabel("Nội dung")` |
| Dòng chi tiết đơn xin NVL/BTP | Select hàng (required), Số lượng (required, aria-label "Số lượng"), nút xóa dòng aria-label "Xóa", tối đa `REQUEST_LINE_LIMITS.maxLines` | `GetByLabel("Số lượng")`, `GetByLabel("Xóa")` |
| Sửa đơn / Hủy đơn | Button; Hủy: "Lý do hủy (không bắt buộc)" | `GetByRole(Button, "Sửa đơn")`, `GetByRole(Button, "Hủy đơn")` |
| Duyệt | modal "Ghi chú duyệt (không bắt buộc)" | `GetByRole(Button, "Duyệt")`, `GetByLabel("Ghi chú duyệt (không bắt buộc)")` |
| Từ chối | "Lý do từ chối" required | `GetByRole(Button, "Từ chối")`, `GetByLabel("Lý do từ chối")` |
| Hoàn thành / Không hoàn thành | "Lý do không hoàn thành" required | `GetByRole(Button, "Hoàn thành")`, `GetByRole(Button, "Không hoàn thành")` |
| Bảng đơn | Loại đơn, Người tạo, Tổ, Ngày tạo, Ngày cần, Trạng thái (Chờ duyệt, Chờ xử lý, Đã duyệt, Bị từ chối, Hoàn thành, Đã hủy, Không hoàn thành) | `GetByRole(Row)` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Bảng công / tổng hợp | `GET /api/v1/accountant/attendance/timesheets` (+ `/summary`); `GET /api/v1/team-leader/attendance/timesheets` (+ `/summary`); `GET /api/v1/common/my-attendance/timesheets` (+ `/summary`) | filter ngày/tổ | Bảng + StatTiles |
| Đồng bộ | `GET .../attendance/sync-health`, `.../sync-sessions`, `.../unmapped-codes` | — | Banner / bảng đồng bộ |
| Gán mã | `POST .../attendance/map-code` | `{attendanceCode, employeeId}` | Mã chuyển khỏi danh sách chưa khớp |
| Nhân viên RJ | `GET .../attendance/rj-employees`; `POST .../rj-employees/sync` | `onlyUnmapped?` | Bảng NV RJ |
| Chấm công hộ | `GET .../attendance/adjustments`; `POST .../attendance/adjustments` | `{employeeId, workDate, adjustmentType, requestId?, checkIn, checkOut, reason}` | Bản ghi điều chỉnh mới |
| Import Excel | `POST .../attendance/import/preview`; `POST .../attendance/import` | multipart | Xem trước → kết quả nhập |
| Suất ăn tổ | `GET /api/v1/team-leader/meals/suggestion?date`, `.../meals/members?date`; `POST .../meals/confirmation`; (đổi suất từng người: `.../meals/members`) | `{date, …}` | "Xác nhận lúc …" |
| Suất ăn của tôi | `GET /api/v1/common/my-meals/today` | `date?` | Tick hiện tại |
| Tổng hợp / khách / giá | `GET /api/v1/accountant/meals/summary?date`; `POST .../meals/guest-count`; `POST/PUT .../meals/price` | `{date, guestCount, guestNote?}` / `{mealPrice}` | Bảng tổng hợp cập nhật |
| Cài đặt giờ chốt | `GET/PUT /api/v1/admin/meal-settings` | `MealSettings {reminderTime, cutoffTime}` | Toast lưu |
| Đơn từ | `GET/POST /api/v1/common/requests`; `GET .../requests/types`; `GET .../requests/{id}`; `POST .../requests/{id}/cancel` | `{requestType, handlerRole?, title?, targetDate?, content, lines[]}` | Toast "Đã gửi đơn, đang chờ xử lý." / "Đã hủy đơn." |
| Hộp thư xử lý | `GET /api/v1/common/request-inbox`; `POST .../request-inbox/{id}/approve` / `reject` / `complete` / `not-complete` | `{note}` / `{reason}` | Toast "Đã duyệt đơn." / "Đã từ chối đơn." / "Đã hoàn thành đơn." / "Đã ghi nhận đơn không hoàn thành." |

## 4. Điểm nghi vấn lệch pha

- `/my/attendance` và `/my/meals` bỏ Admin ở chấm công nhưng `roles` của suất ăn dùng `Object.values(ROLES).filter(...)` (một số role bị loại); cần đối chiếu danh sách role chính xác trong `meal/routes.tsx` khi viết ca phân quyền.
- Ba trang chấm công khác nhau (Admin/Manager/Accountant dùng chung `/attendance`) nhưng tab "Đồng bộ" chỉ Admin → kiểm ẩn/hiện tab theo role, không phải theo route.
- "Chấm công hộ" yêu cầu Giờ vào / Giờ ra "bắt buộc" qua nhãn nhưng rule nằm ở `Form.Item required` không có validator giờ ra > giờ vào ở FE.
- Import Excel dự phòng: `accept=".xlsx,.xls"` chỉ chặn phần mở rộng; không kiểm kích thước.
- Suất ăn khách/đơn giá bị `disable` theo trạng thái chốt (`locked`/`closed`); khi disable thì nút Lưu không thể bấm → test cần assert disabled thay vì thông báo lỗi.
- Giờ chốt suất ăn kiểm khoảng cho phép bằng `validator` FE và thông báo `cutoffRange` có placeholder `{from}`/`{to}`; chuỗi hardcode giờ mặc định 08:30 trong hint.
- Đơn từ: loại đơn có/không có bước duyệt phụ thuộc hằng số FE `APPROVAL_TYPES` ("khớp NeedsApproval ở BE") → có nguy cơ lệch với BE; loại Khác và Sửa máy không có dòng chi tiết (`NO_LINE_TYPES`).
- "Người xử lý" chỉ bắt buộc với một số loại đơn; FE không hiện rõ loại nào bắt buộc ngoài rule trong form.
- `Ngày cần` không chặn quá khứ.
- "Xử lý ngay" dùng query `fromRequest`/`requestType` để mở màn khác; FE không kiểm tra id hợp lệ.
- Ô "Số lượng", nút xóa dòng dùng `aria-label` ghép từ tên hàng → cần regex.
