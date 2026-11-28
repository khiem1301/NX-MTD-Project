# ST_CONTRACT — Module 13: Lương & Công nợ đơn hàng

> Nguồn: `fe/src/features/payroll/*`, `features/order-payment/*`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/payrolls` | AccountantPayrollPage ("Bảng lương") | Accountant | Chọn tháng → Xuất dữ liệu tính lương → Nhập bảng lương (Excel đã tính) → Xem trước → Nhập → Gửi duyệt |
| `/payrolls/approval` | PayrollApprovalPage ("Duyệt bảng lương") | Admin | Lọc trạng thái → xem chi tiết → Duyệt (khóa tháng) / Từ chối / Mở lại về Nháp |
| `/my/payslips` | MyPayslipPage ("Phiếu lương của tôi") | Mọi role đã đăng nhập (route không khai báo `roles`) | Danh sách tháng đã duyệt → chi tiết phiếu → Quay lại |
| `/my/estimated-pay` | MyEstimatedPayPage ("Sản lượng ước tính") | Worker | Chọn khoảng ngày → xem tổng ước tính → chi tiết theo ngày |
| `/accounting/order-payments` | OrderPaymentsPage ("Công nợ đơn hàng") + OrderPaymentModal | Accountant | Mở đơn → Chốt / đổi giá trị → Thêm / Sửa lần thanh toán |

- Role sai → redirect `/`. `/my/payslips` không giới hạn role (ai cũng vào; dữ liệu chỉ của mình).

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Chọn tháng | MonthPicker "Tháng" | `GetByLabel("Tháng")` |
| Xuất dữ liệu tính lương | Button tải `BangLuong_<yyyy-MM>.xlsx` | `GetByRole(Button, "Xuất dữ liệu tính lương")` |
| Chọn file Excel | Upload `.xlsx`; sau đó nút "Xem trước" | `Locator("input[type=file]")`, `GetByRole(Button, "Xem trước")` |
| Tóm tắt xem trước | Hợp lệ / Lỗi / Trùng; bảng: Sheet (tổ), Dòng, SLĐ, Họ tên, Số công, Thực lĩnh, Số khoản, Trạng thái, Ghi chú | `GetByText("Hợp lệ")` |
| Nhập bảng lương | Button + Modal.confirm "Nhập bảng lương?" | `GetByRole(Button, "Nhập bảng lương")`, `GetByRole(Button, "Xác nhận")` |
| Gửi duyệt | Button (Kế toán) | `GetByRole(Button, "Gửi duyệt")` |
| Tải file gốc | Button | `GetByRole(Button, "Tải file gốc")` |
| Tháng chưa có bảng lương | Empty "Tháng này chưa có bảng lương. Hãy nhập file Excel đã tính…" | `GetByText("Tháng này chưa có bảng lương")` |
| Admin: lọc trạng thái | Select "Lọc theo trạng thái" (Nháp, Chờ duyệt, Đã duyệt, Từ chối) | `GetByPlaceholder("Lọc theo trạng thái")` |
| Bảng lương | Tháng, Trạng thái, Tổng quỹ lương, Số phiếu lương, Gửi duyệt lúc, Duyệt lúc, Lý do từ chối | `GetByRole(Row)` |
| Duyệt / Từ chối / Mở lại | Duyệt: Popconfirm (khóa tháng); Từ chối: lý do bắt buộc; Mở lại về Nháp: lý do | `GetByRole(Button, "Duyệt")`, `GetByRole(Button, "Từ chối")`, `GetByRole(Button, "Mở lại về Nháp")` |
| Phiếu lương | Nhóm Khoản cộng / Khoản trừ / Bảo hiểm / Thông tin khác; Thực nhận; Duyệt lúc | `GetByText("Thực nhận")` |
| Sản lượng ước tính | Khoảng ngày; thẻ Tạm tính, Sản lượng, Thành tiền, Chủ nhật, Phế phẩm; xem chi tiết ngày (DayDetail) | `GetByText("Tạm tính")` |
| Công nợ: tìm / lọc | "Tìm theo mã đơn hoặc khách hàng"; "Mọi trạng thái" | `GetByPlaceholder("Tìm theo mã đơn hoặc khách hàng")` |
| Chốt / đổi giá trị | Giá trị chốt required số ("Nhập giá trị chốt."), phải sao cho tổng đã thanh toán nằm giữa 0 và giá trị chốt ("Tổng đã thanh toán phải nằm giữa 0 và giá trị chốt của đơn hàng."); Lý do required+whitespace ("Vui lòng nhập lý do.", max `REASON_MAX_LENGTH`) | `GetByRole(Button, "Chốt / đổi giá trị")`, `GetByLabel("Giá trị chốt")`, `GetByLabel("Lý do")` |
| Thêm lần thanh toán | Ngày thanh toán required; Số tiền required ≠ 0 ("Nhập số tiền khác 0.") trong khoảng "Còn nhập được"; Hình thức required (Chuyển khoản/Tiền mặt); Ghi chú max 500; sửa: Lý do sửa required | `GetByRole(Button, "Thêm lần thanh toán")`, `GetByLabel("Ngày thanh toán")`, `GetByLabel("Số tiền")`, `GetByLabel("Hình thức")` |
| Sửa lần thanh toán | Button trong bảng "Các lần thanh toán" | `GetByRole(Button, "Sửa lần thanh toán")` |
| Cảnh báo chưa chốt | "Chốt giá trị đơn hàng trước khi ghi nhận thanh toán." | `GetByText("Chốt giá trị đơn hàng trước khi ghi nhận thanh toán.")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Xem bảng lương tháng | `GET /api/v1/accountant/payrolls?periodMonth` | — | Tóm tắt + danh sách phiếu |
| Xuất dữ liệu | `GET /api/v1/accountant/payrolls/export?periodMonth` | — | Tải `BangLuong_<tháng>.xlsx` |
| Xem trước / nhập | `POST /api/v1/accountant/payrolls/import/preview`; `POST .../payrolls/import?periodMonth` | multipart `.xlsx` | Bảng xem trước; nhập xong reload |
| Gửi duyệt | `POST /api/v1/accountant/payrolls/{id}/submit` | — | Trạng thái "Chờ duyệt" |
| Tải file gốc | `GET /api/v1/{accountant\|admin}/payrolls/{id}/file` | — | Tải file |
| Danh sách / chi tiết (Admin) | `GET /api/v1/admin/payrolls?status&pageSize=100`; `GET .../admin/payrolls/{id}` | — | Bảng + modal |
| Duyệt / Từ chối / Mở lại | `POST /api/v1/admin/payrolls/{id}/approve`; `.../reject` `{reason}`; `.../reopen` `{reason}` | — | Trạng thái "Đã duyệt" / "Từ chối" / "Nháp"; đóng modal khi duyệt |
| Phiếu lương của tôi | `GET /api/v1/common/my-payslips`; `GET .../my-payslips/{periodMonth}` | — | Danh sách / chi tiết |
| Sản lượng ước tính | `GET /api/v1/common/my-estimated-pay?from&to` (+ `/day?date` = `myEstimatedPayDay`) | — | Thẻ tổng + chi tiết ngày |
| Công nợ: danh sách / chi tiết | `GET /api/v1/accountant/order-payments` (+ `/{id}`) | `page, keyword, state` | Bảng + modal |
| Chốt giá trị | `PUT /api/v1/accountant/order-payments/{id}/final-amount` | `{amount, reason, rowVer}` | Toast "Đã lưu giá trị chốt." |
| Thêm / sửa thanh toán | `POST /api/v1/accountant/order-payments/{id}/payments`; `PUT .../payments/{paymentId}` | `{paymentDate, amount, paymentMethod, paymentNote?, editReason?}` | Toast "Đã ghi nhận lần thanh toán." / "Đã sửa lần thanh toán." |

