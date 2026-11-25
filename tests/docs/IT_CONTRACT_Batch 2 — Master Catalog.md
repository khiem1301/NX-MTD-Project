# IT_CONTRACT — Batch 2: Master Catalog (Customer · Supplier · Material · Product · Vehicle)

> Nguồn: Controller, DTO, Entity, EF Configuration, Base Controller, `Roles`. **Không** đọc Service/Repository.
> Ký hiệu `[?]` = chưa xác nhận được qua DTO/Entity/Config (cần QA xác nhận khi chạy thật).
> Prefix theo Base Controller; `[controller]` = tên class bỏ hậu tố `Controller` (ASP.NET so khớp route không phân biệt hoa/thường). Có `ParameterTransformer` (kebab-case) hay không `[?]` — không đọc `Program.cs`.

| Base Controller | Prefix | `[Authorize]` |
|---|---|---|
| AdminBaseController | `/api/v1/admin/{controller}` | `Roles = "Admin"` |
| ManagerBaseController | `/api/v1/manager/{controller}` | `Roles = "Admin,Manager"` (Admin gọi được mọi API Manager) |
| AccountantBaseController | `/api/v1/accountant/{controller}` | `Roles = "Accountant"` |
| WarehouseKeeperBaseController | `/api/v1/warehouse-keeper/{controller}` | `Roles = "WarehouseKeeper"` |

Không có endpoint nào có `[AllowAnonymous]` hoặc policy riêng. Tất cả trả qua `this.ToResponse(Result)`.

---

## 1. Bảng Endpoints

`PageQuery` (dùng chung mọi `*Query`): `Page` (int, mặc định 1, ≥1), `PageSize` (mặc định 20, 1..100), `Keyword` (≤100 ký tự), `SortBy`, `SortDir` (`asc` = tăng dần; khác `asc`/null = **giảm dần**).

### 1.1 Customer — `Admin/CustomersController` (UC09, không có xóa)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| C1 | GET | `/api/v1/admin/customers` | Admin | Query: `CustomerQuery` = PageQuery + `Status` (`Active`\|`Discontinued`) |
| C2 | GET | `/api/v1/admin/customers/options` | Admin | — (không phân trang) |
| C3 | POST | `/api/v1/admin/customers` | Admin | Body `SaveCustomerDto(CustomerName, Phone?, Email?, DefaultAddress?)` |
| C4 | PUT | `/api/v1/admin/customers/{id:int}` | Admin | Route `id`; Body `SaveCustomerDto` |
| C5 | POST | `/api/v1/admin/customers/{id:int}/discontinue` | Admin | Route `id`; Body `DiscontinueCustomerDto(Reason?)` (không `[FromBody]` tường minh, `[ApiController]` suy ra Body) |
| C6 | POST | `/api/v1/admin/customers/{id:int}/reactivate` | Admin | Route `id` |

### 1.2 Supplier

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| S1 | GET | `/api/v1/admin/suppliers` | Admin | Query `SupplierQuery` = PageQuery + `Status` (`Active`\|`Discontinued`; trống = tất cả). Trả `SupplierDto(SupplierId, SupplierName, Phone?, Email?, Address?, MaterialTypes?, Note?, Status, CreatedAt, PurchaseOrderCount, DiscontinueReason?)` |
| S2 | GET | `/api/v1/admin/suppliers/{id:int}` | Admin | Route `id`. Trả `SupplierDetailDto(Supplier: SupplierDto, History: AuditLogDto[])` |
| S3 | POST | `/api/v1/admin/suppliers` | Admin | Body `SaveSupplierDto(SupplierName, Phone?, Email?, Address?, MaterialTypes?, Note?)` |
| S4 | PUT | `/api/v1/admin/suppliers/{id:int}` | Admin | Route `id`; Body `SaveSupplierDto` |
| S5 | POST | `/api/v1/admin/suppliers/{id:int}/discontinue` | Admin | Route `id`; Body `DiscontinueSupplierDto(Reason?)` — Reason **không bắt buộc**, ghi nhật ký nếu có |
| S6 | POST | `/api/v1/admin/suppliers/{id:int}/reactivate` | Admin | Route `id` |
| S7 | GET | `/api/v1/manager/suppliers` | Admin, Manager | — (chỉ NCC đang hợp tác). Trả `SupplierOptionDto(SupplierId, SupplierName)[]` |

