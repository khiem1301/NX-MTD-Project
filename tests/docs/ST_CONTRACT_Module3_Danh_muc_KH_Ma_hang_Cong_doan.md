# ST_CONTRACT — Module 3: Danh mục Khách hàng, Mã hàng & Công đoạn

> Nguồn: `fe/src/features/orders/{pages/CustomersPage,pages/ProductsPage,components/CustomerFormModal,ProductFormModal,Spec*}`, `features/production/{pages/OperationsPage,AdminPieceRatesPage,components/Operation*,PieceRate*,constants}`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/admin/customers` | CustomersPage | Admin | Menu → danh sách → Thêm/Sửa (modal) / Ngừng hợp tác / Hợp tác lại |
| `/admin/products` | ProductsPage (scope admin) | Admin | Danh sách mã hàng → Thêm/Sửa (modal) / Định mức (SpecEditor) / Ngừng / Dùng lại |
| `/accounting/products` | ProductsPage (scope accountant) | Accountant | Như trên, quyền hạn chế theo scope |
| `/operations` | OperationsPage | Accountant (quản lý), Admin (chỉ xem) | Chọn mã hàng → bảng công đoạn → Thêm/Sửa/Nhập Excel/Nhóm công đoạn; gửi duyệt đơn giá |
| `/admin/piece-rates` | AdminPieceRatesPage | Admin | Danh sách đơn giá chờ duyệt → Duyệt / Từ chối / Duyệt nhiều |

- Role sai → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tìm kiếm (KH/Mã hàng) | placeholder "Tìm theo mã, tên..." | `GetByPlaceholder("Tìm theo mã, tên...")` |
| Lọc trạng thái | Select ("Tất cả"/Đang hợp tác/Ngừng hợp tác) | `GetByRole(Combobox)` trong toolbar |
| Bảng KH | Tên khách hàng, Số điện thoại, Email, Địa chỉ, Trạng thái, Thao tác | `GetByRole(Row, new{Name=<tên KH>})` |
| Tên khách hàng | required, max 200 (`PARTNER_LIMITS.name`) | `GetByLabel("Tên khách hàng")` |
| Số điện thoại (KH) | `partnerRules.phone`, maxLength 12 | `GetByLabel("Số điện thoại")` |
| Email (KH) | `partnerRules.email`, max 100, regex email | `GetByLabel("Email")` |
| Địa chỉ (KH) | required, max 500 | `GetByLabel("Địa chỉ")` |
| Ngừng hợp tác / Hợp tác lại | IconAction; ngừng mở modal Lý do (không bắt buộc, max `REASON_MAX_LENGTH`) | `row.GetByRole(Button, "Ngừng hợp tác")`, `"Hợp tác lại"`, `GetByLabel("Lý do")` |
| Bảng Mã hàng | Mã hàng, Ảnh, Trạng thái, Đã dùng, Thao tác | `GetByRole(Row, new{Name=<mã hàng>})` |
| Mã hàng (form) | required; zod: trim, 1–30 ký tự, chỉ chữ không dấu/số và `. _ -`; tự viết hoa; kiểm tồn tại (`productCodeExists`); khóa khi mã đã dùng (tooltip `codeLockedHint`) | `GetByLabel("Mã hàng")` |
| Mô tả (form) | zod `productDescriptionSchema` | `GetByLabel("Mô tả")` (xác nhận `MESSAGES.orders.description`) |
| Ảnh mã hàng | upload `image/png,jpeg,webp`, tối đa `MAX_EXTRA_IMAGES` ảnh phụ | `Locator("input[type=file]")` |
| Ngừng / Dùng lại / Định mức | IconAction (Ngừng có Popconfirm) | `row.GetByRole(Button, "Ngừng")`, `"Dùng lại"`, nút Định mức theo `MESSAGES.orders.spec.button` |
| Chọn mã hàng (Công đoạn) | Select "Chọn mã hàng" | `GetByRole(Combobox)` / `GetByText("Chọn mã hàng")` |
| Thêm công đoạn / Nhóm công đoạn / Nhập từ Excel | Button (disable khi chưa chọn mã hàng hoặc là Admin) | `GetByRole(Button, "Thêm công đoạn")`, `"Nhập từ Excel"`; nút nhóm chỉ có icon |
| Mã công đoạn | required+whitespace, max 30, pattern `^[A-Za-z0-9._-]+$` ("Mã chỉ gồm chữ không dấu, số và các ký tự . _ -") | `GetByLabel("Mã công đoạn")` |
| Tên công đoạn | required+whitespace, max 200 | `GetByLabel("Tên công đoạn")` |
| Thứ tự | required, số 1–32767 | `GetByLabel("Thứ tự")` |
| Bậc (1-9) | số 1–9 | `GetByLabel("Bậc (1-9)")` |
| TG chuẩn (giây) | số 0.01–9999999.99 | `GetByLabel("TG chuẩn (giây)")` |
| Là công đoạn cuối | Checkbox (công đoạn cuối cũ chuyển sang) | `GetByLabel("Là công đoạn cuối (tính hàng hoàn thiện)")` |
| Nhóm công đoạn | Select/gõ tên mới, max 200 | `GetByLabel("Nhóm công đoạn")` |
| Chi tiết vải công đoạn làm trên | chọn chi tiết Panel theo định mức | `GetByLabel("Chi tiết vải công đoạn làm trên")` |
| Đơn giá (đồng/sản phẩm) | required, 0.01 ≤ giá ≤ 100.000.000 ("Đơn giá phải lớn hơn 0."/"Đơn giá không được vượt quá 100.000.000 đồng.") | `GetByLabel("Đơn giá (đồng/sản phẩm)")` |
| Ngày hiệu lực | required; ngày quá khứ có `pastDateHint` | `GetByLabel("Ngày hiệu lực")` |
| Nhóm: thêm nhóm | input "Tên nhóm mới, vd May tay", max 200, nút "Thêm nhóm" | `GetByPlaceholder("Tên nhóm mới, vd May tay")`, `GetByRole(Button, "Thêm nhóm")` |
| Nhập công đoạn từ Excel | Upload `.xlsx`, bảng xem trước sửa được, nút Thêm dòng | `Locator("input[type=file]")`, `GetByRole(Button, "Thêm dòng")` |
| Duyệt / Từ chối đơn giá (Admin) | Modal Ghi chú (không bắt buộc cho duyệt; từ chối xem `rejectTitle`); duyệt nhiều | `row.GetByRole(Button, "Duyệt")`, `GetByLabel("Ghi chú")` |
| Trạng thái đơn giá | Nháp / Chờ duyệt / Đã duyệt / Bị từ chối | `GetByText("Chờ duyệt")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách / thêm / sửa KH | `GET/POST /api/v1/admin/customers`; `PUT .../customers/{id}` | `{customerName, phone?, email?, address}` | Toast "Đã lưu."; reload bảng |
| Ngừng / Hợp tác lại KH | `POST .../customers/{id}/discontinue` `{reason?}`; `.../reactivate` | — | Trạng thái đổi |
| Danh sách / thêm / sửa mã hàng | `GET/POST /api/v1/{admin\|accountant}/products`; `PUT /api/v1/admin/products/{id}` | `{productCode (UPPER), description, ảnh}` | Toast "Đã lưu."; mã trùng → thông báo `productCodeExists` |
| Ảnh mẫu | `.../products/sample-image` | multipart | Hiện ảnh |
| Ngừng / Dùng lại mã hàng | `POST /api/v1/admin/products/{id}/discontinue`, `.../reactivate` | — | Trạng thái đổi |
| Định mức (xem/sửa) | `GET/PUT /api/v1/{scope}/products/{id}/spec` | spec (màu/size, chi tiết, NVL, bìa) | Toast lưu; lỗi hiển thị theo `specValidation` |
| Nhập Excel mã hàng/định mức | `POST /api/v1/admin/products/excel/parse`, `.../excel/check`, `.../products/{id}/import/preview`, `.../import`, `.../import/preview-data`, `.../import-data` | multipart / dữ liệu đã sửa | Bảng xem trước + tóm tắt; nhập xong reload |
| Danh sách công đoạn | `GET /api/v1/{accountant\|admin}/operations` | `productId, group, search` | Bảng công đoạn |
| Thêm / sửa công đoạn | `POST /api/v1/{role}/operations`; `PUT .../operations/{id}` | `{operationCode, operationName, sequence, skillLevel?, standardTimeSec?, groupName?, isFinal, componentIds, rate, effectiveDate}` | Toast "Đã lưu." |
| Ngừng / Dùng lại công đoạn | `POST .../operations/{id}/discontinue`, `.../reactivate` | — | Trạng thái đổi |
| Nhóm công đoạn | `GET/POST /api/v1/{role}/operation-groups`; `PUT/DELETE .../operation-groups/{id}`; `.../operation-groups/operations` | `{groupName}` | Cập nhật bảng nhóm |
| Nhập công đoạn Excel | `POST .../operations/import/preview`, `.../operations/import`, `.../import/preview-data`, `.../import-data` | multipart | Bảng xem trước, nhập xong reload |
| Đơn giá | `GET/POST /api/v1/{role}/piece-rates`; `PUT /api/v1/accountant/piece-rates/{id}` | `{operationId, rate, effectiveDate}` | Toast "Đã lưu." |
| Gửi duyệt | `POST /api/v1/accountant/piece-rates/{id}/submit`, `.../submit-many` | `{ids}` | Toast "Đã gửi duyệt." |
| Duyệt / Từ chối (Admin) | `POST /api/v1/admin/piece-rates/{id}/approve`, `.../approve-many`, `.../{id}/reject` | `{note?}` | Toast "Đã duyệt đơn giá." / "Đã từ chối đơn giá." / "Đã duyệt N đơn giá." |

