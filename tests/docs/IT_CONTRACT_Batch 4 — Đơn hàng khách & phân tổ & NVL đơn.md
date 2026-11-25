# IT_CONTRACT — Batch 4: Đơn hàng khách & phân tổ / NVL đơn

Nguồn đọc: Controller, DTO, Entity, `Configurations/*`, `*BaseController`, `Roles`, `TextLimits`, `PageQuery`. **Không** đọc Service/Repository/Program.cs. Mục nào cần logic Service để xác nhận đều đánh `[?]`.

Quy ước: `{C}` = tên controller sau `[controller]`. Mọi API cần JWT (thiếu token → 401); sai role → 403. Giá trị nhận diện `bool?`/`string?` = nullable; kiểu tham chiếu không `?` = NOT NULL (suy từ `= null!` / nullable context).

---

## 1. Bảng Endpoints

### 1.1 Prefix & role

| Prefix | Role thực tế (`[Authorize]`) |
|---|---|
| `/api/v1/admin/{C}` | `Admin` |
| `/api/v1/manager/{C}` | `Admin,Manager` (Admin gọi được) |
| `/api/v1/warehouse-keeper/{C}` | `WarehouseKeeper` (Admin **không** gọi được) |
| `/api/v1/common/{C}` | Base không có `[Authorize]`; controller `OrderLookupsController` gắn `Roles.StaffExceptWorker` = Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, QaQc, Electrician (loại `Worker`) |

### 1.2 Admin — `CustomerOrders` (`/api/v1/admin/CustomerOrders`)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| A1 | POST | `/api/v1/admin/CustomerOrders/import/preview` | Admin | Body multipart: `file` (IFormFile) |
| A2 | POST | `/api/v1/admin/CustomerOrders/import/preview-data` | Admin | Body JSON `OrderImportExcelDto` |
| A3 | GET | `/api/v1/admin/CustomerOrders` | Admin | Query `OrderQuery`: `Page, PageSize, Keyword, SortBy, SortDir, Status, CustomerId, DueFrom, DueTo` |
| A4 | GET | `/api/v1/admin/CustomerOrders/next-code` | Admin | — |
| A5 | GET | `/api/v1/admin/CustomerOrders/{id:int}` | Admin | Route `id` |
| A6 | POST | `/api/v1/admin/CustomerOrders` | Admin | Body `CreateOrderDto` |
| A7 | PUT | `/api/v1/admin/CustomerOrders/{id:int}` | Admin | Route `id`; Body `UpdateOrderDto` (có `RowVer`) |
| A8 | POST | `/api/v1/admin/CustomerOrders/{id:int}/cancel` | Admin | Route `id`; Body `CancelOrderDto{Reason?, RowVer}` |
| A9 | POST | `/api/v1/admin/CustomerOrders/{id:int}/start-production` | Admin | Route `id`; Body `StartOrderProductionDto{RowVer}` |
| A10 | POST | `/api/v1/admin/CustomerOrders/{id:int}/stop` | Admin | Route `id`; Body `StopOrderDto{Reason, RowVer}` |

### 1.3 Manager — `CustomerOrders` (`/api/v1/manager/CustomerOrders`)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| M1 | GET | `/api/v1/manager/CustomerOrders` | Admin,Manager | Query `OrderQuery` |
| M2 | GET | `/api/v1/manager/CustomerOrders/{id:int}` | Admin,Manager | Route `id` |

### 1.4 Manager — `OrderLineTeams` (`/api/v1/manager/OrderLineTeams`)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| T1 | GET | `/api/v1/manager/OrderLineTeams` | Admin,Manager | Query `OrderLineTeamQuery`: `Page, PageSize, Keyword, SortBy, SortDir, OnlyUnallotted(bool)` |
| T2 | GET | `/api/v1/manager/OrderLineTeams/teams` | Admin,Manager | Query `orderId:int`, `productId:int` |
| T3 | PUT | `/api/v1/manager/OrderLineTeams` | Admin,Manager | Body `SetOrderLineTeamsDto{OrderLineId, TeamIds[]}` |
| T4 | POST | `/api/v1/manager/OrderLineTeams/bulk` | Admin,Manager | Body `AddOrderLineTeamsDto{OrderLineIds[], TeamIds[]}` |

