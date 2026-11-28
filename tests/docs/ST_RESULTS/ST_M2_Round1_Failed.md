# ST Module 2 — Round 1: các ca Failed

Kết quả: **35 ca, 24 Passed, 11 Failed**. Log: `ST_M2_Round1.trx` (các ca sai do code test đã sửa và chạy lại, kết quả gộp vào Round 1).

| Ca | Thông báo lỗi |
|---|---|
| 001 | `POST /admin/employees` trả 200, đặc tả yêu cầu 201. Các bước sau mã HTTP (toast, mật khẩu tạm, F5, đăng nhập) chưa được kiểm |
| 004 | Ô Mã nhân viên nhận được 21 ký tự (đặc tả: 21 ký tự không nhập được). Các bước sau chưa được kiểm |
| 005 | Gõ "Nguyễn Văn 2": ô tự xóa chữ số còn "Nguyễn Văn ", không báo lỗi. Các bước mã chấm công 0/100000000 chưa được kiểm |
| 010 | Form "Thêm nhân viên văn phòng" không có vai trò "Công nhân" trong danh sách Vai trò |
| 012 | Cho nghỉ việc với ngày mai được chấp nhận: payload resignDate=2026-11-28, API trả 200 (đặc tả: phải từ chối) |
| 020 | Ô Mã tổ nhận được 21 ký tự (đặc tả: 21 ký tự bị chặn). Các bước sau chưa được kiểm |
| 023 | Chuyển tổ với ngày hiệu lực 01/01/2026, trước ngày vào làm 01/06/2026, vẫn thành công: API trả 200 và nhân viên đã sang tổ đích |
| 025 | Bổ nhiệm tổ trưởng: API 2xx nhưng không có toast "Đã lưu." (FE `useAppoint` không khai báo successMessage). Các bước đổi vai trò, vào /team/meals, /hr/grades chưa được kiểm |
| 027 | Gõ mã bậc "b-1": ô tự xóa dấu "-" còn "b1", không báo lỗi pattern. Các bước sau chưa được kiểm |
| 033 | Phần Worker và Manager đạt. Phần TeamLeader tổ cắt không chạy được: thiếu tài khoản tổ trưởng tổ cắt (thiếu dữ liệu mồi) |
| 035 | `POST /grade-suggestions/generate` trả lỗi GRADE_AI_UNAVAILABLE: server deploy chưa cấu hình khóa Gemini (lỗi môi trường) |
