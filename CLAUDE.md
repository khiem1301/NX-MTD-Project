# QUY CHUẨN KIỂM THỬ TÍCH HỢP (REPORT 5.2) & TỐI ƯU TOKEN

## 1. Quy tắc Tiết kiệm Token (Bắt buộc)
- Chỉ đọc đúng các file thuộc module được chỉ định trong câu lệnh. Tuyệt đối không quét toàn bộ repository, không đọc thư mục `bin/`, `obj/`, `node_modules/`, `dist/` hay thư mục `UnitTests/`.
- Xuất kết quả ngắn gọn, đi thẳng vào code hoặc file Markdown, không giải thích dài dòng.

## 2. Quy tắc Tách biệt ngữ cảnh (Khi trích xuất đặc tả)
- Khi được yêu cầu tạo file `IT_CONTRACT_*.md`, **CHỈ** đọc `Controller/Routes`, `Request/Response DTO`, `Middleware/Auth` và `Entity/DB Schema`.
- **TUYỆT ĐỐI KHÔNG** đọc hoặc tóm tắt logic xử lý bên trong `Service` hay `Repository`.

## 3. Quy tắc Khóa kỳ vọng (Assert-Locking)
- Viết đủ 100% `Test Case ID` từ bảng QA cung cấp.
- **CẤM** sửa giá trị kỳ vọng trong `Assert` (như đổi HTTP `400/404/409` thành `500`, hoặc bỏ qua bước kiểm tra DB) để ép test `Passed`.
- Khi test `Failed` ở Round 1: Chỉ sửa file test nếu thiếu dữ liệu mồi (`Seed Data`) hoặc lỗi kết nối Test DB. Mọi lỗi logic, thiếu validation, lỗi `500` hoặc thiếu rollback transaction đều phải giữ nguyên `Failed` và sửa trong code ứng dụng.

## 4. Quy tắc Dữ liệu & Assert kép
- Chạy trên Test Database thật ở `localhost` (không mock Repository/DbContext).
- Mỗi test case phải tự bơm dữ liệu, tự Rollback Transaction / Reset DB sau khi chạy, và lấy ID động từ `TestDataSeeder` (không hardcode ID `1, 2, 3...`).
- Mọi test case bắt buộc có Assert kép: (1) HTTP Status & JSON Response; (2) Query trực tiếp xuống Test DB kiểm tra trạng thái bản ghi.