### 1.5 Manager — `OrderMarkers` (`/api/v1/manager/OrderMarkers`)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| K1 | GET | `/api/v1/manager/OrderMarkers/product-spec/{productId:int}` | Admin,Manager | Route `productId` |
| K2 | POST | `/api/v1/manager/OrderMarkers/excel/parse` | Admin,Manager | Body multipart `file` |
| K3 | POST | `/api/v1/manager/OrderMarkers/excel/check` | Admin,Manager | Body `ExcelBundleCheckDto{ProductId?, OrderId?, Bundle, Replace=false}` |
| K4 | POST | `/api/v1/manager/OrderMarkers/{orderId:int}/products/{productId:int}/import-data` | Admin,Manager | Route `orderId, productId`; Query `parts` (CSV, phải chứa `markers`), `replace=false`; Body `SpecWorkbookDto` |
| K5 | POST | `/api/v1/manager/OrderMarkers/{orderId:int}/products/{productId:int}/import/preview` | Admin,Manager | Route `orderId, productId`; Query `replace=false`; multipart `file` |
| K6 | POST | `/api/v1/manager/OrderMarkers/{orderId:int}/products/{productId:int}/import` | Admin,Manager | Route `orderId, productId`; Query `replace=false`; multipart `file` |
| K7 | GET | `/api/v1/manager/OrderMarkers/{orderId:int}` | Admin,Manager | Route `orderId` |
| K8 | PUT | `/api/v1/manager/OrderMarkers/{orderId:int}/products/{productId:int}` | Admin,Manager | Route `orderId, productId`; Body `SaveOrderMarkersDto{Markers[]}` |

### 1.6 `OrderMaterials` — Admin / Manager / WarehouseKeeper

Admin: `/api/v1/admin/OrderMaterials` · Manager: `/api/v1/manager/OrderMaterials` · WK: `/api/v1/warehouse-keeper/OrderMaterials`

| # | Method | Path (sau prefix) | Có ở | Tham số |
|---|---|---|---|---|
| O1 | GET | `/paged` | Admin, Manager | Query `OrderMaterialPageQuery`: `Page, PageSize, Keyword, SortBy, SortDir, OrderId, Supply, ReceiptState` |
| O2 | GET | `` (gốc) | Admin, Manager, **WK** | Query `orderId:int` |
| O3 | GET | `/product-materials` | Admin, Manager | Query `orderId:int` |
| O4 | POST | `` | Admin, Manager | Body `CreateOrderMaterialDto{OrderId, MaterialId, RequiredQty, CustomerQty, Note?}` |
| O5 | POST | `/batch` | Admin, Manager | Body `CreateOrderMaterialBatchDto{OrderId, Lines[{MaterialId, RequiredQty, CustomerQty, Note?}]}` |
| O6 | PUT | `/{id:int}` | Admin, Manager | Route `id`; Body `UpdateOrderMaterialDto{MaterialId, RequiredQty, CustomerQty, Note?}` |
| O7 | GET | `/customer-delivery/{orderId:int}` | Admin, Manager | Route `orderId` |
| O8 | PUT | `/customer-delivery/{orderId:int}` | Admin, Manager | Route `orderId`; Body `SaveOrderCustomerDeliveryDto{DeliveryCount:short, DeliveryDates:DateOnly[]}` |
| O9 | DELETE | `/{id:int}` | Admin, Manager | Route `id` |
| O10 | POST | `/import/preview` | Admin, Manager | Query `orderId`, `replace=false`; multipart `file` |
| O11 | POST | `/import` | Admin, Manager | Query `orderId`, `replace=false`; multipart `file` |
| O12 | POST | `/import/preview-data` | **chỉ Admin** | Query `orderId`; Body `OrderMaterialExcelRow[]` |
| O13 | POST | `/import-data` | Admin, Manager | Query `orderId`, `replace=false`; Body `OrderMaterialExcelRow[]` |

### 1.7 Common — `OrderLookups` (`/api/v1/common/OrderLookups`)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| L1 | GET | `/api/v1/common/OrderLookups/orders` | StaffExceptWorker | Query `OrderLookupQuery`: `Page, PageSize, Keyword, SortBy, SortDir, Status (null=Running), ProductId?` |
| L2 | GET | `/api/v1/common/OrderLookups/orders/{id:int}/lines` | StaffExceptWorker | Route `id`; Query `sewingTeamId:int?` |
| L3 | GET | `/api/v1/common/OrderLookups/products` | StaffExceptWorker | Query `PageQuery` |
| L4 | GET | `/api/v1/common/OrderLookups/product-colors` | StaffExceptWorker | Query `productId:int?` |

