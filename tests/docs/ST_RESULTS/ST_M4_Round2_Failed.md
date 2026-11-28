# ST Module 4 — Round 2: các ca Failed

Kết quả: **22 ca, 17 Passed, 5 Failed** (Round 1: 6 Passed, 16 Failed). Code đang chạy: `main` tại `7a561f8c`, server http://125.253.114.82.

Cách chạy: chạy đủ 22 ca (11 Passed, 11 Failed). Sau đó sửa lỗi trong code test và chạy lại 9 ca (001, 003, 005, 006, 008, 009, 010, 012, 014), rồi chạy lại riêng ca 006. Kết quả các lần chạy lại được gộp vào Round 2. Giá trị kỳ vọng theo test case giữ nguyên.

Lỗi code test đã sửa (các bước này chưa từng chạy tới ở Round 1): regex URL chưa tính `?tab=materials` sau khi tạo đơn; ca 014 đọc sai tên trường trong payload; ca 006 chưa bấm "Mở rộng" để xem hết ghi chú. Ca 001 chuyển phần kiểm cấu trúc payload sang soft assert để kiểm tiếp các bước sau.

| Ca | Thông báo lỗi |
|---|---|
| 001 | Payload POST /admin/customer-orders gửi lines[] ở cấp gốc, không phải blocks[].lines[] như test case. Tạo đơn, tổng SL, thành tiền, F5 và danh sách đều đạt. QA cần xác nhận test case hay API đúng |
| 007 | Ô nhập tự sửa giá trị sai rồi vẫn gửi API: đơn giá -1 thành 1; 100000000,01 thành 100000000; 1500,555 thành 1500; SL 1,5 thành 1. SL 1000001 không gửi API nhưng không thấy ô báo đỏ. Các ý còn lại đạt |
| 010 | Hủy đơn không nhập lý do bị server từ chối 400 "Vui lòng nhập lý do.", trong khi ô trên giao diện ghi "Lý do hủy (không bắt buộc)". Các bước sau (trạng thái, ẩn nút, F5) chưa được kiểm |
| 012 | Nút "Chuyển sang sản xuất" bị disable nhưng rê chuột không hiện hint "Chưa nhận NVL nào…" (ConfirmAction.tsx không truyền sự kiện của Tooltip xuống nút). Chuyển đơn DH-2511-008 (đơn mồi đã đủ NVL, thay cho bước Thủ kho xác nhận phiếu nhập) sang sản xuất đạt |
| 018 | BỊ CHẶN: không giao được dòng đơn cho MAY1, server trả TEAM_ORDER_BUSY vì tổ đang làm đơn DH-2511-006 chưa đạt 90% (thiếu dữ liệu mồi: tổ may rảnh có tài khoản tổ trưởng) |
