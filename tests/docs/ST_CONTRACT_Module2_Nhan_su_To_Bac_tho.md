# ST_CONTRACT — Module 2: Nhân sự, Tổ & Phân hạng

> Nguồn: `fe/src/features/{users,teams,grade}`, `constants/{apiEndpoints,messages}.ts`. Không đọc Backend. `{role}` = `admin` hoặc `manager` (RolePath).

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/admin/employees` | EmployeesPage (Admin) | Admin | Menu → danh sách; nút Xem → `?id=<employeeId>` mở EmployeeDetailModal |
| `/admin/employees/:id` | EmployeeDetailRedirect | Admin | Chuyển về `/admin/employees?id=:id` |
| `/manager/employees` | EmployeesPage (Manager) | Manager | Như trên; bộ lọc Vai trò ẩn các role trong `MANAGER_HIDDEN_ROLES` |
| `/manager/employees/:id` | EmployeeDetailRedirect | Manager | Chuyển về `/manager/employees?id=:id` |
| `/teams` | TeamsPage + TeamDetailModal | Admin, Manager | Danh sách tổ → Xem → modal chi tiết (thành viên, chuyển tổ, bổ nhiệm) |
| `/hr/grades` | GradePage (tab "Đề xuất bậc", "Bậc & mức tiền") | TeamLeader, TeamDeputy (tổ may), Admin, Manager | Tab Đề xuất bậc: TL đề xuất, Admin/Manager áp dụng/đặt trực tiếp; tab Bậc & mức tiền: danh mục bậc |

- Role sai → redirect `/`. Nút "Thêm nhân viên văn phòng", "Nhập từ Excel", cột Tài khoản, Cho nghỉ việc/Khóa/Mở khóa/Đặt lại MK chỉ hiện với Admin/Manager (`canManage`); "Cho nghỉ việc" ẩn với chính mình và với NV đã nghỉ.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Ô tìm kiếm NV | DebouncedSearch, placeholder "Tìm theo mã, tên..." | `GetByPlaceholder("Tìm theo mã, tên...")` |
| Lọc Tổ / Vai trò / Trạng thái | Select (Tổ có showSearch) | `GetByRole(Combobox)` theo thứ tự trong toolbar (nên bổ sung TestId) |
| Bảng NV | Cột Mã nhân viên, Họ tên, Vai trò, Tổ, Kiểu lương, Bậc thợ, Mã chấm công, Ngày vào làm, Trạng thái, Tài khoản, Thao tác | `GetByRole(Row, new{Name=<mã NV>})` |
| Xem (mắt) | IconAction title "Xem" | `row.GetByRole(Button, new{Name="Xem"})` |
| Thêm nhân viên văn phòng | Button primary | `GetByRole(Button, new{Name="Thêm nhân viên văn phòng"})` |
| Mã nhân viên | required+whitespace, max 20, pattern chữ/số/`. _ -` ("Mã chỉ gồm chữ, số và . _ -"), khóa khi sửa | `GetByLabel("Mã nhân viên")` |
| Tên đăng nhập | disabled, tự sinh từ họ tên | `GetByLabel("Tên đăng nhập")` |
| Họ tên | required+whitespace, max 100, chỉ chữ + khoảng trắng | `GetByLabel("Họ tên")` |
| Giới tính / Ngày sinh | Select Nam/Nữ; DatePicker không chọn ngày tương lai | `GetByLabel("Giới tính")`, `GetByLabel("Ngày sinh")` |
| Số điện thoại | maxLength 12; `0\d{9}` hoặc `+84\d{9}` | `GetByLabel("Số điện thoại")` |
| Vai trò / Tổ / Kiểu lương | Select; Vai trò và Kiểu lương required; đổi Vai trò/Tổ gợi ý Kiểu lương | `GetByLabel("Vai trò")`, `GetByLabel("Tổ")`, `GetByLabel("Kiểu lương")` |
| Mã chấm công | số 1–99999999 | `GetByLabel("Mã chấm công")` |
| Chọn từ RJ… | Button (chỉ khi tạo mới), điền Họ tên + Mã chấm công | `GetByRole(Button, new{Name="Chọn từ RJ…"})` |
| Ngày vào làm | DatePicker required | `GetByLabel("Ngày vào làm")` |
| Lưu / Hủy | Button | `GetByRole(Button, new{Name="Lưu"})`, `"Hủy"` |
| Cho nghỉ việc | Ngày nghỉ (required, mặc định hôm nay); Lý do nghỉ việc (required+whitespace, max `REASON_MAX_LENGTH`); nút Xác nhận (danger) | `GetByRole(Button, "Cho nghỉ việc")`, `GetByLabel("Ngày nghỉ")`, `GetByLabel("Lý do nghỉ việc")`, `GetByRole(Button, "Xác nhận")` |
| Khóa tài khoản / Mở khóa | Lý do khóa required | `GetByRole(Button, "Khóa tài khoản")`, `GetByLabel("Lý do khóa")`, `GetByRole(Button, "Mở khóa")` |
| Đặt lại mật khẩu | Popconfirm "Đặt lại mật khẩu tạm cho nhân viên này? Họ sẽ bị đăng xuất." → TempPasswordModal "Mật khẩu tạm" (hiện một lần, nút Sao chép) | `GetByRole(Button, "Đặt lại mật khẩu")` rồi nút OK của Popconfirm |
| Nhập từ Excel | Upload.Dragger `.xlsx` (1 file); xem trước (Alert tóm tắt + bảng); nút "Nhập N dòng hợp lệ" (disable khi `validCount=0`) → Modal.confirm; kết quả có "Sao chép tất cả" | `GetByRole(Button, "Nhập từ Excel")`, `Locator("input[type=file]")`, `GetByRole(Button, "Sao chép tất cả")` |
| Tab Lịch sử | Tabs trong chi tiết NV | `GetByRole(Tab, "Lịch sử")` |
| Bảng tổ | Cột Mã tổ, Loại tổ, Tổ trưởng, Tổ phó, Số người, Trạng thái, Thao tác | `GetByRole(Row, new{Name=<mã tổ>})` |
| Thêm tổ | Mã tổ required, max 20, pattern mã; Loại tổ required | `GetByRole(Button, "Thêm tổ")`, `GetByLabel("Mã tổ")`, `GetByLabel("Loại tổ")` |
| Ngừng / Dùng lại tổ | IconAction (Ngừng có Popconfirm) | `row.GetByRole(Button, "Ngừng")`, `"Dùng lại"` |
| Thêm nhân viên vào tổ | Select nhiều "Nhân viên chưa thuộc tổ nào" required; Lý do max | `GetByRole(Button, "Thêm nhân viên")`, `GetByLabel("Nhân viên chưa thuộc tổ nào")` |
| Chuyển sang tổ khác | Tổ đích required; Ngày hiệu lực required (không tương lai); Lý do max | `GetByRole(Button, "Chuyển sang tổ khác")`, `GetByLabel("Tổ đích")`, `GetByLabel("Ngày hiệu lực")` |
| Loại khỏi tổ / Bổ nhiệm / Thôi giữ chức | Popconfirm (người đang giữ chức chuyển về Công nhân) | `GetByRole(Button, "Loại khỏi tổ")`, `"Bổ nhiệm tổ trưởng"`, `"Bổ nhiệm tổ phó"`, `"Thôi giữ chức"` |
| Thêm bậc | Mã bậc pattern `^[A-Za-z0-9]{1,5}$` (maxLength 5), Mức tiền required, Hiệu lực từ required | `GetByRole(Button, "Thêm bậc")`, `GetByLabel("Mã bậc (chữ hoa / số, tối đa 5 ký tự)")` |
| Đổi tiền / Sửa mức tiền | Mức mới required; Hiệu lực từ (sửa: phải sau hôm nay; đổi: không trước `earliest`) | `GetByRole(Button, "Đổi tiền")`, `GetByLabel("Hiệu lực từ")` |
| Ngừng dùng / Dùng lại / Xóa bậc | Popconfirm "Xóa bậc này?" / "Xóa mức tiền bậc chưa hiệu lực này?" | `GetByRole(Button, "Ngừng dùng")`, `"Dùng lại"` |
| Đề xuất bậc | Công nhân required (disable người đã có đề xuất chờ); Bậc đề xuất required; Lý do đề xuất required+whitespace, max `REASON_MAX_LENGTH` | `GetByRole(Button, "Đề xuất bậc")`, `GetByLabel("Công nhân")`, `GetByLabel("Bậc đề xuất")`, `GetByLabel("Lý do đề xuất")` |
| Đặt bậc trực tiếp | Tổ → Công nhân (disable đến khi chọn tổ) → Bậc mới → Hiệu lực từ → Lý do (đều required) | `GetByRole(Button, "Đặt bậc trực tiếp")`, `GetByLabel("Bậc mới")` |
| Áp dụng / Không áp dụng | Hiệu lực từ required; Ghi chú quyết định max 500 | `row.GetByRole(Button, "Áp dụng")`, `"Không áp dụng"`, `GetByLabel("Ghi chú quyết định")` |
| Gợi ý bậc bằng AI | Modal: số ngày lookback, Tạo lại (confirm), Xác nhận (confirm; disable khi `sent=0`) | Nút mở chỉ có icon Robot → nên thêm `data-testid`; `GetByRole(Button, "Xác nhận")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách NV | `GET /api/v1/{role}/employees` | `page,pageSize,keyword,teamId,role,status(All\|Active\|Resigned)` | Bảng + phân trang; lỗi: QueryError + Thử lại |
| Chi tiết NV | `GET /api/v1/{role}/employees/{id}` | — | Modal chi tiết + tab Lịch sử |
| Thêm NV | `POST /api/v1/{role}/employees` | `employeeCode, fullName, gender?, dateOfBirth?, phone?, role, teamId?, payType, startDate, attendanceCode?` | Toast "Đã thêm nhân viên và tạo tài khoản."; TempPasswordModal; reload |
| Sửa NV | `PUT /api/v1/admin/employees/{id}` hoặc `PUT /api/v1/manager/employees/{id}` | như trên (không có `employeeCode`) + `rowVer` | Toast "Đã lưu."; reload |
| Cho nghỉ việc | `POST /api/v1/{role}/employees/{id}/resign` | `{resignDate, reason, rowVer}` | Đóng modal; trạng thái "Đã nghỉ" |
| Khóa / Mở khóa | `POST .../employees/{id}/account/lock` `{reason}`; `.../account/unlock` | — | Trạng thái tài khoản đổi |
| Đặt lại mật khẩu | `POST .../employees/{id}/account/reset-password` | — | TempPasswordModal hiện mật khẩu |
| Import NV | `POST /api/v1/{role}/employee-import` (xem trước + nhập, qua `useEmployeeImport`) | multipart `.xlsx` | Alert tóm tắt + bảng; nhập xong hiện mật khẩu tạm + dòng lỗi |
| Danh sách / chi tiết tổ | `GET /api/v1/{role}/teams` (`includeDiscontinued`), `GET .../teams/{id}` | — | Bảng / modal |
| Thêm / Sửa tổ | `POST /api/v1/{role}/teams`; `PUT .../teams/{id}` | `{teamCode, teamType}` | Toast "Đã lưu." |
| Ngừng / Dùng lại | `POST .../teams/{id}/discontinue`; `.../reactivate` | — | Toast "Đã lưu." |
| Chuyển tổ | `POST .../teams/{id}/transfer-in` | `{employeeIds, effectiveDate, reason?}` | Toast "Đã lưu." |
| Bổ nhiệm / thôi chức | `POST .../teams/{id}/appoint` `{employeeId, position}`; `.../dismiss` `{employeeId}` | — | Toast "Đã lưu." |
| Thêm / Loại thành viên | `GET .../teams/candidates`; `POST .../teams/{id}/add-members`, `.../remove-members` | `{employeeIds, reason?}` | Toast "Đã lưu." |
| Danh sách đánh giá bậc | `GET /api/v1/{admin\|manager\|team-leader}/grade-evaluations` | `EvaluationQuery` | Bảng đề xuất |
| Danh sách công nhân | `GET /api/v1/team-leader/grade-evaluations/workers` hoặc `{base}/workers` | — | Select công nhân |
| Đề xuất bậc | `POST /api/v1/team-leader/grade-evaluations` | `{workerId, proposedGradeCode, reason}` | Toast `GR_MESSAGES.proposed` |
| Đặt bậc trực tiếp | `POST {base}/set` | `SetGradePayload` | Toast `GR_MESSAGES.set` |
| Áp dụng / Không áp dụng | `POST {base}/{id}/apply` | `{effectiveDate, decisionNote?}` | Toast `applied` / `notApplied` |
| Bậc & mức tiền | `GET/POST /api/v1/admin/grade-levels`, `PUT/DELETE .../grade-levels/{id}`; `GET/POST /api/v1/admin/grade-codes`, `PUT/DELETE .../grade-codes/{code}`; `GET /api/v1/common/grade-code-options` | `SaveGradeCodePayload`, `SaveLevelPayload` | Toast `codeSaved/codeDeleted/levelSaved/levelDeleted` |
| Gợi ý AI | `GET /api/v1/team-leader/grade-suggestions`; `POST .../generate {lookbackDays}`; `PUT/DELETE .../items/{workerId}`; `POST .../confirm` | — | Toast kết quả (đã gửi/bỏ qua), bảng gợi ý |