### 1.3 Material (UC38; cùng `IMaterialService`, 4 controller)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| M1 | GET | `/api/v1/admin/materials` | Admin | Query `MaterialQuery` = PageQuery + `MaterialType`, `Status` |
| M2 | GET | `/api/v1/admin/materials/{id:int}` | Admin | Route `id` |
| M3 | POST | `/api/v1/admin/materials` | Admin | Body `CreateMaterialDto(MaterialCode?, MaterialName, MaterialType, Unit, Color?, WidthM?)` |
| M4 | PUT | `/api/v1/admin/materials/{id:int}` | Admin | Route `id`; Body `UpdateMaterialDto(MaterialName, MaterialType, Unit, Color?, WidthM?)` |
| M5 | POST | `/api/v1/admin/materials/reopen` | Admin | Body `ReopenMaterialDto(MaterialCode)` |
| M6 | PATCH | `/api/v1/admin/materials/{id:int}/status` | Admin | Route `id`; Body `ChangeMaterialStatusDto(Status)` |
| M7–M11 | GET / GET{id} / POST / PUT{id} / PATCH{id}/status | `/api/v1/manager/materials[...]` | Admin, Manager | như M1, M2, M3, M4, M6 (**không có** `reopen`) |
| M12 | POST | `/api/v1/accountant/materials` | Accountant | Body `CreateMaterialDto` (chỉ Create) |
| M13–M17 | GET / GET{id} / POST / PUT{id} / PATCH{id}/status | `/api/v1/warehouse-keeper/materials[...]` | WarehouseKeeper | như M1, M2, M3, M4, M6 (**không có** `reopen`) |

### 1.4 Product — `Admin/ProductsController` (UC10, không xóa — GBR-14)

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| P1 | GET | `/api/v1/admin/products` | Admin | Query `ProductQuery` = PageQuery + `Status` (`Active`\|`Discontinued`; null = tất cả) |
| P2 | GET | `/api/v1/admin/products/{id:int}` | Admin | Route `id` |
| P3 | POST | `/api/v1/admin/products` | Admin | Body `SaveProductDto(ProductCode, SampleImageUrl?, Description?, ExtraImages? = null)` |
| P4 | PUT | `/api/v1/admin/products/{id:int}` | Admin | Route `id`; Body `SaveProductDto` |
| P5 | POST | `/api/v1/admin/products/{id:int}/discontinue` | Admin | Route `id` (không body) |
| P6 | POST | `/api/v1/admin/products/{id:int}/reactivate` | Admin | Route `id` (không body) |
| P7 | POST | `/api/v1/admin/products/sample-image` | Admin | multipart `file` (`IFormFile?`); `RequestSizeLimit` 6 MiB (6 291 456 B). Trả khóa ảnh |
| P8 | GET | `/api/v1/admin/products/sample-image` | Admin | Query `key` (string). Trả file, header `Cache-Control: private, max-age=3600` |
| P9 | GET | `/api/v1/admin/products/{id:int}/spec` | Admin | Route `id` |
| P10 | PUT | `/api/v1/admin/products/{id:int}/spec` | Admin | Route `id`; Body `SaveProductSpecDto(Components[], Variants[], Norms[], MarkerComponents[], MarkerColors[])` — chi tiết ở 2.8 |
| P11 | POST | `/api/v1/admin/products/excel/parse` | Admin | multipart `file`; chỉ đọc |
| P12 | POST | `/api/v1/admin/products/excel/check` | Admin | Body `ExcelBundleCheckDto(ProductId?, OrderId?, Bundle: ExcelBundleDto, Replace=false)`; chỉ đọc. Trả `ExcelBundleCheckResultDto(Products?, Spec, OrderMaterials?, MissingMaterials[])` |
| P13 | POST | `/api/v1/admin/products/{id:int}/import/preview` | Admin | Route `id`; multipart `file`; Query `orderId?`, `parts?` (csv: components,norms,groups,markers; lower-case + distinct), `replace` (bool, mặc định false); chỉ đọc |
| P14 | POST | `/api/v1/admin/products/{id:int}/import/preview-data` | Admin | Route `id`; Body `SpecWorkbookDto` (2.8); Query `orderId?`, `parts?`, `replace`; chỉ đọc. Trả `SpecImportPreviewDto` |
| P15 | POST | `/api/v1/admin/products/{id:int}/import` | Admin | Route `id`; multipart `file`; Query `orderId?`, `parts?`, `replace` |
| P16 | POST | `/api/v1/admin/products/{id:int}/import-data` | Admin | Route `id`; Body `SpecWorkbookDto`; Query `orderId?`, `parts?`, `replace`. Trả `SpecImportResultDto(Components, Norms, MarkerGroups, OrderMarkers, Removed=0)` |
| P17 | GET | `/api/v1/accountant/products` | Accountant | Query `ProductQuery` |
| P18 | GET | `/api/v1/accountant/products/{id:int}/spec` | Accountant | Route `id` |
| P19 | GET | `/api/v1/accountant/products/sample-image` | Accountant | Query `key` |

