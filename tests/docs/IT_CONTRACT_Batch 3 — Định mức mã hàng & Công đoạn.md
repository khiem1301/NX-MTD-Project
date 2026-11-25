# IT_CONTRACT — Batch 3: Định mức mã hàng & Công đoạn

Phạm vi đọc: Controller, DTO, Entity, EF Configuration, `*BaseController`, `Roles`, `PageQuery`, `TextLimits`. **Không** đọc Service / Repository / Validator / Program.cs.
Hệ quả: mọi điều chỉ nằm trong Validator/Service (MaxLength của DTO, quy tắc nghiệp vụ, bảng bị ghi) được đánh dấu `[?]`.

Quy ước: `{id}` = `int`. Prefix Admin = `/api/v1/admin/<controller>`, Accountant = `/api/v1/accountant/<controller>` (từ `[Route("api/v1/admin|accountant/[controller]")]`; token `[controller]` = `Products`, `Operations`, `OperationGroups`, `PieceRates`; routing không phân biệt hoa/thường; việc có đổi sang kebab-case hay không nằm ở Program.cs nên `[?]`).

---

## 1. Bảng Endpoints

Role thực tế = `[Authorize(Roles = ...)]` ở base controller. Admin → `Admin`; Accountant → `Accountant`. **Không có** `Manager` ở bất kỳ controller nào trong batch.

### 1.1 Admin / Products — `/api/v1/admin/products` (Role: Admin)

| # | Method | URL | Tham số |
|---|---|---|---|
| A1 | POST | `/api/v1/admin/products/excel/parse` | multipart: `file` (IFormFile) |
| A2 | POST | `/api/v1/admin/products/excel/check` | Body `ExcelBundleCheckDto` |
| A3 | POST | `/api/v1/admin/products/{id}/import/preview-data` | Route `id`; Query `orderId?`, `parts?`, `replace=false`; Body `SpecWorkbookDto` |
| A4 | POST | `/api/v1/admin/products/{id}/import-data` | Route `id`; Query `orderId?`, `parts?`, `replace=false`; Body `SpecWorkbookDto` |
| A5 | GET | `/api/v1/admin/products` | Query `ProductQuery` (`page`, `pageSize`, `keyword`, `sortBy`, `sortDir`, `status`) |
| A6 | GET | `/api/v1/admin/products/{id}` | Route `id` |
| A7 | POST | `/api/v1/admin/products` | Body `SaveProductDto` |
| A8 | PUT | `/api/v1/admin/products/{id}` | Route `id`; Body `SaveProductDto` |
| A9 | GET | `/api/v1/admin/products/{id}/spec` | Route `id` |
| A10 | PUT | `/api/v1/admin/products/{id}/spec` | Route `id`; Body `SaveProductSpecDto` |
| A11 | POST | `/api/v1/admin/products/{id}/import/preview` | Route `id`; multipart `file`; Query `orderId?`, `parts?`, `replace=false` |
| A12 | POST | `/api/v1/admin/products/{id}/import` | Route `id`; multipart `file`; Query `orderId?`, `parts?`, `replace=false` |
| A13 | POST | `/api/v1/admin/products/sample-image` | multipart `file?` (nullable); `RequestSizeLimit` 6 MB (6 291 456 B) |
| A14 | GET | `/api/v1/admin/products/sample-image` | Query `key` (string) → trả file, header `Cache-Control: private, max-age=3600` |
| A15 | POST | `/api/v1/admin/products/{id}/discontinue` | Route `id` |
| A16 | POST | `/api/v1/admin/products/{id}/reactivate` | Route `id` |

`parts` = chuỗi phân cách dấu phẩy, controller tách + `ToLowerInvariant` + `Distinct`; giá trị theo comment: `components`, `norms`, `groups`, `markers`; trống = tất cả.

### 1.2 Accountant / Products — `/api/v1/accountant/products` (Role: Accountant)

| # | Method | URL | Tham số |
|---|---|---|---|
| B1 | GET | `/api/v1/accountant/products` | Query `ProductQuery` |
| B2 | GET | `/api/v1/accountant/products/{id}/spec` | Route `id` |
| B3 | GET | `/api/v1/accountant/products/sample-image` | Query `key` |

### 1.3 Admin / Operations — `/api/v1/admin/operations` (Role: Admin)

| # | Method | URL | Tham số |
|---|---|---|---|
| C1 | GET | `/api/v1/admin/operations` | Query `productId` (int, không `?`), `includeDiscontinued` (bool) |

### 1.4 Accountant / Operations — `/api/v1/accountant/operations` (Role: Accountant)

| # | Method | URL | Tham số |
|---|---|---|---|
| D1 | GET | `/api/v1/accountant/operations` | Query `productId`, `includeDiscontinued` |
| D2 | POST | `/api/v1/accountant/operations/import/preview` | multipart `file`; Query `productId`, `sheetName?`, `effectiveDate?` (DateOnly), `replace=false` |
| D3 | POST | `/api/v1/accountant/operations/import` | multipart `file`; Query `productId`, `sheetName?`, `finalStt?`, `effectiveDate?`, `submitRates` (bool), `replace=false` |
| D4 | POST | `/api/v1/accountant/operations/import/preview-data` | Body `OperationExcelSheetDto`; Query `productId`, `effectiveDate?`, `replace=false` |
| D5 | POST | `/api/v1/accountant/operations/import-data` | Body `OperationExcelSheetDto`; Query `productId`, `finalStt?`, `effectiveDate?`, `submitRates`, `replace=false` |
| D6 | POST | `/api/v1/accountant/operations` | Body `SaveOperationDto` |
| D7 | PUT | `/api/v1/accountant/operations/{id}` | Route `id`; Body `SaveOperationDto` |
| D8 | POST | `/api/v1/accountant/operations/{id}/discontinue` | Route `id` |
| D9 | POST | `/api/v1/accountant/operations/{id}/reactivate` | Route `id` |

