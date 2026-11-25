# IT_CONTRACT — Batch 5: Mua hàng

> Nguồn: Controller, DTO, Entity, EF Configuration (`Infrastructure/Data/Configurations`), `*BaseController`, `Roles.cs`, `PageQuery`, `TextLimits`. **Không** đọc Service/Repository/Validator/Program.cs/Trigger SQL → mọi điểm chưa xác nhận được qua DTO/Schema đánh dấu `[?]`.
> Prefix route lấy từ `[Route("api/v1/<role>/[controller]")]` của BaseController. `[controller]` = tên class bỏ hậu tố `Controller` (giữ PascalCase; routing ASP.NET không phân biệt hoa/thường). `[?]` có `SlugifyParameterTransformer` hay không (nằm ở Program.cs).

## 1. Endpoints

Role thực tế theo BaseController: Manager prefix = `Admin,Manager`; Accountant prefix = `Accountant` (KHÔNG gồm Admin); WarehouseKeeper prefix = `WarehouseKeeper` (KHÔNG gồm Admin); Admin prefix = `Admin`. Không có `[Authorize]`/`[AllowAnonymous]` ở cấp action.

| ID | Method | URL thật | Role | Tham số |
|---|---|---|---|---|
| E01 | GET | `/api/v1/manager/PurchaseOrders` | Admin, Manager | Query `PurchaseOrderQuery`: `page`, `pageSize`, `keyword`, `sortBy`, `sortDir`, `goodsStatus`, `stockOnly`(bool?), `receivable`(bool?) |
| E02 | GET | `/api/v1/manager/PurchaseOrders/{id:int}` | Admin, Manager | Route `id` |
| E03 | POST | `/api/v1/manager/PurchaseOrders` | Admin, Manager | Body `CreatePurchaseOrderDto` |
| E04 | GET | `/api/v1/warehouse-keeper/PurchaseOrders` | WarehouseKeeper | Query `PurchaseOrderQuery` (như E01) |
| E05 | GET | `/api/v1/warehouse-keeper/PurchaseOrders/{id:int}` | WarehouseKeeper | Route `id` |
| E06 | GET | `/api/v1/manager/PurchaseOrderTracking` | Admin, Manager | Query `PurchaseOrderFlowQuery`: `page`, `pageSize`, `keyword`, `sortBy`, `sortDir`, `goodsStatus`, `paymentStatus`, `payableOnly` (bị server ghi đè = `false`) |
| E07 | GET | `/api/v1/manager/PurchaseOrderTracking/{id:int}` | Admin, Manager | Route `id` |
| E08 | POST | `/api/v1/manager/PurchaseOrderTracking/{id:int}/place` | Admin, Manager | Route `id`; Body `PlacePurchaseOrderDto` |
| E09 | POST | `/api/v1/manager/PurchaseOrderTracking/{id:int}/cancel` | Admin, Manager | Route `id`; Body `CancelPurchaseOrderDto` |
| E10 | POST | `/api/v1/manager/PurchaseOrderTracking/{id:int}/reprice` | Admin, Manager | Route `id`; Body `RepricePurchaseOrderDto` |
| E11 | GET | `/api/v1/accountant/PurchasePayments` | Accountant | Query `PurchaseOrderFlowQuery` (`payableOnly` bị server ghi đè = `true`) |
| E12 | GET | `/api/v1/accountant/PurchasePayments/{id:int}` | Accountant | Route `id` |
| E13 | POST | `/api/v1/accountant/PurchasePayments/{id:int}/confirm` | Accountant | Route `id`; Body `ConfirmPaymentDto` |
| E14 | PUT | `/api/v1/accountant/PurchasePayments/{id:int}/payments/{paymentId:int}` | Accountant | Route `id`, `paymentId`; Body `UpdatePurchasePaymentDto` |
| E15 | POST | `/api/v1/accountant/PurchasePayments/{id:int}/return` | Accountant | Route `id`; Body `ReturnPaymentDto` |
| E16 | GET | `/api/v1/manager/Suppliers` | Admin, Manager | Không tham số (chỉ NCC đang hợp tác → `SupplierOptionDto[]`) |
| E17 | GET | `/api/v1/admin/Suppliers` | Admin | Query `SupplierQuery`: `page`, `pageSize`, `keyword`, `sortBy`, `sortDir`, `status` |
| E18 | GET | `/api/v1/admin/Suppliers/{id:int}` | Admin | Route `id` → `SupplierDetailDto` |
| E19 | POST | `/api/v1/admin/Suppliers` | Admin | Body `SaveSupplierDto` |
| E20 | PUT | `/api/v1/admin/Suppliers/{id:int}` | Admin | Route `id`; Body `SaveSupplierDto` |
| E21 | POST | `/api/v1/admin/Suppliers/{id:int}/discontinue` | Admin | Route `id`; Body `DiscontinueSupplierDto` (không có `[FromBody]` nhưng `[ApiController]` tự suy từ complex type) |
| E22 | POST | `/api/v1/admin/Suppliers/{id:int}/reactivate` | Admin | Route `id`; không body |

