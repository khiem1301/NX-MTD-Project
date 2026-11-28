# ST Module 4 — Round 1: các ca Failed

Kết quả: **22 ca, 6 Passed, 16 Failed**. Log: `ST_M4_Round1.trx` (các ca sai do code test đã sửa và chạy lại, kết quả gộp vào Round 1).

Lỗi chặn chính: trên server deploy, `POST /admin/customer-orders` trả 500 COMMON_INTERNAL_ERROR. Đã tái hiện độc lập bằng một đơn tối thiểu hợp lệ gửi thẳng qua API (AK-0522, 1 màu, 1 size).

| Ca | Thông báo lỗi |
|---|---|
| 001 | Tạo đơn hợp lệ bị 500 "Lỗi hệ thống. Vui lòng thử lại.". Mã đơn gợi ý, tổng SL và thành tiền hiển thị đúng trước khi lưu; các bước sau chưa được kiểm |
| 002 | Thêm khách ngay trên form được, nhưng lưu đơn bị 500; các bước sau chưa được kiểm |
| 003 | BỊ CHẶN: không tạo được đơn mồi (500) nên chưa kiểm được lỗi trùng mã đơn |
| 004 | Lưu đơn để kiểm trim mã bị 500; kết quả kiểm 31 ký tự và "ĐH01" chưa được ghi nhận vì ca dừng ở lỗi 500 |
| 005 | Lưu đơn có ngày năm 2020 bị 500, không phải chấp nhận hay lỗi nghiệp vụ rõ ràng |
| 006 | Lưu đơn với địa chỉ 500 và ghi chú 1000 ký tự bị 500; phần F5 chưa được kiểm |
| 007 | Ô nhập tự sửa giá trị sai rồi vẫn gửi API: đơn giá -1 thành 1; 100000000,01 thành 100000000; 1500,555 thành 1500; SL 1,5 thành 1. SL 1000001 không gửi API nhưng không thấy ô báo đỏ. Chọn mã hàng, màu trùng, khối không có SL, giá 0 và 100000000 đạt |
| 008 | BỊ CHẶN: không tạo được đơn mồi (500) nên chưa kiểm được xung đột 2 tab |
| 009 | BỊ CHẶN: không tạo được đơn mồi (500) nên chưa kiểm được sao chép đơn |
| 010 | BỊ CHẶN: không tạo được đơn mồi (500) nên chưa kiểm được hủy đơn |
| 011 | Dừng đơn DH-2510-003 (có lý do) bị 500. Bỏ trống lý do bị chặn đúng |
| 012 | BỊ CHẶN: không tạo được đơn mồi (500) nên chưa kiểm nút Chuyển sang sản xuất |
| 013 | Giảm SL đơn đang sản xuất DH-2511-005 xuống dưới số đã làm: server trả 500 thay vì lỗi nghiệp vụ rõ ràng. Chặn xóa trống SL đạt; dữ liệu đơn không đổi (đạt) |
| 014 | Xem trước và sửa dòng nhập từ Excel đạt, nhưng lưu đơn bị 500 |
| 017 | Lưu bìa của đơn DH-2511-007 (PUT order-markers) bị 500. Giá trị bìa do test tự nhập |
| 018 | Không giao được dòng đơn cho MAY1: TEAM_ORDER_BUSY, tổ đang làm đơn DH-2511-006 chưa đạt 90% (thiếu dữ liệu mồi: tổ may rảnh có tài khoản tổ trưởng) |
