# ST_CONTRACT — Module 6: Kho NVL — Nhập, Thiếu giao & Xuất

> Nguồn: `fe/src/features/warehouse/{routes,constants,pages,components,api}`, `features/material-return/*`, `constants/apiEndpoints.ts`. Không đọc Backend. `{role}` = `warehouse-keeper` | `admin` | `manager` | `team-leader` theo `RolePath`.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/warehouse/materials` | MaterialListPage (danh mục NVL) | WarehouseKeeper, Admin, Manager | Thêm/Sửa NVL (modal), đổi trạng thái Đang dùng/Ngừng dùng |
| `/warehouse/order-materials` | OrderMaterialLinkRedirect | Admin, Manager | Chuyển tới `/admin/orders/:id?tab=<NVL>` hoặc `/manager/orders/:id?tab=...` (không có `orderId` → danh sách đơn) |
| `/warehouse/incoming-materials` | IncomingMaterialsPage (NVL sắp về: theo đơn khách, mua hàng, thiếu giao) | WarehouseKeeper | Từ đây tạo phiếu nhập; liên kết sang `/warehouse/short-deliveries` |
| `/warehouse/material-receipts` | MaterialReceiptListPage + ReceiptDetailModal | WarehouseKeeper, Admin, Manager | Lập phiếu nhập → Xác nhận / Trả lại / Sửa; xuất Excel phiếu nhập |
| `/warehouse/short-deliveries` | ShortDeliveryListPage + ShortDeliveryModal | WarehouseKeeper, Admin, Manager | Xử lý thiếu (Giao bù / Không bù); Nhập phiếu bổ sung (ReplenishReceiveModal) |
| `/warehouse/material-issues` | MaterialIssueListPage + IssueCreateModal/IssueDetailModal | WarehouseKeeper, TeamLeader, TeamDeputy | Kho lập phiếu cấp → Tổ xác nhận nhận / từ chối |
| `/warehouse/material-returns` | MaterialReturnPage | TeamLeader, TeamDeputy (lập phiếu trả), WarehouseKeeper (nhận) | Tổ lập phiếu trả → Kho xác nhận / từ chối |

- Role sai → redirect `/`. Tổ trưởng/phó bị lọc thêm theo loại tổ.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Tìm / Lọc loại NVL | placeholder "Tìm theo mã, tên..."; Select Loại | `GetByPlaceholder("Tìm theo mã, tên...")` |
| Thêm NVL | Button primary | `GetByRole(Button, "Thêm NVL mới")` hoặc nút Thêm của trang |
| Mã NVL | max `MATERIAL_CODE_MAX_LENGTH`, theo hint `materialCodeHint` | `GetByLabel("Mã NVL")` |
| Tên NVL | required+whitespace, max `MATERIAL_NAME_MAX_LENGTH` | `GetByLabel("Tên NVL")` |
| Loại | Select required (loại Vải bật Khổ vải, khóa Đơn vị) | `GetByLabel("Loại")` |
| Đơn vị tính | required+whitespace, max 20; khóa khi là vải | `GetByLabel("Đơn vị tính")` |
| Khổ vải (m) | required với vải, `WIDTH_MIN_M`–`WIDTH_MAX_M` (0,01–3,5) | `GetByLabel("Khổ vải (m)")` |
| Màu NVL | max 50 | `GetByLabel("Màu NVL")` |
| Phiếu nhập: Nguồn nhập | Select required (khóa khi sửa) | `GetByLabel("Nguồn nhập")` |
| Đơn mua / Đơn hàng | Select required theo nguồn; placeholder chọn đơn mua | `GetByLabel("Đơn mua")`, `GetByLabel("Đơn hàng")` |
| Ngày nhập | DatePicker required | `GetByLabel("Ngày nhập")` |
| Dòng phiếu nhập | Dòng đơn mua / NVL (required), Số đạt (required, 0–`MAX_MATERIAL_QTY`); nút xóa dòng disable khi còn 1 dòng; nút "Thêm NVL còn phải nhận chưa có trong phiếu" | `GetByRole(Button, "Thêm NVL còn phải nhận chưa có trong phiếu")` |
| Lưu phiếu nhập | Button Lưu | `GetByRole(Button, "Lưu")` |
| Xác nhận phiếu nhập | Button primary; disable nếu `missingPhoto` (thiếu ảnh đính kèm) | `GetByRole(Button, "Xác nhận")` |
| Trả lại phiếu | Button danger → modal lý do | `GetByRole(Button, "Trả lại")` |
| Xuất Excel phiếu | Button tải `PhieuNhap_<mã>.xlsx` | `GetByRole(Button, "In / xuất Excel")` |
| Thiếu giao: Xử lý | Modal: chọn Giao bù (Số lần giao bù dự kiến, Ngày dự kiến giao bù — required "Vui lòng chọn ngày dự kiến giao bù.") hoặc Không bù; nút disable khi `hasOver` | `GetByRole(Button, "Xử lý thiếu")`, `GetByLabel("Số lần giao bù dự kiến")`, `GetByLabel("Ngày dự kiến giao bù")` |
| Phiếu bổ sung (giao bù) | Ngày nhập required; Ghi chú max 1000 | `GetByLabel("Ngày nhập")`, `GetByLabel("Ghi chú")` |
| Tạo đơn mua (Kho) | Nhà cung cấp, Ngày đặt hàng ("Vui lòng chọn ngày đặt hàng."), Số lần NCC dự kiến giao (1–99), Đơn giá > 0 ("Nhập đơn giá lớn hơn 0.") | `GetByLabel("Nhà cung cấp")`, `GetByLabel("Ngày đặt hàng")`, `GetByLabel("Số lần nhà cung cấp dự kiến giao")` |
| Lập phiếu cấp | Chọn Kho (NVL/BTP), Đơn hàng, Lệnh cắt, Tổ nhận, Ngày cấp; ít nhất một dòng ("issueNeedLine"/"issueNeedMaterialLine") | `GetByRole(Button, "Lập phiếu cấp")` (xem `LABELS`), `GetByLabel("Kho")`, `GetByLabel("Đơn hàng")` |
| Xác nhận nhận / Từ chối phiếu cấp | Tổ: Button primary "Xác nhận"; "Từ chối" (danger) kèm lý do | `GetByRole(Button, "Xác nhận")`, `GetByRole(Button, "Từ chối")` |
| Lập phiếu trả | Loại hàng trả (NVL / phế phẩm, BTP); Lệnh cắt hoặc Đơn hàng; Thêm phế phẩm; nhập SL trả ≥ 1 dòng ("Nhập số lượng trả ít nhất một dòng."); Ghi chú max 500 | `GetByRole(Button, "Lập phiếu trả")`, `GetByLabel("Loại hàng trả")`, `GetByRole(Button, "Thêm phế phẩm")` |
| Xác nhận / Từ chối phiếu trả (Kho) | Button trong modal chi tiết | `GetByRole(Button, "Xác nhận")`, `"Từ chối"` |
| Trạng thái | Phiếu nhập: Chờ xác nhận/Đã xác nhận/Bị trả lại; Cấp: Chờ tổ nhận/Đã nhận/Bị từ chối; Trả: Chờ kho nhận/Kho đã nhận/Bị từ chối/Đã rút | `GetByText("Chờ xác nhận")` ... |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách / thêm / sửa NVL | `GET/POST /api/v1/{role}/materials`; `PUT .../materials/{id}` | `{materialCode, materialName, materialType, unit, widthM?, color?}` | Toast lưu; reload |
| Đổi trạng thái NVL | `PATCH .../materials/{id}/status` | `{status}` | Trạng thái đổi |
| NVL của đơn | `GET /api/v1/{role}/order-materials` (+ `/paged`, `/product-materials`); `POST`, `/batch`, `PUT /{id}`, `DELETE /{id}`; `.../import/preview`, `.../import`; `.../customer-delivery/{orderId}` | `OrderMaterial` | Tab NVL trong chi tiết đơn |
| NVL sắp về | `GET /api/v1/warehouse-keeper/incoming-materials` | — | 3 bảng (khách / mua / thiếu) |
| Đơn mua (Kho) | `GET/POST /api/v1/{role}/purchase-orders` (+ `/{id}`); `GET /api/v1/manager/suppliers` | `PurchaseOrderFormPayload` | Tạo đơn mua mới (đây là luồng "Tạo đơn mua" của Module 5) |
| Phiếu nhập | `GET /api/v1/{role}/material-receipts` (+ `/{id}`); `POST`; `PUT /{id}`; `GET /{id}/excel` | `{source, purchaseOrderId?, orderId?, receiptDate, lines[{purchaseOrderLineId, orderMaterialId, acceptedQty}]}` | Toast lưu; xuất file `PhieuNhap_<mã>.xlsx` |
| Xác nhận / trả lại phiếu | `POST .../material-receipts/{id}/confirm`; `POST .../{id}/return` `{reason}` | — | Trạng thái "Đã xác nhận" / "Bị trả lại" |
| Phiếu bổ sung | `POST .../material-receipts/supplementary` | `{shortDeliveryId, receiptDate, lines, note?}` | Tạo phiếu giao bù |
| Thiếu giao | `GET .../short-deliveries` (+ `/{id}`); `POST .../short-deliveries/{id}/resolve` | `{action, expected…}` | Trạng thái "Hoàn thành" / "Đã hủy" |
| Phiếu cấp | `GET/POST .../material-issues` (+ `/{id}`, `/{id}/excel`); `POST .../{id}/confirm`; `POST .../{id}/reject` `{reason}` | `IssuePayload` | "Đã nhận" / "Bị từ chối" |
| Gợi ý vải / lệnh cắt | `GET /api/v1/{role}/work-orders` (+ `/{id}`, `/{id}/fabric-needs`) | — | FabricSuggestionBar |
| Phiếu trả | `GET /api/v1/{team-leader\|warehouse-keeper}/material-returns` (+ `/{id}`); `GET .../team-leader/material-returns/returnable`; `POST` tạo; confirm/reject/withdraw theo `materialReturnApi` | `{kind, workOrderId?, orderId?, lines, scrap, note?}` | Trạng thái "Kho đã nhận" / "Bị từ chối" / "Đã rút" |

## 4. Điểm nghi vấn lệch pha

- `/warehouse/order-materials` chỉ là redirect sang chi tiết đơn; menu Kho ở FE không có màn NVL của đơn riêng — test cần kiểm điều hướng chứ không phải màn hình.
- Nút Xác nhận phiếu nhập bị `disabled` khi `missingPhoto`; FE không hiện lý do rõ ràng ngoài trạng thái disable (kiểm ảnh phụ thuộc BE/Attachment).
- Khổ vải bắt buộc 0,01–3,5 m chỉ khi chọn loại Vải; đổi loại sau khi nhập có thể để lại giá trị cũ ở trường bị ẩn.
- Số đạt `acceptedQty` chỉ chặn 0–`MAX_MATERIAL_QTY` ở FE, không chặn vượt số còn phải nhận (do BE trả `UQ`/lỗi); `hasOver` ở ShortDeliveryModal chỉ disable nút, không hiện thông báo.
- Phiếu cấp: thông báo thiếu dòng dùng `message.warning` (không phải lỗi form) → `AssertToastAsync` phải tìm đúng text `issueNeedLine`.
- Ngày nhập/Ngày cấp không chặn ngày tương lai.
- Tạo đơn mua có ở Kho (`/warehouse/purchase-orders`) nhưng `PurchaseOrderFormModal` không thuộc route nào trong `routes.tsx`; cần xác định màn hình mở modal này (có thể từ IncomingMaterialsPage).
- Phiếu trả: chỉ TL/TD thấy nút "Lập phiếu trả" nhờ `<Can>`, còn trang mở được cho cả Kho: FE ẩn nút nhưng route vẫn truy cập được bằng URL.
- Trạng thái dùng khóa chữ hoa (`PENDING`, `CONFIRMED`) trong khi cả hai tập (Phiếu cấp / Phiếu trả) có nhãn "Bị từ chối" giống nhau → dễ nhầm khi `GetByText`.
- Nhiều nút (xóa dòng, xem chi tiết) chỉ có icon → nên có `data-testid`.
