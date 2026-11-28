# ST Module 1 — Round 1: các ca Failed

Kết quả: **27 ca, 23 Passed, 4 Failed**. Log: `ST_M1_Round1.trx`.

| Ca | Thông báo lỗi |
|---|---|
| 002 | Sau đăng nhập ở `/login?from=/profile` app chuyển về `/` (Trang chủ) thay vì `/profile`; không có `GET /common/profile` (test timeout chờ response) |
| 007 | Username 300 ký tự: không hiện Alert; hiện lỗi tiếng Anh dưới ô "The length of 'Username' must be 50 characters or fewer. You entered 300 characters." |
| 011 | Không chạy được: danh sách tài khoản không có TeamLeader thuộc tổ cắt (thiếu dữ liệu mồi) |
| 027 | UserMenu hiện "Mã NV: <username>" (vd DongKTT23) thay vì Mã nhân viên trên thẻ hồ sơ (ST_AUTO_...) |
