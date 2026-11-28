# ST_CONTRACT — Module 4: Đơn hàng khách & Phân tổ

> Nguồn: `fe/src/features/orders/{pages/Order*,components/Order*,ProductBlockEditor,utils/orderValidation,api/orderApi}`, `features/production/{pages/OrderLineTeamsPage,components/OrderLineTeam*}`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/admin/orders` | OrdersPage | Admin | Nút "Tạo đơn hàng" → `/admin/orders/new`; Xem → `/admin/orders/:id`; Sao chép → `/admin/orders/new?copyFrom=:id` |
| `/admin/orders/new` | OrderFormPage (tạo, có thể nhập Excel đơn) | Admin | Lưu → trang chi tiết / danh sách; Quay lại → `/admin/orders` |
| `/admin/orders/:id` | OrderDetailPage | Admin | Sửa → `/admin/orders/:id/edit`; Sao chép; Hủy đơn hàng; Dừng đơn hàng; Chuyển sang sản xuất; Quay lại → `/admin/orders` |
| `/admin/orders/:id/edit` | OrderFormPage (sửa) | Admin | Lưu/Quay lại → `/admin/orders/:id` |
| `/manager/orders` | OrderOverviewListPage | Manager | Xem → `/manager/orders/:id` |
| `/manager/orders/:id` | OrderOverviewPage (xem tổng quan, NVL, bìa) | Manager | Quay lại → `/manager/orders` |
| `/order-line-teams` | OrderLineTeamsPage ("Chia đơn cho tổ may") | Admin, Manager | Chọn tổ may cho từng dòng đơn hoặc hàng loạt |

- Role sai → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tạo đơn hàng | Button primary | `GetByRole(Button, new{Name="Tạo đơn hàng"})` |
| Tìm / Lọc Trạng thái / Khách hàng | Search + Select | `GetByPlaceholder("Tìm theo mã, tên...")`, `GetByRole(Combobox)` |
| Mã đơn hàng | required; trim, 1–30, regex `[A-Za-z0-9._-]+` ("Mã đơn hàng tối đa 30 ký tự." / "Mã đơn hàng chỉ gồm chữ không dấu, số và . _ -") | `GetByLabel("Mã đơn hàng")` |
| Khách hàng | Select required; có nút "Thêm khách" (mở CustomerFormModal) | `GetByLabel("Khách hàng")`, `GetByRole(Button, "Thêm khách")` |
| Ngày nhận | DatePicker required | `GetByLabel("Ngày nhận")` |
| Ngày giao | DatePicker required; phải ≥ Ngày nhận ("Ngày giao phải sau hoặc bằng ngày nhận.") | `GetByLabel("Ngày giao")` |
| Địa chỉ giao hàng | required, max 500 | `GetByLabel("Địa chỉ giao hàng")` |
| Ghi chú | max 1000 | `GetByLabel("Ghi chú")` |
| Thêm mã hàng vào đơn | Button; mỗi block: Mã hàng (required "Vui lòng chọn mã hàng."), Đơn giá CMT (required, 0–100.000.000, tối đa 2 số lẻ) | `GetByRole(Button, "Thêm mã hàng vào đơn")`, `GetByLabel("Mã hàng")`, `GetByLabel("Đơn giá CMT")` |
| Thêm màu / Thêm size | Button; Màu required, max 50, không trùng; ô số lượng số nguyên 0–1.000.000; block phải có ít nhất 1 màu có SL | `GetByRole(Button, "Thêm màu")`, `"Thêm size"` |
| Xem / Tạo mã hàng ngay trong đơn | Button icon có `aria-label` (`viewStagedProduct`, `createProduct`) | `GetByLabel(<aria-label>)` |
| Nhập đơn từ Excel | OrderImportReviewModal: tải file, xem trước, sửa, xác nhận | `Locator("input[type=file]")` |
| Chi tiết: Sửa / Sao chép đơn / Hủy đơn hàng / Dừng đơn hàng | Button; Hủy và Dừng mở modal lý do (Dừng bắt buộc: "Lý do dừng"; Hủy không bắt buộc) | `GetByRole(Button, "Sửa")`, `"Sao chép đơn"`, `"Hủy đơn hàng"`, `"Dừng đơn hàng"` |
| Chuyển sang sản xuất | Button; Popconfirm "Chuyển đơn hàng sang sản xuất?"; bị disable với hint "Chưa nhận NVL nào nên chưa thể chuyển sang sản xuất." | `GetByRole(Button, "Chuyển sang sản xuất")` rồi OK |
| Bảng đơn | Cột Mã đơn hàng, Khách hàng, Ngày giao, Tổng SL, Đã giao, Còn phải giao, Thành tiền CMT, NVL, Nguy cơ trễ, Trạng thái | `GetByRole(Row, new{Name=<mã đơn>})` |
| Chia đơn cho tổ may: tìm | "Tìm đơn, mã hàng, màu, size" | `GetByPlaceholder("Tìm đơn, mã hàng, màu, size")` |
| Chọn tổ may (từng dòng) | Select có aria-label "Tổ may của {đơn} - {mã hàng} - {màu}/{size}" | `GetByLabel(new Regex("Tổ may của"))` |
| Chọn tổ may (hàng loạt) | Select aria-label "Chọn tổ may" + nút áp dụng/xong | `GetByLabel("Chọn tổ may")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách đơn (Admin) | `GET /api/v1/admin/customer-orders` | `page,pageSize,keyword,status,customerId` | Bảng + phân trang |
| Mã đơn gợi ý | `GET /api/v1/admin/customer-orders/next-code` | — | Điền sẵn Mã đơn hàng |
| Tạo / sửa đơn | `POST /api/v1/admin/customer-orders`; `PUT .../customer-orders/{id}` | header đơn + `blocks[{productId, unitPrice, lines[{color,size,qty}]}]` + `rowVer` | Điều hướng chi tiết; lỗi theo trường hiện dưới ô; "Vui lòng sửa các ô báo đỏ ở phần mã hàng." nếu FE chặn |
| Chi tiết đơn | `GET /api/v1/admin/customer-orders/{id}` | — | Trang chi tiết |
| Hủy / Dừng | `POST .../customer-orders/{id}/cancel`, `.../stop` | `{reason}` | Trạng thái đơn đổi |
| Chuyển sang sản xuất | `POST .../customer-orders/{id}/start-production` | — | Toast `productionStarted` "Đã chuyển đơn hàng sang sản xuất." |
| Nhập đơn Excel | `POST .../customer-orders/import/preview`, `.../import`, `.../import/preview-data` | multipart | Toast `orderExcel.saved` / kết quả từng đơn |
| Tạo KH nhanh | `POST /api/v1/admin/customers`; `GET .../customers/options` | `{customerName, phone?, email?, address}` | Khách mới có trong Select |
| Danh sách / chi tiết đơn (Manager) | `GET /api/v1/manager/customer-orders`; `GET .../customer-orders/{id}` | — | Bảng / trang tổng quan |
| Bìa của đơn (Manager) | `GET/PUT /api/v1/manager/order-markers/{orderId}/products/{productId}` (+ `import/preview`, `import`, `import-data`) | bìa theo mã hàng | Toast lưu |
| Danh sách / lưu chia tổ | `GET /api/v1/manager/order-line-teams` (và `.../teams`); `PUT .../order-line-teams` | `SetOrderLineTeamsPayload` | Toast "Đã lưu việc chia đơn." |
| Chia tổ hàng loạt | `POST /api/v1/manager/order-line-teams/bulk` | `AddOrderLineTeamsPayload` | Toast lưu; bỏ chọn |