### 1.5 Admin / OperationGroups — `/api/v1/admin/operationgroups` (Role: Admin)

| # | Method | URL | Tham số |
|---|---|---|---|
| E1 | GET | `/api/v1/admin/operationgroups` | Query `productId` |

### 1.6 Accountant / OperationGroups — `/api/v1/accountant/operationgroups` (Role: Accountant)

| # | Method | URL | Tham số |
|---|---|---|---|
| F1 | GET | `/api/v1/accountant/operationgroups` | Query `productId` |
| F2 | POST | `/api/v1/accountant/operationgroups` | Body `CreateOperationGroupDto` |
| F3 | PUT | `/api/v1/accountant/operationgroups/{id}` | Route `id`; Body `RenameOperationGroupDto` |
| F4 | DELETE | `/api/v1/accountant/operationgroups/{id}` | Route `id` |
| F5 | PUT | `/api/v1/accountant/operationgroups/operations` | Body `SetOperationsGroupDto` |

### 1.7 Admin / PieceRates — `/api/v1/admin/piecerates` (Role: Admin)

| # | Method | URL | Tham số |
|---|---|---|---|
| G1 | GET | `/api/v1/admin/piecerates` | Query `PieceRateQuery` (`operationId?`, `productId?`, `status?` + `PageQuery`); controller cố định `includeDraft: false` |
| G2 | POST | `/api/v1/admin/piecerates/{id}/approve` | Route `id`; Body `ApprovePieceRateDto` |
| G3 | POST | `/api/v1/admin/piecerates/approve-many` | Body `ApproveManyPieceRatesDto` |
| G4 | POST | `/api/v1/admin/piecerates/{id}/reject` | Route `id`; Body `RejectPieceRateDto` |

### 1.8 Accountant / PieceRates — `/api/v1/accountant/piecerates` (Role: Accountant)

| # | Method | URL | Tham số |
|---|---|---|---|
| H1 | GET | `/api/v1/accountant/piecerates` | Query `PieceRateQuery` (mặc định `includeDraft` của Service `[?]`) |
| H2 | POST | `/api/v1/accountant/piecerates` | Body `SavePieceRateDto` |
| H3 | PUT | `/api/v1/accountant/piecerates/{id}` | Route `id`; Body `SavePieceRateDto` |
| H4 | POST | `/api/v1/accountant/piecerates/submit-many` | Body `SubmitPieceRatesDto` |
| H5 | POST | `/api/v1/accountant/piecerates/{id}/submit` | Route `id` (không body) |

### 1.9 Hình dạng DTO (từ record)

**Query chung**
- `PageQuery`: `Page=1`, `PageSize=20` (mặc định), `Keyword?`, `SortBy?`, `SortDir?`. `SafePage` = `max(1,Page)`; `SafePageSize` = `PageSize<1 → 20`, tối đa `100` (kẹp, không báo lỗi). `Desc` = true trừ khi `SortDir` = "asc" (không phân biệt hoa/thường).
- `ProductQuery : PageQuery` + `Status?` (comment: `Active`/`Discontinued`, null = tất cả).
- `PieceRateQuery : PageQuery` + `OperationId?`, `ProductId?`, `Status?` (comment: PascalCase, vd `PendingApproval`).

**Mã hàng / Excel bundle**
- `SaveProductDto(ProductCode, SampleImageUrl?, Description?, ExtraImages? = null)`; `ExtraImages` tối đa 5 (comment): null = giữ nguyên, `[]` = xóa hết.
- `ExcelBundleCheckDto(ProductId?, OrderId?, Bundle: ExcelBundleDto, Replace=false)`.
- `ExcelBundleDto(HasProducts, Products: ProductExcelRow[], Spec: SpecWorkbookDto, HasOrderMaterials, OrderMaterials: OrderMaterialExcelRow[])`.   - `ProductExcelRow(RowNumber: int, ProductCode: string, Description: string?)` — `ProductImportDtos.cs`
  - `OrderMaterialExcelRow(RowNumber: int, MaterialCode: string, RequiredQty: decimal?, CustomerQty: decimal?, Note: string?, ParseError: string?, MaterialName: string? = null, Unit: string? = null)` — `OrderMaterialDtos.cs`; `MaterialName`/`Unit` (cột tùy chọn, DEV-ALL-123) chỉ để hiển thị và gợi ý khi thêm NVL chưa có vào danh mục, NVL đã có thì lấy theo danh mục.
  - Ràng buộc DB tương ứng của các trường này: `ProductCode` varchar(30) UNIQUE, `Description` nvarchar(1000) (Mục 2.2); bảng NVL của đơn nằm ngoài batch → `[?]`.
  - Kết quả kiểm `ProductImportRowDto.Action` = `New | Exists (đã có, bỏ qua) | Error`, `Note` = lý do lỗi hoặc "đã có". `OrderMaterialImportRowDto.Action` = `New | Update | Unchanged | Error` (thêm `Pending` ở bundle check).