> **Không tồn tại route `Manager/ProductsController`** (thư mục `Controllers/Manager` có 26 file, không có `ProductsController`, nên không có `/api/v1/manager/products*`). Comment DEV-ALL-117 ghi "Admin / Quản đốc nhập Excel" nhưng chỉ Admin có route import mã hàng (P11–P16). QA viết test phân quyền: Manager gọi `/api/v1/manager/products*` → 404; Manager gọi `/api/v1/admin/products*` → 403; Accountant gọi POST/PUT/import ở `/api/v1/accountant/products*` → không có route (404/405); WarehouseKeeper gọi `/api/v1/admin/products*` → 403.

### 1.5 Vehicle

| # | Method | URL | Role | Tham số |
|---|---|---|---|---|
| V1 | GET | `/api/v1/admin/vehicles` | Admin | Query `VehicleLiveStatusQuery` = PageQuery + `Running` (bool?; true = đang chạy, false = không chạy, null = tất cả) |
| V2 | GET | `/api/v1/warehouse-keeper/vehicles/available` | WarehouseKeeper | — |
| V3 | GET | `/api/v1/accountant/vehicles` | Accountant | Query `VehicleQuery` = PageQuery + `Status` |
| V4 | POST | `/api/v1/accountant/vehicles` | Accountant | Body `SaveVehicleDto(PlateNumber, VehicleType, FuelNormPer100Km)` |
| V5 | PUT | `/api/v1/accountant/vehicles/{id:int}` | Accountant | Route `id`; Body `SaveVehicleDto` |
| V6 | POST | `/api/v1/accountant/vehicles/{id:int}/discontinue` | Accountant | Route `id` (service nhận `false`) |
| V7 | POST | `/api/v1/accountant/vehicles/{id:int}/reactivate` | Accountant | Route `id` (service nhận `true`) |

---

## 2. Ràng buộc dữ liệu (Input DTO & DB Schema)

Mặc định: `Status` mặc định DB = `'ACTIVE'`. `varchar` = `IsUnicode(false)`, còn lại `nvarchar`. `CreatedAt` = `datetime2(0)` default `dbo.fn_VnNow()`.

### 2.1 `Customer`
| Cột | Kiểu / MaxLength | NULL | Ràng buộc |
|---|---|---|---|
| CustomerId | int (PK, identity) | NOT NULL | |
| CustomerName | nvarchar(200) | NOT NULL | **UNIQUE** `UQ_Customer_Name` |
| Phone | varchar(15) | NULL | **UNIQUE filtered** `UQ_Customer_Phone` `WHERE [Phone] IS NOT NULL` |
| Email | varchar(100) | NULL | **UNIQUE filtered** `UQ_Customer_Email` `WHERE [Email] IS NOT NULL` |
| DefaultAddress | nvarchar(500) | NULL | |
| Status | varchar(20) | NOT NULL | default `'ACTIVE'` |
| DiscontinueReason | nvarchar(500) (`TextLimits.Reason`) | NULL | DTO comment: bắt buộc khi ngừng hợp tác (DTO để `string?`) |

