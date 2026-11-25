# IT_CONTRACT — Batch 6: Nhập kho NVL & thiếu giao

Nguồn: Controller, DTO, Entity, EF Configuration, base controller, `Roles`, `PageQuery`, `TextLimits`, `ApiResponses`. Không đọc Service/Repository/Validator/Program.cs/SQL Trigger-View. Mục đánh dấu `[?]` là chưa xác nhận được qua DTO/Controller/Config.

Phạm vi base controller:
| Prefix | Authorize |
|---|---|
| `/api/v1/manager/[controller]` | `Roles.AdminOrManager` = `Admin,Manager` |
| `/api/v1/warehouse-keeper/[controller]` | `Roles.WarehouseKeeper` (chỉ `WarehouseKeeper`, **không** có Admin) |
| `/api/v1/common/[controller]` | Base không có `[Authorize]`; `AttachmentsController` gắn `[Authorize]` (mọi user đã đăng nhập, không ràng buộc role) |

Response: thành công `200 { Success=true, Data, Message }`; lỗi `{ Success=false, ErrorCode, Message, Errors? }` với `Validation→400, Unauthorized→401, Forbidden→403, NotFound→404, Conflict→409`. Tên `[controller]` = tên class bỏ hậu tố `Controller` (route không phân biệt hoa/thường).

---

## 1. Bảng Endpoints

### 1.1 Manager — `MaterialReceiptsController` (Admin, Manager)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/manager/material-receipts` | Admin, Manager | Query `MaterialReceiptQuery`: `Status`, `OrderId`, `PurchaseOrderId`, `Source`, `Page`(1), `PageSize`(20, max 100), `Keyword`, `SortBy`, `SortDir` |
| GET | `/api/v1/manager/material-receipts/{id:int}` | Admin, Manager | Route `id` |
| GET | `/api/v1/manager/material-receipts/{id:int}/excel` | Admin, Manager | Route `id`; trả file `.xlsx` (lỗi → ApiFailure) |
| POST | `/api/v1/manager/material-receipts/{id:int}/confirm` | Admin, Manager | Route `id`; không body; `EmployeeId` lấy từ `ICurrentUser` |
| POST | `/api/v1/manager/material-receipts/{id:int}/return` | Admin, Manager | Route `id`; Body `ReasonDto { Reason }` |

### 1.2 Warehouse Keeper — `MaterialReceiptsController` (WarehouseKeeper)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/warehouse-keeper/material-receipts` | WarehouseKeeper | Query `MaterialReceiptQuery` (như trên) |
| GET | `/api/v1/warehouse-keeper/material-receipts/{id:int}` | WarehouseKeeper | Route `id` |
| GET | `/api/v1/warehouse-keeper/material-receipts/{id:int}/excel` | WarehouseKeeper | Route `id` |
| POST | `/api/v1/warehouse-keeper/material-receipts` | WarehouseKeeper | Body `SaveMaterialReceiptDto` |
| POST | `/api/v1/warehouse-keeper/material-receipts/supplementary` | WarehouseKeeper | Body `SaveSupplementaryReceiptDto` |
| PUT | `/api/v1/warehouse-keeper/material-receipts/{id:int}` | WarehouseKeeper | Route `id`; Body `SaveMaterialReceiptDto` |

### 1.3 Warehouse Keeper — `IncomingMaterialsController`
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/warehouse-keeper/incoming-materials` | WarehouseKeeper | Không có tham số; trả `IncomingMaterialsDto { Customer[], Supplier[], ClosedShort[] }` |

### 1.4 Manager — `ShortDeliveriesController`
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/manager/short-deliveries` | Admin, Manager | Query `ShortDeliveryQuery`: `AwaitingReplenish?`, `ResolvedOnly?` + `PageQuery` |
| GET | `/api/v1/manager/short-deliveries/{id:int}` | Admin, Manager | Route `id` (= `RequestId`) |
| POST | `/api/v1/manager/short-deliveries/{id:int}/resolve` | Admin, Manager | Route `id`; Body `ResolveShortDeliveryDto` |