- `SpecWorkbookDto(HasComponents, Components: SpecComponentExcelRow[], HasNorms, Norms: SpecNormExcelRow[], HasMarkerGroups, MarkerGroups: SpecMarkerGroupExcelRow[], HasOrderMarkers, OrderMarkers: SpecOrderMarkerExcelRow[], SizeColumns: string[])`.
  - `SpecComponentExcelRow(RowNumber, Name, ComponentType?, PiecesPerGarment: short?, ParseError?)`
  - `SpecNormExcelRow(RowNumber, Color, ComponentName, MaterialCode, SpecNote?, ParseError?)`
  - `SpecMarkerGroupExcelRow(RowNumber, MarkerNo: int?, ComponentNames[], Colors[], ParseError?)`
  - `SpecOrderMarkerExcelRow(RowNumber, ProductCode?, MarkerNo: int?, MarkerCode, LengthM: decimal?, WidthM: decimal?, Status, Ratios: MarkerRatioDto[], ParseError?)`; `MarkerRatioDto(SizeCode, PiecesPerPly: short)`.

**Spec (PUT `{id}/spec`)**
- `SaveProductSpecDto(Components, Variants, Norms, MarkerComponents, MarkerColors)` — cả 5 mảng không nullable.
  - `SaveProductComponentDto(ProductComponentId: int?, ComponentName, ComponentType, PiecesPerGarment: short)`
  - `SaveProductVariantDto(ProductVariantId: int?, Color, SizeCode)`
  - `SaveProductNormDto(ComponentName, Color, MaterialId: int, SpecNote?)`
  - `SaveProductMarkerComponentDto(MarkerNo: int, ComponentName)`
  - `SaveProductMarkerColorDto(MarkerNo: int, Color)`

**Response chính**
- `ProductListItemDto(ProductId, ProductCode, SampleImageUrl?, Status, IsUsed, CreatedAt, ExtraImages[])`
- `ProductDetailDto(ProductId, ProductCode, SampleImageUrl?, Description?, Status, IsUsed, CreatedAt, ExtraImages[], Colors[], History: AuditLogDto[])`
- `ProductSpecDto(ProductId, Components: ProductComponentDto[], Variants: ProductVariantDto[], Norms: ProductNormDto[], MarkerComponents: ProductMarkerComponentDto[], MarkerFabrics: ProductMarkerFabricDto[])`
  - `ProductComponentDto(ProductComponentId, ComponentName, ComponentType, PiecesPerGarment, Status)`
  - `ProductVariantDto(ProductVariantId, Color, SizeCode, Status)`
  - `ProductNormDto(ProductComponentMaterialId, ComponentName, Color, MaterialId, MaterialCode, MaterialName, Unit, MaterialColor, SpecNote?, MaterialType="")`
  - `ProductMarkerComponentDto(MarkerNo, ComponentName)`; `ProductMarkerFabricDto(MarkerNo, Color, MaterialId, MaterialCode, MaterialName, MaterialColor)`
- `SpecImportPreviewDto(ProductId, ProductCode, OrderId?, OrderCode?, Components[], Norms[], MarkerGroups[], OrderMarkers[], HasComponents, HasNorms, HasMarkerGroups, HasOrderMarkers, ValidCount, ErrorCount, DuplicateCount, ChangeCount, CrossErrors[], Notes[], CanImport, RemovedCount=0, RemovedNames?, OrderSizes?)`; mỗi dòng có `Action` = `New | Update | Unchanged | Error`.
- `SpecImportResultDto(Components, Norms, MarkerGroups, OrderMarkers, Removed=0)`
- `ExcelBundleCheckResultDto(Products?, Spec, OrderMaterials?, MissingMaterials[])`; `Action` thêm giá trị `Pending`.
- Upload ảnh (A13): kiểu trả về của `UploadSampleImageAsync` nằm trong Service → `[?]` (comment: "Trả khóa ảnh").

**Công đoạn / nhóm / đơn giá**
- `SaveOperationDto(ProductId, OperationCode, OperationName, StationSequence: short, SkillLevel: byte?, StandardTimeSec: decimal?, TeamType?, IsFinalStep: bool, GroupName? = null, ComponentIds: int[]? = null)`
- `OperationDto(OperationId, ProductId, ProductCode, OperationCode, OperationName, StationSequence, SkillLevel?, StandardTimeSec?, TeamType, IsFinalStep, Status, CurrentRate?, NextRate?, OperationGroupId?, GroupName?, ComponentIds?, UpcomingRate?)`; `OperationRateDto(PieceRateId, Rate, EffectiveDate, Status, RejectReason?)`
- `CreateOperationGroupDto(ProductId, GroupName)`; `RenameOperationGroupDto(GroupName)`; `SetOperationsGroupDto(ProductId, OperationIds: int[], OperationGroupId: int?)` (null = bỏ nhóm); `OperationGroupDto(OperationGroupId, GroupName, OperationCount)`
- `OperationExcelSheetDto(SheetName, ProductText?, HasRateColumn, Rows: OperationExcelRowDto[], HasGroupColumn=false, HasComponentColumn=false)`; `OperationExcelRowDto(RowNumber, Stt, Name?, SkillLevel: byte?, StandardTimeSec: decimal?, Equipment?, Rate: decimal?, ParseError?, GroupName?, ComponentNames?, IsFinal=false)`
- `OperationImportPreviewDto(SheetName, SheetNames[], ProductText?, NewCount, UpdateCount, UnchangedCount, ErrorCount, ProductHasFinalStep, HasRateColumn, RateNewCount, RateUpdateCount, RateUnchangedCount, RateSkippedCount, Rows[], HasGroupColumn, HasComponentColumn, FinalSttInFile?, RemoveCount=0, Removed?, Blocked?, WarningCount=0)`; `OperationImportRowDto.Action` = `New | Update | Unchanged | Error`, `RateAction` mặc định `"None"`.
- `OperationImportResultDto(Created, Updated, Unchanged, Skipped, RateCreated, RateUpdated, RateUnchanged, RateSkipped, Removed=0)`
- `SavePieceRateDto(OperationId, Rate: decimal, EffectiveDate: DateOnly)`; `SubmitPieceRatesDto(Ids: int[])`; `ApprovePieceRateDto(Note?)`; `ApproveManyPieceRatesDto(Ids: int[], Note?)`; `RejectPieceRateDto(Reason)`
- `PieceRateDto(PieceRateId, OperationId, OperationCode, OperationName, ProductId, ProductCode, Rate, EffectiveDate, Status, SubmittedAt?, ApprovedAt?, ApprovalNote?, RejectReason?)`

