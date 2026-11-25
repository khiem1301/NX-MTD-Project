# IT_CONTRACT — Batch 7: Lệnh cắt & bàn cắt & bàn giao BTP

Nguồn: Controller, DTO, Entity, EF Configuration (`WorkOrder`, `WorkOrderLine`, `CuttingLay`, `CuttingLayLine`, `CuttingLayFabric`, `CutHandover`, `CutHandoverLine`), 3 base controller, `CuttingTeamOnlyAttribute`, `Roles`, `PageQuery`, `TextLimits`, `WarehouseConstants` (chỉ các hằng Status/TeamTypes/ShiftCodes). Không đọc Service/Repository/Validator/Program.cs/SQL Trigger-View. Mục `[?]` = chưa xác nhận được qua Controller/DTO/Config.

Phạm vi base controller:
| Prefix | Authorize | Filter thêm |
|---|---|---|
| `/api/v1/manager/[controller]` | `Admin,Manager` | — |
| `/api/v1/team-leader/[controller]` | `TeamLeader,TeamDeputy` | `[CuttingTeamOnly]` trên cả 3 controller của batch này |
| `/api/v1/warehouse-keeper/[controller]` | `WarehouseKeeper` (không có Admin) | — |

Response: thành công `200 { Success=true, Data, Message }`; lỗi `{ Success=false, ErrorCode, Message, Errors? }` (`400/401/403/404/409` theo `ApiResponses`, xem Batch 6). Segment `[controller]` giả định dạng kebab-case như Batch 6 (`work-orders`, `cutting-lays`, `cut-handovers`) `[?]` — cấu hình slugify nằm ở `Program.cs` nên không xác nhận được ở đây.

`[CuttingTeamOnly]`: nếu `ICurrentUser.TeamTypeCode != "CUTTING"` → **403** với `ErrorCode = ErrorCodes.WarehouseCuttingTeamOnly`, message `WarehouseMessages.CuttingTeamOnly`. Chạy sau `[Authorize]` (role sai → 401/403 mặc định trước).

---

## 1. Bảng Endpoints

`PageQuery` chung: `Page`(1), `PageSize`(20, tối đa 100; <1 → 20), `Keyword`, `SortBy`, `SortDir` (`asc` → tăng dần, mọi giá trị khác/null → giảm dần).

### 1.1 Manager — `WorkOrdersController` (Admin, Manager)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/manager/work-orders` | Admin, Manager | Query `WorkOrderQuery`: `Status?`, `OrderId?` + `PageQuery`; service nhận `teamId = null` (thấy mọi tổ) |
| GET | `/api/v1/manager/work-orders/{id:int}` | Admin, Manager | Route `id` |
| POST | `/api/v1/manager/work-orders` | Admin, Manager | Body `SaveWorkOrderDto` |
| POST | `/api/v1/manager/work-orders/fabric-check` | Admin, Manager | Body `CheckWorkOrderFabricDto` (chỉ cảnh báo, không ghi DB) |
| GET | `/api/v1/manager/work-orders/replacement-requests` | Admin, Manager | Không tham số; trả `ReplacementRequestDto[]` |
| POST | `/api/v1/manager/work-orders/replacement` | Admin, Manager | Body `CreateReplacementWorkOrderDto` |
| PUT | `/api/v1/manager/work-orders/{id:int}` | Admin, Manager | Route `id`; Body `SaveWorkOrderDto` |
| POST | `/api/v1/manager/work-orders/{id:int}/close-short` | Admin, Manager | Route `id`; Body `CloseShortWorkOrderDto { Reason }` |
| POST | `/api/v1/manager/work-orders/{id:int}/cancel` | Admin, Manager | Route `id`; không body |