## 4. Điểm nghi vấn lệch pha

- Admin nhập mã đơn tay nhưng có `next-code`; FE chỉ regex định dạng, không kiểm trùng mã (trùng biết qua lỗi API).
- Ngày giao chỉ kiểm ≥ Ngày nhận, không chặn ngày nhận/giao quá khứ.
- "Hủy đơn hàng" không bắt buộc lý do, "Dừng đơn hàng" bắt buộc (`required={pending==='stop'}`): hai nhãn lý do khác nhau, dễ test sai kỳ vọng.
- Nút "Chuyển sang sản xuất" bị disable bằng hint từ FE khi chưa nhận NVL: điều kiện nằm ở dữ liệu trả về; FE không kiểm lại khi bấm.
- Với đơn đã vào sản xuất, FE báo "Đơn đã vào sản xuất: không được để trống số lượng đã khai" (`oldLineEmpty`) — logic phụ thuộc trạng thái, cần ca test riêng.
- Đơn giá CMT cho phép `0`; chỉ chặn số âm và >100.000.000.
- Nhập đơn từ Excel: FE hardcode loading qua `message.loading(...,0)` chỉ tắt khi xong; nếu API lỗi giữa chừng có nguy cơ toast loading không tắt.
- `copyFrom` lấy từ query string: FE không xử lý rõ ràng khi id không tồn tại.
- Manager không có quyền sửa đơn nhưng vẫn vào được `/order-line-teams`; Admin cũng vào được — FE không phân biệt phạm vi ghi.
- Nhiều nút chỉ có icon + `aria-label` (xem/tạo mã hàng trong đơn): Semantic Locator phải dựa vào aria-label.