Kiểu trả về thực tế của mọi endpoint (trừ A14/B3 trả `File`) là `this.ToResponse(Result<T>)`; ánh xạ `Result → HTTP status/JSON envelope` nằm ngoài danh sách đọc → `[?]` (mã 400/404/409 do `ToResponse` quyết định).

---

## 2. Ràng buộc dữ liệu (Input & DB Schema)

### 2.1 Input (DTO / Controller)

| Trường | Ràng buộc xác nhận được | Ghi chú |
|---|---|---|
| `{id}` | Route constraint `:int` (id không phải số → 404 do không khớp route, không vào action) | A3–A12, A15–A16, D7–D9, F3–F4, G2, G4, H3, H5 |
| `file` (IFormFile) | Non-nullable ở A1, A11, A12, D2, D3 → thiếu file gây lỗi bind/NRE `[?]`; A13 là `IFormFile?` (thiếu file → `Content = []`, `FileName = ""`, Service quyết định lỗi `[?]`) | Comment `FileUpload`: ≤ 5 MB; `RequestSizeLimit` A13 = 6 MB |
| `productId` (query, D1–D3, D4–D5, C1, E1, F1) | `int` không nullable → thiếu = 0 hoặc 400 do `[ApiController]` `[?]` | |
| `effectiveDate` | `DateOnly?` | |
| `PieceRate Rate` | `decimal` (không nullable) | DB `decimal(18,2)` |
| `ProductCode` DTO | Không có attribute; MaxLength DTO `[?]` (Validator) | DB: `varchar(30)` |
| `Reason` (`RejectPieceRateDto`) | `string` non-null; comment controller: "bắt buộc lý do" | `TextLimits.Reason = 500` (khớp `NVARCHAR(500)`); việc Validator áp dụng giới hạn này cho `Reason` là `[?]` |
| `ExtraImages` | tối đa 5 (comment DTO) — thực thi `[?]` | |
| `PageSize` | kẹp về [1,100], không lỗi | `PageQuery` |

`TextLimits.Password` (50) không liên quan batch này.

### 2.2 DB Schema (EF Configuration)

Mặc định thời gian: `GETDATE-VN` = `dbo.fn_VnNow()`.

**Product** (`UQ_Product_Code`)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| ProductCode | varchar(30) (`IsUnicode(false)`) | NOT NULL, **UNIQUE** (không filter) |
| SampleImageUrl | nvarchar(500) | NULL |
| Description | nvarchar(1000) | NULL |
| Status | varchar(20) | default `'ACTIVE'` |
| CreatedAt | datetime2(0) | default `fn_VnNow()` |

**ProductColor**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Color | nvarchar(50) | NOT NULL; **UNIQUE (ProductId, Color)** `UQ_PCol`; UNIQUE (ProductColorId, ProductId) `UQ_PCol_IdProduct` (đích của FK ghép) |
| ProductId | int | FK → Product `FK_PCol_Product` (ClientSetNull) |
| CreatedAt | datetime2(0) | default `fn_VnNow()` |

**ProductVariant**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| SizeCode | varchar(10) | NOT NULL; **UNIQUE (ProductColorId, SizeCode)** `UQ_PV`; UNIQUE (ProductVariantId, ProductId) |
| Status | varchar(20) | default `'ACTIVE'` |
| ProductId | int | FK → Product `FK_PV_Product` |
| (ProductColorId, ProductId) | int, int | FK ghép → ProductColor(ProductColorId, ProductId) `FK_PV_Color` |

**ProductComponent**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| ComponentName | nvarchar(100) | NOT NULL; **UNIQUE (ProductId, ComponentName)** `UQ_PC`; UNIQUE (ProductComponentId, ProductId) |
| ComponentType | varchar(10) | NOT NULL (giá trị `PANEL` / `TRIM` theo comment Entity; CHECK constraint `[?]` — không có trong EF config) |
| PiecesPerGarment | smallint | NOT NULL, default `1` |
| Status | varchar(20) | default `'ACTIVE'` |
| ProductId | int | FK → Product `FK_PC_Product` |

