# ST Module 3 — Round 1: các ca Failed

Kết quả: **28 ca, 17 Passed, 11 Failed**. Log: `ST_M3_Round1.trx` (các ca sai do code test đã sửa và chạy lại, kết quả gộp vào Round 1).

| Ca | Thông báo lỗi |
|---|---|
| 001 | `POST /admin/customers` trả 200, đặc tả yêu cầu 201. Các điểm còn lại (toast, F5, Select khách hàng, options) đều đạt |
| 002 | Không lưu được khách hàng khi bỏ trống SĐT và Email: form đánh dấu bắt buộc cả hai trường |
| 010 | Sửa mô tả mã hàng AK-0522 (đã dùng trong đơn) bị API trả 400: errors.sampleImageUrl "Ảnh mẫu không hợp lệ, vui lòng chọn lại" dù không đổi ảnh. Ô Mã bị khóa và tooltip đúng; phần đơn hàng cũ chưa được kiểm |
| 011 | Thiếu tên thành phần báo đúng. Sau khi thêm thành phần thì không lưu được: cảnh báo "Dạng bìa A: phải chọn ít nhất một màu và có ít nhất một nhóm bìa" vì mã tạo từ file mẫu có 0 màu. Test chưa khai màu/size nên bước lưu, F5 và Select chi tiết vải CHƯA được kiểm |
| 013 | Nhập TG chuẩn "12.5": ô tự bỏ dấu chấm và lưu standardTimeSec=125, không báo lỗi (sai dữ liệu). Các điểm còn lại đạt |
| 014 | "CD#1" bị tự bỏ ký tự # và không báo lỗi; Thứ tự 0 và 32768, Bậc 0 và 10, TG chuẩn 0 đều không báo lỗi |
| 015 | Đơn giá 0 và 100000001 không hiện đúng câu trong đặc tả (đơn giá 0 hiện "Nhập đơn giá lớn hơn 0."). Nhập 0.01 bị lưu thành 1 ₫ |
| 016 | Khi tick "Là công đoạn cuối" không có thông báo công đoạn cuối hiện tại sẽ chuyển. Sau khi lưu chỉ còn đúng 1 công đoạn cuối (đạt) |
| 018 | Ngày hiệu lực 01/01/2025 không hiện gợi ý ngày đã qua (pastDateHint). Phần lưu/từ chối khớp kết quả server (đạt) |
| 023 | Sau khi Admin từ chối, danh sách công đoạn của Kế toán hiện "Chưa có" thay vì "Bị từ chối". Ghi chú trong chi tiết, sửa, gửi lại và duyệt đều đạt |
| 026 | Admin ở /operations: ba nút Thêm công đoạn, Nhập từ Excel, Nhóm công đoạn bị ẩn hẳn, đặc tả yêu cầu hiện nhưng disabled. API tạo công đoạn bị chặn (đạt) |
