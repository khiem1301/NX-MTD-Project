# ST_CONTRACT — Module 10: Kho TP — Xuất hàng, Tồn kho & Kiểm kê

> Nguồn: `fe/src/features/dispatch/*`, `features/stocktake/*`, `features/warehouse/{pages/InventoryPage,components/BalanceTable,NxtReport*}`, `constants/apiEndpoints.ts`. Không đọc Backend. `{role}` theo `RolePath`.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/warehouse/dispatch-notes` | DispatchNotesPage ("Phiếu xuất hàng") + DispatchModal | Admin, Manager, WarehouseKeeper, Accountant | Quản đốc lập phiếu → Thủ kho Xuất hàng / Từ chối → Kế toán Xác nhận khách đã nhận; Quản đốc Hủy phiếu |
| `/warehouse/stocktakes` | StocktakePage ("Kiểm kê kho") + StocktakeModal | WarehouseKeeper, Manager, Admin | Thủ kho lập phiếu và gửi → Quản đốc/Admin Duyệt và điều chỉnh kho / Trả lại → Thủ kho Sửa và gửi lại |
| `/warehouse/inventories` | InventoryPage ("Tồn kho": tab theo loại kho, tab "tổ đang giữ", tab báo cáo NXT) | WarehouseKeeper, Manager, Admin | Xem tồn, thẻ kho, báo cáo nhập-xuất-tồn |

- Role sai → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Lập phiếu xuất | Button primary (Quản đốc) | `GetByRole(Button, "Lập phiếu xuất")` |
| Chọn đơn hàng | Select required, placeholder "Chọn đơn hàng" | `GetByText("Chọn đơn hàng")` |
| Ngày giao | DatePicker required | `GetByLabel("Ngày giao")` |
| Địa chỉ giao hàng | required+whitespace, max 500 | `GetByLabel("Địa chỉ giao hàng")` |
| Ghi chú | max 1000 | `GetByLabel("Ghi chú")` |
| Số xuất mỗi dòng | số 0..`maxQtyRule(limit)` (≤ `DISPATCH_MAX_QTY`); disable khi không còn xuất được; hiện Đặt / Đã xuất / Còn xuất được / Tồn khả dụng | `GetByRole(Spinbutton)` theo dòng |
| Xác nhận (lập phiếu) | Button OK của modal; toast "Đã lập phiếu xuất hàng, thành phẩm đã được giữ chỗ." | `GetByRole(Button, "Xác nhận")` |
| Hủy phiếu | Popconfirm "Hủy phiếu xuất hàng này? Số giữ chỗ sẽ được nhả…" | `GetByRole(Button, "Hủy phiếu")` |
| Xác nhận xuất hàng (Thủ kho) | Button primary; disable khi chưa có ảnh phiếu xuất ("Cần ảnh phiếu xuất có Quản đốc ký trước khi xuất") | `GetByRole(Button, "Xác nhận xuất hàng")` |
| Cách giao | Select | `GetByLabel("Cách giao")` |
| Xe giao hàng | Select required "Vui lòng chọn xe giao hàng." (placeholder "Chọn xe đang rảnh"; không có xe: `noVehicle`) | `GetByLabel("Xe giao hàng")` |
| Tài xế | required+whitespace ("Vui lòng nhập tên tài xế."), max `DRIVER_NAME_MAX` | `GetByLabel("Tài xế")` |
| Km lúc đi | số nguyên 0..`MAX_ODOMETER`, required ("Nhập km lúc đi (từ 0 trở lên).") | `GetByLabel("Km lúc đi")` |
| Từ chối phiếu (Thủ kho) | modal "Từ chối phiếu xuất hàng" + "Lý do từ chối" | `GetByRole(Button, "Từ chối")`, `GetByLabel("Lý do từ chối")` |
| Ảnh phiếu xuất / biên bản giao | Upload (ảnh, "bắt buộc") | `Locator("input[type=file]")` |
| Xác nhận khách đã nhận (Kế toán) | "Ngày khách nhận" required; disable khi thiếu ảnh biên bản giao | `GetByRole(Button, "Xác nhận khách đã nhận")`, `GetByLabel("Ngày khách nhận")` |
| Lập phiếu kiểm kê | Button "Lập phiếu kiểm kê" | `GetByRole(Button, "Lập phiếu kiểm kê")` |
| Kho / Ngày kiểm kê | Select required / DatePicker required | `GetByLabel("Kho")`, `GetByLabel("Ngày kiểm kê")` |
| Phạm vi | Input max 300, placeholder "VD: kệ A, vải đơn DH-01" | `GetByPlaceholder("VD: kệ A, vải đơn DH-01")` |
| Lọc theo đơn hàng / tìm | Select / search | `GetByPlaceholder("Lọc theo đơn hàng")` |
| Số đếm / Ghi chú dòng | số 0..`MAX_MATERIAL_QTY` ("Để trống số đếm nếu khớp sổ."); ghi chú max 300; phải có ≥ 1 dòng, tối đa 1.000 dòng | `GetByRole(Spinbutton)` theo dòng |
| Lưu và gửi | toast "Đã gửi phiếu kiểm kê cho Quản đốc / Admin duyệt." | `GetByRole(Button, "Lưu")` |
| Duyệt và điều chỉnh kho | Button primary + Modal.confirm `confirmApprove`; toast "Đã duyệt phiếu kiểm kê." | `GetByRole(Button, "Duyệt và điều chỉnh kho")` |
| Trả lại phiếu | modal "Trả lại phiếu kiểm kê" + "Lý do trả lại" | `GetByRole(Button, "Trả lại")`, `GetByLabel("Lý do trả lại")` |
| Sửa và gửi lại | Button (Thủ kho, phiếu bị trả) | `GetByRole(Button, "Sửa và gửi lại")` |
| Tồn kho | Tabs theo loại kho; BalanceTable (search + "Lọc theo đơn hàng"); thẻ kho; báo cáo NXT | `GetByRole(Tab, new{Name=<loại kho>})` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách / chi tiết phiếu xuất | `GET /api/v1/{manager\|warehouse-keeper\|accountant}/dispatch-notes` (+ `/{id}`); `GET .../manager/dispatch-notes/dispatchable?orderId`; `GET .../{id}/excel` | `DispatchNoteQuery` | Bảng + modal; xuất Excel |
| Lập phiếu xuất | `POST /api/v1/manager/dispatch-notes` | `{orderId, deliveryDate, deliveryAddress, note?, lines[{orderLineId, qty}]}` | Toast "Đã lập phiếu xuất hàng, thành phẩm đã được giữ chỗ." |
| Hủy phiếu | `POST /api/v1/manager/dispatch-notes/{id}/cancel` | `{rowVer}` | Trạng thái CANCELLED |
| Xe rảnh | `GET /api/v1/warehouse-keeper/vehicles/available` | — | Select "Xe giao hàng" |
| Xuất hàng | `POST /api/v1/warehouse-keeper/dispatch-notes/{id}/export` | `{deliveryMethod, vehicleId?, driver?, odometerStart?, rowVer}` | Toast thông điệp từ BE; trạng thái chuyển sang đã xuất |
| Từ chối phiếu | `POST /api/v1/warehouse-keeper/dispatch-notes/{id}/reject` | `{reason, rowVer}` | Trạng thái REJECTED |
| Xác nhận khách nhận | `POST /api/v1/accountant/dispatch-notes/{id}/confirm-delivery` | `{customerReceivedDate, …}` | Trạng thái DELIVERED |
| Danh sách / chi tiết kiểm kê | `GET /api/v1/{role}/stocktakes` (+ `/{id}`) | `StocktakeQuery` | Bảng + modal |
| Tạo / sửa kiểm kê | `POST /api/v1/{role}/stocktakes`; `PUT .../stocktakes/{id}` | `{warehouseType, stocktakeDate, scope?, lines[{…, countedQty, note}]}` | Toast "Đã gửi phiếu kiểm kê cho Quản đốc / Admin duyệt." |
| Duyệt / trả lại | `POST .../stocktakes/{id}/approve`; `.../{id}/return` `{reason}` | — | "Đã duyệt phiếu kiểm kê." / "Đã trả phiếu kiểm kê cho Thủ kho." |
| Tồn kho / thẻ kho / NXT / tổ giữ | `GET /api/v1/{role}/inventories` (+ `/stock-card`, `/nxt-report`, `/team-holdings`) | filter (đơn hàng, ngày) | Bảng tồn / báo cáo |

## 4. Điểm nghi vấn lệch pha

- Bốn Role cùng vào `/warehouse/dispatch-notes` nhưng nút theo vai trò/trạng thái (`rowVer` gửi kèm); FE không giải thích rõ khi Role không có quyền (nút chỉ ẩn).
- Nút "Xác nhận xuất hàng"/"Xác nhận khách đã nhận" bị `disabled` khi thiếu ảnh (tooltip/hint `needIssuePhoto`/`needDeliveryPhoto`): test phải assert disabled + text hint.
- Số xuất: `maxQtyRule(limit)` dựa vào "còn xuất được"/"tồn khả dụng" tải trước; thay đổi đồng thời chỉ biết qua lỗi API.
- Ngày giao/khách nhận/kiểm kê không chặn ngày quá khứ hoặc tương lai.
- Xe giao hàng chỉ liệt kê xe rảnh; `Km lúc đi` không kiểm ≥ km cuối chuyến trước (chỉ hiển thị hint `odometerHint`).
- Kiểm kê: FE chặn 0 dòng và > 1.000 dòng bằng `message.warning`; "Để trống số đếm nếu khớp sổ" nghĩa là dòng trống không gửi → test phải nhập ≥ 1 dòng.
- Duyệt kiểm kê điều chỉnh tồn kho theo `currentQty` hiện tại (`stockChanged` cảnh báo) — FE chỉ hiển thị, không chặn.
- `InventoryPage` là một trang nhiều tab, không route con; tab đang mở không lưu trên URL.
- Trạng thái dùng khóa chữ hoa (`PENDING`, `APPROVED`, `RETURNED`, `DELIVERED`…), nhãn tiếng Việt khác nhau theo màn.
- Nhiều nút (xem, xuất Excel) chỉ có icon → nên bổ sung `data-testid`.