**ProductComponentMaterial**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| SpecNote | nvarchar(200) | NULL |
| Status | varchar(20) | default `'ACTIVE'` (Entity default = `Constants.RecordStatus.Active`) |
| (ProductColorId, ProductComponentId, MaterialId) | int ×3 | **UNIQUE** `UQ_PCM`; index `IX_PCM_Material` |
| MaterialId | int | FK → Material `FK_PCM_Material` |
| (ProductComponentId, ProductId) | int, int | FK ghép → ProductComponent `FK_PCM_Component` |
| (ProductColorId, ProductId) | int, int | FK ghép → ProductColor `FK_PCM_Color` |

→ FK ghép buộc component và color phải **cùng ProductId**.

**ProductMarker** (có trigger `trg_ProductMarker_Check`)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| MarkerNo | tinyint | NOT NULL |
| MarkerCode | varchar(20) | NOT NULL; **UNIQUE (OrderId, ProductId, MarkerNo, MarkerCode)** `UQ_PM` |
| LengthM | **decimal(7,2)** | NOT NULL |
| WidthM | **decimal(4,2)** | NOT NULL (tối đa theo comment Entity 3,5 m, DEV-ALL-102; thực thi bởi trigger/CHECK `[?]`) |
| Status | varchar(20) | default `'ACTIVE'` |
| OrderId | int | FK → CustomerOrder `FK_PM_Order` |
| ProductId | int | FK → Product `FK_PM_Product` |

**ProductMarkerRatio** (có trigger `trg_ProductMarkerRatio_Check`)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| (ProductMarkerId, SizeCode) | int, varchar(10) | **PK ghép** |
| PiecesPerPly | smallint | NOT NULL |
| ProductMarkerId | int | FK → ProductMarker `FK_PMR_Marker` |

**ProductMarkerComponent**
- PK ghép (ProductId, MarkerNo, ProductComponentId); `MarkerNo` tinyint.
- FK ghép (ProductComponentId, ProductId) → ProductComponent `FK_PMC_Component`. Không có FK nào tới "nhóm bìa" (MarkerNo chỉ là số).

**ProductMarkerFabric**
- **UNIQUE (ProductId, MarkerNo, ProductColorId)** `UQ_PMF` → một (nhóm bìa, màu) chỉ một vải.
- FK: `FK_PMF_Color` (ghép ProductColorId+ProductId), `FK_PMF_Product`, `FK_PMF_Material`.

**Operation**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| OperationCode | varchar(30) | NOT NULL; **UNIQUE (ProductId, OperationCode)** `UQ_Operation`; UNIQUE (OperationId, ProductId) |
| OperationName | nvarchar(200) | NOT NULL |
| StationSequence | smallint | NOT NULL |
| SkillLevel | tinyint | NULL |
| StandardTimeSec | **decimal(9,2)** | NULL |
| TeamType | varchar(10) | NOT NULL, default `'SEWING'` |
| IsFinalStep | bit | NOT NULL |
| Status | varchar(20) | default `'ACTIVE'` |
| ProductId | int | FK → Product `FK_Operation_Product` |
| (OperationGroupId, ProductId) | int?, int | FK ghép → OperationGroup `FK_Operation_Group` (OperationGroupId nullable; không khai báo OnDelete) |
| — | — | **UNIQUE filtered** `UX_Operation_FinalStep` trên `ProductId` WHERE `IsFinalStep = 1 AND Status = 'ACTIVE'` → tối đa 1 công đoạn cuối ACTIVE / mã hàng |
| — | — | Index lọc `IX_Operation_Group` WHERE `OperationGroupId IS NOT NULL` |

**OperationGroup**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| GroupName | nvarchar(200) | NOT NULL; **UNIQUE (ProductId, GroupName)** `UQ_OG_Name`; UNIQUE (OperationGroupId, ProductId); index `IX_OG_Name` |
| ProductId | int | FK → Product `FK_OG_Product` |

Entity `OperationGroup` không có cột Status → xóa nhóm (F4) là hard delete hay bị chặn: `[?]`.

**OperationComponent**
- PK ghép (OperationId, ProductComponentId); cột `ProductId` thứ ba.
- FK ghép `FK_OC_Operation` (OperationId, ProductId) và `FK_OC_Component` (ProductComponentId, ProductId) → buộc công đoạn và chi tiết cùng ProductId.

**PieceRate** (có trigger `trg_PieceRate_Approved`)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Rate | **decimal(18,2)** | NOT NULL |
| EffectiveDate | date | NOT NULL; **UNIQUE (OperationId, EffectiveDate)** `UQ_PieceRate` (không filter theo Status) |
| Status | varchar(20) | default `'DRAFT'` |
| SubmittedAt, ApprovedAt | datetime2(0) | NULL |
| DecisionNote | nvarchar(500) | NULL |
| OperationId | int | FK → Operation `FK_PieceRate_Operation` |

### 2.3 Khớp lệch DTO ↔ DB cần lưu ý khi sinh input biên
- `StandardTimeSec` DTO `decimal?` — DB `decimal(9,2)`: giá trị > 9 999 999,99 hoặc > 2 chữ số thập phân bị DB làm tròn/tràn nếu Validator không chặn `[?]`.
- `Rate` DTO `decimal` — DB `decimal(18,2)`.
- `LengthM` `decimal(7,2)`, `WidthM` `decimal(4,2)` (tối đa 99,99; yêu cầu nghiệp vụ ≤ 3,5).
- `ComponentName` DB 100, `OperationName` 200, `GroupName` 200, `Color` 50, `SizeCode` 10, `OperationCode` 30, `ProductCode` 30, `MarkerCode` 20, `SpecNote` 200, `Description` 1000, `SampleImageUrl` 500, `DecisionNote` 500.
- `PiecesPerGarment` smallint DTO ↔ DB smallint; `MarkerNo` DTO `int` ↔ DB `tinyint` (0–255; giá trị ngoài khoảng gây lỗi DB nếu Validator không chặn `[?]`). `SkillLevel` `byte?` ↔ tinyint.