### 1.8 Body/Response DTO (rút gọn)

| DTO | Trường |
|---|---|
| `CreateOrderDto` | `OrderCode, CustomerId, ReceivedDate(DateOnly), DueDate(DateOnly), DeliveryAddress, Note?, Lines[OrderLineInput], StagedProducts[StagedProductDto]?` |
| `UpdateOrderDto` | như Create + `RowVer(byte[])` |
| `OrderLineInput` | `OrderLineId?, ProductId(int; âm = mã staged theo TempId), Color, SizeCode, OrderedQty(int), CmtUnitPrice(decimal)` |
| `StagedProductDto` | `TempId, ProductCode, SampleImageUrl?, Description?, ExtraImages?` |
| Response save đơn | `OrderSaveResultDto{OrderId, Products[{TempId, ProductId, ProductCode}]}` |
| `OrderDetailDto` (Admin) | có `CmtUnitPrice` (trong `Lines`), `TotalAmount` (ở list), `History[AuditLogDto]`, `RowVer`, `HasOrderMarkers`, `HasOrderMaterials`, `StopReason` |
| `OrderOverviewDto` (Manager) | **không** có `CmtUnitPrice`, `TotalAmount`, `History`, `RowVer` |
| `SaveOrderMarkerDto` | `ProductMarkerId?, MarkerNo, MarkerCode, LengthM, WidthM?, Status, Ratios[{SizeCode, PiecesPerPly:short}]` |
| `OrderMaterialDto` | `OrderMaterialId, OrderId, MaterialId, …, RequiredQty, CustomerQty, PurchaseQty?, Source?, ReceivedQty, ShortQty, …` |

---

## 2. Ràng buộc dữ liệu (Input & DB Schema)

### 2.1 `CustomerOrder` (UQ_CustomerOrder_Code, IX_CustomerOrder_Status)

| Cột | Kiểu DB | NULL | Ràng buộc / mặc định |
|---|---|---|---|
| OrderId | int PK | NOT NULL | Identity (mặc định EF) |
| OrderCode | varchar(30) | NOT NULL | **UNIQUE** `UQ_CustomerOrder_Code` (không filter) |
| CustomerId | int | NOT NULL | FK `FK_CustomerOrder_Customer` → Customer |
| ReceivedDate, DueDate | date | NOT NULL | — |
| MaterialStatus | varchar(10) | NOT NULL | Default `'AWAITING'` |
| Status | varchar(25) | NOT NULL | Default `'NEW'` |
| DelayRiskSince | date | NULL | — |
| IsDelayRisk | bit | computed | `CASE WHEN DelayRiskSince IS NULL THEN 0 ELSE 1`, **chỉ đọc** (entity `bool?`) |
| StopReason | nvarchar(500) | NULL | `TextLimits.Reason = 500` |
| FinalAmount | decimal(18,2) | NULL | Kế toán nhập, có thể âm |
| FinalAmountReason | nvarchar(500) | NULL | — |
| DeliveryAddress | nvarchar(500) | NOT NULL | Không có default DB (entity default `string.Empty`) |
| Note | nvarchar(1000) | NULL | — |
| CustomerDeliveryCount | smallint | NULL | — |
| CustomerDeliveryDate / DateTo | date | NULL | — |
| CreatedAt | datetime2(0) | NOT NULL | Default `fn_VnNow()` |
| UpdatedAt | datetime2(0) | NULL | — |
| RowVer | rowversion | NOT NULL | Concurrency token |

### 2.2 `OrderLine` (UQ_OrderLine, IX_OrderLine_Product)

| Cột | Kiểu | NULL | Ràng buộc |
|---|---|---|---|
| OrderLineId | int PK | NOT NULL | — |
| OrderId | int | NOT NULL | FK `FK_OrderLine_Order` → CustomerOrder |
| ProductVariantId + ProductId | int, int | NOT NULL | FK ghép `FK_OrderLine_Variant` → `ProductVariant(ProductVariantId, ProductId)` |
| OrderedQty | int | NOT NULL (entity `int` non-nullable) | Không thấy CHECK trong config (giá trị > 0 do tầng ứng dụng, không xác nhận) |
| CmtUnitPrice | decimal(18,2) | NOT NULL | — |
| **UNIQUE** | `(OrderId, ProductVariantId)` | | `UQ_OrderLine` |