## 4. Điểm nghi vấn lệch pha

- Khách hàng: Số điện thoại/Email không bắt buộc ở FE nhưng Địa chỉ bắt buộc; FE không kiểm trùng tên khách hàng (chỉ biết khi API lỗi).
- Mã hàng: FE tự viết hoa và kiểm tồn tại trước khi lưu (`productCodeExists`) — nếu API kiểm tra thất bại, FE chưa thấy fallback rõ ràng.
- Mô tả mã hàng chỉ kiểm qua schema zod riêng, chưa rõ giới hạn hiển thị cho người dùng.
- Công đoạn: `standardTimeSec` min 0.01 ở FE nhưng trường không bắt buộc → ô trống bằng "không giới hạn" nên có thể lưu NULL.
- "Công đoạn cuối" chuyển từ công đoạn khác sang (`isFinalMoves`): FE chỉ thông báo, không xác nhận trước khi chuyển.
- Admin vào `/operations` chỉ xem: các nút `disabled` thay vì ẩn → test cần assert disabled, không phải not-visible.
- Đơn giá: FE cho nhập ngày hiệu lực quá khứ (chỉ cảnh báo `pastDateHint`); không chặn trước ngày hiện hành.
- Duyệt nhiều (`approve-many`) hiển thị toast tự ghép chuỗi "Đã duyệt N đơn giá." ngay ở FE (hardcode), không dùng thông điệp từ BE.
- Nhập Excel mã hàng/định mức/công đoạn: chỉ kiểm phần mở rộng `.xlsx` ở FE; không kiểm kích thước.
- Nút Nhóm công đoạn, Định mức chỉ có icon → nên thêm `data-testid` để dùng Semantic Locator.