---

## 3. Các bảng DB chịu tác động (khi thành công)

DTO/Controller không cho thấy câu lệnh ghi → mọi dòng dưới là **suy ra từ tên DTO, comment và FK**, chưa xác nhận bằng Service. `[?]` = chưa xác nhận. Mọi endpoint GET và các endpoint `preview`/`check`/`parse`/`preview-data` được ghi chú "không ghi gì" trong comment controller → **không INSERT/UPDATE/DELETE** (đã xác nhận bằng comment, không phải bằng code).

### Admin / Products
| # | Endpoint | Bảng bị ghi |
|---|---|---|
| A1 | `excel/parse` | Không ghi (comment: "không ghi gì") |
| A2 | `excel/check` | Không ghi (comment) |
| A3 | `{id}/import/preview-data` | Không ghi (comment: "xem trước") |
| A4 | `{id}/import-data` | `ProductComponent` INSERT/UPDATE `[?]`; `ProductComponentMaterial` INSERT/UPDATE `[?]`; `ProductMarkerComponent`, `ProductMarkerFabric` INSERT/DELETE `[?]`; nếu có `orderId`: `ProductMarker` + `ProductMarkerRatio` INSERT/UPDATE `[?]`; `replace=true`: DELETE hoặc đổi `Status` (ngừng dùng) các dòng cũ không còn trong file `[?]`; `AuditLog` `[?]` |
| A5, A6, A9, A14 | GET | Không ghi |
| A7 | POST `products` | `Product` INSERT; `AuditLog` INSERT `[?]` (DTO chi tiết có `History: AuditLogDto[]`); ảnh phụ lưu ở bảng/kho nào `[?]` |
| A8 | PUT `products/{id}` | `Product` UPDATE; `AuditLog` `[?]`; ảnh phụ `[?]` |
| A10 | PUT `{id}/spec` | `ProductComponent` INSERT/UPDATE/DELETE (hoặc `Status`→ngừng dùng nếu đã được dùng, theo comment `SaveProductSpecDto`); `ProductComponentMaterial` DELETE+INSERT (thay bằng danh sách gửi lên, chỉ các biến thể/màu có trong danh sách); `ProductMarkerComponent` thay nguyên; `ProductMarkerFabric` thay nguyên (vải do backend suy ra từ định mức); **`ProductVariant`/`ProductColor` không bị sửa** (comment: "Biến thể không bao giờ bị xóa ở đây"); `OperationComponent` `[?]` khi xóa thành phần |
| A11 | `{id}/import/preview` | Không ghi (comment) |
| A12 | `{id}/import` | Như A4 |
| A13 | POST `sample-image` | Không bảng DB nào xác nhận được; trả khóa ảnh, khóa này chỉ vào `Product.SampleImageUrl` khi A7/A8 `[?]`; có thể ghi file vào kho lưu trữ `[?]` |
| A15 | `{id}/discontinue` | `Product` UPDATE `Status`; `AuditLog` `[?]` |
| A16 | `{id}/reactivate` | `Product` UPDATE `Status`; `AuditLog` `[?]` |

### Accountant / Products
| B1, B2, B3 | GET | Không ghi |

### Operations
| # | Endpoint | Bảng bị ghi |
|---|---|---|
| C1, D1 | GET | Không ghi |
| D2, D4 | preview | Không ghi (comment: "chưa ghi DB" / "không ghi gì") |
| D3, D5 | import | `Operation` INSERT/UPDATE; `OperationGroup` INSERT `[?]` (cột NHÓM CÔNG ĐOẠN, DEV-ALL-116); `OperationComponent` INSERT/DELETE `[?]` (cột CHI TIẾT); nếu có `effectiveDate`: `PieceRate` INSERT/UPDATE với `Status` Nháp, hoặc Chờ duyệt nếu `submitRates=true` (comment); `replace=true`: DELETE hoặc ngừng dùng `Operation` không còn trong file `[?]`; `AuditLog` `[?]` |
| D6 | POST | `Operation` INSERT; `OperationGroup` INSERT `[?]` (nếu `GroupName` mới); `OperationComponent` INSERT `[?]` (nếu `ComponentIds`) |
| D7 | PUT | `Operation` UPDATE; `OperationGroup` INSERT `[?]`; `OperationComponent` DELETE+INSERT `[?]` |
| D8 | discontinue | `Operation` UPDATE `Status`; `AuditLog` `[?]` |
| D9 | reactivate | `Operation` UPDATE `Status` (bị chặn bởi `UX_Operation_FinalStep` nếu đã có công đoạn cuối ACTIVE khác) |

### OperationGroups
| E1, F1 | GET | Không ghi |
| F2 | POST | `OperationGroup` INSERT |
| F3 | PUT `{id}` | `OperationGroup` UPDATE `GroupName` |
| F4 | DELETE `{id}` | `OperationGroup` DELETE `[?]`; `Operation.OperationGroupId` → NULL `[?]` hoặc bị chặn nếu còn công đoạn `[?]` |
| F5 | PUT `operations` | `Operation` UPDATE `OperationGroupId` (null = bỏ nhóm) |