### 2.3 `OrderLineTeam`

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| OrderLineTeamId | int PK | — |
| OrderLineId | int NOT NULL | FK `FK_OLT_OrderLine` → OrderLine, **ON DELETE CASCADE** |
| SewingTeamId | int NOT NULL | FK `FK_OLT_Team` → Team (ClientSetNull = NO ACTION ở DB) |
| AssignedAt | datetime2(0) NOT NULL | Default `fn_VnNow()` |
| **UNIQUE** | `(OrderLineId, SewingTeamId)` | `UQ_OrderLineTeam`; index phụ `IX_OLT_Team` |

### 2.4 `OrderMaterial` (UQ_OM)

| Cột | Kiểu | NULL | Ràng buộc |
|---|---|---|---|
| OrderMaterialId | int PK | NOT NULL | — |
| OrderId | int | NOT NULL | FK `FK_OM_Order` |
| MaterialId | int | NOT NULL | FK `FK_OM_Material` |
| RequiredQty | decimal(18,3) | NOT NULL | — |
| CustomerQty | decimal(18,3) | NOT NULL | — |
| PurchaseQty | decimal(19,3) | computed | `CASE WHEN Required>Customer THEN Required−Customer ELSE 0` — chỉ đọc |
| Source | varchar(10) | computed | `Customer=0 → 'SUPPLIER'; Customer>=Required → 'CUSTOMER'; else 'BOTH'` — chỉ đọc |
| Note | nvarchar(500) | NULL | — |
| **UNIQUE** | `(OrderId, MaterialId)` | | `UQ_OM` |

### 2.5 `DeliveryScheduleDate`

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| DeliveryScheduleDateId | int PK | — |
| PurchaseOrderId / OrderId / RequestId | int NULL | FK Cascade (`FK_DSD_*`); **CHECK `CK_DSD_Owner`**: đúng 1 trong 3 khác NULL |
| SeqNo | smallint NOT NULL | **CHECK `CK_DSD_Seq`**: `SeqNo >= 1` |
| DeliveryDate | date NOT NULL | — |
| **UNIQUE** | `(OrderId, SeqNo)` WHERE `[OrderId] IS NOT NULL` | `UQ_DSD_Order` (tương tự `UQ_DSD_PurchaseOrder`, `UQ_DSD_Request`) |

### 2.6 `ProductMarker` / `ProductMarkerRatio`

| Bảng.Cột | Kiểu | Ràng buộc |
|---|---|---|
| ProductMarker.MarkerCode | varchar(20) | **UNIQUE** `UQ_PM (OrderId, ProductId, MarkerNo, MarkerCode)` |
| ProductMarker.LengthM | decimal(7,2) | — |
| ProductMarker.WidthM | decimal(4,2) | max kiểu = 99.99; **NOT NULL** (entity `decimal` non-nullable) — lệch với `SaveOrderMarkerDto.WidthM` là `decimal?` |
| ProductMarker.Status | varchar(20) | NOT NULL; Default `'ACTIVE'` |
| ProductMarker.MarkerNo | tinyint (entity `byte`) | NOT NULL — DTO dùng `int`, giá trị > 255 vượt kiểu |
| ProductMarker.OrderId / ProductId | int | FK `FK_PM_Order`, `FK_PM_Product` (NO ACTION) |
| ProductMarkerRatio | PK ghép `(ProductMarkerId, SizeCode)` | `SizeCode varchar(10)`; FK `FK_PMR_Marker` (NO ACTION); `PiecesPerPly` kiểu `short` ở DTO |

### 2.7 Ràng buộc đầu vào (DTO / Query)

