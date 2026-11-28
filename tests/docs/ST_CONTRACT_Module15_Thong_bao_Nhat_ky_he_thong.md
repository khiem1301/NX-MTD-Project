# ST_CONTRACT — Module 15: Thông báo & Nhật ký hệ thống

> Nguồn: `fe/src/features/{notification,audit-log}/*`, `components/layout/*` (chuông thông báo), `constants/routes.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/notifications` | NotificationsPage ("Thông báo": tab "Hộp thư", tab "Đã gửi") | Mọi role đã đăng nhập (`Object.values(ROLES)`) | Chuông thông báo trên header → trang này; Admin/Manager thấy thêm nút "Gửi thông báo nhanh" và tab "Đã gửi" |
| `/admin/audit-logs` | AuditLogPage ("Nhật ký thay đổi") + AuditLogDetailModal | Admin | Lọc → Xem chi tiết thay đổi |

- Role sai (audit-logs) → redirect `/`. Chỉ Admin/Manager gửi thông báo (`isSender`); các role khác chỉ có tab Hộp thư.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Chuông thông báo | Button `aria-label="Thông báo"` (huy hiệu số chưa đọc, danh sách mới nhất) | `GetByLabel("Thông báo")` |
| Tab Hộp thư / Đã gửi | Tabs ("Đã gửi" chỉ Admin/Manager) | `GetByRole(Tab, "Hộp thư")`, `GetByRole(Tab, "Đã gửi")` |
| Bảng Hộp thư | Cột Thông báo (đậm nếu chưa đọc), Lúc, Thao tác | `GetByRole(Row)` |
| Xem chi tiết | IconAction title "Xem chi tiết" | `row.GetByRole(Button, "Xem chi tiết")` |
| Đánh dấu đã đọc | IconAction title "Đánh dấu đã đọc" | `row.GetByRole(Button, "Đánh dấu đã đọc")` |
| Đánh dấu tất cả đã đọc | Button | `GetByRole(Button, "Đánh dấu tất cả đã đọc")` |
| Gửi thông báo nhanh | Button primary (Admin/Manager) → modal | `GetByRole(Button, "Gửi thông báo nhanh")` |
| Nội dung | required+whitespace, max 1000 | `GetByLabel("Nội dung")` |
| Mức ưu tiên | Select | `GetByLabel("Mức ưu tiên")` |
| Gửi tới | Select: Toàn xưởng / Theo tổ / Theo vai trò | `GetByLabel("Gửi tới")` |
| Tổ (khi Theo tổ) | Select nhiều, required | `GetByLabel("Tổ")` |
| Vai trò (khi Theo vai trò) | Select nhiều, required | `GetByLabel("Vai trò")` |
| Gửi | Button OK modal | `GetByRole(Button, "Gửi")` |
| Nhật ký: tìm | placeholder "Tìm theo lý do / người thực hiện" | `GetByPlaceholder("Tìm theo lý do / người thực hiện")` |
| Nhật ký: lọc Đối tượng / Hành động / khoảng ngày | Select "Đối tượng", "Hành động"; RangePicker | `GetByPlaceholder("Đối tượng")`, `GetByPlaceholder("Hành động")` |
| Bảng nhật ký | Thời gian, Người thực hiện, Đối tượng, Hành động, Nội dung, Lý do | `GetByRole(Row)` |
| Chi tiết thay đổi | Modal "Chi tiết thay đổi": Thời gian, Người thực hiện, Nội dung, Đối tượng, Hành động, Lý do; bảng Trường / Trước / Sau (hoặc Trường / Giá trị) | `GetByRole(Dialog)`, `GetByText("Chi tiết thay đổi")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách thông báo | `GET /api/v1/common/notifications` | `NotificationQuery` (trang, lọc đã đọc) | Bảng Hộp thư |
| Số chưa đọc (chuông) | `GET /api/v1/common/notifications/unread-count` | — | Huy hiệu số |
| Đánh dấu đã đọc | `POST /api/v1/common/notifications/{id}/read` | — | Dòng hết đậm; số chưa đọc giảm |
| Đánh dấu tất cả đã đọc | `POST /api/v1/common/notifications/read-all` | — | Mọi dòng hết đậm |
| Tab Đã gửi | `GET /api/v1/{admin\|manager}/announcements` | `q` | Bảng thông báo đã gửi |
| Đích gửi | `GET /api/v1/{role}/announcements/targets` | — | Select Tổ / Vai trò |
| Gửi thông báo | `POST /api/v1/{role}/announcements` | `{content, priority, targetScope (ALL\|TEAM\|ROLE), teamIds?, roleIds?}` | Đóng modal; thông báo xuất hiện ở tab Đã gửi |
| Toast thông báo mới | (`useNewNotificationToast`, poll `unread-count`/list) | — | Toast khi có thông báo mới |
| Nhật ký thay đổi | `GET /api/v1/admin/audit-logs` (+ danh sách bảng ở bộ lọc Đối tượng) | `tableName, action, keyword, from, to, page` | Bảng nhật ký + modal chi tiết |

## 4. Điểm nghi vấn lệch pha

- `isSender` chỉ ẩn nút/tab trên FE (Admin/Manager); route `/notifications` vẫn mở cho mọi role → test phân quyền phải dựa trên nút/tab, không phải redirect.
- Tiêu đề thông báo dùng `n.title`, còn form gửi chỉ có "Nội dung": tiêu đề do BE sinh; FE không hiển thị trước.
- "Theo tổ"/"Theo vai trò" bắt buộc chọn ít nhất một giá trị nhưng FE không chặn gửi thông báo cho tổ đã ngừng.
- Toast thông báo mới (`useNewNotificationToast`) phụ thuộc polling: thời điểm hiện toast không xác định → dễ test không ổn định nếu dùng `Task.Delay` (cấm) — cần dùng `Expect` có timeout.
- Nhật ký thay đổi chỉ đọc, không có thao tác ghi; nhãn Đối tượng lấy từ `auditTableLabel(t)` (map FE) → bảng mới ở BE chưa có nhãn FE sẽ hiện tên kỹ thuật.
- Chuông và trang dùng cùng API nhưng hai cache khác nhau; đánh dấu đã đọc ở trang có thể chưa cập nhật số ở chuông ngay.
- Bộ lọc nhật ký không có giới hạn khoảng ngày như màn phiếu lỗi (366 ngày); chưa rõ phía BE.
- Các nút trong bảng thông báo là IconAction chỉ có `title` (Tooltip) → `GetByRole(Button,{Name})` có thể không khớp tên accessible.
