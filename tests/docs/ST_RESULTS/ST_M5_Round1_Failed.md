# ST Module 5 — Round 1: các ca Failed

Kết quả: **19 ca, 13 Passed, 6 Failed**. Server http://125.253.114.82, code `main` tại `7a561f8c`.

Cách chạy: chạy đủ 19 ca, sau đó sửa lỗi trong code test (dữ liệu mồi thiếu SĐT/email, định vị ô chọn và ô nhập, chờ danh sách tải, thời điểm tải ảnh chứng từ mồi, kiểm trạng thái ô báo lỗi) và chạy lại các ca bị ảnh hưởng. Kết quả các lần chạy lại được gộp vào Round 1. Giá trị kỳ vọng theo test case giữ nguyên. Đơn mua dùng để test là đơn mua dự trữ do test tự tạo (trừ ca 005 đặt hàng đơn mẫu DM-20261126-004).

| Ca | Thông báo lỗi |
|---|---|
| 002 | Email "ncc@" có báo lỗi nhưng nội dung là "Email không hợp lệ (dạng ten@mien.com, …)", test case ghi "Email không hợp lệ.". Tên trống, tên 200/201, email 100/101 và lưu giá trị biên đều đạt. QA cần xác nhận câu thông báo |
| 006 | Ô nhập tự sửa giá trị sai mà không báo lỗi: số lần giao 0 thành 1; 100 thành 10; đơn giá gõ "0,5" thành 5. Các ý còn lại đạt |
| 007 | Đơn mua đã đặt hàng, chưa thanh toán không có nút "Hủy đơn mua" (chỉ có "Sửa đơn mua"; server trả canCancel=false). Theo FE chỉ hủy được đơn chưa đặt hàng. Các bước sau chưa được kiểm. QA cần xác nhận quy tắc hủy |
| 010 | Số tiền "0,01" không nhập được: ô chỉ nhận số nguyên và tự đổi thành 1, trong khi test case ghi 0,01 hợp lệ. 0, bỏ trống, vượt số còn phải trả báo lỗi đúng; 6.000.000 hợp lệ. Hình thức luôn chọn sẵn, không bỏ trống được |
| 011 | Ảnh thứ 6 không bị chặn: giao diện cho tải tối đa 20 ảnh, test case ghi tối đa 5. File .exe đổi đuôi .jpg, ảnh 25MB, ghi chú 501 ký tự bị chặn đúng; ảnh hợp lệ xem lại được |
| 015 | Đơn giá "0,01" không nhập được: ô chỉ nhận số nguyên và tự đổi thành 1. Đơn giá 0 báo lỗi, ghi chú 1001 bị chặn, sau khi gửi lại chỉ đơn giá thay đổi (đạt) |