| Trường | Ràng buộc quan sát được |
|---|---|
| `PageQuery.Page` | Default 1; validator: ≥ 1 |
| `PageQuery.PageSize` | Default 20; `MaxPageSize = 100`; validator: 1..100. **Lưu ý**: `SafePageSize` (dùng khi truy vấn) tự clamp, nhưng validator trả lỗi nếu ngoài khoảng |
| `PageQuery.Keyword` | ≤ 100 ký tự (validator) |
| `SortDir` | `"asc"` (không phân biệt hoa/thường) → ASC; mọi giá trị khác/null → DESC |
| `Reason` (Cancel/Stop) | DB ≤ 500 (`TextLimits.Reason`); `CancelOrderDto.Reason` nullable, `StopOrderDto.Reason` non-null |
| `MarkerGroup`/`WidthM` | Comment DTO: > 0 và ≤ 3,5 m (DEV-ALL-102) — rule nằm ở validator/Service `[?]` |
| `ExpectedDelivery` | `DeliveryCount` (short) = số ngày trong `DeliveryDates` (comment DEV-ALL-113) |
| `OrderLineInput.ProductId` | Dương = mã có sẵn; âm = `StagedProductDto.TempId` cùng đơn |
| `RowVer` | `byte[]` → base64 trong JSON; sai/thiếu → khả năng 409 (concurrency) `[?]` |
| `OrderLookupQuery.Status` | null = `OrderStatuses.Running` (giá trị cụ thể không đọc) |
| `OrderMaterialPageQuery.Supply/ReceiptState` | Giá trị thuộc `OrderMaterialSupplies` / `OrderMaterialReceiptStates` (file không đọc) `[?]` |

---

## 3. Các bảng DB chịu tác động (khi thành công)

> Đọc không có Service nên cột này suy từ DTO/Entity/Config. `[?]` = chưa xác nhận.

| # | Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|---|
| A1, A2, A3, A4, A5, M1, M2 | — (chỉ đọc; A1/A2 xác nhận "Không ghi gì" qua comment) | — | — | — |
| A6 Create | CustomerOrder, OrderLine, ProductVariant `[?]` (variant màu×size mới), Product `[?]` (nếu có `StagedProducts`), AuditLog `[?]` (có `History: AuditLogDto`) | — | — | — |
| A7 Update | OrderLine (dòng mới), ProductVariant `[?]`, Product `[?]` (staged), AuditLog `[?]` | CustomerOrder (RowVer, UpdatedAt), OrderLine (dòng cũ có `OrderLineId`) | OrderLine (dòng bỏ khỏi danh sách, chỉ khi `Status=NEW`; cascade → OrderLineTeam) |
| A8 Cancel | AuditLog `[?]` | CustomerOrder (Status, StopReason? `[?]`, RowVer) | — |
| A9 StartProduction | AuditLog `[?]` | CustomerOrder (Status NEW→sản xuất, RowVer) | — |
| A10 Stop | AuditLog `[?]` | CustomerOrder (Status, StopReason, RowVer) | — |
| T1, T2 | — | — | — |
| T3 SetTeams | OrderLineTeam (tổ mới) | — | OrderLineTeam (tổ bị bỏ; danh sách rỗng = xóa hết) |
| T4 AddTeams | OrderLineTeam (chỉ cặp chưa có; tổ đang có giữ nguyên) | — | — |
| K1, K2, K3, K5, K7 | — | — | — |
| K4 import-data | ProductMarker, ProductMarkerRatio | ProductMarker `[?]` | ProductMarker/Ratio `[?]` khi `replace=true` (comment: xóa hoặc ngừng dùng nếu đang dùng); body ngoài `markers` bị bỏ qua |
| K6 import (file) | ProductMarker, ProductMarkerRatio; ProductComponent/Norm/MarkerGroup `[?]` (comment: "định mức mã hàng + bìa của đơn trong một giao dịch") | tương ứng `[?]` | tương ứng khi `replace=true` `[?]` |
| K8 Save | ProductMarker, ProductMarkerRatio (bìa `ProductMarkerId=null`) | ProductMarker (bìa cũ), ProductMarkerRatio | ProductMarker/Ratio (bìa không có trong danh sách) |
| O1, O2, O3, O7, O10, O12, L1–L4 | — | — | — |
| O4 Create | OrderMaterial | CustomerOrder.MaterialStatus `[?]` | — |
| O5 Batch | OrderMaterial (all-or-nothing) | CustomerOrder.MaterialStatus `[?]` | — |
| O6 Update | — | OrderMaterial | — |
| O8 SaveCustomerDelivery | DeliveryScheduleDate (`OrderId` set, `SeqNo` 1..n) | CustomerOrder (`CustomerDeliveryCount/Date/DateTo` `[?]`) | DeliveryScheduleDate cũ của đơn `[?]` |
| O9 Delete | — | CustomerOrder.MaterialStatus `[?]` | OrderMaterial (comment import: không xóa được nếu đã có phiếu nhập/đơn mua → FK `MaterialReceiptLine`, `PurchaseOrderLine`) |
| O11 / O13 Import | OrderMaterial (New), Material `[?]` (comment DEV-ALL-123 thêm NVL vào danh mục) | OrderMaterial (Update) | OrderMaterial (khi `replace=true`, trừ "Blocked") |