### Cấu trúc Request DTO

`PageQuery` (base): `Page` int=1, `PageSize` int=20 (clamp 1..100: <1 → 20, >100 → 100 ở `SafePageSize`), `Keyword`, `SortBy`, `SortDir` (`asc` không phân biệt hoa thường = tăng dần, còn lại = giảm dần).

| DTO | Trường (kiểu) |
|---|---|
| `CreatePurchaseOrderDto` | `OrderId` int?, `SupplierId` int, `OrderDate` DateOnly, `ExpectedDeliveryCount` short, `Note` string?, `Lines` `SavePurchaseOrderLineDto[]`, `ExpectedDeliveryDates` DateOnly[]? |
| `SavePurchaseOrderLineDto` | `MaterialId` int?, `NewMaterialName` string?, `NewMaterialUnit` string?, `NewMaterialColor` string?, `OrderedQty` decimal, `UnitPrice` decimal |
| `PlacePurchaseOrderDto` | `SupplierId` int, `OrderDate` DateOnly, `ExpectedDeliveryCount` short, `Note` string?, `Lines` `PlacePurchaseLineDto[]` (`PurchaseOrderLineId` int, `OrderedQty` decimal, `UnitPrice` decimal), `RowVer` byte[] (base64 JSON), `ExpectedDeliveryDates` DateOnly[]? |
| `CancelPurchaseOrderDto` | `Reason` string, `RowVer` byte[] |
| `RepricePurchaseOrderDto` | `Note` string?, `Lines` `RepricePurchaseLineDto[]` (`PurchaseOrderLineId` int, `UnitPrice` decimal), `RowVer` byte[] |
| `ConfirmPaymentDto` | `PaymentDate` DateOnly, `PaidAmount` decimal, `PaymentMethod` string, `PaymentNote` string?, `RowVer` byte[], `ActualAmount` decimal? |
| `UpdatePurchasePaymentDto` | `PaymentDate` DateOnly, `Amount` decimal, `PaymentMethod` string, `PaymentNote` string?, `Reason` string, `RowVer` byte[] |
| `ReturnPaymentDto` | `Reason` string, `RowVer` byte[] |
| `SaveSupplierDto` | `SupplierName` string, `Phone` string?, `Email` string?, `Address` string?, `MaterialTypes` string?, `Note` string? |
| `DiscontinueSupplierDto` | `Reason` string? |

Response chính: E01/E04 `PagedResult<PurchaseOrderListItemDto>`; E02/E05/E03 `PurchaseOrderDto`; E06/E11 `PagedResult<PurchaseOrderFlowListItemDto>`; E07/E12 và kết quả E08–E10, E13–E15 `[?]` (khả năng `PurchaseOrderFlowDto`, kiểu trả của Service không xác nhận được qua Controller); E14 `[?]`; E16 `SupplierOptionDto[]`; E17 `PagedResult<SupplierDto>`; E18 `SupplierDetailDto`; E19–E22 `[?]` (khả năng `SupplierDto`). Bọc ngoài bởi `this.ToResponse(...)` (định dạng envelope/HTTP status map nằm ngoài phạm vi đọc `[?]`).

## 2. Ràng buộc dữ liệu (Input & DB Schema)

Các DTO là `record` thuần, **không có DataAnnotations**; validation (nếu có) nằm ở Validator → không đọc (`[?]`). Bảng dưới là ràng buộc cứng ở DB/EF.