### 1.2 Team Leader — `WorkOrdersController` (TeamLeader, TeamDeputy + CUTTING)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/team-leader/work-orders` | TeamLeader, TeamDeputy (tổ CUTTING) | Query `WorkOrderQuery`; `teamId = ICurrentUser.TeamId ?? 0` |
| GET | `/api/v1/team-leader/work-orders/{id:int}` | như trên | Route `id`; lọc theo `teamId` |
| GET | `/api/v1/team-leader/work-orders/{id:int}/material-allowance` | như trên | Route `id`; Query `exceptRequestId?` (int) → `WorkOrderMaterialAllowanceDto[]` |

### 1.3 Warehouse Keeper — `WorkOrdersController` (WarehouseKeeper)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/warehouse-keeper/work-orders` | WarehouseKeeper | Query `WorkOrderQuery`; `teamId = null` |
| GET | `/api/v1/warehouse-keeper/work-orders/{id:int}` | WarehouseKeeper | Route `id` |
| GET | `/api/v1/warehouse-keeper/work-orders/{id:int}/fabrics` | WarehouseKeeper | Route `id` → `FabricRequirementDto[]` |
| GET | `/api/v1/warehouse-keeper/work-orders/{id:int}/fabric-needs` | WarehouseKeeper | Route `id`; Query `color?`, `sizeCode?` → `FabricNeedDto[]` |

### 1.4 Team Leader — `CuttingLaysController` (CUTTING)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/team-leader/cutting-lays` | TeamLeader, TeamDeputy (tổ CUTTING) | Query `CuttingLayQuery`: `WorkOrderId?`, `WorkDate?` (DateOnly), `Status?` + `PageQuery` |
| GET | `/api/v1/team-leader/cutting-lays/{id:int}` | như trên | Route `id` |
| POST | `/api/v1/team-leader/cutting-lays` | như trên | Body `SaveCuttingLayDto` |
| PUT | `/api/v1/team-leader/cutting-lays/{id:int}` | như trên | Route `id`; Body `SaveCuttingLayDto` |