## 4. Điểm nghi vấn lệch pha

- Nút ghi "Thêm nhân viên văn phòng" (kèm gợi ý công nhân tạo ở Chấm công) nhưng form vẫn cho chọn mọi vai trò (chỉ khóa qua `roleLocked`) → có thể tạo sai loại.
- Tên đăng nhập tự sinh nên FE không chặn trùng; trùng mã/tên chỉ biết khi API trả lỗi.
- Ngày nghỉ việc không chặn ngày trước ngày vào làm hay ngày tương lai; Lý do chỉ có required + max.
- Đổi Vai trò/Tổ chỉ "gợi ý" Kiểu lương, có thể lưu cặp Vai trò–Kiểu lương lệch.
- `rowVer` gửi lại khi sửa/nghỉ việc nhưng FE chưa có xử lý riêng cho xung đột đồng thời, chỉ hiện lỗi chung.
- Import Excel chỉ chặn `accept=".xlsx"`; `Modal.confirm` dùng `run.mutateAsync` mà không thấy `catch` rõ ràng cho lỗi API.
- Chuyển tổ: Ngày hiệu lực không chặn trước ngày vào làm; quy tắc "Tổ trưởng/phó phải thôi giữ chức trước" chỉ có thông báo, kiểm ở BE.
- Mã bậc cho phép chữ thường (pattern `[A-Za-z0-9]`) nhưng nhãn ghi "chữ hoa", không có `normalize` viết hoa.
- Hai quy tắc ngày khác nhau cho mức tiền: `EditLevelModal` bắt buộc sau hôm nay, `ChangeAmountModal` dùng `earliest`.
- `GradePage` không tách quyền theo tab: cần kiểm bằng UI xem TL/TD có thấy nút sửa ở tab "Bậc & mức tiền" không.
- Nút AI chỉ là icon, không có tên accessible → khó dùng Semantic Locator.