---

## 4. Lưu ý kỹ thuật cho QA

### 4.1 Trigger & đối tượng DB
- `trg_ProductMarker_Check` (ProductMarker) và `trg_ProductMarkerRatio_Check` (ProductMarkerRatio): EF cấu hình `HasTrigger` → không dùng `OUTPUT`. **Nội dung trigger chưa đọc**, QA phải đối chiếu trực tiếp trong SQL Server (nhiều khả năng là kiểm tra WidthM/tỉ lệ/size — `[?]`). Test K4/K6/K8 cần có case vi phạm trigger và assert DB không đổi.
- `fn_VnNow()`: default cho `CreatedAt`, `AssignedAt` (giờ VN, không phải UTC).
- View `vw_OrderLineProgress`: nguồn `DeliveredQty` (tổng xuất phiếu `DELIVERED`). Seed phiếu xuất mới nhìn thấy được ở DTO.
- Cột computed (`IsDelayRisk`, `PurchaseQty`, `Source`): **không** insert/update được; test chỉ query để assert.
- `CK_DSD_Owner`, `CK_DSD_Seq`, filter-unique `UQ_DSD_Order`: chỉ vi phạm được khi insert trực tiếp DB; qua API dựa vào validator.

### 4.2 Hoa/thường của Status (DB vs DTO)
| Trường | DB | DTO / Comment | Ghi chú |
|---|---|---|---|
| `CustomerOrder.Status` | **UPPER** (`NEW`; default) | `OrderQuery.Status` comment "PascalCase"; `OrderListItemDto.Status` string | **Lệch**: comment bảo PascalCase nhưng DB default `'NEW'` UPPER. Cần xác nhận query filter case-insensitive hay mapping (`OrderStatuses` không đọc) |
| `CustomerOrder.MaterialStatus` | UPPER (`AWAITING`) | `StartProduction`: "MaterialStatus khác AWAITING" | Giá trị khác `[?]` |
| `ProductMarker.Status` | UPPER (`ACTIVE`) | `OrderMarkerDto`: "Active / Discontinued"; `SpecOrderMarkerExcelRow`: "ACTIVE / DISCONTINUED" | **Lệch 3 nơi**: DB default `ACTIVE` vs comment PascalCase vs Excel UPPER |
| Import `Action` (Material/Spec) | — | `New \| Update \| Unchanged \| Error` (+ `Pending` ở bundle) | PascalCase |
| `OrderMaterial.Source` | UPPER (`SUPPLIER/CUSTOMER/BOTH`) | `string?` | Computed |
| `Customer.Status` (qua Customer DTO) | — | `Active \| Discontinued` | PascalCase |
| `Component.Type` (Excel) | — | `PANEL / TRIM` | UPPER |
| `SortDir` | — | `asc` (insensitive) | khác → DESC |

### 4.3 Ma trận phân quyền chéo

| Chức năng | Admin | Manager | Accountant | WK | TeamLeader/Deputy | QaQc/Electrician | Worker | Anonymous |
|---|---|---|---|---|---|---|---|---|
| `/admin/CustomerOrders` (A1–A10) | ✅ | 403 | 403 | 403 | 403 | 403 | 403 | 401 |
| `/manager/CustomerOrders` (M1–M2) | ✅ | ✅ | 403 | 403 | 403 | 403 | 403 | 401 |
| `/manager/OrderLineTeams` (T1–T4) | ✅ | ✅ | 403 | 403 | 403 | 403 | 403 | 401 |
| `/manager/OrderMarkers` (K1–K8) | ✅ | ✅ | 403 | 403 | 403 | 403 | 403 | 401 |
| `/admin/OrderMaterials` (O1–O13) | ✅ | 403 | 403 | 403 | 403 | 403 | 403 | 401 |
| `/manager/OrderMaterials` (O1–O11, O13) | ✅ | ✅ | 403 | 403 | 403 | 403 | 403 | 401 |
| `/warehouse-keeper/OrderMaterials` (O2) | **403** | 403 | 403 | ✅ | 403 | 403 | 403 | 401 |
| `/common/OrderLookups` (L1–L4) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | **403** | 401 |