### PieceRates
| # | Endpoint | Bảng bị ghi |
|---|---|---|
| G1, H1 | GET | Không ghi |
| H2 | POST | `PieceRate` INSERT (`Status` mặc định DB `'DRAFT'`; Entity comment "Nháp") |
| H3 | PUT `{id}` | `PieceRate` UPDATE (chỉ khi chưa duyệt, theo comment controller) |
| H4, H5 | submit(-many) | `PieceRate` UPDATE `Status` → chờ duyệt, `SubmittedAt` |
| G2, G3 | approve(-many) | `PieceRate` UPDATE `Status` → đã duyệt, `ApprovedAt`, `DecisionNote` (= `Note`); trigger `trg_PieceRate_Approved` chạy `[?]` |
| G4 | reject | `PieceRate` UPDATE `Status` → bị từ chối, `DecisionNote` (= `Reason`) `[?]` |

Tất cả có thể ghi `AuditLog`/thông báo `[?]`.

---

## 4. Lưu ý kỹ thuật cho QA

### 4.1 Trigger DB
| Trigger | Bảng | Tác động test |
|---|---|---|
| `trg_ProductMarker_Check` | ProductMarker | EF cấu hình `HasTrigger` → không dùng `OUTPUT` khi ghi. Nội dung kiểm tra `[?]` (dự đoán: giới hạn khổ ≤ 3,5 m, theo comment Entity) |
| `trg_ProductMarkerRatio_Check` | ProductMarkerRatio | Nội dung `[?]` |
| `trg_PieceRate_Approved` | PieceRate | Nội dung `[?]`; có thể chặn sửa/xóa đơn giá đã duyệt. Dùng assert DB sau khi approve để phát hiện |

Assert kép với các bảng có trigger: kiểm tra trực tiếp DB, không chỉ đọc response.

### 4.2 Quy ước HOA/thường của giá trị
| Đối tượng | DB (varchar) | DTO / Query |
|---|---|---|
| `Product.Status` | `ACTIVE` (default); giá trị ngừng dùng `[?]` (dự đoán `DISCONTINUED`, theo comment `SpecOrderMarkerExcelRow.Status: ACTIVE / DISCONTINUED`) | `ProductQuery.Status`: PascalCase `Active` / `Discontinued` |
| `ProductComponent.ComponentType` | `PANEL` / `TRIM` | `ProductComponentDto.ComponentType` và `SaveProductComponentDto.ComponentType`: chưa rõ có nhận chữ thường không `[?]`; `SpecComponentExcelRow.ComponentType` comment `PANEL / TRIM` |
| `Operation.TeamType` | `SEWING` (default); `CUTTING` `[?]` | `SaveOperationDto.TeamType`: comment "PascalCase `Sewing` \| `Cutting` (mặc định Sewing)"; `OperationDto.TeamType` trả lại dạng nào `[?]` |
| `Operation.Status` | `ACTIVE`, … | `OperationDto.Status` PascalCase hay hoa `[?]` |
| `PieceRate.Status` | `DRAFT` (default); các trạng thái khác `[?]` | `PieceRateQuery.Status`: PascalCase `PendingApproval` (comment); `PieceRateDto.Status` `[?]` |
| `Import Action` | — | `New \| Update \| Unchanged \| Error` (PascalCase); `Pending` ở bundle check; `RateAction` mặc định `"None"` |
| `SpecOrderMarkerExcelRow.Status` | — | `ACTIVE / DISCONTINUED` (chữ HOA, ô trống = ACTIVE) — khác `Product.Status` PascalCase của query |
| `parts` | — | Controller tự `ToLowerInvariant` → gửi `COMPONENTS` vẫn hợp lệ |

→ Test cần khai báo rõ Status DB (HOA) vs Status API (PascalCase) khi seed và khi assert; các giá trị `[?]` phải đọc từ DB thực tế sau khi chạy.

### 4.3 Ma trận phân quyền chéo
| Tài nguyên | Admin (`admin/…`) | Accountant (`accountant/…`) |
|---|---|---|
| Products list / spec / sample-image | ✔ (A5, A9, A14) | ✔ chỉ GET (B1–B3) |
| Products detail `{id}`, create, update, discontinue, reactivate | ✔ | ✘ (không có route → 404) |
| Products save spec `PUT {id}/spec`, import Excel (spec) | ✔ | ✘ |
| Operations list | ✔ chỉ GET | ✔ GET + toàn bộ ghi/nhập |
| Operations create/update/discontinue/reactivate/import | ✘ | ✔ |
| OperationGroups list | ✔ chỉ GET | ✔ GET + create/rename/delete/set |
| PieceRates list | ✔ (không thấy `Draft`: `includeDraft:false` cố định) | ✔ (thấy Nháp `[?]`) |
| PieceRates create/update/submit | ✘ | ✔ |
| PieceRates approve/approve-many/reject | ✔ | ✘ |

Các case phân quyền cần test (từ ma trận): 401 khi không token; 403 khi role sai (Accountant gọi `/admin/...`, Admin gọi `/accountant/...`, `Manager`, `Worker`…); 404 khi gọi route không tồn tại ở prefix kia (vd Accountant `POST /accountant/products`, Admin `POST /admin/operations`, Admin `POST /admin/operationgroups`). `Manager` không được phép ở mọi endpoint (kể cả khi comment nói "Admin / Quản đốc nhập Excel").