Không có `CreatedAt` (khác Supplier/Material/Product). Không có FK ra ngoài; được `CustomerOrder` tham chiếu.

### 2.2 `Supplier`
| Cột | Kiểu / MaxLength | NULL | Ràng buộc |
|---|---|---|---|
| SupplierId | int PK | NOT NULL | |
| SupplierName | nvarchar(200) | NOT NULL | **UNIQUE** `UQ_Supplier_Name` |
| Phone | varchar(15) | NULL | **UNIQUE filtered** `UQ_Supplier_Phone` `WHERE [Phone] IS NOT NULL` |
| Email | varchar(100) | NULL | **UNIQUE filtered** `UQ_Supplier_Email` `WHERE [Email] IS NOT NULL` |
| Address | nvarchar(500) | NULL | |
| MaterialTypes | nvarchar(200) | NULL | chuỗi tự do `[?]` định dạng |
| Note | nvarchar(500) | NULL | |
| Status | varchar(20) | NOT NULL | default `'ACTIVE'` |
| DiscontinueReason | nvarchar(500) | NULL | |
| CreatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |

Được `PurchaseOrder` tham chiếu.

### 2.3 `Material`
| Cột | Kiểu / MaxLength | NULL | Ràng buộc |
|---|---|---|---|
| MaterialId | int PK | NOT NULL | |
| MaterialCode | varchar(30) | NOT NULL | **UNIQUE** `UQ_Material_Code`. DTO `MaterialCode` null/trống → tự sinh `V-0001` (vải) / `PL-0001` (phụ liệu) |
| MaterialName | nvarchar(200) | NOT NULL | thuộc UNIQUE ghép |
| Color | nvarchar(50) | NOT NULL | default `''` (không phân màu); thuộc UNIQUE ghép |
| MaterialType | varchar(10) | NOT NULL | thuộc UNIQUE ghép; giá trị: `FABRIC`, `TRIM`, `SCRAP` (theo comment Entity). Không thấy CHECK trong Config `[?]` |
| Unit | nvarchar(20) | NOT NULL | Vải luôn `m` (giá trị gửi lên bị bỏ qua) |
| WidthM | decimal(4,2) | NULL | max 99.99; bắt buộc với FABRIC, bỏ qua với TRIM (DTO comment DEV-ALL-102) |
| Status | varchar(20) | NOT NULL | default `'ACTIVE'`; giá trị "ngừng sử dụng" `[?]` |
| CreatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |

- **UNIQUE ghép** `UQ_Material_NameType` = (`MaterialName`, `MaterialType`, `Color`), không filter. Cùng tên khác màu = 2 NVL.
- `UpdateMaterialDto`: đổi `Color` chỉ được khi NVL chưa dùng ở định mức, NVL của đơn, chứng từ, tồn kho (comment DTO). Bảng bị kiểm tra: `ProductComponentMaterial`, `ProductMarkerFabric`, `OrderMaterial`, `MaterialReceiptLine`, `MaterialIssueLine`, `PurchaseOrderLine`, `RequestLine`, `CuttingLayFabric`, `StockBalance` (theo navigation Entity `[?]` bảng nào thật sự bị check).
- `MaterialCode` không có trong `UpdateMaterialDto` ⇒ không đổi mã sau khi tạo.

### 2.4 `Product`
| Cột | Kiểu / MaxLength | NULL | Ràng buộc |
|---|---|---|---|
| ProductId | int PK | NOT NULL | |
| ProductCode | varchar(30) | NOT NULL | **UNIQUE** `UQ_Product_Code` |
| SampleImageUrl | nvarchar(500) | NULL | khóa ảnh upload hoặc URL http/https (dữ liệu cũ) |
| Description | nvarchar(1000) | NULL | |
| Status | varchar(20) | NOT NULL | default `'ACTIVE'` |
| CreatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |

