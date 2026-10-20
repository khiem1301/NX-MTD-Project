# NX-MTD – Khác biệt giữa code và tài liệu

> Ghi theo Coding Rules mục 8. Chỉ thêm mục mới vào cuối file. Trạng thái: `Mở` · `Đã sửa tài liệu` · `Bỏ`.
> Tài liệu gốc: Report-3 SRS (16/10), NX-MTD_AI_Context.md (19/10), NX-MTD_Database.sql (65 bảng), NX-MTD_Tong_hop_Use_Case.md (20/10).

## Bảng tổng hợp

| Mã | Ngày | Người | UC / bảng | Tóm tắt | Trạng thái |
|---|---|---|---|---|---|
| DEV-TH-12-1 | 21/10/2026 | Thái | `PurchaseOrder`, `PurchaseOrderLine`, `MaterialReceipt`, `MaterialReceiptLine` | Đơn mua NVL không bắt buộc gắn đơn hàng (mua dùng chung kim, chỉ...) | Mở |
| DEV-TH-12-2 | 21/10/2026 | Thái | `Material` | Quản đốc nhập tên NVL mới khi tạo đơn mua, NVL tự vào danh mục | Mở |

---

### DEV-TH-12-1 · Đơn mua NVL không bắt buộc gắn đơn hàng

- **Ngày / Người / Task / PR:** 21/10/2026 · Thái · TH-12 · (chưa mở PR)
- **Ảnh hưởng tới:** bảng `PurchaseOrder` (`OrderId`), `PurchaseOrderLine` (`OrderMaterialId`, thêm `MaterialId`, `MaterialColor`), `MaterialReceipt` (`OrderId`), `MaterialReceiptLine` (`OrderMaterialId`, thêm `MaterialId`, `MaterialColor`); unique `UQ_POL`, `UQ_MRL`; AI Context mục 5 (luồng NVL), phân công Minh (đơn mua)
- **Tài liệu gốc ghi:** `PurchaseOrder.OrderId` bắt buộc, mỗi dòng đơn mua trỏ vào `OrderMaterial` của một đơn hàng; thiếu NVL thì xin khách trước, khách không cấp mới mua và Quản đốc tạo `PurchaseOrder` cho đơn đó.
- **Code đang làm:** `OrderId` và `OrderMaterialId` cho phép NULL. Đơn mua không có `OrderId` là đơn mua NVL dùng chung nhiều đơn (kim, chỉ...); dòng của đơn này xác định NVL bằng `MaterialId` + `MaterialColor`. Có CHECK `CK_POL_Target` / `CK_MRL_Target` (một dòng chỉ theo một trong hai cách) và unique dạng filtered (`UQ_POL`, `UQ_POL_Stock`, `UQ_MRL`, `UQ_MRL_Stock`). Đơn mua có `OrderId` vẫn như tài liệu (dòng phải khớp NVL đã khai báo của đơn). Kho NVL tính theo (NVL, màu) nên không phải sửa `IStockService`. API: `GET/POST /api/v1/manager/purchase-orders`, đơn tạo xong ở `PENDING_FINALISATION` / `UNPAID`. FE: màn "NVL của đơn", khi chưa chọn đơn thì nút Tạo mở form đơn mua dùng chung (chỉ Quản đốc).
- **Lý do:** Quản đốc cần mua kim, chỉ dùng cho nhiều đơn mà không thể chờ Admin tạo mã đơn hàng cho từng lần mua (yêu cầu của Thái ngày 21/10/2026).
- **Ảnh hưởng người khác:** Minh (sở hữu `PurchaseOrder`, NCC, thanh toán): service đơn mua đã có phần tạo/xem, Minh nối tiếp bước chọn NCC, chốt đặt hàng và thanh toán; `OrderId` của đơn có thể NULL nên mọi màn/báo cáo theo đơn phải xử lý đơn không có đơn hàng. Thái (TH-12): phiếu nhập từ NCC cho đơn mua dùng chung chưa làm (hiện chỉ nhận nguồn khách cấp, vẫn bắt buộc `OrderId`).
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: các cột trên, CHECK và unique mới
  - [ ] `NX-MTD_AI_Context.md` mục 5 (luồng NVL): thêm trường hợp đơn mua dùng chung; mục 6.2 nếu đổi người sở hữu
  - [ ] Report-3: UC đơn mua / thiếu NVL (luồng thay thế mua dùng chung), mockup màn "NVL của đơn"
  - [ ] `NX-MTD_Tong_hop_Use_Case.md`: UC liên quan đơn mua
- **File migrate:** `be/SQL/Migrations/20261021_01_TH-12_stock-purchase-order.sql`
- **Trạng thái:** Mở

### DEV-TH-12-2 · Quản đốc nhập tên NVL mới khi tạo đơn mua

- **Ngày / Người / Task / PR:** 21/10/2026 · Thái · TH-12 · (chưa mở PR)
- **Ảnh hưởng tới:** bảng `Material`; AI Context mục 2 (vai trò), màn Danh mục NVL
- **Tài liệu gốc ghi:** Danh mục NVL do Thủ kho quản lý (menu "Danh mục NVL" chỉ role Thủ kho); đơn mua chỉ chọn NVL đã có.
- **Code đang làm:** Trong form đơn mua dùng chung, Quản đốc chọn NVL có sẵn hoặc gõ tên NVL chưa có kèm đơn vị. Khi lưu đơn, hệ thống dùng lại NVL phụ liệu trùng tên (không phân biệt hoa thường), nếu chưa có thì tạo `Material` mới: `MaterialType = TRIM`, `Category = OTHER`, mã tự sinh `PL-xxxx`. NVL được tạo trước khi đơn mua lưu; nếu lưu đơn lỗi thì NVL mới vẫn còn trong danh mục.
- **Lý do:** Kim, chỉ... mua lần đầu thường chưa có trong danh mục; bắt Quản đốc chờ Thủ kho thêm danh mục rồi mới tạo được đơn làm chậm việc mua.
- **Ảnh hưởng người khác:** Thủ kho thấy NVL mới trong Danh mục NVL, có thể sửa nhóm/đơn vị.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 2: Quản đốc được thêm NVL phụ liệu (nhóm OTHER) khi tạo đơn mua
  - [ ] Report-3: UC quản lý danh mục NVL / UC đơn mua
- **Trạng thái:** Mở
