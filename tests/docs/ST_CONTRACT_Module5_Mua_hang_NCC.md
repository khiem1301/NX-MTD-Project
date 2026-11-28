# ST_CONTRACT — Module 5: Mua hàng & Nhà cung cấp

> Nguồn: `fe/src/features/purchase/{routes,constants,pages,components,api,hooks}`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/suppliers` | SuppliersPage ("Nhà cung cấp") | Admin | Thêm/Sửa NCC (modal), Ngừng hợp tác / Hợp tác lại |
| `/purchase-orders` | PurchaseOrdersPage ("Đơn mua NVL") + PurchaseOrderModal | Admin, Manager | Mở đơn → Chọn NCC và đặt hàng / Hủy đơn mua / Sửa đơn giá và gửi lại |
| `/purchase-payments` | PurchasePaymentsPage ("Thanh toán đơn mua") | Accountant | Mở đơn đã đặt → Xác nhận thanh toán / Trả lại Quản đốc / Sửa lần thanh toán |

- Role sai → redirect `/`. Quản đốc chỉ chọn được NCC đang hợp tác khi đặt hàng (nếu không có: "Chưa có nhà cung cấp nào đang hợp tác. Nhờ Admin thêm trước.").

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tìm NCC | "Tìm theo tên, số điện thoại" | `GetByPlaceholder("Tìm theo tên, số điện thoại")` |
| Tìm đơn mua | "Tìm theo mã đơn mua, mã đơn hàng, nhà cung cấp" | `GetByPlaceholder("Tìm theo mã đơn mua, mã đơn hàng, nhà cung cấp")` |
| Lọc trạng thái | Select: "Tất cả trạng thái", "Mọi trạng thái đặt hàng", "Mọi trạng thái thanh toán" | `GetByRole(Combobox)` theo placeholder |
| Thêm nhà cung cấp | Button | `GetByRole(Button, new{Name="Thêm nhà cung cấp"})` |
| Tên nhà cung cấp | required ("Vui lòng nhập tên nhà cung cấp."), max `PARTNER_LIMITS.name` | `GetByLabel("Tên nhà cung cấp")` |
| Số điện thoại | `partnerRules.phone` | `GetByLabel("Số điện thoại")` |
| Email | regex ("Email không hợp lệ."), max 100 | `GetByLabel("Email")` |
| Địa chỉ / Loại NVL cung cấp / Ghi chú | max theo `PARTNER_LIMITS` | `GetByLabel("Địa chỉ")`, `GetByLabel("Loại NVL cung cấp")`, `GetByLabel("Ghi chú")` |
| Ngừng hợp tác / Hợp tác lại | modal Lý do ngừng hợp tác (không bắt buộc) | `row.GetByRole(Button, "Ngừng hợp tác")`, `"Hợp tác lại"` |
| Bảng NCC | Tên, SĐT, Email, Địa chỉ, Trạng thái, Số đơn mua | `GetByRole(Row, new{Name=<tên NCC>})` |
| Bảng đơn mua | Mã đơn mua, Đơn hàng, Mua dự trữ, Nhà cung cấp, Đặt hàng, Thanh toán, Ngày đặt, Giá trị đơn, Đã thanh toán | `GetByRole(Row, new{Name=<mã PO>})` |
| Chọn NCC và đặt hàng | Nhà cung cấp required ("Vui lòng chọn nhà cung cấp."); Ngày đặt hàng required; Số lần NCC dự kiến giao required, số 1–99; Ngày NCC dự kiến giao required; Ghi chú max 1000 | `GetByRole(Button, "Chọn NCC và đặt hàng")`, `GetByLabel("Nhà cung cấp")`, `GetByLabel("Ngày đặt hàng")`, `GetByLabel("Số lần nhà cung cấp dự kiến giao")`, `GetByLabel("Ngày nhà cung cấp dự kiến giao")` |
| Dòng đơn mua (đặt hàng) | Số lượng > 0 ("Nhập số lượng lớn hơn 0."), Đơn giá > 0 ("Nhập đơn giá lớn hơn 0."); sau đặt hàng bị khóa | `GetByRole(Spinbutton)` trong dòng |
| Hủy đơn mua | modal Lý do hủy đơn | `GetByRole(Button, "Hủy đơn mua")` |
| Sửa đơn giá và gửi lại | chỉ sửa Đơn giá (≥ 0.01, ₫) + Ghi chú max 1000; hiện "Lý do Kế toán trả lại" | `GetByRole(Button, "Sửa đơn giá và gửi lại")` |
| Xác nhận thanh toán | Ngày thanh toán required; Hình thức required; Số tiền thanh toán required ≥ 0.01 và ≤ còn phải thanh toán; Tổng giá trị thực tế (khi đóng thiếu) required ≤ tổng đơn; Ghi chú thanh toán max 500; Ảnh chứng từ tối đa 5 | `GetByRole(Button, "Xác nhận thanh toán")`, `GetByLabel("Ngày thanh toán")`, `GetByLabel("Hình thức")`, `GetByLabel("Số tiền thanh toán")`, `GetByLabel("Tổng giá trị thực tế")` |
| Trả lại Quản đốc | modal Lý do trả lại | `GetByRole(Button, "Trả lại Quản đốc")`, `GetByLabel("Lý do trả lại")` |
| Sửa lần thanh toán | Số tiền ≤ tối đa cho lần này; Lý do sửa required + whitespace, max `REASON_MAX_LENGTH` | `GetByRole(Button, "Sửa lần thanh toán")`, `GetByLabel("Lý do sửa")` |
| Nhập giá trị thực tế | Giá trị thực tế required, ≥ đã thanh toán; Lý do required | `GetByLabel("Giá trị thực tế")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách / chi tiết NCC | `GET /api/v1/admin/suppliers`; `GET .../suppliers/{id}` | `page,pageSize,keyword,status` | Bảng / modal |
| Thêm / sửa NCC | `POST /api/v1/admin/suppliers`; `PUT .../suppliers/{id}` | `SaveSupplierPayload` | Toast lưu; reload |
| Ngừng / Hợp tác lại NCC | `POST .../suppliers/{id}/discontinue` `{reason?}`; `.../reactivate` | — | Trạng thái đổi |
| Danh sách NCC đang hợp tác | `GET /api/v1/manager/suppliers` | — | Select NCC khi đặt hàng |
| Danh sách / chi tiết đơn mua | `GET /api/v1/manager/purchase-order-tracking` (+ `/{id}`); Admin/Manager/Accountant dùng `detailUrl(role,id)` | filter | Bảng + modal chi tiết |
| Đặt hàng | `POST /api/v1/manager/purchase-order-tracking/{id}/place` | `PlacePayload` (supplierId, orderDate, expected…, lines giá/SL, note) | Đơn chuyển "Đã đặt hàng"; số lượng và NCC bị khóa |
| Hủy đơn mua | `POST .../purchase-order-tracking/{id}/cancel` | `{reason}` | Trạng thái "Đã hủy" |
| Sửa giá gửi lại | `POST .../purchase-order-tracking/{id}/reprice` | `RepricePayload` (giá từng dòng, note) | Đơn quay lại Kế toán |
| Danh sách thanh toán | `GET /api/v1/accountant/purchase-payments` (+ `/{id}`) | filter | Bảng + modal |
| Xác nhận thanh toán | `POST /api/v1/accountant/purchase-payments/{id}/confirm` | `{paymentDate, amount, method, paymentNote?, actualAmount?, proofs[]}` | Trạng thái "Đã thanh toán" hoặc "Thanh toán một phần" (theo `willBePaid`/`willBePartial`) |
| Sửa lần thanh toán | `PUT /api/v1/accountant/purchase-payments/{id}/payments/{paymentId}` | `{paymentDate, amount, method, paymentNote, editReason}` | Toast lưu; hiển thị "Đã sửa" |
| Trả lại Quản đốc | `POST /api/v1/accountant/purchase-payments/{id}/return` | `{reason}` | Đơn rời hàng chờ Kế toán |