### 1.5 Warehouse Keeper — `ShortDeliveriesController`
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/warehouse-keeper/short-deliveries` | WarehouseKeeper | Query `ShortDeliveryQuery`; Controller **ép** `ResolvedOnly = true` (ghi đè giá trị client) |
| GET | `/api/v1/warehouse-keeper/short-deliveries/{id:int}` | WarehouseKeeper | Route `id` |

### 1.6 Common — `AttachmentsController` (`[Authorize]`, mọi role đăng nhập)
| Method | URL | Role | Tham số |
|---|---|---|---|
| POST | `/api/v1/common/attachments` | Mọi user đã xác thực | `multipart/form-data`: Form `ownerType` (string), `ownerId` (int), `purpose` (string?, mặc định `"Photo"`), `file` (IFormFile?); `RequestSizeLimit` = 6 MB |
| GET | `/api/v1/common/attachments` | Mọi user đã xác thực | Query `ownerType`, `ownerId` |
| GET | `/api/v1/common/attachments/{id:long}/content` | Mọi user đã xác thực | Route `id`; trả file binary + header `Cache-Control: private, max-age=3600` |
| DELETE | `/api/v1/common/attachments/{id:long}` | Mọi user đã xác thực | Route `id` |

### 1.7 DTO Request

| DTO | Field | Kiểu | Ghi chú (từ summary DTO) |
|---|---|---|---|
| `SaveMaterialReceiptDto` | `OrderId` | `int?` | Source=CUSTOMER cần `OrderId` |
| | `Source` | `string` | `CUSTOMER` / `SUPPLIER` |
| | `PurchaseOrderId` | `int?` | Source=SUPPLIER cần |
| | `ReceiptDate` | `DateOnly` | |
| | `Note` | `string?` | |
| | `Lines` | `IReadOnlyList<SaveReceiptLineDto>` | |
| `SaveReceiptLineDto` | `OrderMaterialId` | `int?` | Khách cấp |
| | `PurchaseOrderLineId` | `int?` | Nhà cung cấp |
| | `AcceptedQty` | `decimal` | |
| `SaveSupplementaryReceiptDto` | `ShortageRequestId` | `int` | |
| | `ReceiptDate` | `DateOnly` | |
| | `Note` | `string?` | |
| | `Lines` | `IReadOnlyList<SupplementaryLineDto>` | |
| `SupplementaryLineDto` | `RequestLineId` | `int` | |
| | `AcceptedQty` | `decimal` | |
| `ReasonDto` | `Reason` | `string` | Giới hạn `TextLimits.Reason` = 500 (DB `ReturnReason` NVARCHAR(500)) |
| `ResolveShortDeliveryDto` | `Resolution` | `string` | `REPLENISH` / `NO_REPLENISH` |
| | `Lines` | `IReadOnlyList<ReplenishInputDto>?` | |
| | `Note` | `string?` | |
| | `ExpectedDeliveryCount` | `short?` | Giao bù: bắt buộc, 1–99 (theo comment) |
| | `ExpectedDeliveryDates` | `IReadOnlyList<DateOnly>?` | Đúng `ExpectedDeliveryCount` ngày |
| `ReplenishInputDto` | `RequestLineId` | `int` | |
| | `ReplenishQty` | `decimal` | |

### 1.8 DTO Response (rút gọn)
- `MaterialReceiptListItemDto`: `MaterialReceiptId, ReceiptCode, ReceiptType, OrderId?, OrderCode?, Source, PurchaseOrderId?, PoCode?, ReceiptDate, Status, SubmittedAt, ConfirmedAt?, ConfirmedByName?, LineCount`.
- `MaterialReceiptDto`: thêm `OriginalReceiptId?, OriginalReceiptCode?, ShortageRequestId?, ShortDeliveryRequestId?, ReturnReason?, Note?, Lines[]`.
- `MaterialReceiptLineDto`: `MaterialReceiptLineId, OrderMaterialId?, PurchaseOrderLineId?, OriginalReceiptLineId?, MaterialId, MaterialCode, MaterialName, Unit, MaterialColor, UnitPrice?, ToReplenishQty?, AcceptedQty, DeliveryNo?, ExpectedDeliveryCount?`.
- `ShortDeliveryListItemDto`: `RequestId, RequestCode, Status, Resolution?, ReceiptId, ReceiptCode, Source, PoCode?, OrderCode?, CreatedAt`.
- `ShortDeliveryDto`: thêm `ResultNote?, Lines[], ExpectedDeliveryCount?(short), ReplenishDeliveryCount(0), ExpectedDeliveryDate?, ExpectedDeliveryDateTo?, ExpectedDeliveryDates?`.
- `ShortDeliveryLineDto`: `RequestLineId, MaterialReceiptLineId?, MaterialId, MaterialCode, MaterialName, Unit, MaterialColor, ShortfallQty, ReplenishQty?, ReplenishedQty`.
- `IncomingCustomerMaterialDto`, `IncomingSupplierMaterialDto`, `ClosedShortPurchaseDto`: xem `IncomingMaterialDtos.cs`.
- `AttachmentDto`: `AttachmentId(long), OwnerType, OwnerId, Purpose, FileName?, ContentType?, UploadedAt`.

---

## 2. Ràng buộc dữ liệu (Input & DB Schema)

### 2.1 Input (Query/Form)
| Trường | Ràng buộc đọc được |
|---|---|
| `PageQuery.Page` | mặc định 1; `SafePage` ép ≥ 1 |
| `PageQuery.PageSize` | mặc định 20; tối đa 100 (`SafePageSize`); `PageSize<1` → 20 |
| `PageQuery.SortDir` | khác `asc` (không phân biệt hoa/thường) ⇒ DESC |
| `PageQuery.Keyword` | `[?]` Validator quy định tối đa 100 ký tự (chỉ có trong `PageQueryValidatorBase`, không phải Controller/DTO) |
| `ReasonDto.Reason` | `TextLimits.Reason` = 500 |
| Attachment upload | file ≤ 5 MB (comment `FileUpload`), request ≤ 6 MB (`RequestSizeLimit`); `file == null` → content rỗng + `FileName=""` rồi giao Service xử lý |
| `ExpectedDeliveryCount` | `short?`, 1–99 (comment) |

### 2.2 DB — `MaterialReceipt`
| Cột | Kiểu | NULL | Mặc định / Ghi chú |
|---|---|---|---|
| MaterialReceiptId | int | NOT NULL | PK |
| ReceiptCode | varchar(30) | NOT NULL | **UNIQUE** `UQ_MaterialReceipt_Code` |
| ShortageRequestId | int | NULL | FK `FK_MR_ShortageRequest` → Request |
| OrderId | int | NULL | FK `FK_MR_Order` → CustomerOrder |
| Source | varchar(10) | NULL (entity `string?`) | **Computed persisted**: `PurchaseOrderId IS NULL → 'CUSTOMER'` else `'SUPPLIER'` — không ghi được |
| PurchaseOrderId | int | NULL | FK `FK_MR_PO` → PurchaseOrder |
| ReceiptDate | date | NOT NULL | |
| Status | varchar(20) | NOT NULL | default `'PENDING'` |
| ReturnReason | nvarchar(500) | NULL | |
| Note | nvarchar(1000) | NULL | |
| SubmittedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |
| ConfirmedBy | int | NULL | FK `FK_MR_ConfirmedBy` → Employee |
| ConfirmedAt | datetime2(0) | NULL | |
| RowVer | rowversion | NOT NULL | concurrency token |
- Index: `IX_MR_Order (OrderId, Status)`.

### 2.3 DB — `MaterialReceiptLine`
| Cột | Kiểu | NULL | Ghi chú |
|---|---|---|---|
| MaterialReceiptLineId | int | NOT NULL | PK |
| MaterialReceiptId | int | NOT NULL | FK `FK_MRL_Receipt` (ClientSetNull) |
| OrderMaterialId | int | NULL | FK `FK_MRL_OrderMaterial` |
| PurchaseOrderLineId | int | NULL | FK `FK_MRL_POL` |
| OriginalReceiptLineId | int | NULL | FK tự tham chiếu `FK_MRL_OrigLine` |
| AcceptedQty | decimal(18,3) | NOT NULL | |
| MaterialId | int | NULL | FK `FK_MRL_Material` |
- **UNIQUE** `UQ_MRL (MaterialReceiptId, OrderMaterialId)` WHERE `OrderMaterialId IS NOT NULL`.
- **UNIQUE** `UQ_MRL_Stock (MaterialReceiptId, MaterialId)` WHERE `OrderMaterialId IS NULL`.
- Trigger: `trg_MaterialReceiptLine_Check`.

### 2.4 DB — `StockBalance`
| Cột | Kiểu | NULL | Ghi chú |
|---|---|---|---|
| StockBalanceId | int | NOT NULL | PK |
| WarehouseType | varchar(15) | NOT NULL | giá trị `[?]` |
| MaterialId | int | NULL | FK `FK_SB_Material` |
| OrderLineId | int | NULL | FK `FK_SB_OrderLine` |
| ProductComponentMaterialId | int | NULL | FK `FK_SB_PCM` |
| OnHandQty | decimal(18,3) | NOT NULL | |
| UpdatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |
- **UNIQUE** `UQ_SB_Item (WarehouseType, MaterialId, OrderLineId, ProductComponentMaterialId)` (không filter).
- Trigger: `trg_StockBalance_Check`.

### 2.5 DB — `StockTransaction`
| Cột | Kiểu | NULL | Ghi chú |
|---|---|---|---|
| StockTransactionId | bigint | NOT NULL | PK |
| StockBalanceId | int | NOT NULL | FK `FK_ST_Balance` (ClientSetNull) |
| SourceDocType | varchar(30) | NOT NULL | giá trị `[?]` |
| SourceDocId | int | NOT NULL | |
| SourceLineId | int | NULL | |
| QtyChange | decimal(18,3) | NOT NULL | |
| BalanceAfter | decimal(18,3) | NOT NULL | |
| TransactionDate | date | NOT NULL | |
| CreatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |
- **UNIQUE** `UQ_ST_SourceLine (SourceDocType, SourceDocId, SourceLineId, StockBalanceId)`.
- Trigger: `trg_StockTransaction_AppendOnly`, `trg_StockTransaction_PostOnly` (chỉ ghi qua `usp_Stock_Post`, không UPDATE/DELETE).

### 2.6 DB — `Attachment`
| Cột | Kiểu | NULL | Ghi chú |
|---|---|---|---|
| AttachmentId | bigint | NOT NULL | PK |
| OwnerType | varchar(30) | NOT NULL | |
| OwnerId | int | NOT NULL | không có FK (đa hình) |
| Purpose | varchar(30) | NOT NULL | default `'PHOTO'` |
| FileUrl | nvarchar(500) | NOT NULL | |
| FileName | nvarchar(255) | NULL | |
| ContentType | varchar(100) | NULL | |
| IsCurrent | bit | NOT NULL | default `1` |
| RemovedAt | datetime2(0) | NULL | gỡ ảnh ⇒ `IsCurrent=0`; job dọn xóa sau 15 ngày |
| UploadedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |
- Index `IX_Attachment_Owner (OwnerType, OwnerId)`; không UNIQUE.

### 2.7 DB — `OrderMaterial`
| Cột | Kiểu | NULL | Ghi chú |
|---|---|---|---|
| OrderMaterialId | int | NOT NULL | PK |
| OrderId | int | NOT NULL | FK `FK_OM_Order` |
| MaterialId | int | NOT NULL | FK `FK_OM_Material` |
| RequiredQty | decimal(18,3) | NOT NULL | |
| CustomerQty | decimal(18,3) | NOT NULL | |
| PurchaseQty | decimal(19,3) | NULL | **Computed**: `RequiredQty>CustomerQty ? RequiredQty-CustomerQty : 0` |
| Source | varchar(10) | NULL | **Computed**: `CustomerQty=0→SUPPLIER`; `CustomerQty>=RequiredQty→CUSTOMER`; else `BOTH` |
| Note | nvarchar(500) | NULL | |
- **UNIQUE** `UQ_OM (OrderId, MaterialId)`.

### 2.8 DB — `Request` (phiếu SHORT_DELIVERY) & `RequestLine`
`Request`: `RequestId` PK; `RequestCode` varchar(30) NOT NULL **UNIQUE** `UQ_Request_Code`; `RequestTypeCode` varchar(30) NOT NULL; `Status` varchar(20) NOT NULL (không default); `MaterialReceiptId` int NULL (FK `FK_Request_MaterialReceipt`, 1-1); `OrderId` int NULL; `TargetDate`/`TargetDateTo` date NULL; `ExpectedDeliveryCount` smallint NULL; `Title` nvarchar(200) NULL; `Content` nvarchar(2000) NULL; `DecisionNote`/`ResultNote` nvarchar(1000) NULL; `ShortageResolution` varchar(20) NULL; `CreatedAt` datetime2(0) NOT NULL default `fn_VnNow()`; `DecidedAt/CompletedAt/UpdatedAt` datetime2(0) NULL; `RowVer` rowversion NOT NULL.
- **UNIQUE** `UX_Request_Receipt (MaterialReceiptId)` WHERE `MaterialReceiptId IS NOT NULL AND RequestTypeCode='SHORT_DELIVERY'` ⇒ mỗi phiếu nhập gốc tối đa 1 yêu cầu thiếu giao.
- **UNIQUE** `UX_Request_Defect (DefectRecordId)` WHERE `DefectRecordId IS NOT NULL` (không thuộc batch).

`RequestLine`: `RequestLineId` PK; `RequestId` int NOT NULL (FK ClientSetNull); `Quantity` decimal(18,3) NOT NULL; `ReplenishQty` decimal(18,3) NULL; `MaterialId`, `MaterialReceiptLineId`, `PurchaseOrderLineId`, `OrderMaterialId` int NULL; `ItemDescription` nvarchar(300) NULL. Trigger `trg_RequestLine_Check`.
- `[?]` Config `RequestLineConfiguration` không khai báo FK cho `PurchaseOrderLineId` / `OrderMaterialId` (chỉ có property trong entity).

---

## 3. Bảng DB chịu tác động (khi thành công)

> Dựa trên DTO/Controller/Config/Comment; không đọc Service nên mọi ghi nhận phía Service đánh `[?]`.

| Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|
| GET (tất cả endpoint GET, kể cả `/excel`, `/content`, IncomingMaterials) | — | — | — |
| POST WK `MaterialReceipts` | `MaterialReceipt` (Status mặc định `PENDING`, ReceiptCode sinh `[?]`), `MaterialReceiptLine` | — | — |
| POST WK `MaterialReceipts/supplementary` | `MaterialReceipt` (có `ShortageRequestId`, `OriginalReceiptId`[?]), `MaterialReceiptLine` (có `OriginalReceiptLineId`[?]) | `[?]` | — |
| PUT WK `MaterialReceipts/{id}` | `MaterialReceiptLine` mới `[?]` | `MaterialReceipt` (ReceiptDate, Note, Order/PO, RowVer; trạng thái `[?]`), `MaterialReceiptLine` | `MaterialReceiptLine` cũ `[?]` |
| POST Manager `{id}/confirm` | `StockTransaction` (qua `usp_Stock_Post`) `[?]`, `StockBalance` (nếu chưa có) `[?]`, `Request`/`RequestLine` SHORT_DELIVERY khi nhập thiếu `[?]` (DTO có `ShortDeliveryRequestId`) | `MaterialReceipt` (`Status`, `ConfirmedBy`, `ConfirmedAt`, `RowVer`), `StockBalance.OnHandQty/UpdatedAt` (comment UC39 "tăng tồn NVL"), `Request`/`RequestLine` (phiếu bổ sung xác nhận → `ReplenishedQty`) `[?]` | — |
| POST Manager `{id}/return` | — | `MaterialReceipt` (`Status`, `ReturnReason`, `RowVer`) | — |
| POST Manager `ShortDeliveries/{id}/resolve` | `[?]` | `Request` (`Status`, `ShortageResolution`, `ExpectedDeliveryCount`, `TargetDate`, `TargetDateTo`, `ResultNote`, `DecidedAt`/`CompletedAt`, `RowVer`), `RequestLine.ReplenishQty` | — |
| POST Common `Attachments` | `Attachment` (file lưu đĩa/storage `[?]`) | `Attachment` bản cũ cùng owner/purpose → `IsCurrent=0, RemovedAt` `[?]` | — |
| DELETE Common `Attachments/{id}` | — | `Attachment.IsCurrent=0`, `RemovedAt` (soft delete theo comment entity) `[?]` | Xóa cứng chỉ do job dọn sau 15 ngày |

---

## 4. Lưu ý kỹ thuật cho QA

**Trigger/Constraint DB (chỉ tên, chưa đọc nội dung)**
- `trg_MaterialReceiptLine_Check`, `trg_StockBalance_Check`, `trg_RequestLine_Check`: có thể phát sinh lỗi DB (→ 500 nếu không được map) khi dữ liệu sai; test phải khóa kỳ vọng theo Assert-Locking, không đổi kỳ vọng để ép Pass.
- `trg_StockTransaction_AppendOnly` + `PostOnly`: **không thể seed/rollback bằng INSERT/UPDATE/DELETE trực tiếp** vào `StockTransaction`; seed tồn qua `StockBalance` hoặc `usp_Stock_Post`. Dọn dữ liệu nên dùng Transaction Rollback/Reset DB.
- `UQ_ST_SourceLine`: confirm lặp cùng phiếu không được post trùng thẻ kho.
- `UQ_MRL`, `UQ_MRL_Stock`: trùng `OrderMaterialId` (hoặc `MaterialId` khi `OrderMaterialId IS NULL`) trong cùng phiếu bị DB chặn.
- `UX_Request_Receipt`: mỗi phiếu nhập gốc chỉ 1 `SHORT_DELIVERY`.

**Hoa/thường & giá trị trạng thái**
- DB `MaterialReceipt.Status` default `'PENDING'` (HOA, varchar); DTO `Status` là `string` truyền nguyên; `Source` computed `CUSTOMER`/`SUPPLIER` (HOA) và DTO/query `Source` dùng cùng HOA. Danh sách đầy đủ Status (`CONFIRMED`, `RETURNED`…) `[?]` vì không đọc file hằng số.
- `Resolution`: DTO/DB `ShortageResolution` dùng `REPLENISH` / `NO_REPLENISH` (HOA, varchar(20)).
- `ReceiptType` trong DTO là chuỗi dạng "Mới/Bổ sung" (comment `New`/`Supplementary`) — **không phải cột DB**; suy ra từ `ShortageRequestId IS NOT NULL`.
- `Attachment.Purpose`: DB default `'PHOTO'` (HOA) nhưng Controller default `"Photo"` (Pascal) ⇒ lệch; kiểm tra giá trị thực lưu và chuẩn hóa ở Service `[?]`. Cột varchar(30), collation mặc định thường không phân biệt hoa/thường nhưng cần xác nhận.

**Ma trận phân quyền chéo**
| Endpoint nhóm | Admin | Manager | WarehouseKeeper | Role khác | Chưa đăng nhập |
|---|---|---|---|---|---|
| `/manager/material-receipts*`, `/manager/short-deliveries*` | 200 | 200 | 403 | 403 | 401 |
| `/warehouse-keeper/material-receipts*`, `IncomingMaterials`, `ShortDeliveries` | **403** | **403** | 200 | 403 | 401 |
| `/common/attachments*` | 200 | 200 | 200 | 200 (kể cả Worker) | 401 |
- Admin được vào prefix `manager` nhưng **không** vào `warehouse-keeper`.
- Tạo/sửa phiếu chỉ WK; xác nhận/trả chỉ Admin/Manager. WK không có endpoint confirm/return/resolve.
- Attachments không kiểm role ở Controller ⇒ IDOR/quyền theo `ownerType/ownerId` nếu có phải kiểm ở Service `[?]`.

**Điểm bất thường / cần lưu ý**
1. WK `ShortDeliveries` luôn `ResolvedOnly=true`; Manager `ShortDeliveries` không ép ⇒ cùng `id` WK có thể nhận 404 với yêu cầu chưa xử lý nếu Service áp bộ lọc ở `GetByIdAsync` `[?]` (Controller WK `GetById` không ép gì).
2. Entity `MaterialReceipt.Source` là `string?` và `MaterialReceiptLine.MaterialId` là `int?` (nullable ở DB) trong khi `MaterialReceiptLineDto.MaterialId` là `int` non-null ⇒ dòng có `MaterialId NULL` có thể làm DTO lỗi/ lấy từ OrderMaterial/PO line `[?]`.
3. `MaterialReceipt` có hai nav tới Request: `Request` (1-1 qua `Request.MaterialReceiptId` = yêu cầu thiếu giao sinh từ phiếu) và `ShortageRequest` (qua `ShortageRequestId` = yêu cầu mà phiếu bổ sung trả lời) ⇒ DTO tương ứng `ShortDeliveryRequestId` / `ShortageRequestId`.
4. `OrderMaterial.Source`, `PurchaseQty` và `MaterialReceipt.Source` là cột computed: seed bằng EF/INSERT **không được set** các cột này (sẽ lỗi), chỉ set `RequiredQty`/`CustomerQty` và `PurchaseOrderId`.
5. `StockBalance` unique `UQ_SB_Item` không filter: các cột nullable trong unique index SQL Server coi `NULL` bằng nhau ⇒ chỉ 1 dòng cho mỗi tổ hợp (WarehouseType, MaterialId, NULL, NULL).
6. `Attachment.OwnerId` không có FK; `AttachmentDto` không trả `FileUrl`; nội dung lấy qua `/content`. `file == null` không bị Controller chặn (400 do Service/Validator `[?]`).
7. `ExpectedDeliveryCount` DTO là `short?` nhưng `ShortDeliveryDto.ExpectedDeliveryCount` lưu "cộng dồn" nhiều lần chọn giao bù (comment) ⇒ không phải giá trị gửi lên.
8. `PageQuery` không có Validator trong Controller; `Page`/`PageSize` sai thường được clamp (`Safe*`) hoặc 400 từ `PageQueryValidatorBase` (không đọc nội dung ở batch này nhưng file Validator đã tồn tại) `[?]`.
9. File Excel (`/excel`): trả `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; không phải JSON ⇒ Assert kép: kiểm Content-Type + dữ liệu DB, không parse JSON.
10. `Roles.Handlers` / `StaffExceptWorker` không áp dụng cho batch này (Attachments chỉ `[Authorize]`).
- Route thực tế: tên controller được chuyển sang kebab-case (đã xác nhận qua IT_B6 Round 1: PascalCase cũ trả 404).