### 4.4 Điểm bất thường / lệch DTO — Entity — Comment
1. **`OrderLine` không có `Color`, `SizeCode`**; `OrderLineInput`/`OrderLineDto` có. Dữ liệu màu/size nằm ở `ProductVariant` (FK ghép `ProductVariantId + ProductId`). Seed/assert DB phải join `ProductVariant`.
2. **UNIQUE `(OrderId, ProductVariantId)`**: tạo hai `OrderLineInput` trùng ProductId+Color+SizeCode trong cùng đơn → vi phạm UNIQUE (hoặc validator chặn) — nên là 409/400, **không** được là 500.
3. **Manager vs Admin cùng tên `OrderMaterialsController`**: Manager **thiếu** `import/preview-data` (O12) so với Admin; Manager gọi → 404/405 (không phải 403).
4. **Manager `OrderLineTeams`**: comment nói "Quản đốc, Admin chia dòng" — khớp `Admin,Manager`. Comment entity: "Tổ cắt chỉ có một tổ nên không chia" → `TeamIds` truyền tổ cắt nên bị từ chối `[?]` (validator/Service).
5. **Comment WidthM** "tối đa 3,5 m" nhưng DB `decimal(4,2)` cho tới 99.99 → giới hạn 3,5 chỉ ở tầng ứng dụng; vượt 3,5 phải 400/422, **không** để DB nhận. `SaveOrderMarkerDto.WidthM` là `decimal?` nhưng comment "bắt buộc" và cột DB `WidthM` NOT NULL → thiếu `WidthM` phải bị validator chặn (400/422), không được tới DB (lỗi 500).
6. **Mismatch kiểu `IsDelayRisk`**: Entity `bool?` (computed), DTO `bool`.
7. **`OrderDetailDto` không có `FinalAmount`, `DelayRiskSince`, `CustomerDelivery*`** dù có ở Entity (phục vụ Accountant / O7). Không assert các cột này qua `GET /{id}`.
8. **`OrderMaterialDto`**: `PurchaseQty`/`Source` null-able ở DTO nhưng cột computed không bao giờ NULL; `OrderMaterialDto.ReceivedQty = CustomerReceivedQty + PurchasedReceivedQty` (comment), `ShortQty = max(Required − Received, 0)`.
9. **`ReceivedCount`, `ExpectedDelivery*`** trong `OrderMaterialDto` là dữ liệu cả đơn (lấy từ `CustomerOrder` + `DeliveryScheduleDate`), không phải theo từng `OrderMaterial`.
10. **`CustomerOrdersController` Admin**: comment "Không có xóa" → không có `DELETE`; nếu test gọi `DELETE` kỳ vọng 405.
11. **`OrderMarkers/import-data`**: thiếu `parts=markers` → trả lỗi `ErrorCodes.SpecImportInvalid` (`ResultKind.Validation`) — **trước** khi gọi Service; các bảng khác trong body bị bỏ qua dù có gửi (assert DB: ProductComponent/Norm không đổi).
12. **Upload file**: `IFormFile file` non-nullable (`[ApiController]` → model validation 400 nếu thiếu `file`) `[?]` — chưa đọc cấu hình tùy chỉnh nên xác nhận bằng test.
13. **`SetTeams` PUT**: danh sách rỗng = bỏ chia (comment DTO), khác với `AddTeams` (chỉ thêm, giữ tổ cũ).
14. **Concurrency**: A7–A10 cần `RowVer` đúng; test cần đọc `RowVer` từ DB/GET trước (lưu ý base64). `OrderLine`/`OrderMaterial` **không** có `RowVer`.
15. **`LookupDtos.cs`** (`OrderOptionDto`, `OrderLineOptionDto`, …) **không** được controller nào trong batch này tham chiếu; `OrderLookups` dùng `OrderLookupDto`, `OrderLineLookupDto`, `ProductLookupDto` (trong `OrderDtos.cs`, không có đơn giá — D26/GBR-02). Assert response L1–L4 **không** chứa `CmtUnitPrice`.
16. **GBR-02**: `M2` (Manager) không trả `CmtUnitPrice`, `TotalAmount`, `History`, `RowVer`; test cần assert các field này vắng mặt.
17. **Route case**: `[controller]` = tên class (`CustomerOrders`, `OrderLineTeams`, `OrderMarkers`, `OrderMaterials`, `OrderLookups`); ASP.NET routing không phân biệt hoa/thường.
