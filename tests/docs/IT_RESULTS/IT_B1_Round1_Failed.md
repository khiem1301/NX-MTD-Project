# IT Batch 1 — Round 1: các ca Failed (001–212)

Kết quả: **212 ca, 179 Passed, 33 Failed** (+ IT_B1_151 không ổn định: 2/6 lần chạy lại bị 500). Log: `IT_B1_Round1.trx`, `IT_B1_Round1.json`.
Nhóm: **A** = lệch mã HTTP so với spec · **B** = thiếu validation / quy tắc nghiệp vụ · **C** = lỗi 500 / race · **D** = rò rỉ dữ liệu / bảo mật · **E** = lệch cấu trúc / audit.

| Ca | Nhóm | Spec | Thực tế |
|---|---|---|---|
| 017 | B | Refresh token quá hạn 1s → 401 | 200 (ClockSkew 1 phút trong `TokenService`) |
| 027 | B | Phone 15 ký tự → 200 | 400 (`PhoneRule` chỉ nhận 10 số / +84+9 số) |
| 034 | A | Sai/thiếu CurrentPassword → 400 | 409 `AUTH_WRONG_CURRENT_PASSWORD` |
| 035 | A | Sai CurrentPassword → 400/401 | 409 |
| 037 | B | NewPassword ngắn hơn min → 400 | 200 (không có chính sách độ dài tối thiểu) |
| 038 | B | NewPassword trùng cũ → 400 | 200 |
| 049 | B | Thiếu `teamId` → 400 | 200, danh sách rỗng |
| 071 | A | `TeamId` không tồn tại → 400/404 | 409 |
| 072 | B | Thiếu `StartDate` → 400 | 200, tạo nhân viên (DateOnly mặc định lọt validator) |
| 075 | E | Import RJ ghi 1 dòng AuditLogs | 2 dòng |
| 080 | A | Import vào tổ đã ngừng → 400 | 409 `TEAM_NOT_ACTIVE` |
| 089 | D | Manager xem chi tiết: `Account = null` | Trả đầy đủ `Account` (username, status, …) |
| 105 | A | Admin tự nghỉ việc → 400 | 409 `ACCOUNT_CANNOT_LOCK_SELF` |
| 113 | A | Admin tự khóa → 400 | 409 `ACCOUNT_CANNOT_LOCK_SELF` |
| 117 | B | Reset mật khẩu người đã nghỉ → 400 | 200, cấp mật khẩu tạm cho người đã nghỉ |
| 130 | B | Reactivate tổ đang ACTIVE → 400/409 | 200 |
| 135 | A | Thêm người đã nghỉ vào tổ → 400 | 404 |
| 137 | A | Gỡ người không thuộc tổ → 400 | 409 `TEAM_MEMBER_NOT_IN_TEAM` |
| 140 | A | Chuyển vào tổ đã ngừng → 400 | 409 `TEAM_NOT_ACTIVE` |
| 141 | A/B | Ngày hiệu lực ngày mai: chấp nhận hoặc từ chối; `0001-01-01` → 400 | Ngày mai 409 `TEAM_TRANSFER_FUTURE_DATE`; `0001-01-01` được chấp nhận (200) |
| 142 | B | Bổ nhiệm Leader thứ 2 → 400/409 | 200, tự hạ Leader cũ về Worker |
| 143 | B | Bổ nhiệm Deputy thứ 2 → 400/409 | 200 (tự thay) |
| 146 | A | Bổ nhiệm người ngoài tổ → 400 | 409 `TEAM_MEMBER_NOT_IN_TEAM` |
| 147 | B | Leader → Deputy cùng tổ → 400/409 | 200 |
| 150 | A | Bổ nhiệm người đã nghỉ → 400 | 409 `TEAM_MEMBER_NOT_IN_TEAM` |
| 151 | C | 2 appoint Leader đồng thời: 1×200, 1×400/409 | Không ổn định: 2/6 lần có 1 request trả 500 `COMMON_INTERNAL_ERROR` (race / trigger), request kia 200 |
| 154 | A | Dismiss người ở tổ khác → 400 | 409 `TEAM_MEMBER_NOT_IN_TEAM` |
| 171 | B | `AttendanceCode = int.MaxValue` → 200/201 | 400 (validator giới hạn 1–99.999.999) |
| 196 | B | Tạo mức tiền cho bậc B9 (ngừng dùng) → 400 | 200 |
| 197 | B | `MonthlyAmount = 0` → 400 | 200, lưu mức 0 |
| 198 | C | `MonthlyAmount = 10^16` (vượt decimal(18,2)) → 400 | 500 `COMMON_INTERNAL_ERROR` (overflow) |
| 200 | B | `EffectiveDate` ngày hôm qua → 400 | 200, tạo được mức lùi ngày |
| 208 | E | `History[]` của nhân viên có 4 mục (lock/unlock/reset/PUT) | Chỉ 1 mục: thao tác tài khoản không vào History |
| 210 | B | Appoint vào tổ đã có Leader → 400/409 | 200 (cùng nguyên nhân 142) |

## Ghi chú cho Bước 8
- **Ưu tiên cao:** 089 (rò rỉ `Account` cho Manager), 198 và 151 (500), 117 (mật khẩu tạm cho người đã nghỉ), 072/200/196/197 (thiếu validation).
- **Cần Business quyết định spec hay code đúng:** 142/143/147/210 (app chủ định "tự thay người giữ chức"), 034/035 (409 theo D36 trong code), 105/113 (409), nhóm A khác (400 hay 409/404).
- **Finding đã ghi (không Failed):** 174 — dữ liệu Email bị mất; 179 — không đổi được tên/thứ tự bậc thợ; 099 — `Account.Status` vẫn ACTIVE sau khi nghỉ việc (đăng nhập bị chặn bằng trạng thái Employee).
- Chạy lặp: 095 và 172 pass 6/6; 151 pass 4/6.