### 2.5 `ProductImage` (ảnh phụ)
| Cột | Kiểu | NULL | Ràng buộc |
|---|---|---|---|
| ProductImageId | int PK | NOT NULL | |
| ProductId | int | NOT NULL | **FK** `FK_ProductImage_Product` → `Product` (`ClientSetNull`) |
| ImageKey | nvarchar(500) | NOT NULL | |
| SortOrder | tinyint | NOT NULL | **UNIQUE ghép** `UQ_ProductImage_Sort` (ProductId, SortOrder); comment: 1–5. Không thấy CHECK 1–5 trong Config `[?]` |
| CreatedAt | datetime2(0) | NOT NULL | default `fn_VnNow()` |

`SaveProductDto.ExtraImages`: tối đa 5; `null` = giữ nguyên; `[]` = xóa hết.

### 2.6 `Vehicle`
| Cột | Kiểu / MaxLength | NULL | Ràng buộc |
|---|---|---|---|
| VehicleId | int PK | NOT NULL | |
| PlateNumber | varchar(20) | NOT NULL | **UNIQUE** `UQ_Vehicle_Plate` |
| VehicleType | nvarchar(50) | NOT NULL | |
| FuelNormPer100Km | decimal(6,2) | NOT NULL | max 9999.99 |
| Status | varchar(20) | NOT NULL | default `'ACTIVE'`; giá trị "ngừng" `[?]` |

Không có `CreatedAt`, `DiscontinueReason`. Được `VehicleTrip` tham chiếu.

### 2.7 DTO-only (không phải cột)
- `CustomerDto`/`CustomerOptionDto`: `Status`, `DiscontinueReason?`, `DefaultAddress?`.
- `ProductListItemDto.IsUsed`, `ProductDetailDto.IsUsed/Colors/History(AuditLogDto)`: dữ liệu suy ra `[?]`.
- `VehicleDto.TripCount`, `LastOdometer`; `VehicleLiveStatusDto.IsRunning` (= có `VehicleTrip` trạng thái `STARTED`), `OpenTripCount` (>1 = dữ liệu bất thường), `CurrentTripId`, `OdometerStart`, `DriverName`, `DispatchCode`: suy ra từ `VehicleTrip`.
- `MaterialDto.WidthM` = `decimal?`.

### 2.8 DTO đầu vào spec / import (Product)

- `SaveProductSpecDto`: `Components[SaveProductComponentDto(ProductComponentId?, ComponentName, ComponentType, PiecesPerGarment:short)]`, `Variants[SaveProductVariantDto(ProductVariantId?, Color, SizeCode)]`, `Norms[SaveProductNormDto(ComponentName, Color, MaterialId:int, SpecNote?)]`, `MarkerComponents[(MarkerNo:int, ComponentName)]`, `MarkerColors[(MarkerNo:int, Color)]`. `ComponentType` = `PANEL`/`TRIM`. Biến thể phải **đã có** (do đơn hàng tạo), lưu spec không sửa biến thể. Norm tham chiếu thành phần theo **tên** + **màu** (màu phải có trong Variants).
- `SpecWorkbookDto(HasComponents, Components[], HasNorms, Norms[], HasMarkerGroups, MarkerGroups[], HasOrderMarkers, OrderMarkers[], SizeColumns[])`:
  - `SpecComponentExcelRow(RowNumber, Name, ComponentType? PANEL|TRIM, PiecesPerGarment? short, ParseError?)`
  - `SpecNormExcelRow(RowNumber, Color, ComponentName, MaterialCode, SpecNote?, ParseError?)` (tham chiếu NVL theo **MaterialCode**, khác `SaveProductNormDto` dùng MaterialId)
  - `SpecMarkerGroupExcelRow(RowNumber, MarkerNo? int, ComponentNames[], Colors[], ParseError?)`
  - `SpecOrderMarkerExcelRow(RowNumber, ProductCode?, MarkerNo? int, MarkerCode, LengthM? decimal, WidthM? decimal, Status `ACTIVE`|`DISCONTINUED` (trống = ACTIVE), Ratios[MarkerRatioDto], ParseError?)`