### 4.4 Điểm bất thường DTO / Comment ↔ Entity
1. **`Operation` ↔ `Product` cấu hình 1-1**: `HasOne(d => d.Product).WithOne(p => p.Operation).HasForeignKey<Operation>(ProductId)`; nhưng DB cho phép nhiều Operation / Product (`UQ_Operation` chỉ unique theo `(ProductId, OperationCode)`; ràng buộc "1 công đoạn cuối" dùng index lọc). Test tạo ≥ 2 công đoạn / mã hàng để xác nhận không bị lỗi mô hình.
2. **`PieceRateDto` có `ApprovalNote` và `RejectReason`, Entity chỉ có `DecisionNote`** (nvarchar 500). Cả hai trường DTO có thể cùng ánh xạ từ `DecisionNote` theo Status `[?]` → assert DB cột `DecisionNote`, so sánh DTO theo Status.
3. **`OperationRateDto.RejectReason`** cũng lấy từ `DecisionNote` `[?]`.
4. **`SubmitPieceRatesDto`**: comment "Duyệt nhiều đơn giá chờ duyệt một lần" nhưng DTO dùng cho **submit** (gửi duyệt); comment bị sao chép nhầm từ `ApproveManyPieceRatesDto`.
5. **`ProductVariantDto.Color`** là chuỗi nhưng Entity `ProductVariant` chỉ có `ProductColorId` → màu lấy qua join `ProductColor.Color`. Tương tự `ProductNormDto.Color`, `MaterialColor` (`Material.Color`), `Unit`, `MaterialType`.
6. **`ProductMarkerComponent`**: có `MarkerNo` nhưng không có bảng "nhóm bìa" → không có FK; MarkerNo không có tồn tại ràng buộc DB, chỉ Service kiểm tra `[?]`. `ProductMarkerFabric.MarkerNo` tương tự.
7. **`ProductMarker` (bìa của đơn)** nằm trong Entity thuộc phạm vi nhưng chỉ bị ghi qua import có `orderId` (A4, A12, A3/A11 xem trước); không có endpoint CRUD riêng trong batch.
8. **`MarkerNo`**: DTO `int` vs DB `tinyint`; `SpecMarkerGroupExcelRow.MarkerNo` `int?`.
9. **`ProductComponentMaterial`** UNIQUE `(ProductColorId, ProductComponentId, MaterialId)` — định mức theo màu × chi tiết × NVL, không theo size (comment DEV-ALL-87). `SaveProductNormDto` không có Quantity/Waste (comment: "không còn lượng / hao hụt").
10. **`SaveProductVariantDto`**: "biến thể phải đã có (do đơn hàng tạo)", PUT spec không tạo biến thể; nếu gửi biến thể/màu chưa tồn tại → lỗi kỳ vọng ở Service `[?]`, cần seed `ProductColor` + `ProductVariant` bằng `TestDataSeeder`.
11. **Comment Admin/Operations & OperationGroups** (DEV-ALL-117): Admin chỉ xem; route ghi của Admin đã bị gỡ → test 404/405 cho `POST/PUT/DELETE` trên prefix admin.
12. **Controller `PreviewImportData`/`ImportData` (A3, A4)** chỉ gửi `workbook` (không bọc `ExcelBundleDto`), ngược với `excel/check` bọc trong `ExcelBundleCheckDto.Bundle`. Dễ nhầm body khi viết test.
13. **`Admin PieceRates.List`** truyền `includeDraft:false` cứng; Accountant dùng mặc định → test lọc Draft theo từng role.
14. **`OperationGroup.OperationCount`** tính cả công đoạn đã ngừng dùng (comment) → assert đếm gồm `Status != ACTIVE`.
15. **Giới hạn `Reason` 500** (`TextLimits.Reason`) khớp `DecisionNote` nvarchar(500): test biên 500/501 cho `RejectPieceRateDto.Reason`; `ApprovePieceRateDto.Note` cũng lưu vào `DecisionNote` `[?]`.
16. **`PieceRate` UNIQUE `(OperationId, EffectiveDate)`** không lọc Status → bản ghi Bị từ chối vẫn chiếm ngày hiệu lực; tạo đơn giá trùng ngày sau khi từ chối có thể trả lỗi (409/400 `[?]`) hoặc lỗi DB nếu Service không xử lý (500 = bug).
17. **`UX_Operation_FinalStep`**: tạo công đoạn cuối thứ 2 (ACTIVE) cho cùng mã hàng, hoặc reactivate công đoạn cuối khi đã có công đoạn cuối ACTIVE khác → vi phạm UNIQUE nếu Service không chặn trước (500 = lỗi ứng dụng).
18. **FK ghép** (component/color/variant/group/operation phải cùng `ProductId`): dùng id của mã hàng khác trong `SaveOperationDto.ComponentIds`, `SetOperationsGroupDto.OperationIds/OperationGroupId`, `SaveProductNormDto` là case biên cần test.
19. **Số lượng ảnh / kích thước**: `RequestSizeLimit` chỉ áp dụng cho A13 (6 MB) trong khi comment `FileUpload` nói ≤ 5 MB → ngưỡng 5 MB do Service kiểm `[?]`; A1/A11/A12/D2/D3 dùng giới hạn mặc định của Kestrel/ASP.NET.
20. **Rollback**: A4/A12/D3/D5 comment "trong một giao dịch" → test rollback: một dòng lỗi giữa chừng thì không bảng nào bị ghi (assert DB).