### 2.1 `Supplier`
| Cột | Kiểu | NOT NULL | Ràng buộc |
|---|---|---|---|
| SupplierId | int PK identity | ✔ | |
| SupplierName | nvarchar(200) | ✔ | **UNIQUE** `UQ_Supplier_Name` |
| Phone | varchar(15) | ✘ | **UNIQUE filtered** `UQ_Supplier_Phone` WHERE `Phone IS NOT NULL` |
| Email | varchar(100) | ✘ | **UNIQUE filtered** `UQ_Supplier_Email` WHERE `Email IS NOT NULL` |
| Address | nvarchar(500) | ✘ | |
| MaterialTypes | nvarchar(200) | ✘ | |
| Note | nvarchar(500) | ✘ | |
| Status | varchar(20) | ✔ | default `'ACTIVE'` |
| DiscontinueReason | nvarchar(500) (=`TextLimits.Reason`) | ✘ | |
| CreatedAt | datetime2(0) | ✔ | default `dbo.fn_VnNow()` |

### 2.2 `PurchaseOrder`
| Cột | Kiểu | NOT NULL | Ràng buộc |
|---|---|---|---|
| PurchaseOrderId | int PK | ✔ | |
| PoCode | varchar(30) | ✔ | **UNIQUE** `UQ_PurchaseOrder_Code` |
| OrderId | int | ✘ | FK `FK_PO_Order` → `CustomerOrder` (NULL = đơn mua dự trữ) |
| SupplierId | int | ✘ | FK `FK_PO_Supplier` → `Supplier` |
| SourceRequestId | int | ✘ | FK `FK_PO_Request` → `Request` (không lộ ra DTO) |
| OrderDate | date | ✘ | |
| ExpectedDeliveryCount | smallint | ✘ | |
| ExpectedDeliveryDate / ExpectedDeliveryDateTo | date | ✘ | (To: ngày giao đến khi giao ≥2 lần) |
| GoodsStatus | varchar(25) | ✔ | default `'PENDING_FINALISATION'` |
| PaymentStatus | varchar(20) | ✔ | default `'UNPAID'` |
| RejectReason | nvarchar(500) | ✘ | `TextLimits.Reason` |
| PaymentReturnReason | nvarchar(500) | ✘ | `TextLimits.Reason` |
| PaymentDate | date | ✘ | |
| PaidAmount | decimal(18,2) | ✘ | |
| ActualAmount | decimal(18,2) | ✘ | DEV-ALL-129 |
| PaymentMethod | varchar(20) | ✘ | |
| PaymentNote | nvarchar(500) | ✘ | |
| Note | nvarchar(1000) | ✘ | |
| CreatedAt | datetime2(0) | ✔ | default `dbo.fn_VnNow()` |
| RowVer | rowversion | ✔ | concurrency token |

### 2.3 `PurchaseOrderLine` (có trigger `trg_PurchaseOrderLine_Check` — nội dung `[?]`)
| Cột | Kiểu | NOT NULL | Ràng buộc |
|---|---|---|---|
| PurchaseOrderLineId | int PK | ✔ | |
| PurchaseOrderId | int | ✔ | FK `FK_POL_PO` → `PurchaseOrder` (ClientSetNull) |
| OrderMaterialId | int | ✘ | FK `FK_POL_OrderMaterial` → `OrderMaterial` |
| MaterialId | int | ✘ | FK `FK_POL_Material` → `Material` |
| OrderedQty | decimal(18,3) | ✔ | không có CHECK ở EF config |
| NotReplenishedQty | decimal(18,3) | ✔ | không khai báo default ở EF (default DB `[?]`) |
| UnitPrice | decimal(18,2) | ✔ | không có CHECK ở EF config |
| Amount | decimal(18,2) | ✘ | **computed** `CONVERT(decimal(18,2), OrderedQty*UnitPrice)`, non-persisted → không INSERT/UPDATE được |

UNIQUE filtered: `UQ_POL (PurchaseOrderId, OrderMaterialId)` WHERE `OrderMaterialId IS NOT NULL`; `UQ_POL_Stock (PurchaseOrderId, MaterialId)` WHERE `OrderMaterialId IS NULL`.

