# ST_CONTRACT — Module 1: Đăng nhập, Trang chủ & Hồ sơ cá nhân

> Nguồn: `fe/src/features/{auth,dashboard,profile}`, `components/{guards,layout}`, `constants/{routes,roles,apiEndpoints}.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/login` | LoginPage (public) | Chưa đăng nhập. Đã đăng nhập → `<Navigate>` `/` | Sau: quay lại `location.state.from.pathname`, mặc định `/` |
| `/` | HomePage (một Home riêng theo role: Admin, Manager, Accountant, WarehouseKeeper, TeamLeader/TeamDeputy dùng chung, QaQc, Worker, Electrician) | Mọi role đã đăng nhập | Trước: login. Chưa đăng nhập → `/login` (kèm `state.from`) |
| `/profile` | ProfilePage + ProfileEditModal | Mọi role đã đăng nhập | Vào từ UserMenu "Hồ sơ của tôi" |
| (mọi route khi `isTemporaryPassword=true`) | ForceChangePasswordPage thay thế nội dung | Tài khoản dùng mật khẩu tạm | Đổi xong token mới → ProtectedRoute cho vào; nút "Đăng xuất" → `/login` |
| `*` | Redirect `/` | — | Route sai/role sai → `/` |

- Sai role hoặc sai loại tổ (cắt/may, `matchesTeamType`) → redirect `/`.
- Thoát: UserMenu → "Đăng xuất" → xóa token + cache → `/login`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tên đăng nhập | Input; trim, required (MSG01 "Vui lòng nhập tên đăng nhập.") | `GetByLabel("Tên đăng nhập")` |
| Mật khẩu | Input.Password; required (MSG01 "Vui lòng nhập mật khẩu.") | `GetByLabel("Mật khẩu")` |
| Nút Đăng nhập | submit, loading khi gọi API | `GetByRole(Button, "Đăng nhập")` |
| Alert lỗi đăng nhập | hiện khi API lỗi không kèm `errors` theo trường | `GetByRole(Alert)` / `.ant-alert-error` |
| Avatar/tên người dùng (header) | button `aria-label=fullName` mở dropdown | `GetByRole(Button, new{Name=<fullName>})` |
| "Hồ sơ của tôi" / "Đăng xuất" | menu item trong dropdown | `GetByRole(Menuitem, "Hồ sơ của tôi")`, `GetByRole(Menuitem, "Đăng xuất")` |
| Ô tìm chức năng | AutoComplete, Ctrl+K, không phân biệt dấu | `GetByLabel("Tìm chức năng")` / `GetByPlaceholder("Tìm chức năng…")` |
| Nút thu gọn menu | button | `GetByLabel("Mở hoặc thu gọn menu")` |
| Thẻ hồ sơ (Mã NV, Tên đăng nhập, Họ tên, Vai trò, Tổ, Ngày vào, Ngày sinh, SĐT, Bậc) | Descriptions (chỉ đọc). Bậc chỉ hiện khi có `currentGradeCode` | `GetByText(<label>)` trong `.ant-descriptions` |
| Nút Sửa (hồ sơ) | mở modal "Cập nhật hồ sơ" | `GetByRole(Button, "Sửa")` |
| Ngày sinh | DatePicker; không chọn ngày tương lai | `GetByLabel("Ngày sinh")` |
| Số điện thoại | Input maxLength 12; chỉ nhận số và `+`; pattern `0\d{9}` hoặc `+84\d{9}`; không bắt buộc | `GetByLabel("Số điện thoại")` |
| Mật khẩu hiện tại | Password; bắt buộc nếu nhập bất kỳ ô mật khẩu nào (modal) / luôn bắt buộc (ChangePasswordForm); bỏ ô này khi mật khẩu tạm | `GetByLabel("Mật khẩu hiện tại")` |
| Mật khẩu mới | Password; required (có điều kiện), max 50 ("Mật khẩu tối đa 50 ký tự.") | `GetByLabel("Mật khẩu mới")` |
| Nhập lại mật khẩu | Password; phải khớp mật khẩu mới (MSG05 "Mật khẩu nhập lại không khớp.") | `GetByLabel("Nhập lại mật khẩu mới")` (xác nhận text trong `MESSAGES.hr.confirmPassword`) |
| Nút Lưu / Hủy (modal) | Lưu = `form.submit()`, loading | `GetByRole(Button, "Lưu")`, `GetByRole(Button, "Hủy")` |
| Nút Đổi mật khẩu (màn bắt buộc) | submit | `GetByRole(Button, "Đổi mật khẩu")` |
| Thẻ/tile Home theo role | StatTile, danh sách, biểu đồ tròn tiến độ đơn | `GetByText(<tiêu đề tile>)` (xem `features/dashboard/constants.ts`) |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Đăng nhập | `POST /api/v1/common/auth/login` | `{username (trim), password}` | OK: lưu token, redirect `from` hoặc `/`. Lỗi: Alert, hoặc lỗi dưới từng ô nếu có `errors` |
| Khởi động / F5 | `GET /api/v1/common/auth/me` (+ `POST .../auth/refresh` khi 401) | — | Còn phiên: giữ trang. Refresh lỗi: xóa phiên → `/login` |
| Đăng xuất | `POST /api/v1/common/auth/logout` | — | Luôn xóa phiên FE (bỏ qua lỗi BE) → `/login` |
| Mở Home | `GET /api/v1/{admin\|manager\|accountant\|warehouse-keeper\|team-leader\|qa-qc\|worker\|electrician}/home` | — | Hiện tile/danh sách. Lỗi: `QueryError` có nút thử lại. Có trang tiến độ đơn: `orderProgressApi` |
| Mở Hồ sơ | `GET /api/v1/common/profile` | — | Spin → thẻ thông tin. Lỗi: `QueryError` |
| Lưu ngày sinh/SĐT | `PUT /api/v1/common/profile` | `{dateOfBirth: yyyy-MM-dd\|null, phone: string\|null}` | Toast `MESSAGES.common.saved`, đóng modal, reload hồ sơ. Lỗi theo trường hiện dưới ô. Không đổi gì thì chỉ đóng modal, không gọi API |
| Đổi mật khẩu (modal / màn bắt buộc) | `POST /api/v1/common/profile/change-password` | `{currentPassword?, newPassword, confirmPassword}` | Toast MSG08 "Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất."; lưu token mới. 409 sai mật khẩu hiện tại: lỗi dưới ô "Mật khẩu hiện tại không đúng." (MSG07). Lỗi khác: `message.error` |

