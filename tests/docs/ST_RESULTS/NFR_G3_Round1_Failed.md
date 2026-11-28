# NFR Group 3 (Security) — Round 1: các ca Failed

Kết quả: **53 ca, 38 Passed, 15 Failed**. Log: `NFR_G3_Round1.trx`. Bản code: commit `361d9b90`, API Production trên localhost, database `NXMTD_NFR` dựng mới.
Cột "Nhóm" là đề xuất phân loại cho Triage, không thay đổi kết quả Failed. Chi tiết từng phép kiểm: `soft/NFR_G3_xxx.txt`.

| Ca | Nhóm | Spec | Thực tế |
|---|---|---|---|
| 006 | Lỗi bảo mật - Cao | acc/attendance/rj-employees/sync chỉ Admin; Accountant, Manager phải 403 | Accountant (KETOAN) và Manager (QUANDOC) gọi POST rj-employees/sync nhận 200. 10/12 phép kiểm còn lại đạt |
| 008 | Cần nhóm chốt | Worker, Electrician gọi cm/request-inbox phải 403 | Electrician nhận 200. Frontend (features/requests/menu.ts) khai báo Electrician là người xử lý đơn: nhiều khả năng test case chưa cập nhật theo thiết kế |
| 014 | Lệch spec - không rò rỉ | Tham số định danh chèn thêm bị bỏ qua (trả dữ liệu A) hoặc 400 | my-attendance/timesheets và summary: TeamId=4 được áp dụng làm bộ lọc, trả rỗng. EmployeeId / userId / workerId của B bị bỏ qua. Tái hiện thủ công: không có dòng nào của B |
| 015 | Lệch mã HTTP - không đổi dữ liệu | confirm-batch lẫn ID của B: 400/403 hoặc chỉ xử lý ID của A | Từ chối cả lô bằng 404; phiếu của B vẫn PENDING |
| 020 | Lệch mã HTTP - không đổi dữ liệu | Worker đính kèm vào đối tượng của người khác: 403 hoặc 400 | Trả 404 cho PurchaseOrder, DispatchNote, Request, Payroll; file không xuất hiện ở đối tượng đích |
| 024 | Lệch mã HTTP - không đổi dữ liệu | productId không thuộc orderId: 404 | Trả 409; bìa của đơn 7 và đơn 8 không đổi |
| 030 | Lỗi bảo mật - Cao | Dùng lại refresh token sau logout phải 401 | (a) Sau logout (HTTP 200), refresh token cũ vẫn đổi được token mới: HTTP 200. Frontend gọi logout không gửi refresh token. (b)(c)(d) đạt 401. (e) server không xoay vòng token, dùng lại được: ghi nhận rủi ro |
| 031 | Lỗi bảo mật - Cao | Sau tối đa 10 lần sai: 429 hoặc tạm khóa tài khoản | 100 lần sai cùng user và 100 user khác nhau: không có 429; sau 10 lần sai, mật khẩu đúng vẫn đăng nhập được (200) |
| 032 | Lệch mã HTTP - không đổi dữ liệu | Manager reset / khóa / mở khóa tài khoản Admin: 403 | Cả 3 trả 404; Admin vẫn đăng nhập bằng mật khẩu cũ |
| 033 | Lệch mã HTTP | Đổi mật khẩu với mật khẩu cũ sai: 400 | Trả 409. Các phép thử injection và chuỗi dài đạt |
| 039 | Lệch mã HTTP | Đúng 5 MB: 2xx; 5 MB + 1 byte: 413 | Giới hạn được thực thi (đúng 5 MB thành công, quá 1 byte bị chặn) nhưng trả 400 ở attachments và sample-image |
| 041 | Lỗi bảo mật - Cao (DoS) | Zip-bomb xlsx: 400 trong < 10 s, RAM tăng tối đa 200 MB, không 500 | File nén 1,1 MB (giải nén > 1,1 GB): cả 6 API import được thử trả 500; log API có System.OutOfMemoryException; RAM đỉnh +2114 MB. API không bị sập trong lần chạy này |
| 046 | Lệch spec - đã chặn an toàn | pageSize -1 / 0 / 2147483647 và ngày ngoài miền: 400 kèm thông điệp | pageSize sai được tự điều chỉnh (-1, 0 thành 20; quá lớn thành 100) và trả 200; khoảng ngày 9999-12-30..31 trả 200. Không có 500 |
| 047 | Lệch mã HTTP | Mảng 100.000 phần tử: 400/413 trong < 5 s; rỗng: 400 | Phản hồi nhanh (0,8 - 1,2 s) nhưng trả 409 (piece-rates/approve-many) và 404 (admin, manager order-materials/batch) |
| 050 | Lỗi chức năng - Trung bình | Mỗi role mở route của mình: 0 lỗi Console, 0 response 4xx/5xx | 4/160: /admin/break-even, /admin/cost-summary, /monthly-costs gọi API nhận 404 (có lỗi Console); WarehouseKeeper mở /warehouse/material-returns thì FE gọi API của Tổ trưởng (team-leader/material-returns/returnable) nhận 403 |