### 2.4 `PurchasePayment`
| Cột | Kiểu | NOT NULL | Ràng buộc |
|---|---|---|---|
| PurchasePaymentId | int PK | ✔ | |
| PurchaseOrderId | int | ✔ | FK `FK_PP_PurchaseOrder` (ClientSetNull); index `IX_PP_PurchaseOrder` |
| PaymentDate | date | ✔ | |
| Amount | decimal(18,2) | ✔ | **CHECK `CK_PP_Amount`: Amount > 0** |
| PaymentMethod | varchar(20) | ✔ | |
| PaymentNote | nvarchar(500) | ✘ | |
| CreatedAt | datetime2(0) | ✔ | default `dbo.fn_VnNow()` |
| UpdatedAt | datetime2(0) | ✘ | |
| EditReason | nvarchar(500) | ✘ | `TextLimits.Reason` |

### 2.5 `DeliveryScheduleDate`
| Cột | Kiểu | NOT NULL | Ràng buộc |
|---|---|---|---|
| DeliveryScheduleDateId | int PK | ✔ | |
| PurchaseOrderId / OrderId / RequestId | int | ✘ | FK cascade delete (`FK_DSD_PurchaseOrder/Order/Request`); **CHECK `CK_DSD_Owner`: đúng 1 trong 3 khác NULL** |
| SeqNo | smallint | ✔ | **CHECK `CK_DSD_Seq`: SeqNo ≥ 1** |
| DeliveryDate | date | ✔ | |

UNIQUE filtered: `UQ_DSD_PurchaseOrder (PurchaseOrderId, SeqNo)` WHERE `PurchaseOrderId IS NOT NULL` (tương tự cho Order, Request).

### 2.6 `Material` (bảng liên quan, bị INSERT khi tạo PO với NVL mới)
`MaterialCode` varchar(30) NOT NULL UNIQUE; `MaterialName` nvarchar(200) NOT NULL; `Color` nvarchar(50) NOT NULL default `''`; `MaterialType` varchar(10) NOT NULL (FABRIC/TRIM/SCRAP); `Unit` nvarchar(20) NOT NULL; `WidthM` decimal(4,2) NULL; `Status` varchar(20) NOT NULL default `'ACTIVE'`; UNIQUE `UQ_Material_NameType (MaterialName, MaterialType, Color)`.

### 2.7 Giới hạn dùng chung
`TextLimits.Reason` = 500 (RejectReason, PaymentReturnReason, EditReason, DiscontinueReason); `PageQuery.MaxPageSize` = 100, `DefaultPageSize` = 20.

## 3. Bảng DB chịu tác động khi thành công

Chỉ xác nhận được từ DTO/Entity/Config; thao tác của Service ghi `[?]`.

| ID | Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|---|
| E01, E02, E04–E07, E11, E12, E16, E17 | GET danh sách/chi tiết | — | — | — |
| E18 | GET Supplier chi tiết | — | — | — (đọc `AuditLog` cho `History` — `[?]` theo `SupplierDetailDto.History`) |
| E03 | Tạo PO | `PurchaseOrder`; `PurchaseOrderLine`; `DeliveryScheduleDate` (theo `ExpectedDeliveryDates`, SeqNo 1..n); `Material` `[?]` (khi dùng `NewMaterial*`: DTO nói thêm vào danh mục, phụ liệu, nhóm OTHER); `AuditLog` `[?]` | `OrderMaterial` `[?]` (link `OrderMaterialId`) | — |
| E08 | Place | `AuditLog` `[?]` | `PurchaseOrder` (SupplierId, OrderDate, ExpectedDeliveryCount/Date/DateTo, Note, GoodsStatus, RowVer); `PurchaseOrderLine` (OrderedQty, UnitPrice) | `DeliveryScheduleDate` `[?]` (thay lịch) + INSERT lại `[?]` |
| E09 | Cancel | `AuditLog` `[?]` | `PurchaseOrder` (GoodsStatus, `RejectReason` `[?]`) | — |
| E10 | Reprice | `AuditLog` `[?]` | `PurchaseOrderLine.UnitPrice`; `PurchaseOrder` (Note, PaymentStatus `[?]`, RowVer) | — |
| E13 | Confirm payment | `PurchasePayment` (DEV-ALL-136); `AuditLog` `[?]` | `PurchaseOrder` (PaymentStatus, PaymentDate, PaidAmount, PaymentMethod, PaymentNote, ActualAmount `[?]`, RowVer) | — |
| E14 | Sửa lần thanh toán | `AuditLog` `[?]` | `PurchasePayment` (PaymentDate, Amount, PaymentMethod, PaymentNote, UpdatedAt, EditReason); `PurchaseOrder` (PaidAmount/PaymentDate… `[?]`) | — (entity ghi rõ "không xóa") |
| E15 | Return payment | `AuditLog` `[?]` | `PurchaseOrder` (PaymentStatus, PaymentReturnReason, RowVer) | — |
| E19 | Tạo NCC | `Supplier`; `AuditLog` `[?]` | — | — |
| E20 | Sửa NCC | `AuditLog` `[?]` | `Supplier` | — |
| E21 | Ngừng hợp tác | `AuditLog` `[?]` | `Supplier` (Status, DiscontinueReason) | — |
| E22 | Hợp tác lại | `AuditLog` `[?]` | `Supplier` (Status; DiscontinueReason `[?]`) | — |