- `ExcelBundleDto(HasProducts, Products[ProductExcelRow], Spec: SpecWorkbookDto, HasOrderMaterials, OrderMaterials[OrderMaterialExcelRow])`. `ProductExcelRow`, `OrderMaterialExcelRow`, `MarkerRatioDto`, `ProductImportPreviewDto`, `OrderMaterialImportPreviewDto` `[?]` (ngoài phạm vi đã đọc).
- Preview `SpecImportPreviewDto`: `Action` ∈ `New | Update | Unchanged | Error` (PascalCase; bundle check thêm `Pending`); `CanImport`, `ValidCount`, `ErrorCount`, `DuplicateCount`, `ChangeCount`, `RemovedCount`, `CrossErrors[]`, `Notes[]`. Còn lỗi/`CrossErrors` ⇒ không nhập được.
- `ExcelBundleCheckResultDto.MissingMaterials[MissingMaterialDto(MaterialCode, SuggestedType FABRIC|TRIM, Colors[], SuggestedName?, SuggestedUnit?)]`: mã NVL chưa có trong danh mục.
- `ProductSpecDto` (GET spec): `Components[(ProductComponentId, ComponentName, ComponentType, PiecesPerGarment, Status)]`, `Variants[(ProductVariantId, Color, SizeCode, Status)]`, `Norms[]`, `MarkerComponents[]`, `MarkerFabrics[]`.

---

## 3. Bảng DB chịu tác động khi thành công

`AuditLog` ghi chú `[?]`: DTO `ProductDetailDto.History`, `DiscontinueCustomerDto` (“lưu vào hồ sơ và nhật ký”) cho thấy có nhật ký, nhưng bảng/điều kiện ghi chưa xác nhận qua DTO/Config.

| Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|
| C1, C2, S1, S2, S7, M1, M2, M7, M8, M13, M14, P1, P2, P8, P9, P17–P19, V1–V3 | — | — | — (chỉ đọc) |
| C3 POST customers | `Customer`; `AuditLog` `[?]` | — | — |
| C4 PUT customers | `AuditLog` `[?]` | `Customer` | — |
| C5 discontinue | `AuditLog` `[?]` | `Customer` (`Status`, `DiscontinueReason`) | — |
| C6 reactivate | `AuditLog` `[?]` | `Customer` (`Status`; `DiscontinueReason` có bị xóa không `[?]`) | — |
| S3 POST suppliers | `Supplier`; `AuditLog` `[?]` | — | — |
| S4 PUT suppliers | `AuditLog` `[?]` | `Supplier` | — |
| S5 / S6 | `AuditLog` `[?]` | `Supplier` (`Status`, `DiscontinueReason`) | — |
| M3 / M12 / M-POST (Admin, Manager, WK, Accountant) | `Material`; `AuditLog` `[?]` | — | — |
| M4 / PUT | `AuditLog` `[?]` | `Material` | — |
| M5 reopen | `AuditLog` `[?]` | `Material` (`Status`) | — |
| M6 / PATCH status | `AuditLog` `[?]` | `Material` (`Status`) | — |
| P3 POST products | `Product`; `ProductImage` (0..5, nếu `ExtraImages`); `AuditLog` `[?]` | — | — |
| P4 PUT products | `ProductImage` (thêm mới) `[?]`; `AuditLog` `[?]` | `Product`; `ProductImage` (đổi SortOrder/Key) `[?]` | `ProductImage` `[?]` khi `ExtraImages` thu nhỏ / `[]` |
| P5 / P6 | `AuditLog` `[?]` | `Product` (`Status`) | — |
| P7 upload sample-image | ghi file vào storage (không phải bảng DB) `[?]`; **không** ghi DB cho tới khi lưu P3/P4 | — | — |
| P10 PUT spec | `ProductComponent` (thành phần mới), `ProductComponentMaterial` (Norms), nhóm bìa chi tiết + màu (`ProductMarker`, `ProductMarkerFabric` `[?]` tên bảng chính xác) | `ProductComponent`, `ProductComponentMaterial` (thay bằng danh sách gửi lên cho các biến thể có trong `Variants`), nhóm bìa (thay nguyên) | `ProductComponent` bỏ khỏi danh sách: chưa dùng → xóa; đã dùng → UPDATE `Status` ngừng dùng. `ProductVariant` **không** bị sửa/xóa |
| P11, P12, P13, P14 | — (chỉ đọc theo comment "không ghi gì") | — | — |
| P15, P16 import (1 giao dịch) | `ProductComponent` (Components), `ProductComponentMaterial` (Norms), nhóm bìa (MarkerGroups; `ProductMarker`/`ProductMarkerFabric` `[?]`); nếu có `orderId`: bìa cắt của đơn (OrderMarkers, bảng `[?]`) | cùng nhóm, dòng `Action=Update` | `replace=true`: dòng cũ không có trong file bị xóa, hoặc UPDATE `Status` ngừng dùng nếu đang được dùng (`SpecImportResultDto.Removed`). Không có `orderId` ⇒ bảng bìa của đơn trong file bị bỏ qua (Notes) |
| V4 POST vehicles | `Vehicle`; `AuditLog` `[?]` | — | — |
| V5 PUT vehicles | `AuditLog` `[?]` | `Vehicle` | — |
| V6 / V7 | `AuditLog` `[?]` | `Vehicle` (`Status`) | — |