### 1.5 Team Leader — `CutHandoversController` (CUTTING)
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/team-leader/cut-handovers` | TeamLeader, TeamDeputy (tổ CUTTING) | Query `CutHandoverQuery`: `Status?`, `WorkOrderId?` + `PageQuery` |
| GET | `/api/v1/team-leader/cut-handovers/{id:int}` | như trên | Route `id` |
| GET | `/api/v1/team-leader/cut-handovers/handoverable` | như trên | Query `workOrderId` (int, không nullable → thiếu = 0) → `HandoverableLineDto[]` |
| POST | `/api/v1/team-leader/cut-handovers` | như trên | Body `CreateCutHandoverDto` |
| POST | `/api/v1/team-leader/cut-handovers/{id:int}/cancel` | như trên | Route `id`; không body |

### 1.6 Warehouse Keeper — `CutHandoversController`
| Method | URL | Role | Tham số |
|---|---|---|---|
| GET | `/api/v1/warehouse-keeper/cut-handovers` | WarehouseKeeper | Query `CutHandoverQuery`; `teamId = null` |
| GET | `/api/v1/warehouse-keeper/cut-handovers/{id:int}` | WarehouseKeeper | Route `id` |
| POST | `/api/v1/warehouse-keeper/cut-handovers/{id:int}/confirm` | WarehouseKeeper | Route `id`; Body `ConfirmCutHandoverDto`; `EmployeeId` từ `ICurrentUser` |
| POST | `/api/v1/warehouse-keeper/cut-handovers/{id:int}/reject` | WarehouseKeeper | Route `id`; Body `ReasonDto { Reason }` |

---

## 2. Ràng buộc dữ liệu

### 2.1 DB Schema (EF Configuration + Entity)
Quy ước: kiểu tham chiếu không `?` (`= null!`) và kiểu giá trị không `?` = NOT NULL; có `?` = NULLABLE. Cột không có config riêng giữ mặc định EF (`int` → `int`, `DateOnly` → `date`, `short` → `smallint`) `[?]` kiểu SQL chính xác.

**WorkOrder** (`Lệnh cắt`)
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| WorkOrderId | int | NOT NULL | PK |
| WorkOrderCode | varchar(30), non-Unicode | NOT NULL | **UNIQUE** `UQ_WorkOrder_Code` (không filter) |
| OrderId | int | NOT NULL | FK `FK_WO_Order` → CustomerOrder |
| ProductId | int | NOT NULL | FK `FK_WO_Product` → Product |
| CuttingTeamId | int | NOT NULL | FK `FK_WO_Team` → Team |
| SourceRequestId | int | NULL | FK `FK_WO_Request` → Request (lệnh cắt bù) |
| DueDate | DateOnly (date) | NOT NULL | — |
| Status | varchar(20) | NOT NULL | Default `'OPEN'`; giá trị: `OPEN`, `CUTTING`, `COMPLETED`, `CANCELLED` |
| Note | nvarchar(1000) | NULL | — |
| FabricTolerancePercent | decimal(5,2) | NOT NULL | Default DB `[?]` (CLR = 0) |
| CreatedAt | datetime2(0) | NOT NULL | Default `dbo.fn_VnNow()` |
| CompletedAt | datetime2(0) | NULL | — |

**WorkOrderLine** — trigger `trg_WorkOrderLine_Check`
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| WorkOrderLineId | int | NOT NULL | PK |
| WorkOrderId | int | NOT NULL | FK `FK_WOL_WO` |
| OrderLineId | int | NOT NULL | FK `FK_WOL_OrderLine` |
| ProductComponentMaterialId | int | NULL | FK `FK_WOL_PCM` (NULL = cả bộ) |
| PlannedQty | int | NOT NULL | Không có CHECK trong config `[?]` |
| — | — | — | **UNIQUE** `UQ_WOL` (WorkOrderId, OrderLineId, ProductComponentMaterialId); không filter ⇒ SQL Server coi các NULL là bằng nhau: chỉ 1 dòng "cả bộ" cho mỗi (WO, OrderLine) |

**CuttingLay**
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| CuttingLayId | int | NOT NULL | PK |
| WorkOrderId | int | NOT NULL | FK `FK_CL_WO` |
| CuttingTeamId | int | NOT NULL | FK `FK_CL_Team` |
| WorkDate | DateOnly (date) | NOT NULL | — |
| ShiftCode | varchar(10) | NOT NULL | `MORNING` / `AFTERNOON` / `OVERTIME` (`ShiftCodes.All`) |
| LaySequence | smallint | NOT NULL | — |
| Note | nvarchar(500) | NULL | — |
| Status | varchar(10) | NOT NULL | Default `'RECORDED'`; giá trị `RECORDED`, `LOCKED` |
| CreatedAt | datetime2(0) | NOT NULL | Default `dbo.fn_VnNow()` |
| — | — | — | **UNIQUE** `UQ_CL` (CuttingTeamId, WorkDate, ShiftCode, LaySequence) |

**CuttingLayLine** — trigger `trg_CuttingLayLine_Check`
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| CuttingLayLineId | int | NOT NULL | PK |
| CuttingLayId | int | NOT NULL | FK `FK_CLL_Lay` |
| OrderLineId | int | NOT NULL | FK `FK_CLL_OrderLine` |
| ProductComponentMaterialId | int | NULL | FK `FK_CLL_PCM` |
| ActualQty | int | NOT NULL | — |
| — | — | — | **UNIQUE** `UQ_CLL` (CuttingLayId, OrderLineId, ProductComponentMaterialId) |

**CuttingLayFabric**
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| CuttingLayFabricId | int | NOT NULL | PK |
| CuttingLayId | int | NOT NULL | FK `FK_CLF_Lay` |
| MaterialId | int | NOT NULL | FK `FK_CLF_Material` |
| UsedM | decimal(9,2) | NULL | NULL = bàn cũ chưa có chiều dài |
| — | — | — | **UNIQUE** `UQ_CLF` (CuttingLayId, MaterialId) |

**CutHandover**
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| CutHandoverId | int | NOT NULL | PK |
| HandoverCode | varchar(30) | NOT NULL | **UNIQUE** `UQ_CutHandover_Code` |
| WorkOrderId | int | NOT NULL | FK `FK_CH_WO` |
| HandedAt | datetime2(0) | NOT NULL | Default `dbo.fn_VnNow()` |
| Status | varchar(20) | NOT NULL | Default `'PENDING'`; giá trị `PENDING`, `CONFIRMED`, `REJECTED`, `CANCELLED` |
| ConfirmedBy | int | NULL | FK `FK_CH_ConfirmedBy` → Employee |
| ConfirmedAt | datetime2(0) | NULL | — |
| RejectReason | nvarchar(500) (`TextLimits.Reason`) | NULL | — |

**CutHandoverLine** — trigger `trg_CutHandoverLine_Check`
| Cột | Kiểu | Null | Ràng buộc |
|---|---|---|---|
| CutHandoverLineId | int | NOT NULL | PK |
| CutHandoverId | int | NOT NULL | FK `FK_CHL_Handover` |
| OrderLineId | int | NOT NULL | FK `FK_CHL_OrderLine` |
| ProductComponentMaterialId | int | NULL | FK `FK_CHL_PCM` |
| HandedQty | int | NOT NULL | — |
| ConfirmedQty | int | NULL | NULL đến khi kho xác nhận |
| DiscrepancyReason | nvarchar(500) (`TextLimits.Reason`) | NULL | — |
| — | — | — | **UNIQUE** `UQ_CHL` (CutHandoverId, OrderLineId, ProductComponentMaterialId) |

Mọi FK cấu hình `OnDelete(ClientSetNull)` (EF không cascade; DB-level `NO ACTION` `[?]`), riêng `FK_WO_Request`, `FK_WOL_PCM`, `FK_CLL_PCM`, `FK_CHL_PCM`, `FK_CH_ConfirmedBy` dùng mặc định EF cho FK optional.

### 2.2 Input DTO
Không đọc Validator ⇒ chỉ ghi kiểu/nullability từ DTO; giới hạn độ dài/khoảng giá trị `[?]`.

| DTO | Trường (kiểu, null) |
|---|---|
| `SaveWorkOrderDto` | `OrderId` int, `ProductId` int, `CuttingTeamId` int, `DueDate` DateOnly, `Note` string?, `Lines` `SaveWorkOrderLineDto[]`, `FabricTolerancePercent` decimal = 0 (DB decimal(5,2) ⇒ tối đa 999.99) |
| `SaveWorkOrderLineDto` | `OrderLineId` int, `PlannedQty` int, `ProductComponentMaterialId` int? = null (null = cả bộ; có giá trị = cắt lẻ, không tính vào trần số đặt) |
| `CheckWorkOrderFabricDto` | `OrderId` int, `CuttingTeamId` int, `Lines` `SaveWorkOrderLineDto[]` |
| `CloseShortWorkOrderDto` | `Reason` string (bắt buộc theo comment; **không có cột lưu Reason trong `WorkOrder`**) |
| `CreateReplacementWorkOrderDto` | `RequestId` int, `CuttingTeamId` int, `DueDate` DateOnly, `Note` string? |
| `SaveCuttingLayDto` | `WorkOrderId` int, `WorkDate` DateOnly, `ShiftCode` string, `Fabrics` `SaveLayFabricDto[]`, `Note` string?, `Lines` `SaveLayLineDto[]` |
| `SaveLayFabricDto` | `MaterialId` int, `UsedM` decimal? (DB decimal(9,2)) |
| `SaveLayLineDto` | `OrderLineId` int, `ProductComponentMaterialId` int?, `ActualQty` int |
| `CreateCutHandoverDto` | `WorkOrderId` int, `Lines` `CreateCutHandoverLineDto[]` |
| `CreateCutHandoverLineDto` | `OrderLineId` int, `ProductComponentMaterialId` int?, `HandedQty` int |
| `ConfirmCutHandoverDto` | `Lines` `ConfirmHandoverLineDto[]` |
| `ConfirmHandoverLineDto` | `CutHandoverLineId` int, `ConfirmedQty` int, `DiscrepancyReason` string? (≤ 500 theo DB) |
| `ReasonDto` (reject) | `Reason` string (RejectReason ≤ 500 theo DB) |
| Query | `WorkOrderQuery{Status?,OrderId?}`, `CuttingLayQuery{WorkOrderId?,WorkDate?,Status?}`, `CutHandoverQuery{Status?,WorkOrderId?}` — đều kế thừa `PageQuery` |

### 2.3 Output DTO (suy ra từ Entity)
- `WorkOrderDto`: `WorkOrderCode`, `OrderCode`, `ProductCode`, `CuttingTeamCode`, `Lines`, `Fabrics`, `FabricChecks` là dữ liệu join/tính toán, không phải cột của `WorkOrder`.
- `WorkOrderLineDto.CutQty/HandedQty/ComponentName/Color/SizeCode/FabricMaterialIds`: tính toán/join `[?]` nguồn (có View `VwWorkOrderLineProgress` — chỉ biết qua tên file config).
- `CutHandoverDto.CuttingTeamId/CuttingTeamCode/OrderCode/ConfirmedByName`: join qua `WorkOrder`/`Employee`; `CutHandover` không có cột `CuttingTeamId`.

---

## 3. Bảng DB chịu tác động (khi thành công)

| Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|
| POST `manager/work-orders` | `WorkOrder`, `WorkOrderLine` | `[?]` | — |
| POST `manager/work-orders/fabric-check` | — | — | — (chỉ đọc) |
| POST `manager/work-orders/replacement` | `WorkOrder` (`SourceRequestId` = `RequestId`), `WorkOrderLine` | `[?]` `Request` (đánh dấu đã có lệnh) | — |
| PUT `manager/work-orders/{id}` | `[?]` `WorkOrderLine` mới | `WorkOrder`; `[?]` `WorkOrderLine` | `[?]` `WorkOrderLine` bị bỏ |
| POST `manager/work-orders/{id}/close-short` | — | `WorkOrder` (`Status`, `CompletedAt` `[?]`) | — |
| POST `manager/work-orders/{id}/cancel` | — | `WorkOrder.Status = CANCELLED` | — |
| GET (mọi endpoint GET của §1.1–1.3) | — | — | — |
| POST `team-leader/cutting-lays` | `CuttingLay` (`CuttingTeamId` = `TeamId` `[?]`, `LaySequence` do server cấp `[?]`), `CuttingLayLine`, `CuttingLayFabric` | `[?]` `WorkOrder.Status` OPEN→CUTTING | — |
| PUT `team-leader/cutting-lays/{id}` | `[?]` | `CuttingLay`; `[?]` `CuttingLayLine`, `CuttingLayFabric` | `[?]` dòng cũ của `CuttingLayLine`, `CuttingLayFabric` |
| POST `team-leader/cut-handovers` | `CutHandover` (`Status=PENDING`), `CutHandoverLine` | — | — |
| POST `team-leader/cut-handovers/{id}/cancel` | — | `CutHandover.Status = CANCELLED` | — |
| POST `warehouse-keeper/cut-handovers/{id}/confirm` | `[?]` `StockTransaction` | `CutHandover` (`Status`, `ConfirmedBy`, `ConfirmedAt`), `CutHandoverLine` (`ConfirmedQty`, `DiscrepancyReason`); `[?]` `StockBalance`; `[?]` `WorkOrder` (`Status=COMPLETED`, `CompletedAt` khi đủ — theo comment `WorkOrderStatuses.Completed`) | — |
| POST `warehouse-keeper/cut-handovers/{id}/reject` | — | `CutHandover` (`Status=REJECTED`, `RejectReason`; `ConfirmedBy/ConfirmedAt` `[?]`) | — |
| GET `material-allowance`, `fabrics`, `fabric-needs`, `handoverable`, mọi GET list/detail | — | — | — |

---

## 4. Lưu ý kỹ thuật cho QA

**Trigger / View (chỉ biết tên từ Config, chưa đọc nội dung)**
- `trg_WorkOrderLine_Check` (WorkOrderLine), `trg_CuttingLayLine_Check` (CuttingLayLine), `trg_CutHandoverLine_Check` (CutHandoverLine): có thể chặn INSERT/UPDATE (ví dụ vượt số kế hoạch/đã cắt) `[?]` — case âm cần assert lỗi DB được map sang HTTP nào `[?]`.
- Có entity view `VwWorkOrderLineProgress` (file config tồn tại) `[?]` có thể là nguồn `CutQty/HandedQty`.
- Default `dbo.fn_VnNow()` cho `CreatedAt`/`HandedAt`: seed bằng EF/SQL không gán tay sẽ lấy giờ VN.

**Status: DB ↔ DTO**
- Tất cả Status lưu và trả về dạng **UPPER_SNAKE** (`OPEN`, `CUTTING`, `COMPLETED`, `CANCELLED`; `RECORDED`, `LOCKED`; `PENDING`, `CONFIRMED`, `REJECTED`, `CANCELLED`); DTO trả nguyên chuỗi DB.
- Ngoại lệ: `FabricCheckStatuses` (`WorkOrderFabricCheckDto.Status`) là **PascalCase** (`Enough`, `Short`, `Unknown`) và không lưu DB.
- `CANCELLED` ở `WorkOrder` (2 chữ L) trùng chính tả với `HandoverStatuses.Cancelled`; `CuttingLay.Status` giới hạn varchar(10) (không có chỗ cho giá trị dài hơn 10 ký tự).
- Query `Status` là chuỗi tự do (`string?`); giá trị không hợp lệ xử lý thế nào `[?]` (không đọc Validator).

**Ma trận phân quyền chéo**
| Hành động | Admin | Manager | WarehouseKeeper | TeamLeader/Deputy tổ CUTTING | TeamLeader/Deputy tổ khác (SEWING/OFFICE) | Accountant/QaQc/Electrician/Worker |
|---|---|---|---|---|---|---|
| `manager/*` (work-orders) | 200 | 200 | 403 | 403 | 403 | 403 |
| `team-leader/*` (work-orders, cutting-lays, cut-handovers) | 403 | 403 | 403 | 200 | **403** (`WarehouseCuttingTeamOnly`) | 403 |
| `warehouse-keeper/*` (work-orders, cut-handovers) | **403** (không có Admin) | 403 | 200 | 403 | 403 | 403 |
Không token → 401. Role không khớp prefix → 403 từ `[Authorize]`.

**Cách ly dữ liệu theo tổ (IDOR)**
- Chỉ prefix `team-leader` truyền `teamId` vào service (= `ICurrentUser.TeamId ?? 0`); tài khoản không gắn tổ ⇒ `0` ⇒ không khớp tổ nào (list rỗng / `404` hoặc `403` khi theo id `[?]`).
- Prefix `manager` và `warehouse-keeper` truyền `null` ⇒ thấy mọi tổ.
- Lệnh/bàn/phiếu của tổ cắt khác gọi qua `team-leader/*/{id}` ⇒ kỳ vọng không truy cập được `[?]` mã lỗi (403 vs 404).

**Điểm lệch / bất thường DTO–Entity–Comment**
1. `CloseShortWorkOrderDto.Reason` bắt buộc nhưng `WorkOrder` **không có cột Reason** (chỉ có `Note` nvarchar(1000)) ⇒ nơi lưu lý do `[?]` (có thể ghi vào `Note` hoặc bảng khác); QA cần xác nhận cột nào để Assert DB.
2. `SaveCuttingLayDto` không có `LaySequence` và `CuttingTeamId`, nhưng entity bắt buộc cả hai (NOT NULL, nằm trong `UQ_CL`) ⇒ server tự cấp; `CuttingLayDto.LaySequence` là `int` còn entity là `smallint` (>32767 tràn).
3. `CuttingLayDto` không trả `CuttingTeamId`; `CutHandover` không có `CuttingTeamId` nhưng `CutHandoverDto/ListItemDto` có (join qua `WorkOrder`).
4. `CuttingLayDto.Fabrics[].UsedM` và `CuttingLayFabric.UsedM` nullable (bàn cũ), nhưng `SaveCuttingLayDto` comment yêu cầu "đủ mọi vải" và không vượt vải còn lại ⇒ `UsedM = null` khi tạo mới có được chấp nhận hay không `[?]`.
5. `CuttingLayLineDto.ActualQty` / `CuttingLayLine.ActualQty` là "số lượng sau khi cắt" (DEV-ALL-90), không còn cột số lớp/tỉ lệ bìa; `CuttingLayDto.SizeSummary` là chuỗi tính toán (`"M:90, L:60"`).
6. `WorkOrderLineDto.CutQty/HandedQty` không phải cột của `WorkOrderLine` — tính từ `CuttingLayLine`/`CutHandoverLine`; `HandedQty` gồm chờ nhận + đã nhận (theo comment `HandoverableLineDto`), khác với `ConfirmedQty` ở `CutHandoverLine`.
7. `CreateHandoverLineDto(OrderLineId, HandedQty)` có trong `CuttingDtos.cs` nhưng **không được Controller nào dùng** (Controller dùng `CreateCutHandoverLineDto`).
8. `ErrorCodes.WarehouseCuttingTeamOnly`/`WarehouseMessages.CuttingTeamOnly` đặt tên "Warehouse" nhưng dùng cho 403 của tổ trưởng cắt (không liên quan kho).
9. `GET team-leader/cut-handovers/handoverable?workOrderId=` dùng `int` không nullable ⇒ thiếu tham số rơi về `0` (không phải 400 do model binding) `[?]` kết quả service.
10. `WorkOrder.FabricTolerancePercent` decimal(5,2) có Entity default CLR = 0 nhưng Config không khai báo default DB ⇒ seed trực tiếp bằng SQL phải truyền giá trị `[?]`.
11. `UQ_WOL`/`UQ_CLL`/`UQ_CHL` chứa cột nullable `ProductComponentMaterialId` và không có filter: hai dòng "cả bộ" cùng (WO, OrderLine) sẽ vi phạm unique (NULL bằng NULL trong SQL Server) — QA cần biết khi seed nhiều dòng cùng OrderLine.
12. Hai `WorkOrdersController` (Manager/TeamLeader/WarehouseKeeper) dùng chung `IWorkOrderService` nhưng khác tham số `teamId`; `GetPaged/GetById` ở manager và warehouse-keeper trả cùng DTO ⇒ test chéo role cần Assert kết quả giống nhau cho cùng dữ liệu.
13. `FabricTolerancePercent` ở `CheckWorkOrderFabricDto` không có (chỉ có ở `SaveWorkOrderDto`) ⇒ `fabric-check` dùng trần mặc định `[?]`.
14. Các `{id:int}` sai kiểu (vd `abc`) → route không khớp ⇒ **404 từ routing**, không phải `ApiResponses`.
15. Body null/sai JSON ở endpoint có `[FromBody]` → **400** do `[ApiController]` (không qua `ApiResponses`); riêng `close-short` truy cập `dto.Reason` nên thân `null` bị chặn bởi model validation trước khi vào action.