Ảnh chứng từ của E13 không đi qua endpoint nào trong batch (DTO: "tải lên riêng qua Attachment" → bảng `Attachment` thuộc Batch 6).

## 4. Lưu ý kỹ thuật cho QA

**Ma trận phân quyền chéo**

| Nhóm endpoint | Admin | Manager | Accountant | WarehouseKeeper | Khác |
|---|---|---|---|---|---|
| E01–E03, E06–E10, E16 (`/manager/`) | ✔ | ✔ | 403 | 403 | 403 |
| E04–E05 (`/warehouse-keeper/`) | 403 | 403 | 403 | ✔ | 403 |
| E11–E15 (`/accountant/`) | **403** | 403 | ✔ | 403 | 403 |
| E17–E22 (`/admin/`) | ✔ | 403 | 403 | 403 | 403 |

Không token → 401. Admin KHÔNG vào được prefix `accountant` và `warehouse-keeper` (khác `/manager/` vốn cho Admin). `Roles.Handlers`/`StaffExceptWorker` không dùng trong batch này.

**Quy ước Status (DB vs DTO)**
- DB lưu UPPER_SNAKE: `Supplier.Status` `ACTIVE`; `PurchaseOrder.GoodsStatus` mặc định `PENDING_FINALISATION` (comment query còn nêu `ORDERED`, `PARTIALLY_RECEIVED`); `PaymentStatus` mặc định `UNPAID`.
- DTO/Query comment dùng PascalCase: `PurchaseOrderFlowQuery.GoodsStatus` "PascalCase, vd. `PendingFinalisation`", `PaymentStatus` `Unpaid|Paid|Returned`, `SupplierQuery.Status` `Active|Discontinued`, `ConfirmPaymentDto.PaymentMethod` `BankTransfer|Cash`. Cột `PaymentMethod` varchar(20) ở DB — dạng lưu (PascalCase hay UPPER) `[?]`. 
- Lệch trong chính DTO: `PurchaseOrderQuery.GoodsStatus` (E01/E04) comment liệt kê giá trị UPPER (`ORDERED`/`PARTIALLY_RECEIVED`) trong khi `PurchaseOrderFlowQuery.GoodsStatus` (E06/E11) yêu cầu PascalCase → test cả hai dạng; assert DB bằng dạng UPPER, assert JSON theo dạng thực tế trả về.

**Concurrency**: E08–E10, E13–E15 bắt buộc `RowVer` (byte[] → base64 trong JSON); `PurchaseOrder.RowVer` là rowversion/concurrency token → RowVer cũ kỳ vọng conflict (mã HTTP `[?]`, chỉ khóa kỳ vọng theo đặc tả QA). Lưu ý `PurchasePayment` không có RowVer; E14 dùng RowVer của PO.

**Trigger / computed / view**
- `trg_PurchaseOrderLine_Check` trên `PurchaseOrderLine` (nội dung không đọc) → có thể chặn INSERT/UPDATE dòng; QA cần đối chiếu đặc tả.
- `PurchaseOrderLine.Amount` là cột computed — không seed trực tiếp; `NotReplenishedQty` NOT NULL, không default ở EF → khi seed phải gán giá trị.
- `CreatedAt` default hàm `dbo.fn_VnNow()` (giờ VN) — assert cửa sổ 24h của E14 phải dùng cùng nguồn giờ.
- Seed `PurchaseOrder`: `PoCode` varchar(30) UNIQUE; `GoodsStatus`, `PaymentStatus` có default nên có thể bỏ trống.