Không endpoint nào trong batch xóa bản ghi Customer/Supplier/Material/Product/Vehicle (comment "không có xóa").

Lan sang ngoài batch (DTO cho thấy, không thuộc endpoint Batch 2): `CreatePurchaseOrderDto` / `SavePurchaseOrderLineDto` (NVL mới → INSERT `Material` hoặc dùng lại khi trùng tên + màu); `CreateOrderDto.StagedProducts` (INSERT `Product` cùng giao dịch tạo đơn).

---

## 4. Lưu ý kỹ thuật cho QA

1. **HOA/thường `Status`**: DB mặc định `'ACTIVE'` (UPPER) nhưng DTO comment `CustomerQuery`, `ProductQuery`, `OrderQuery` ghi *PascalCase* (`Active`/`Discontinued`). `CustomerDto.Status`/`CustomerOptionDto.Status` trả về dạng nào (UPPER hay Pascal) `[?]` → phải assert cả Response và query DB. Giá trị DB của trạng thái "ngừng" (`DISCONTINUED`/`INACTIVE`/…) `[?]`.
2. **Material.MaterialType** DB/Entity: `FABRIC`/`TRIM`/`SCRAP` (UPPER). `SCRAP` chỉ để trả về kho, nhưng `CreateMaterialDto` không loại trừ trong DTO `[?]`. `MaterialQuery.Status` / `ChangeMaterialStatusDto.Status` không ghi tập giá trị hợp lệ.
3. **Lệch comment DTO ↔ Entity**: `SavePurchaseOrderLineDto` ghi NVL mới thuộc "nhóm OTHER", nhưng `Material` **không có cột nhóm** (chỉ `MaterialType`) ⇒ "OTHER" không tương ứng cột nào; `MaterialType` thực tế là `TRIM` `[?]`.
4. **DiscontinueReason**: `DiscontinueCustomerDto.Reason` là `string?` nhưng comment "bắt buộc" → kiểm tra validator trả 400 khi trống/>500. `Product` và `Vehicle` ngừng dùng không có lý do (không body, không cột).
5. **Phạm vi quyền không đối xứng**: Accountant chỉ `POST materials`; Accountant/Products chỉ xem (comment DEV-ALL-117: Admin sửa định mức, "Admin / Quản đốc nhập Excel", nhưng **không tồn tại `Manager/ProductsController`** ⇒ Quản đốc không có route import mã hàng; xem ghi chú dưới bảng 1.4). `reopen` chỉ Admin. Admin gọi được URL `/manager/*` nhưng **không** gọi được `/accountant/*` hay `/warehouse-keeper/*` (403) — QA cần test ma trận role.
6. **Unique ghép Material**: (Name, Type, Color) không filter → `Color = ''` vẫn tham gia; 2 bản ghi cùng tên/loại nhưng 1 có Color `''` và 1 có màu là hợp lệ. Collation phân biệt hoa/thường của `CustomerName`, `SupplierName`, `MaterialName` phụ thuộc DB collation `[?]`.
7. **Unique filtered Phone/Email** (Customer, Supplier): nhiều bản ghi `NULL` hợp lệ; chuỗi rỗng `''` **không** phải NULL nên hai bản ghi `''` sẽ vi phạm UNIQUE (nếu service không chuẩn hóa `''`→NULL `[?]`).
8. **Số học**: `WidthM` decimal(4,2) ⇒ >99.99 hoặc >2 chữ số thập phân bị làm tròn/lỗi tràn tùy driver; `FuelNormPer100Km` decimal(6,2); `PlateNumber` varchar(20) không Unicode (ký tự tiếng Việt bị mất dấu nếu bypass validator).
9. **Trigger DB**: các file Config đã đọc không khai báo trigger; chỉ dùng hàm `dbo.fn_VnNow()` làm default `CreatedAt` (múi giờ VN, thay vì `GETDATE()`/UTC). Trigger/view khác (vd. `vw_OrderLineProgress`) `[?]`.
10. **Controller**: `P7` cho phép `file == null` (controller truyền byte rỗng + tên rỗng cho service ⇒ validation phải trả 400, không phải 500/NullReference); `P8`/`P19` trả `File(...)` khi thành công, các trường hợp lỗi trả JSON qua `ToResponse`. `SplitParts` bỏ phần tử rỗng, trim, lowercase, distinct; `parts` rỗng ⇒ `null` = mọi bảng.
11. **ProductImage**: ràng buộc 5 ảnh và SortOrder 1–5 chỉ nằm ở comment/DTO, không có CHECK trong Config ⇒ test cả ở tầng API (6 ảnh phải bị từ chối) lẫn kiểm tra DB trực tiếp. `FK ClientSetNull` ⇒ xóa `Product` không cascade (không có API xóa).
12. **Vehicle**: `IsRunning` suy ra từ `VehicleTrip.Status = 'STARTED'` (không có cột cờ); `V2` chỉ trả xe ACTIVE và không có chuyến STARTED ⇒ seed `VehicleTrip` khi test. `Admin/Vehicles` là **chỉ đọc**; CRUD ở Accountant.
13. **Orphan XML comment**: `CreateMaterialDto` có dòng `///` thứ hai (“Unit của vải luôn là m…”) nằm ngoài `<summary>`, và `CreatePurchaseOrderDto` có hai khối `<summary>` liên tiếp — chỉ ảnh hưởng tài liệu, không ảnh hưởng hành vi.
14. **Lệch comment ↔ code (Excel)**: comment `ExcelBundleDto` nói đây là body của các API "-data", nhưng P14/P16 thực tế nhận `SpecWorkbookDto`; chỉ P12 nhận `ExcelBundleCheckDto` bọc `ExcelBundleDto`.
15. **Reason không đối xứng**: `DiscontinueSupplierDto.Reason` không bắt buộc (comment), còn `DiscontinueCustomerDto.Reason` comment "bắt buộc" ⇒ test riêng hai trường hợp. `SupplierDto.PurchaseOrderCount` là giá trị suy ra (không có cột).
16. **Status trong DTO Excel**: `SpecOrderMarkerExcelRow.Status` = `ACTIVE`/`DISCONTINUED` (UPPER), trong khi `*Query.Status` và `Action` dùng PascalCase (`Active`/`Discontinued`, `New`/`Update`). Gợi ý giá trị ngừng dùng ở DB là `DISCONTINUED` `[?]`.
17. **Chưa đọc (ngoài phạm vi)**: Validators, Service, Repository, `Program.cs` (route transformer, `ToResponse` ánh xạ Result→HTTP code), `ProductExcelRow`, `OrderMaterialExcelRow`, `MarkerRatioDto`, `ProductImportPreviewDto`, `OrderMaterialImportPreviewDto` ⇒ các mục `[?]` cần xác nhận khi chạy thật.
