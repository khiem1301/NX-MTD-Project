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
---

# QUY CHUẨN KIỂM THỬ HỆ THỐNG (REPORT 5.3 — E2E PLAYWRIGHT)

- **Phạm vi đọc:** chỉ đọc Frontend theo `tests/docs/ST_MODULES.md` (routes, pages, API client). **CẤM** đọc Backend `Service`/`Repository`/`Validator`/`Program.cs`, SQL `Trigger`/`View`, và **CẤM** query/đọc DB nội bộ để Assert — chỉ kiểm tra qua giao diện (hành vi người dùng thấy).
- **Assert-Locking:** viết đủ 100% `Test Case ID` (`ST_M<N>_<NNN>`). **CẤM** sửa giá trị kỳ vọng (text, toast, trạng thái, URL) để ép `Passed`. Round 1 `Failed`: chỉ sửa file test khi sai Locator, thiếu dữ liệu mồi hoặc lỗi môi trường; lỗi logic/thiếu validation/lỗi 500 giữ nguyên `Failed` và sửa code ứng dụng.
- **Locator:** dùng Semantic Locator của Playwright (`GetByRole`, `GetByLabel`, `GetByText`, `GetByPlaceholder`, `GetByTestId`). Hạn chế CSS/XPath; cấm selector theo thứ tự/class sinh tự động.
- **Chờ:** **CẤM** `Task.Delay` / `Thread.Sleep`; dùng auto-wait và `Assertions.Expect(...)`, `WaitForURLAsync`, `WaitForResponseAsync`.
- **Dữ liệu:** mọi bản ghi do test tạo phải có tiền tố `ST_AUTO_` (mã, tên, ghi chú) và được test tự dọn qua giao diện/API công khai sau khi chạy; không hardcode ID.
- **Hạ tầng:** kế thừa `tests/System.Tests/BaseSystemTest.cs` (`NewPageAsRoleAsync`, `GotoAndWaitAsync`, `AssertToastAsync`); URL qua `ST_BASE_URL`, tài khoản trong `appsettings.SystemTest.json`; ảnh lỗi tự lưu `tests/docs/ST_RESULTS/screenshots/<CaseID>.png`.
- **Báo cáo:** `.trx` đặt tên `ST_M<N>_Round<1|2>.trx` → `parse_trx.py` → `export_it_report.py --suite st --batch <N> --round <R>` → `tests/docs/Report-5.3_System Test.xlsx`.