**Điểm bất thường DTO ↔ Entity**
1. `PurchaseOrderLineDto.MaterialId` là `int` không null, nhưng entity `PurchaseOrderLine.MaterialId` là `int?` (DB cho NULL) → dòng không có MaterialId có thể gây lỗi map/500.
2. `PurchaseOrderLineDto` có `MaterialCode/Name/Unit/MaterialColor` không-null; `Material.Color` NOT NULL default `''` nên màu rỗng là `""`, không phải null; `NewMaterialColor` ở request là nullable.
3. Hai nơi chứa dữ liệu thanh toán: cột `PaymentDate/PaidAmount/PaymentMethod/PaymentNote` trên `PurchaseOrder` (flat) và bảng `PurchasePayment` (nhiều lần). `PurchaseOrderFlowDto` trả cả hai (`PaidAmount` + `Payments`) → cần Assert kép nhất quán sau E13/E14.
4. `PurchasePayment.CreatedAt` là DateTime, `PurchaseOrder.PaymentDate` là DateOnly; `PurchasePaymentDto.CanEdit` do server tính (24 giờ từ `CreatedAt`).
5. `SourceRequestId` có trong entity/FK nhưng không có trong bất kỳ DTO nào của batch → không tạo được qua API; seed trực tiếp nếu cần.
6. `DeliveryScheduleDate` không có DTO riêng; lịch giao chỉ đi qua `ExpectedDeliveryDates`. Quan hệ `PurchaseOrder` cấu hình `WithMany()` (không có navigation ngược trong entity). Số dòng lịch phải khớp `ExpectedDeliveryCount` theo comment DEV-ALL-113 (cưỡng chế ở đâu `[?]`); `PurchaseOrderFlowDto` trả cả `ExpectedDeliveryDate/To` lẫn `ExpectedDeliveryDates`.
7. `PurchaseOrderDto` (E02/E03) và `PurchaseOrderFlowDto` (E07) là hai shape khác nhau cho cùng một PO: `PurchaseOrderDto.ExpectedDeliveryCount` có default `null`; `ReceivedCount` default 0.
8. `CreatePurchaseOrderDto` có hai khối `<summary>` liền nhau (comment trùng/lẫn): "OrderId trống = đơn dự trữ; bắt buộc NCC đang hợp tác" và "Tạo đơn mua = đặt hàng luôn" → đối chiếu trạng thái sau E03 (GoodsStatus mặc định DB là `PENDING_FINALISATION` nhưng comment nói đơn vào trạng thái "đã đặt").
9. `PurchaseOrderFlowListItemDto.PaidAmount` nullable, `TotalAmount` non-null; `PurchaseOrder` entity không có cột `TotalAmount` → giá trị tính từ dòng (`Amount` computed).
10. `E21` không có `[FromBody]` (khác các endpoint khác) — body rỗng/thiếu → hành vi binding của `[ApiController]`; `Reason` nullable.
11. `PurchaseOrderFlowQuery.PayableOnly` có thể truyền từ client nhưng bị `with { PayableOnly = ... }` ghi đè (E06 luôn `false`, E11 luôn `true`) → test client gửi `payableOnly=true` ở E06 vẫn trả đơn chưa đặt hàng.
12. E04/E05 (kho) dùng chung `IPurchaseOrderService` với E01/E02 (Manager) — dữ liệu trả về giống nhau, chỉ khác phân quyền.
13. `Supplier.Phone` varchar(15)/`Email` varchar(100) là non-unicode (ký tự có dấu bị mất/`?` nếu vượt); `SupplierName` unicode nvarchar(200). Khóa UNIQUE tên phụ thuộc collation DB (`[?]` phân biệt hoa/thường).
14. Hai controller tên `SuppliersController` (Admin, Manager) và hai `PurchaseOrdersController` (Manager, WarehouseKeeper) trùng tên class khác namespace — phân biệt qua prefix URL.