## 4. Điểm nghi vấn lệch pha

- Login chỉ kiểm tra rỗng; không có min/max ở FE (mật khẩu 51+ ký tự chỉ bị chặn ở màn đổi mật khẩu, không bị chặn ở login).
- `ChangePasswordForm` (màn mật khẩu tạm) không có tính năng nào sau `resetFields`: sau khi đổi thành công không có redirect rõ ràng, chỉ dựa vào ProtectedRoute render lại.
- Trong modal sửa hồ sơ, đổi mật khẩu thành công rồi cập nhật hồ sơ lỗi: toast MSG08 đã hiện nhưng modal vẫn mở và các thiết bị khác đã bị đăng xuất (hai API chạy tuần tự, không có rollback).
- Rule mật khẩu mới ở FE chỉ có `required` và `max 50`; không có min/độ mạnh. Nếu BE yêu cầu, FE chưa phản ánh và test phải chờ lỗi từ API.
- Ngày sinh chỉ chặn ngày tương lai; không chặn tuổi quá nhỏ/lớn. `phoneRule` bỏ qua ô trống nên xóa SĐT = gửi `null`.
- Nhãn hiển thị "Nhập lại mật khẩu mới", "Mật khẩu hiện tại" lấy từ `MESSAGES.hr.*` (chưa xác minh chuỗi chính xác) nên Locator cần đối chiếu với `messages.ts` khi viết test.
- `UserMenu` hiện `Mã NV: {user.username}`: nhãn là mã nhân viên nhưng giá trị là username (có thể lệch nếu hai giá trị khác nhau).
- `HomePage` map cứng role → Home; role lạ → `HOME_BY_ROLE[role]` là `undefined` và gọi sẽ lỗi (không có fallback). TeamLeader/TeamDeputy dùng chung một Home dù có Home Cutting/Sewing riêng trong thư mục.
- Mỗi Home gọi API riêng, chưa có kiểm tra quyền FE ngoài `HOME_BY_ROLE`; route `/` không giới hạn role nên mọi test Home phụ thuộc đúng role đăng nhập.
- `GlobalSearch` chỉ lọc theo menu role hiện tại (`leaves`), không thấy route ngoài menu (vd. `/admin/orders/:id`).
- Login không có "Quên mật khẩu"/khóa tài khoản ở FE: kịch bản nhập sai nhiều lần chỉ dựa vào phản hồi BE (hiện thông báo của BE qua Alert, FE không tự chuẩn hóa).