## 4. Điểm nghi vấn lệch pha

- Màn Đơn mua có `createOrder: 'Tạo đơn mua'` và `purchaseOrders(role)` trong `apiEndpoints`, nhưng route `/purchase-orders` chỉ có đặt hàng/hủy/sửa giá; chưa thấy nút tạo đơn mua → cần xác nhận luồng tạo đơn ở module Kho (Module 6).
- Hai Role cùng vào `/purchase-orders` (Admin, Manager) nhưng nút Đặt hàng/Hủy/Sửa giá do `PurchaseOrderModal` hiện theo trạng thái; FE chưa chặn rõ Admin chỉ xem hay vẫn thao tác.
- Khoản "đã sửa" (`editedNote`) và cửa sổ chỉnh sửa thanh toán (24 giờ ở BE) không được FE kiểm; nút Sửa lần thanh toán vẫn hiện, lỗi chỉ có sau khi gọi API.
- Số tiền thanh toán chỉ được kiểm bằng `validator` FE theo `payableAmount` đã tải; nếu dữ liệu thay đổi (thanh toán đồng thời) kết quả chỉ biết qua lỗi API.
- `Số lần NCC dự kiến giao` giới hạn 1–99 ở FE; không rõ BE.
- Ảnh chứng từ "không bắt buộc, tối đa 5 ảnh": FE giới hạn số lượng nhưng chưa thấy kiểm dung lượng/định dạng.
- Ngày thanh toán / Ngày đặt hàng không chặn ngày tương lai/quá khứ.
- Đơn giá nhập bằng `InputNumber precision 0` nhưng rule `min: 0.01`: giá trị 0 < giá < 1 bị làm tròn về 0 trước khi kiểm, hành vi không rõ.
- Nhiều nút Ngừng/Hợp tác lại là IconAction (icon) → nên có `data-testid`.