## 4. Điểm nghi vấn lệch pha

- `/my/payslips` không khai báo `roles` trong `routes.tsx` (mọi role đăng nhập), trong khi trang mô tả "chỉ xem phiếu lương của tháng đã được duyệt" → kiểm bằng dữ liệu, không bằng route.
- Nhập bảng lương: FE chỉ chặn đuôi `.xlsx`; kết quả hợp lệ/lỗi/trùng do BE phân loại; dòng Lỗi/Trùng không chặn nút Nhập ở FE (cần xác minh có `disabled` theo `validCount`).
- Nút Duyệt dùng Popconfirm và đóng modal ngay trong `onSuccess` của mutation; nếu API lỗi, modal giữ mở nhưng thông báo lỗi do hook chung.
- Mở lại về Nháp / Từ chối có lý do nhưng FE chưa thấy giới hạn độ dài; chưa rõ rule cho "Mở lại".
- Chốt giá trị: validator tự tính khoảng `paid` trong `Math.min(0,v)..Math.max(0,v)` cho phép giá trị chốt âm (khách được hoàn tiền) — hành vi "weOwe" cần ca riêng; `validateTrigger="onSubmit"` nên lỗi chỉ hiện khi bấm Lưu.
- Số tiền thanh toán chấp nhận giá trị âm (hoàn tiền) nhưng không được = 0; khoảng cho phép lấy từ dữ liệu tải trước, thay đổi đồng thời chỉ biết khi API lỗi (`rowVer`).
- Ngày thanh toán không chặn ngày tương lai; không kiểm cửa sổ chỉnh sửa (nếu có) ở FE.
- `MyEstimatedPayPage` hiển thị nhãn "Tạm tính" nhưng tổng lấy hoàn toàn từ API; FE không tự cộng.
- Cùng role Accountant có hai màn hình lương và công nợ nhưng khác module menu; đường dẫn `/payrolls` (không tiền tố `/accounting`) và `/accounting/order-payments` không đồng nhất.
- Nhiều nút (xem chi tiết, tải file) là icon → nên thêm `data-testid`.
