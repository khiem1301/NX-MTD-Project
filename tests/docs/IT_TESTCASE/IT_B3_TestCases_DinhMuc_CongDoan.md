# Integration Test Cases — Batch 3: Định mức mã hàng & Công đoạn (`IT_B3_xxx`)

Nguồn: `IT_CONTRACT — Batch 3`. Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**.

> **Phạm vi trùng với Batch 2:** các ca về ảnh phụ, upload ảnh mẫu, validate ProductCode/Description và CRUD chung của Product đã có ở `IT_B2_107`–`IT_B2_139`. Batch 3 chỉ nhắc lại các ca đại diện, và tập trung vào phần mới: **FK ghép theo ProductId**, **trigger** (`trg_ProductMarker_Check`, `trg_ProductMarkerRatio_Check`, `trg_PieceRate_Approved`), **`UX_Operation_FinalStep`**, **`UQ_PieceRate`**, luồng duyệt đơn giá, và nhập Excel công đoạn.

## 0. Quy ước chung

**HTTP kỳ vọng**
- `401` thiếu/hết hạn/sai chữ ký token. `403` sai role ở prefix. `404` không có tài nguyên, không có route, hoặc `{id:int}` không khớp. `405` có route nhưng sai method. `400` dữ liệu sai. `409` trùng UNIQUE hoặc xung đột trạng thái.
- **Bất kỳ `500` nào cũng là FAIL.** Body lỗi không được lộ tên constraint, tên trigger, `SqlException` hay stack trace. Lỗi do trigger chặn (`RAISERROR`/`THROW`) phải được map thành 400/409.
- `ToResponse` (cách map `Result` sang HTTP) chưa được đọc, nên các mã 400/404/409 trong bảng là kỳ vọng theo nghiệp vụ. Nếu khác thì ghi lại, nhưng `500` luôn là FAIL.

**Quy ước cho vế [DB]**
- Trạng thái lưu VIẾT HOA:
  - `Product`, `ProductComponent`, `ProductComponentMaterial`, `ProductMarker`, `Operation`: `ACTIVE`, giá trị ngừng dùng là `DISCONTINUED` `[?]`.
  - `Operation.TeamType`: `SEWING` / `CUTTING` `[?]`.
  - `PieceRate.Status`: `DRAFT`, `PENDING_APPROVAL` `[?]`, `APPROVED` `[?]`, `REJECTED` `[?]`.
  - Assert thêm rằng không có giá trị PascalCase (`Sewing`, `PendingApproval`) bị lưu nhầm.
- "DB không đổi" = snapshot các bảng liên quan trước và sau khi gọi trùng nhau (`COUNT(*)` và các cột nghiệp vụ, gồm cả `AuditLogs`).
- Với bảng có trigger: **assert trực tiếp trên DB**, không chỉ tin vào response.
- Các cột ngày (`EffectiveDate`, `SubmittedAt`, `ApprovedAt`, `CreatedAt`) không bao giờ được là `0001-01-01`.

**Viết tắt URL:** `A/` = `/api/v1/admin/`, `AC/` = `/api/v1/accountant/`. Tên controller: `products`, `operations`, `operationgroups`, `piecerates`.

**Dữ liệu nền S0** (seed bằng `TestDataSeeder`, reset trước mỗi test)

- **Token:** TK_ADM, TK_ACC, TK_MGR, TK_WK, TK_TL, TK_WRK, TK_EXP (hết hạn), TK_FORGED (sai chữ ký).
- **Material:** MA1 `V-0001` "Kaki" FABRIC, màu Đen; MA2 `V-0002` "Kaki" FABRIC, màu `''`; MA3 `PL-0001` TRIM; MA4 `V-0003` FABRIC, đã ngừng dùng `[?]`.

**PR1 `SP001`** (`ACTIVE`, đơn OD1, mã hàng chính dùng cho hầu hết các test)

- **Màu và biến thể:** `ProductColor` PC_DEN "Đen", PC_TRANG "Trắng". `ProductVariant` (Đen, M), (Đen, L), (Trắng, M), đều do đơn hàng tạo.
- **Components:** C1 "Thân trước" (PANEL, 1, đang được dùng); C2 "Túi" (PANEL, 2, chưa dùng); C3 "Nhãn" (TRIM, 1).
- **Định mức và bìa:**
  - Định mức: (C1, Đen, MA1).
  - Nhóm bìa 1 gồm chi tiết C1 và vải (1, Đen, MA1).
  - `ProductMarker` của OD1: `MarkerNo=1`, `MarkerCode="S1"`, `LengthM=5.20`, `WidthM=1.50`. Ratio: M:2, L:1.
- **Operation:**
  - OP1 `CD01`, seq 1, ACTIVE, thuộc OG1, chi tiết [C1].
  - OP2 `CD02`, seq 2, ACTIVE.
  - OP3 `CD99`, `IsFinalStep=1`, ACTIVE.
  - OP4 `CD98`, `IsFinalStep=1`, đã ngừng `[?]`.
  - OP5 `CD05`, đã ngừng `[?]`, thuộc OG1.
- **OperationGroup:** OG1 "Nhóm May" (gồm OP1, OP5, nên `OperationCount=2`); OG2 "Nhóm Rỗng" (0 công đoạn).
- **PieceRate:**
  - PR_D1: OP1, 2026-12-01, `DRAFT`, Rate 1500.
  - PR_P1: OP2, 2026-12-01, chờ duyệt `[?]`, 2000.
  - PR_A1: OP1, 2026-01-01, đã duyệt `[?]`, 1200.
  - PR_R1: OP2, 2026-11-01, bị từ chối `[?]`, `DecisionNote` = "Sai giá".

**PR2 `SP002`** (`ACTIVE`, đơn OD2, mã hàng khác, dùng làm mục tiêu cho các ca chéo mã hàng và IDOR)

- Màu PC2_XANH "Xanh", biến thể (Xanh, M), component C9 "Thân sau".
- OperationGroup OG9 "Nhóm B", Operation OP9 `CD01` (trùng mã với OP1 nhưng khác mã hàng).
- PieceRate PR_P9: OP9, chờ duyệt.

**PR3 `SP003`:** đã ngừng `[?]`, không có công đoạn.

**File mẫu:** `spec_ok.xlsx`, `spec_err.xlsx`; `ops_ok.xlsx` (2 sheet "May", "Cắt"; có cột ĐƠN GIÁ, NHÓM, CHI TIẾT); `ops_err.xlsx` (dòng thứ 3 có `ParseError`); `ops_2final.xlsx` (2 dòng `IsFinal`); `corrupt.xlsx`; `not_excel.pdf`; `big_6mb.xlsx` (khoảng 6 MB).

---

## 1. Ma trận phân quyền chéo Admin ↔ Accountant

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_001 | S0 | Không gửi token: `GET A/products`, `GET AC/operations?productId=<PR1>`, `GET A/piecerates`, `GET AC/operationgroups?productId=<PR1>` | [API]: 401 với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B3_002 | TK_EXP; TK_FORGED (claim Admin) | `POST A/piecerates/<PR_P1>/approve {Note:null}` | [API]: 401.<br>[DB]: PR_P1 vẫn ở trạng thái chờ duyệt; `ApprovedAt IS NULL`. | Abnormal |
| IT_B3_003 | TK_ADM | `GET AC/operations?productId=<PR1>`; `GET AC/piecerates`; `GET AC/products` | [API]: **403** với cả 3 (prefix accountant chỉ cho `Accountant`).<br>[DB]: không đổi. | Abnormal |
| IT_B3_004 | TK_ADM | `POST AC/operations {…}`; `POST AC/piecerates {OperationId:<OP2>, Rate:2500, EffectiveDate:"2027-01-01"}`; `POST AC/piecerates/<PR_D1>/submit` | [API]: **403** với cả 3.<br>[DB]: không có Operation/PieceRate mới; PR_D1 vẫn `DRAFT`. | Abnormal |
| IT_B3_005 | TK_ADM | `POST A/operations {…}`; `PUT A/operations/<OP1> {…}`; `POST A/operations/<OP1>/discontinue`; `POST A/operations/import-data?productId=<PR1>` | [API]: 404/405 (route ghi của Admin đã bị gỡ, DEV-ALL-117).<br>[DB]: `Operation` không đổi. | Abnormal |
| IT_B3_006 | TK_ADM | `POST A/operationgroups {ProductId:<PR1>, GroupName:"X"}`; `PUT A/operationgroups/<OG1> {…}`; `DELETE A/operationgroups/<OG2>`; `PUT A/operationgroups/operations {…}` | [API]: 404/405.<br>[DB]: `OperationGroup` và `Operation.OperationGroupId` không đổi. | Abnormal |
| IT_B3_007 | TK_ADM | `POST A/piecerates {…}`; `PUT A/piecerates/<PR_D1> {…}`; `POST A/piecerates/submit-many {Ids:[<PR_D1>]}` | [API]: 404/405.<br>[DB]: `PieceRate` không đổi. | Abnormal |
| IT_B3_008 | TK_ACC | `POST A/piecerates/<PR_P1>/approve {}`; `POST A/piecerates/approve-many {Ids:[<PR_P1>]}`; `POST A/piecerates/<PR_P1>/reject {Reason:"x"}` | [API]: **403** (Kế toán không được tự duyệt đơn giá mình lập).<br>[DB]: PR_P1 vẫn chờ duyệt; `DecisionNote` và `ApprovedAt` không đổi. | Abnormal |
| IT_B3_009 | TK_ACC | `POST AC/piecerates/<PR_P1>/approve`; `POST AC/piecerates/<PR_P1>/reject` | [API]: 404/405 (không có route ở prefix accountant).<br>[DB]: không đổi. | Abnormal |
| IT_B3_010 | TK_ACC | `GET A/products`; `PUT A/products/<PR1>/spec {…}`; `POST A/products/<PR1>/import-data {…}` | [API]: 403.<br>[DB]: spec của PR1 không đổi. | Abnormal |
| IT_B3_011 | TK_ACC | `GET AC/products/<PR1>` (chi tiết); `POST AC/products {…}`; `PUT AC/products/<PR1>/spec {…}`; `POST AC/products/<PR1>/discontinue` | [API]: 404/405 (Accountant chỉ có B1–B3).<br>[DB]: PR1 không đổi. | Abnormal |
| IT_B3_012 | TK_MGR, TK_WK, TK_TL, TK_WRK (lần lượt) | `GET A/products`; `GET AC/operations?productId=<PR1>`; `GET A/piecerates`; `POST AC/piecerates {…}` | [API]: 403 với mọi tổ hợp (không có Manager ở bất kỳ controller nào của batch này).<br>[DB]: không có PieceRate mới. | Abnormal |
| IT_B3_013 | TK_MGR | `POST A/products/<PR1>/import-data` (comment nói "Admin / Quản đốc nhập Excel") | [API]: 403.<br>[DB]: không đổi. | Abnormal |
| IT_B3_014 | TK_ADM | `GET A/operations?productId=<PR1>`; `GET A/operationgroups?productId=<PR1>` | [API]: 200 (Admin chỉ được xem).<br>[DB]: không đổi. | Normal |
| IT_B3_015 | TK_ADM | `GET /api/v1/ADMIN/PieceRates`; `GET /api/v1/admin/piece-rates` | [API]: dạng chữ hoa → 200 (route không phân biệt hoa/thường); dạng kebab `piece-rates` → 200 hoặc 404 tùy route transformer `[?]`; ghi lại.<br>[DB]: không đổi. | Boundary |

## 2. Product — các ca đại diện (bổ sung cho Batch 2)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_016 | TK_ADM | `POST A/products {ProductCode:"SP300", Description:"Quần tây"}` | [API]: 201.<br>[DB]: `Product(SP300, Status='ACTIVE')`, `CreatedAt` ≈ hiện tại (giờ VN); chưa có `ProductColor`/`ProductVariant`/`Operation` nào. | Normal |
| IT_B3_017 | SP001 tồn tại | POST `ProductCode:"SP001"`; gửi **đồng thời** 2 POST `"SP301"` | [API]: SP001 → 409; ca đồng thời → 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT(SP301) = 1`. | Abnormal |
| IT_B3_018 | TK_ADM | ProductCode dài 30 / 31 ký tự; `ProductCode:"SPĐẶC"` (Unicode trong cột varchar) | [API]: 30 → 201; 31 → 400; Unicode → 400 (không lưu thành `SP??C`); không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B3_019 | TK_ADM | POST SP302 với `ExtraImages` gồm 6 key | [API]: 400 (giới hạn 5 ảnh chỉ nằm ở comment, nên API phải tự chặn).<br>[DB]: **Rollback**: không có Product SP302; không có ảnh phụ mồ côi. | Abnormal |
| IT_B3_020 | TK_ADM | `GET A/products/<PR1>` | [API]: 200; `Colors` = ["Đen", "Trắng"] (lấy từ `ProductColor`); `IsUsed=true`; có `History[]`.<br>[DB]: `Colors` khớp `ProductColor WHERE ProductId=PR1`. | Normal |
| IT_B3_021 | TK_ACC | `GET AC/products?Status=Active&PageSize=500`; `?Page=-5` | [API]: 200; số phần tử ≤ 100 (PageSize bị kẹp, không báo lỗi); `Page=-5` được coi như trang 1.<br>[DB]: không đổi. | Boundary |
| IT_B3_022 | TK_ACC | `GET AC/products?SortDir=ASC`; `?SortDir=xyz` | [API]: `ASC` → tăng dần (không phân biệt hoa/thường); `xyz` → giảm dần.<br>[DB]: không đổi. | Boundary |
| IT_B3_023 | TK_ACC | `GET AC/products/sample-image?key=../../appsettings.json` | [API]: 400/404, không trả nội dung file hệ thống.<br>[DB]: không đổi. | Abnormal |
| IT_B3_024 | PR1 có Operation và PieceRate | `POST A/products/<PR1>/discontinue` | [API]: 200 hoặc 409 `[?]`; không 500.<br>[DB]: `Operation`, `PieceRate`, `ProductMarker` của PR1 không bị xóa và không bị đổi trạng thái theo (không cascade). | Abnormal |

## 3. Spec `PUT A/products/{id}/spec` — FK ghép cùng ProductId và toàn vẹn

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_025 | PR1 như S0 | `GET A/products/<PR1>/spec`; `GET AC/products/<PR1>/spec` | [API]: 200 với cả hai, dữ liệu giống nhau.<br>- `Variants[].Color` lấy qua join `ProductColor`.<br>- `Norms[]` có `MaterialCode`, `MaterialName`, `Unit`, `MaterialColor`.<br>- `MarkerFabrics[]` có dòng (1, Đen, MA1).<br>[DB]: dữ liệu trả về khớp các join `ProductVariant`–`ProductColor` và `ProductComponentMaterial`–`Material`. | Normal |
| IT_B3_026 | PR1 như S0 | PUT spec:<br>- Components = C1, C2, C3 (giữ `ProductComponentId`) + "Cổ" (PANEL, 1)<br>- Variants = 3 biến thể hiện có<br>- Norms = (Thân trước, Đen, MA1), (Cổ, Đen, MA2), (Thân trước, Trắng, MA2)<br>- MarkerComponents = (1, Thân trước), (1, Cổ)<br>- MarkerColors = (1, Đen), (1, Trắng) | [API]: 200.<br>[DB]:<br>- Thêm `ProductComponent` "Cổ".<br>- `ProductComponentMaterial` của PR1 có đúng 3 dòng gửi lên, `Status='ACTIVE'`.<br>- `ProductMarkerComponent` có đúng 2 dòng.<br>- `ProductMarkerFabric` được backend suy ra cho (1, Đen) và (1, Trắng).<br>- `ProductVariant`/`ProductColor` **không đổi** (cả số lượng lẫn giá trị). | Normal |
| IT_B3_027 | PR1 như S0 | PUT spec chỉ gửi Variants của màu **Đen** (bỏ biến thể màu Trắng) | [API]: 200.<br>[DB]: biến thể (Trắng, M) **không** bị xóa và không bị đổi `Status`; định mức của màu Trắng không bị động tới (chỉ thay định mức của các màu có trong danh sách). | Normal |
| IT_B3_028 | C2 "Túi" chưa dùng; C1 đang được dùng (OperationComponent, đơn OD1) | PUT spec bỏ cả C1 và C2 khỏi Components (kèm bỏ định mức/bìa liên quan) | [API]: 200 (hoặc 409 nếu C1 còn gắn với OperationComponent `[?]`).<br>[DB]: C2 bị DELETE; C1 chỉ chuyển `Status` sang ngừng dùng `[?]` (không DELETE); `OperationComponent` (OP1, C1) **không** bị xóa mồ côi. | Normal |
| IT_B3_029 | PR1 có màu Đen, Trắng | Norm `(Thân trước, "Xanh", MA1)` — màu "Xanh" chỉ có ở PR2 | [API]: 400.<br>[DB]: **Rollback toàn bộ**: Components/Norms/Markers của PR1 không đổi; **không 500** do vi phạm `FK_PCM_Color` (FK ghép ProductColorId + ProductId). | Abnormal |
| IT_B3_030 | C9 thuộc PR2 | PUT spec PR1 với Component `{ProductComponentId:<C9>, ComponentName:"Thân sau", …}` | [API]: 400/404 (chi tiết thuộc mã hàng khác, là IDOR chéo mã hàng).<br>[DB]: C9 của PR2 **không** bị đổi tên/loại; PR1 không đổi. | Abnormal |
| IT_B3_031 | Biến thể (Xanh, M) thuộc PR2 | PUT spec PR1 với Variant `{ProductVariantId:<PV của PR2>, Color:"Xanh", SizeCode:"M"}` | [API]: 400/404.<br>[DB]: `ProductVariant` của PR2 không đổi; PR1 không có màu Xanh. | Abnormal |
| IT_B3_032 | PR1 như S0 | Variant mới chưa tồn tại `(Đen, XL)` không có `ProductVariantId` | [API]: 400, hoặc 200 nhưng bỏ qua biến thể mới `[?]`.<br>[DB]: **không** có dòng `ProductVariant` (Đen, XL) (spec không được tạo biến thể). | Abnormal |
| IT_B3_033 | TK_ADM | Norm với `MaterialId:999999`; với `MaterialId:<MA4>` (đã ngừng) | [API]: 999999 → 400/404 (không 500 do `FK_PCM_Material`); MA4 → 400 `[?]`.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B3_034 | TK_ADM | Hai Norm trùng `(Thân trước, Đen, MA1)` trong cùng request | [API]: 400 (hoặc gộp thành 1 `[?]`); không 500 do `UQ_PCM`.<br>[DB]: không có dòng trùng. | Abnormal |
| IT_B3_035 | TK_ADM | Hai Norm `(Thân trước, Đen, MA1)` và `(Thân trước, Đen, MA2)` | [API]: 200 (khóa UNIQUE gồm cả MaterialId, nên một chi tiết được dùng nhiều NVL).<br>[DB]: có 2 dòng `ProductComponentMaterial` cho (Đen, Thân trước). | Normal |
| IT_B3_036 | TK_ADM | Hai Component cùng tên "Túi"; tên chỉ khác hoa/thường `"túi"` so với "Túi" có sẵn | [API]: 400/409; ca hoa/thường ghi lại theo collation `[?]`; không 500 do `UQ_PC`.<br>[DB]: không đổi. | Abnormal |
| IT_B3_037 | TK_ADM | ComponentName dài 100 / 101 ký tự có dấu; SpecNote dài 200 / 201 ký tự | [API]: bằng ngưỡng → 200; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu, đúng dấu. | Boundary |
| IT_B3_038 | TK_ADM | `ComponentType:"BUTTON"`; `"panel"`; `"PANELXXXXXX"` (11 ký tự); `""` | [API]: 400 (DB không có CHECK, nên API phải chặn); `panel` → 400 hoặc chuẩn hóa `[?]`; 11 ký tự → 400, không 500 (cột `varchar(10)`).<br>[DB]: không tồn tại `ComponentType` nào ngoài {`PANEL`, `TRIM`}. | Boundary |
| IT_B3_039 | TK_ADM | `PiecesPerGarment`: `0`; `-1`; `1`; `32767`; `32768` | [API]: 0 / −1 → 400; 1 → 200; 32767 → 200 hoặc 400 `[?]`; 32768 → 400 (tràn `short` khi bind), không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B3_040 | TK_ADM | MarkerComponents với `MarkerNo`: `0`; `255`; `256`; `-1` (DTO là int, DB là `tinyint`) | [API]: 255 → 200 (hoặc 400 theo giới hạn nghiệp vụ `[?]`); 256 / −1 → **400**, không 500 do tràn `tinyint`; 0 → 400 `[?]`.<br>[DB]: không tồn tại `MarkerNo` ngoài [0..255]. | Boundary |
| IT_B3_041 | TK_ADM | MarkerComponents trỏ tới `"Tay áo"` (không có trong Components); MarkerColors trỏ tới màu "Xanh" (không có ở PR1) | [API]: 400 (`MarkerNo` không có FK, nên Service phải chặn).<br>[DB]: không đổi. | Abnormal |
| IT_B3_042 | TK_ADM | MarkerColors (1, Đen) nhưng không có Norm vải nào cho màu Đen ở các chi tiết của nhóm 1 | [API]: 400 (backend không suy ra được vải) hoặc 200 không tạo `ProductMarkerFabric` `[?]`; không 500.<br>[DB]: không có dòng `ProductMarkerFabric` mà `MaterialId` NULL hoặc sai. | Abnormal |
| IT_B3_043 | Nhóm 1 có 2 chi tiết cùng màu Đen nhưng định mức vải khác nhau (MA1, MA2) | PUT spec như mô tả | [API]: 400 (một cặp nhóm bìa + màu chỉ có một vải) hoặc chọn theo quy tắc `[?]`; **không 500** do `UQ_PMF`.<br>[DB]: không có hai dòng `ProductMarkerFabric` trùng (ProductId, MarkerNo, ProductColorId). | Abnormal |
| IT_B3_044 | TK_ADM | Body thiếu một trong 5 mảng (ví dụ không có `MarkerColors`); hoặc mảng đó là `null` | [API]: 400 (cả 5 mảng đều non-nullable); không 500 / NullReference.<br>[DB]: không đổi. | Abnormal |
| IT_B3_045 | Fault-injection: trigger test-only `THROW` khi INSERT `ProductMarkerFabric` | PUT spec hợp lệ như IT_B3_026 | [API]: lỗi có kiểm soát (ghi lại mã).<br>[DB]: **Rollback sạch**: không có "Cổ"; định mức cũ còn nguyên (không bị DELETE trước khi INSERT thất bại). | Abnormal |
| IT_B3_046 | PR1 như S0 | Gửi **đồng thời** 2 PUT spec (A thêm "Cổ", B thêm "Vai") | [API]: cả hai 200, hoặc 1 request 409; không 500 (deadlock hay `UQ_PC` phải được xử lý).<br>[DB]: trạng thái cuối khớp đúng một request; không có dòng `ProductComponentMaterial`/`ProductMarkerComponent` mồ côi. | Boundary |
| IT_B3_047 | TK_ADM | `PUT A/products/999999/spec`; `PUT A/products/abc/spec`; PUT spec cho PR3 (đã ngừng) | [API]: 404; 404; PR3 → 400 hoặc 200 `[?]`.<br>[DB]: không đổi. | Abnormal |

## 4. Nhập Excel định mức và trigger `ProductMarker` / `ProductMarkerRatio`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_048 | TK_ADM | `POST A/products/excel/parse` với file `spec_ok.xlsx`; rồi `POST A/products/excel/check` với `ExcelBundleCheckDto{ProductId:<PR1>, Bundle:<kết quả parse>}` | [API]: 200 với cả hai; check trả `Spec` (mỗi dòng có `Action`) và `MissingMaterials` (rỗng).<br>[DB]: **không đổi** (snapshot mọi bảng Product*/Material). | Normal |
| IT_B3_049 | TK_ADM | check với Products có `ProductCode:"SP001"` (đã có) và `"SP400"` (mới) | [API]: 200; SP001 → `Action="Exists"`; SP400 → `Action="New"` hoặc `"Pending"`.<br>[DB]: **không** có Product SP400 (check chỉ đọc). | Normal |
| IT_B3_050 | TK_ADM | check với OrderMaterials có `MaterialCode:"V-9999"`, `RequiredQty:-5`, kèm `MaterialName:"Vải mới"` | [API]: 200; dòng `V-9999` nằm trong `MissingMaterials` (có `SuggestedName`); dòng số lượng âm có `Action="Error"`.<br>[DB]: không có `Material` mới. | Abnormal |
| IT_B3_051 | TK_ADM | Gửi body `ExcelBundleCheckDto` sang **`import-data`**; hoặc gửi `SpecWorkbookDto` sang **`excel/check`** (nhầm kiểu body) | [API]: 400 (bind thất bại hoặc dữ liệu rỗng); không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B3_052 | TK_ADM | `POST A/products/<PR1>/import/preview-data?orderId=<OD1>&parts=markers` với một OrderMarker hợp lệ mới (MarkerNo 2, "S2", LengthM 6.00, WidthM 1.50, Ratios M:1, L:1) | [API]: 200; dòng có `Action="New"`; `CanImport=true`; có `OrderSizes`.<br>[DB]: **không đổi** (preview không chạm `ProductMarker`). | Normal |
| IT_B3_053 | Như IT_B3_052 | `POST A/products/<PR1>/import-data?orderId=<OD1>&parts=markers` (cùng body) | [API]: 200; `OrderMarkers=1`.<br>[DB]: có `ProductMarker(OD1, PR1, 2, 'S2', 6.00, 1.50, 'ACTIVE')`; có 2 dòng `ProductMarkerRatio` (M:1, L:1); trigger chạy không báo lỗi. | Normal |
| IT_B3_054 | TK_ADM | OrderMarker với `WidthM`: `3.50`; `3.51`; `99.99`; `100.00` | [API]: 3.50 → 200; 3.51 → **400** (khổ tối đa 3,5 m: trigger chặn hoặc validator chặn `[?]`); 99.99 → 400; 100.00 → 400 (tràn `decimal(4,2)`); **không ca nào 500**.<br>[DB]: không tồn tại `ProductMarker` nào có `WidthM > 3.50`. | Boundary |
| IT_B3_055 | TK_ADM | OrderMarker với `LengthM`: `99999.99`; `100000.00`; `0`; `-1`; `null` | [API]: 99999.99 → 200 hoặc 400 theo giới hạn nghiệp vụ; 100000 → 400 (tràn `decimal(7,2)`); 0 / −1 / null → 400 (cột NOT NULL); không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B3_056 | TK_ADM | OrderMarker có `MarkerCode` dài 20 / 21 ký tự; trùng (OD1, PR1, 1, "S1") với bìa đã có | [API]: 20 → 200; 21 → 400; trùng → `Action="Update"` hoặc `"Unchanged"` (không INSERT trùng); không 500 do `UQ_PM`.<br>[DB]: `COUNT(UQ_PM key) ≤ 1`. | Boundary |
| IT_B3_057 | TK_ADM | Ratios gồm `SizeCode:"XXL"` (không có trong biến thể của OD1); Ratios có `M` xuất hiện 2 lần; `PiecesPerPly:0`; `PiecesPerPly:-1`; `PiecesPerPly:32768` | [API]: 400 với từng ca (trigger `trg_ProductMarkerRatio_Check` chặn hoặc validator chặn `[?]`); ca trùng `M` không được 500 do PK ghép.<br>[DB]: **Rollback**: không có `ProductMarker` "S2" và không có ratio mồ côi. | Abnormal |
| IT_B3_058 | TK_ADM | Một OrderMarker hợp lệ + một OrderMarker có `WidthM=4.00` (bị trigger chặn) trong cùng file | [API]: 400/409; liệt kê lỗi theo `RowNumber`; không 500.<br>[DB]: **Rollback toàn bộ** (1 giao dịch): bìa hợp lệ cũng **không** được ghi. | Abnormal |
| IT_B3_059 | OD2 thuộc PR2 | `POST A/products/<PR1>/import-data?orderId=<OD2>&parts=markers` | [API]: 400/404 (đơn không thuộc mã hàng này); không lộ dữ liệu của OD2.<br>[DB]: không có `ProductMarker` nào với (OD2, PR1). | Abnormal |
| IT_B3_060 | TK_ADM | import-data có `HasOrderMarkers=true` nhưng **không** truyền `orderId` | [API]: 200; `Notes` ghi rằng bảng bìa của đơn đã bị bỏ qua; `OrderMarkers=0`.<br>[DB]: `ProductMarker` không đổi. | Normal |
| IT_B3_061 | TK_ADM | OrderMarker có `Status:"Active"` (PascalCase); `"PAUSED"`; `""` | [API]: `""` → 200 và lưu `ACTIVE`; `"Active"` / `"PAUSED"` → 400 (Excel chỉ nhận `ACTIVE`/`DISCONTINUED` dạng HOA) `[?]`.<br>[DB]: `ProductMarker.Status` chỉ là `ACTIVE` hoặc `DISCONTINUED`. | Boundary |
| IT_B3_062 | TK_ADM | import-data `?parts=COMPONENTS,norms,,components` | [API]: 200; chỉ xử lý components và norms (controller tự chuyển về chữ thường, bỏ phần tử rỗng, loại trùng).<br>[DB]: chỉ `ProductComponent` và `ProductComponentMaterial` thay đổi; các bảng bìa không đổi. | Boundary |
| IT_B3_063 | C2 chưa dùng, C1 đang dùng; file không chứa C1 và C2 | import-data `?replace=true&parts=components` | [API]: 200; `Removed=2`.<br>[DB]: C2 bị DELETE; C1 chuyển `Status` sang ngừng dùng `[?]`; `OperationComponent` (OP1, C1) không mồ côi. | Normal |
| IT_B3_064 | TK_ADM | `POST A/products/<PR1>/import?replace=false` với file `spec_err.xlsx` | [API]: 400; liệt kê lỗi theo dòng.<br>[DB]: **Rollback**: không dòng hợp lệ nào được ghi. | Abnormal |
| IT_B3_065 | TK_ADM | `import` / `import/preview` không gửi `file` (IFormFile non-nullable); gửi `corrupt.xlsx`; gửi `not_excel.pdf` | [API]: 400 với cả 3; **không 500 / NullReference**.<br>[DB]: không đổi. | Abnormal |
| IT_B3_066 | TK_ADM | Gửi **đồng thời** 2 request import-data cùng OrderMarker mới "S3" vào OD1 | [API]: cả hai 200 (idempotent) hoặc 1 request 409; không 500.<br>[DB]: chỉ có 1 dòng `ProductMarker` "S3"; ratio không bị nhân đôi. | Boundary |

## 5. Operations — CRUD (`AC/operations`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_067 | TK_ACC | `POST AC/operations {ProductId:<PR1>, OperationCode:"CD03", OperationName:"May túi", StationSequence:3, SkillLevel:2, StandardTimeSec:45.50, TeamType:"Sewing", IsFinalStep:false, GroupName:null, ComponentIds:[<C2>]}` | [API]: 201; `OperationDto.TeamType` trả lại dạng Pascal hoặc HOA `[?]`, cần ghi lại.<br>[DB]: `Operation(CD03, TeamType='SEWING', Status='ACTIVE', IsFinalStep=0, OperationGroupId NULL)`; có 1 dòng `OperationComponent(CD03, C2, ProductId=PR1)`. | Normal |
| IT_B3_068 | PR1 đã có OP1–OP5 | Kiểm tra sau IT_B3_067: `GET AC/operations?productId=<PR1>&includeDiscontinued=false` | [API]: 200; có ≥ 3 công đoạn ACTIVE (OP1, OP2, OP3, CD03); không có OP4, OP5.<br>[DB]: chứng minh **nhiều** Operation trên một Product vẫn chạy được, dù EF cấu hình 1-1 (`HasOne…WithOne`). | Normal |
| IT_B3_069 | TK_ACC | `includeDiscontinued=true` | [API]: 200; có thêm OP4, OP5; `Status` của chúng phân biệt được với ACTIVE.<br>[DB]: không đổi. | Normal |
| IT_B3_070 | TK_ACC | `POST AC/operations` với `TeamType:"Cutting"`; không gửi `TeamType` | [API]: 201 với cả hai.<br>[DB]: lần lượt là `TeamType='CUTTING'` `[?]` và `'SEWING'` (giá trị mặc định); không lưu dạng PascalCase. | Normal |
| IT_B3_071 | TK_ACC | `TeamType:"Office"`; `"SEWINGXXXXX"` (11 ký tự) | [API]: 400; không 500 (cột `varchar(10)`).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B3_072 | OP1 `CD01` thuộc PR1 | POST với `{ProductId:<PR1>, OperationCode:"CD01"}`; với `"cd01"` | [API]: 409 (`UQ_Operation`; ca chữ thường ghi lại theo collation `[?]`); không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B3_073 | OP9 `CD01` thuộc PR2 | Kiểm tra `CD01` đã tồn tại đồng thời ở PR1 và PR2 | [API]: GET mỗi mã hàng chỉ trả công đoạn của chính nó.<br>[DB]: (PR1, CD01) và (PR2, CD01) cùng tồn tại hợp lệ (UNIQUE tính theo mã hàng). | Normal |
| IT_B3_074 | PR1 đã có OP3 là công đoạn cuối ACTIVE | POST `{…, OperationCode:"CD100", IsFinalStep:true}` | [API]: **409/400**; **không 500** do `UX_Operation_FinalStep`.<br>[DB]: `COUNT(Operation WHERE ProductId=PR1 AND IsFinalStep=1 AND Status='ACTIVE') = 1`. | Abnormal |
| IT_B3_075 | OP3 là công đoạn cuối ACTIVE | `PUT AC/operations/<OP2>` với `IsFinalStep:true` | [API]: 409/400; không 500.<br>[DB]: OP2 vẫn `IsFinalStep=0`. | Abnormal |
| IT_B3_076 | OP3 ACTIVE là công đoạn cuối; OP4 là công đoạn cuối đã ngừng | `POST AC/operations/<OP4>/reactivate` | [API]: **409/400** (đã có công đoạn cuối ACTIVE khác); không 500.<br>[DB]: OP4 vẫn ở trạng thái ngừng. | Abnormal |
| IT_B3_077 | Như IT_B3_076 | Gọi tuần tự: discontinue OP3 rồi reactivate OP4 | [API]: cả hai 200.<br>[DB]: OP3 ngừng, OP4 `ACTIVE`; vẫn đúng 1 công đoạn cuối ACTIVE (index lọc cho phép nhiều công đoạn cuối đã ngừng). | Normal |
| IT_B3_078 | PR2 chưa có công đoạn cuối ACTIVE (giả định) | Gửi **đồng thời** 2 POST tạo công đoạn cuối (CD-F1, CD-F2) cho PR2 | [API]: 1 request 201, 1 request 409; **không 500** (race condition phải được bắt từ unique index).<br>[DB]: đúng 1 công đoạn cuối ACTIVE ở PR2. | Boundary |
| IT_B3_079 | C9 thuộc PR2 | POST công đoạn cho PR1 với `ComponentIds:[<C1>, <C9>]` | [API]: 400/404 (chi tiết thuộc mã hàng khác); không 500 do `FK_OC_Component` (FK ghép).<br>[DB]: **Rollback**: không có Operation mới, không có `OperationComponent` nào. | Abnormal |
| IT_B3_080 | TK_ACC | `ComponentIds:[999999]`; `ComponentIds:[<C1>, <C1>]`; `ComponentIds:[]` | [API]: 999999 → 400/404; trùng → 400 hoặc chỉ lưu 1 `[?]` (không 500 do PK ghép); `[]` → 201 không gắn chi tiết.<br>[DB]: không có dòng `OperationComponent` trùng. | Boundary |
| IT_B3_081 | TK_ACC | POST với `GroupName:"Nhóm Hoàn Thiện"` (nhóm chưa có) | [API]: 201; `GroupName` trả về đúng.<br>[DB]: có `OperationGroup(PR1, N'Nhóm Hoàn Thiện')` mới; `Operation.OperationGroupId` trỏ đúng nhóm đó; cả hai cùng `ProductId`. | Normal |
| IT_B3_082 | OG1 "Nhóm May" đã có ở PR1 | POST với `GroupName:"Nhóm May"`; với `"nhóm may"` | [API]: 201 với cả hai.<br>[DB]: **dùng lại** OG1, không tạo nhóm trùng (`UQ_OG_Name`); ca chữ thường ghi lại theo collation `[?]`; không 500. | Boundary |
| IT_B3_083 | OG9 "Nhóm B" thuộc PR2 | POST công đoạn cho **PR1** với `GroupName:"Nhóm B"` | [API]: 201.<br>[DB]: tạo nhóm "Nhóm B" **mới** thuộc PR1 (không trỏ sang OG9 của PR2); FK ghép `FK_Operation_Group` được thỏa. | Normal |
| IT_B3_084 | TK_ACC | OperationCode dài 30 / 31 ký tự; OperationName dài 200 / 201 ký tự có dấu; GroupName dài 200 / 201 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B3_085 | TK_ACC | `StationSequence`: `0`; `-1`; `32767`; `32768`. `SkillLevel`: `0`; `255`; `256`; `-1` | [API]: StationSequence 32768 và SkillLevel 256 / −1 → 400 (tràn `smallint`/`tinyint` khi bind), không 500; các giá trị nằm trong miền kiểu thì 201 hoặc 400 theo nghiệp vụ `[?]`.<br>[DB]: không có giá trị ngoài miền kiểu. | Boundary |
| IT_B3_086 | TK_ACC | `StandardTimeSec`: `9999999.99`; `10000000.00`; `45.555`; `0`; `-1`; `null` | [API]: 9999999.99 → 201; 10000000 → 400 (tràn `decimal(9,2)`); 45.555 → 400 hoặc làm tròn `[?]`; −1 → 400; null → 201 (cột cho phép NULL).<br>[DB]: giá trị lưu đúng; không 500. | Boundary |
| IT_B3_087 | TK_ACC | Thiếu `OperationCode`; thiếu `OperationName`; `ProductId:999999`; `ProductId:<PR3>` (đã ngừng) | [API]: 400; 400; 404/400 (không 500 do FK); PR3 → 400 `[?]`.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B3_088 | S0 | Gửi **đồng thời** 2 POST cùng (PR1, `"CD200"`) | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`; không có `OperationComponent`/`OperationGroup` thừa do request thua tạo ra. | Boundary |
| IT_B3_089 | OP2 thuộc PR1 | `PUT AC/operations/<OP2> {ProductId:<PR1>, OperationCode:"CD02", OperationName:"May cổ", StationSequence:2, …, ComponentIds:[<C1>, <C3>]}` | [API]: 200.<br>[DB]: OP2 được cập nhật; `OperationComponent` của OP2 được thay bằng đúng {C1, C3}; AuditLogs +1 `[?]`. | Normal |
| IT_B3_090 | OP2 thuộc PR1 | `PUT AC/operations/<OP2>` với `ProductId:<PR2>` (thử chuyển công đoạn sang mã hàng khác) | [API]: 400/409; không 500 (OP2 đang có `OperationComponent`/`PieceRate`/nhóm theo FK ghép của PR1).<br>[DB]: OP2 vẫn thuộc PR1. | Abnormal |
| IT_B3_091 | OP1, OP2 thuộc PR1 | PUT OP2 với `OperationCode:"CD01"` | [API]: 409.<br>[DB]: OP2 không đổi. | Abnormal |
| IT_B3_092 | TK_ACC | `PUT AC/operations/999999 {…}`; `PUT AC/operations/abc {…}`; PUT OP9 (của PR2) với body ghi `ProductId:<PR1>` | [API]: 404; 404; OP9 → 400/404 (ProductId trong body không khớp mã hàng thực của công đoạn).<br>[DB]: OP9 không đổi. | Abnormal |
| IT_B3_093 | OP2 ACTIVE, có đơn giá | `POST AC/operations/<OP2>/discontinue` | [API]: 200.<br>[DB]: `Status` chuyển sang ngừng dùng (HOA) `[?]`; `PieceRate` của OP2 không bị xóa; OG chứa OP2 vẫn tính `OperationCount` gồm cả OP2. | Normal |
| IT_B3_094 | OP5 đã ngừng; OP1 đang ACTIVE | discontinue OP5; reactivate OP1; reactivate 999999 | [API]: 400/409; 400/409; 404.<br>[DB]: không đổi. | Abnormal |

## 6. Operations — nhập Excel (D2–D5): toàn vẹn transaction

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_095 | TK_ACC | `POST AC/operations/import/preview?productId=<PR1>&sheetName=May` với file `ops_ok.xlsx` | [API]: 200; `SheetNames=["May","Cắt"]`; `NewCount` / `UpdateCount` / `UnchangedCount` khớp file; `ProductHasFinalStep=true`; mỗi dòng có `Action` ∈ {New, Update, Unchanged}, `RateAction` mặc định `"None"`.<br>[DB]: **không đổi** (`Operation`, `OperationGroup`, `OperationComponent`, `PieceRate`). | Normal |
| IT_B3_096 | TK_ACC | preview với `sheetName="KhongCo"`; không truyền `sheetName` | [API]: sheet không có → 400/404; không truyền → lấy sheet đầu tiên `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B3_097 | TK_ACC | preview không gửi `file`; gửi `corrupt.xlsx`; gửi `not_excel.pdf`; gửi `big_6mb.xlsx` | [API]: 400 / 400 / 400 / 400 hoặc 413 (giới hạn 5 MB theo comment, hoặc giới hạn mặc định của Kestrel); **không 500 / NullReference**.<br>[DB]: không đổi. | Abnormal |
| IT_B3_098 | TK_ACC | preview không truyền `productId`; `productId=0`; `productId=999999` | [API]: 400 / 400 / 404; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B3_099 | TK_ACC | `POST AC/operations/import-data?productId=<PR1>&effectiveDate=2027-01-01&submitRates=false` với `OperationExcelSheetDto{HasRateColumn:true, HasGroupColumn:true, HasComponentColumn:true, Rows:[3 dòng mới STT 10, 11, 12 có Rate, GroupName "Nhóm May", ComponentNames ["Túi"]]}` | [API]: 200; `Created=3`, `RateCreated=3`.<br>[DB]:<br>- 3 dòng `Operation` (`ACTIVE`, `SEWING`), gắn với OG1 (dùng lại nhóm có sẵn).<br>- 3 dòng `OperationComponent` → C2.<br>- 3 dòng `PieceRate(EffectiveDate=2027-01-01, Status='DRAFT')`. | Normal |
| IT_B3_100 | Như IT_B3_099 | import-data cùng body nhưng `submitRates=true`, `effectiveDate=2027-02-01` | [API]: 200.<br>[DB]: `PieceRate.Status` = chờ duyệt (HOA) `[?]`; `SubmittedAt` khác NULL và ≈ hiện tại. | Normal |
| IT_B3_101 | TK_ACC | import-data `HasRateColumn=true` nhưng **không** truyền `effectiveDate` | [API]: 200; `RateSkipped` = số dòng có đơn giá; hoặc 400 `[?]`.<br>[DB]: không có `PieceRate` mới; Operation vẫn được ghi (nếu 200). | Boundary |
| IT_B3_102 | TK_ACC | `effectiveDate=0001-01-01`; `effectiveDate=9999-12-31`; `effectiveDate=2027-02-30` | [API]: 0001 → **400** (không được lưu ngày mặc định); 9999 → 400 `[?]`; ngày không tồn tại → 400 (bind lỗi); không 500.<br>[DB]: không có `PieceRate` nào với `EffectiveDate='0001-01-01'`. | Boundary |
| IT_B3_103 | TK_ACC | import-data với file `ops_err.xlsx` (dòng 1–2 hợp lệ, dòng 3 có `ParseError`) | [API]: 400; báo lỗi theo `RowNumber`.<br>[DB]: **Rollback toàn bộ**: dòng 1–2 **không** được ghi vào `Operation`/`PieceRate`/`OperationGroup`/`OperationComponent`. | Abnormal |
| IT_B3_104 | PR1 đã có OP3 là công đoạn cuối | import-data có một dòng `IsFinal=true` (công đoạn cuối mới) và không truyền `finalStt` | [API]: 400/409 (xung đột công đoạn cuối, có `Blocked` hoặc `Warning`), hoặc 200 với `finalStt` thay thế theo quy tắc `[?]`; **không 500** do `UX_Operation_FinalStep`.<br>[DB]: vẫn đúng 1 công đoạn cuối ACTIVE ở PR1. | Abnormal |
| IT_B3_105 | TK_ACC | import với file `ops_2final.xlsx` (2 dòng `IsFinal`); và `finalStt=999` (STT không có trong file) | [API]: 400 với cả hai; `FinalSttInFile` báo nhiều giá trị.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B3_106 | TK_ACC | import-data có `finalStt=12` hợp lệ (STT 12 có trong file) và PR1 đã có OP3 là công đoạn cuối | [API]: 200 `[?]` (STT 12 trở thành công đoạn cuối; OP3 bị bỏ cờ cuối hoặc bị ngừng `[?]`).<br>[DB]: đúng 1 công đoạn cuối ACTIVE; việc đổi cờ và INSERT diễn ra trong cùng giao dịch. | Normal |
| IT_B3_107 | File thiếu OP2 (OP2 có đơn giá) và thiếu công đoạn CD03 (chưa dùng) | import-data `replace=true` | [API]: 200; `Removed ≥ 1`; preview trước đó có `RemoveCount` và `Removed[]` khớp.<br>[DB]: công đoạn chưa dùng bị DELETE; OP2 (đã có đơn giá) chỉ chuyển `Status` sang ngừng `[?]`; `PieceRate` của OP2 còn nguyên. | Normal |
| IT_B3_108 | Như IT_B3_107 | Cùng body nhưng `replace=false` | [API]: 200; `Removed=0`.<br>[DB]: OP2 và CD03 giữ nguyên `ACTIVE`. | Normal |
| IT_B3_109 | Đã có PR_D1 (OP1, 2026-12-01, DRAFT) | import-data với một dòng `CD01` có Rate 1800 và `effectiveDate=2026-12-01` | [API]: 200; `RateUpdated=1` (cập nhật bản nháp); không 500 do `UQ_PieceRate`.<br>[DB]: vẫn chỉ 1 dòng (OP1, 2026-12-01) với `Rate=1800.00`. | Normal |
| IT_B3_110 | PR_A1 (OP1, 2026-01-01) đã duyệt | import-data với dòng `CD01` Rate 9999 và `effectiveDate=2026-01-01` | [API]: 200 nhưng `RateSkipped ≥ 1` (không sửa đơn giá đã duyệt), hoặc 400/409; **không 500** (trigger `trg_PieceRate_Approved` có thể chặn).<br>[DB]: PR_A1 vẫn `Rate=1200.00`, giữ nguyên trạng thái đã duyệt. | Abnormal |
| IT_B3_111 | TK_ACC | Rows có `ComponentNames:["Thân sau"]` (chi tiết của PR2) | [API]: 400 (dòng có `Action="Error"`).<br>[DB]: Rollback; không có `OperationComponent` trỏ chéo mã hàng. | Abnormal |
| IT_B3_112 | TK_ACC | Rows có `Rate`: `-1`; `0`; `9999999999999999.99`; `10000000000000000`; `1500.555` | [API]: −1 → lỗi; 0 → lỗi `[?]`; giá trị tối đa → hợp lệ; vượt tối đa → lỗi (tràn `decimal(18,2)`); 3 chữ số thập phân → lỗi hoặc làm tròn `[?]`; không ca nào 500.<br>[DB]: chỉ giá trị hợp lệ được lưu. | Boundary |
| IT_B3_113 | TK_ACC | Rows có `Stt` trùng nhau (2 dòng cùng STT 10); `Name` rỗng; `SkillLevel:300` | [API]: 400 (dòng `Error`); không 500.<br>[DB]: Rollback. | Abnormal |
| IT_B3_114 | TK_ACC | Gửi **đồng thời** 2 request import-data cùng body vào PR1 | [API]: cả hai 200 (request sau thành `Unchanged`) hoặc 1 request 409; không 500 (`UQ_Operation`, `UQ_OG_Name`, `UQ_PieceRate`).<br>[DB]: không có Operation, OperationGroup hay PieceRate trùng. | Boundary |
| IT_B3_115 | Fault-injection: INSERT `PieceRate` lỗi khi import | import-data hợp lệ như IT_B3_099 | [API]: lỗi có kiểm soát.<br>[DB]: **Rollback**: không có `Operation`, `OperationGroup` hay `OperationComponent` mới (không lưu nửa vời). | Abnormal |

## 7. OperationGroups (`AC/operationgroups`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_116 | TK_ACC / TK_ADM | `GET AC/operationgroups?productId=<PR1>`; `GET A/operationgroups?productId=<PR1>` | [API]: 200; OG1 có `OperationCount=2` (**tính cả OP5 đã ngừng**); OG2 có `OperationCount=0`; không có OG9 của PR2.<br>[DB]: `OperationCount` khớp `COUNT(Operation WHERE OperationGroupId=…)` (không lọc theo Status). | Normal |
| IT_B3_117 | TK_ACC | `GET AC/operationgroups` không truyền `productId`; `?productId=999999` | [API]: 400 hoặc danh sách rỗng `[?]` / 200 rỗng hoặc 404; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B3_118 | TK_ACC | `POST AC/operationgroups {ProductId:<PR1>, GroupName:"Nhóm Đóng Gói"}` | [API]: 201.<br>[DB]: có `OperationGroup(PR1, N'Nhóm Đóng Gói')`. | Normal |
| IT_B3_119 | OG1 có ở PR1 | POST `{PR1, "Nhóm May"}`; POST `{PR2, "Nhóm May"}` | [API]: PR1 → 409; PR2 → 201 (UNIQUE tính theo mã hàng).<br>[DB]: PR1 vẫn chỉ có 1 nhóm "Nhóm May". | Abnormal |
| IT_B3_120 | TK_ACC | GroupName dài 200 / 201 ký tự; `""`; chỉ khoảng trắng; `ProductId:999999` | [API]: 200 ký tự → 201; còn lại → 400 / 400 / 400 / 404; không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B3_121 | S0 | Gửi **đồng thời** 2 POST `{PR1, "Nhóm X"}` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B3_122 | OG2 thuộc PR1 | `PUT AC/operationgroups/<OG2> {GroupName:"Nhóm Phụ"}`; PUT OG2 với `"Nhóm May"` | [API]: lần đầu 200; lần sau 409.<br>[DB]: OG2 mang tên "Nhóm Phụ". | Normal |
| IT_B3_123 | OG9 thuộc PR2 | PUT OG9 với `{GroupName:"Nhóm May"}` (trùng tên với nhóm của PR1) | [API]: 200 (UNIQUE tính theo mã hàng).<br>[DB]: OG9 đổi tên; OG1 không đổi. | Boundary |
| IT_B3_124 | OG2 không có công đoạn | `DELETE AC/operationgroups/<OG2>` | [API]: 200/204.<br>[DB]: OG2 bị xóa cứng (Entity không có cột Status). | Normal |
| IT_B3_125 | OG1 chứa OP1 (ACTIVE) và OP5 (đã ngừng) | DELETE OG1 | [API]: 409 (bị chặn vì còn công đoạn), hoặc 200 kèm set `Operation.OperationGroupId=NULL` `[?]`; **không 500** do `FK_Operation_Group` (FK không khai báo OnDelete).<br>[DB]: nếu 409 thì OG1 còn nguyên; nếu 200 thì OP1 và OP5 có `OperationGroupId IS NULL` (không còn trỏ vào id đã xóa). | Abnormal |
| IT_B3_126 | TK_ACC | DELETE 999999; DELETE `abc` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B3_127 | OP2 chưa có nhóm | `PUT AC/operationgroups/operations {ProductId:<PR1>, OperationIds:[<OP2>, <OP3>], OperationGroupId:<OG2>}` | [API]: 200.<br>[DB]: OP2 và OP3 có `OperationGroupId=OG2`. | Normal |
| IT_B3_128 | OP1 thuộc OG1 | `PUT …/operations {ProductId:<PR1>, OperationIds:[<OP1>], OperationGroupId:null}` | [API]: 200.<br>[DB]: OP1 có `OperationGroupId IS NULL`; số `OperationCount` của OG1 giảm đi 1. | Normal |
| IT_B3_129 | OG9 thuộc PR2 | `PUT …/operations {ProductId:<PR1>, OperationIds:[<OP1>], OperationGroupId:<OG9>}` | [API]: 400/404 (nhóm thuộc mã hàng khác); **không 500** do FK ghép `FK_Operation_Group`.<br>[DB]: OP1 vẫn thuộc OG1. | Abnormal |
| IT_B3_130 | OP9 thuộc PR2 | `PUT …/operations {ProductId:<PR1>, OperationIds:[<OP1>, <OP9>], OperationGroupId:<OG2>}` | [API]: 400/404.<br>[DB]: **Rollback**: cả OP1 lẫn OP9 không đổi nhóm. | Abnormal |
| IT_B3_131 | TK_ACC | `OperationIds:[]`; `OperationIds:[999999]`; `OperationIds:[<OP1>, <OP1>]` | [API]: `[]` → 400 hoặc 200 không đổi gì `[?]`; 999999 → 404/400; trùng → 200 (gán 1 lần).<br>[DB]: không có trạng thái trung gian. | Boundary |

## 8. PieceRates — vòng đời đơn giá, `UQ_PieceRate`, trigger `trg_PieceRate_Approved`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_132 | TK_ACC | `POST AC/piecerates {OperationId:<OP2>, Rate:2200.50, EffectiveDate:"2027-03-01"}` | [API]: 201.<br>[DB]: `PieceRate(OP2, 2200.50, 2027-03-01, Status='DRAFT', SubmittedAt NULL, ApprovedAt NULL, DecisionNote NULL)`. | Normal |
| IT_B3_133 | PR_D1 = (OP1, 2026-12-01) | POST `{OperationId:<OP1>, Rate:1600, EffectiveDate:"2026-12-01"}` | [API]: 409; không 500.<br>[DB]: vẫn 1 dòng (OP1, 2026-12-01). | Abnormal |
| IT_B3_134 | PR_R1 = (OP2, 2026-11-01) đã bị từ chối | POST `{OperationId:<OP2>, Rate:1900, EffectiveDate:"2026-11-01"}` (lập lại đơn giá sau khi bị từ chối, cùng ngày) | [API]: 409/400 với message rõ ràng, hoặc 200 nếu Service tái sử dụng dòng bị từ chối `[?]`; **không 500** (`UQ_PieceRate` không lọc theo Status).<br>[DB]: `COUNT(OP2, 2026-11-01) = 1`; ghi lại dòng đó được tái dùng (Status quay về `DRAFT`) hay giữ nguyên. | Abnormal |
| IT_B3_135 | TK_ACC | `EffectiveDate:"0001-01-01"`; thiếu `EffectiveDate` (bind ra `default(DateOnly)`); `"2027-13-01"` | [API]: **400** với cả 3 (không được lưu ngày mặc định); không 500.<br>[DB]: không có dòng `EffectiveDate='0001-01-01'`. | Boundary |
| IT_B3_136 | TK_ACC | `EffectiveDate` = hôm qua; = hôm nay; = ngày mai | [API]: quá khứ → 400 `[?]` (không lập giá hồi tố); hôm nay / ngày mai → 201 `[?]`; ghi lại điểm biên.<br>[DB]: nhất quán với API. | Boundary |
| IT_B3_137 | TK_ACC | `Rate`: `0`; `-0.01`; `0.01`; `9999999999999999.99`; `10000000000000000`; `100.555`; thiếu Rate (bind ra 0) | [API]: 0 / âm / thiếu → 400; 0.01 và giá trị tối đa → 201; vượt tối đa → 400 (tràn `decimal(18,2)`); 3 chữ số thập phân → 400 hoặc làm tròn `[?]`; không 500.<br>[DB]: chỉ ca hợp lệ được lưu, đúng giá trị. | Boundary |
| IT_B3_138 | TK_ACC | `OperationId:999999`; `OperationId:<OP5>` (đã ngừng) | [API]: 404/400 (không 500 do FK); OP5 → 400 `[?]`.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B3_139 | S0 | Gửi **đồng thời** 2 POST `{OP2, 2027-04-01}` với Rate khác nhau | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B3_140 | PR_D1 là `DRAFT` | `PUT AC/piecerates/<PR_D1> {OperationId:<OP1>, Rate:1550, EffectiveDate:"2026-12-15"}` | [API]: 200.<br>[DB]: PR_D1 có `Rate=1550.00`, `EffectiveDate=2026-12-15`; Status vẫn `DRAFT`. | Normal |
| IT_B3_141 | PR_A1 đã duyệt | PUT PR_A1 `{OperationId:<OP1>, Rate:9999, EffectiveDate:"2026-01-01"}` | [API]: **400/409** ("chỉ sửa khi chưa duyệt"); **không 500** khi trigger `trg_PieceRate_Approved` `THROW`.<br>[DB]: PR_A1 vẫn `Rate=1200.00`, giữ trạng thái đã duyệt và `ApprovedAt` cũ (assert trực tiếp DB). | Abnormal |
| IT_B3_142 | PR_P1 đang chờ duyệt | PUT PR_P1 với Rate mới | [API]: 400/409, hoặc 200 kèm đưa về `DRAFT` `[?]`; ghi lại.<br>[DB]: nhất quán; không có trạng thái "chờ duyệt" mà Rate đã bị đổi. | Abnormal |
| IT_B3_143 | PR_D1 (OP1) | PUT PR_D1 đổi `OperationId:<OP9>` (công đoạn của PR2); PUT PR_D1 đổi `EffectiveDate` trùng PR_A1 (2026-01-01) | [API]: OP9 → 400/404; trùng ngày → 409; không 500.<br>[DB]: PR_D1 không đổi. | Abnormal |
| IT_B3_144 | TK_ACC | `PUT AC/piecerates/999999 {…}`; `PUT AC/piecerates/abc {…}` | [API]: 404.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B3_145 | PR_D1 là `DRAFT` | `POST AC/piecerates/<PR_D1>/submit` | [API]: 200.<br>[DB]: `Status` = chờ duyệt (HOA) `[?]`; `SubmittedAt` ≈ hiện tại (giờ VN). | Normal |
| IT_B3_146 | PR_A1 đã duyệt; PR_P1 đang chờ duyệt | submit PR_A1; submit PR_P1; submit 999999 | [API]: 400/409; 400/409; 404.<br>[DB]: `SubmittedAt` của PR_A1 và PR_P1 không đổi. | Abnormal |
| IT_B3_147 | Có 2 bản DRAFT là PR_D1 và PR_D2 | `POST AC/piecerates/submit-many {Ids:[<PR_D1>, <PR_D2>]}` | [API]: 200.<br>[DB]: cả hai chuyển sang chờ duyệt; có `SubmittedAt`. | Normal |
| IT_B3_148 | PR_D1 là DRAFT; PR_A1 đã duyệt | submit-many `{Ids:[<PR_D1>, <PR_A1>, 999999]}` | [API]: 400/409 (cả lô bị từ chối), hoặc 200 kèm danh sách bỏ qua `[?]`; không 500.<br>[DB]: nếu làm theo kiểu tất cả hoặc không gì cả thì PR_D1 vẫn `DRAFT` (**rollback**); nếu xử lý từng phần thì ghi lại chính xác dòng nào đã đổi. | Abnormal |
| IT_B3_149 | TK_ACC | submit-many `{Ids:[]}`; `{Ids:null}`; `{Ids:[<PR_D1>, <PR_D1>]}` | [API]: `[]` / null → 400; trùng → 200 (chỉ submit 1 lần).<br>[DB]: không có `SubmittedAt` bị ghi 2 lần bất thường. | Boundary |
| IT_B3_150 | PR_P1 đang chờ duyệt | `POST A/piecerates/<PR_P1>/approve {Note:"Đồng ý"}` | [API]: 200; `PieceRateDto.ApprovalNote="Đồng ý"`, `RejectReason=null`.<br>[DB]: `Status` = đã duyệt (HOA) `[?]`; `ApprovedAt` ≈ hiện tại; `DecisionNote=N'Đồng ý'`; trigger `trg_PieceRate_Approved` chạy (assert các tác động phụ nếu có `[?]`). | Normal |
| IT_B3_151 | PR_P1 đang chờ duyệt | approve với `{Note:null}`; với body `{}` | [API]: 200 (Note không bắt buộc).<br>[DB]: `DecisionNote IS NULL`; `ApprovedAt` khác NULL. | Normal |
| IT_B3_152 | PR_D1 là `DRAFT`; PR_A1 đã duyệt; PR_R1 bị từ chối | approve PR_D1; approve PR_A1; approve PR_R1; approve 999999 | [API]: 400/409 với 3 ca đầu (chỉ duyệt được đơn giá đang chờ duyệt); 404 với 999999.<br>[DB]: trạng thái và `ApprovedAt` của cả 3 không đổi. | Abnormal |
| IT_B3_153 | PR_P1 đang chờ duyệt | approve với Note dài 500 / 501 ký tự | [API]: 500 → 200; 501 → 400 (không 500 do truncation của `DecisionNote nvarchar(500)`).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B3_154 | PR_P1 đang chờ duyệt | `POST A/piecerates/<PR_P1>/reject {Reason:"Đơn giá cao hơn thị trường"}` | [API]: 200; `RejectReason` = chuỗi trên; `ApprovalNote=null`.<br>[DB]: `Status` = bị từ chối (HOA) `[?]`; `DecisionNote` = Reason; `ApprovedAt IS NULL`. | Normal |
| IT_B3_155 | PR_P1 đang chờ duyệt | reject với `{Reason:""}`; `{Reason:null}`; `{}`; Reason chỉ khoảng trắng | [API]: 400 (lý do là bắt buộc).<br>[DB]: PR_P1 vẫn chờ duyệt. | Abnormal |
| IT_B3_156 | PR_P1 đang chờ duyệt | reject với Reason dài 500 / 501 ký tự có dấu | [API]: 500 → 200; 501 → 400 (`TextLimits.Reason`).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B3_157 | PR_A1 đã duyệt | reject PR_A1 | [API]: 400/409; **không 500** nếu trigger chặn việc sửa đơn giá đã duyệt.<br>[DB]: PR_A1 không đổi. | Abnormal |
| IT_B3_158 | PR_P1 (PR1) và PR_P9 (PR2) đều chờ duyệt | `POST A/piecerates/approve-many {Ids:[<PR_P1>, <PR_P9>], Note:"Duyệt lô"}` | [API]: 200 (Admin duyệt được mọi mã hàng).<br>[DB]: cả hai chuyển sang đã duyệt, có `ApprovedAt` và `DecisionNote=N'Duyệt lô'`. | Normal |
| IT_B3_159 | PR_P1 chờ duyệt; PR_D1 là DRAFT | approve-many `{Ids:[<PR_P1>, <PR_D1>]}` | [API]: 400/409, hoặc 200 kèm danh sách bỏ qua `[?]`; không 500.<br>[DB]: nếu làm theo kiểu tất cả hoặc không gì cả thì PR_P1 vẫn chờ duyệt (**rollback**); PR_D1 không bao giờ được chuyển sang đã duyệt. | Abnormal |
| IT_B3_160 | TK_ADM | approve-many `{Ids:[]}`; `{Ids:[999999]}`; Note dài 501 ký tự | [API]: 400 / 404 hoặc 400 / 400.<br>[DB]: không đổi. | Boundary |
| IT_B3_161 | PR_P1 đang chờ duyệt | Gửi **đồng thời** `approve` (Admin) và `reject` (Admin) cho PR_P1 | [API]: đúng 1 request 200, request còn lại 409/400; không 500.<br>[DB]: PR_P1 ở **một** trạng thái cuối duy nhất; `DecisionNote` và `ApprovedAt` khớp với request thắng. | Boundary |
| IT_B3_162 | PR_P1 đang chờ duyệt | Gửi **đồng thời** 2 request approve PR_P1 | [API]: 1 request 200; request còn lại 200 (idempotent) hoặc 409; không 500.<br>[DB]: `ApprovedAt` chỉ được ghi một lần; trigger không tạo tác động phụ trùng lặp `[?]`. | Boundary |
| IT_B3_163 | PR_D1 là DRAFT | Gửi **đồng thời** `PUT AC/piecerates/<PR_D1>` (đổi Rate) và `POST AC/piecerates/<PR_D1>/submit` | [API]: cả hai 200 theo thứ tự tuần tự hóa, hoặc 1 request 409; không 500.<br>[DB]: không tồn tại trạng thái "chờ duyệt" với Rate bị đổi **sau** khi submit mà không ghi nhận (không có RowVer, nên ghi finding nếu ghi đè lẫn nhau). | Boundary |
| IT_B3_164 | S0 có đủ các trạng thái | `GET A/piecerates`; `GET A/piecerates?Status=Draft` | [API]: 200; **không** có PR_D1 hay bản DRAFT nào (`includeDraft:false` cố định ở phía Admin), kể cả khi lọc `Status=Draft`.<br>[DB]: không đổi. | Abnormal |
| IT_B3_165 | S0 | `GET AC/piecerates`; `?Status=PendingApproval`; `?Status=PENDING_APPROVAL`; `?Status=Xyz` | [API]: Accountant thấy cả bản DRAFT `[?]`; `PendingApproval` → PR_P1, PR_P9; dạng HOA → 400 hoặc map được `[?]`; `Xyz` → 400 hoặc rỗng; không 500.<br>[DB]: không đổi. | Normal |
| IT_B3_166 | TK_ACC | `GET AC/piecerates?ProductId=<PR1>&OperationId=<OP9>` (OP9 không thuộc PR1) | [API]: 200 với danh sách rỗng; không lộ đơn giá của PR2.<br>[DB]: không đổi. | Abnormal |
| IT_B3_167 | PR_P1 đã duyệt (sau IT_B3_150); PR_R1 bị từ chối | `GET AC/operations?productId=<PR1>` | [API]: OP2 có `CurrentRate` / `NextRate` / `UpcomingRate` lấy từ đơn giá đã duyệt; `OperationRateDto.RejectReason` của PR_R1 = "Sai giá" (lấy từ `DecisionNote`).<br>[DB]: khớp `PieceRate.DecisionNote` theo Status. | Normal |

## 9. Kiểm tra chéo: trạng thái và nhật ký

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B3_168 | Đã chạy các ca ở mục 3–8 | `SELECT DISTINCT` các cột `Status` (Product, ProductComponent, ProductComponentMaterial, ProductMarker, Operation, PieceRate), `TeamType`, `ComponentType` | [API]: —<br>[DB]: mọi giá trị đều VIẾT HOA (`ACTIVE`, `DISCONTINUED` `[?]`, `DRAFT`, `PENDING_APPROVAL` `[?]`, `APPROVED` `[?]`, `REJECTED` `[?]`, `SEWING`, `CUTTING`, `PANEL`, `TRIM`); không có PascalCase. | Normal |
| IT_B3_169 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chạy các ca thất bại IT_B3_029, IT_B3_058, IT_B3_074, IT_B3_103, IT_B3_141 | [API]: 4xx đúng như từng ca.<br>[DB]: `MAX(AuditLogs.Id)` không đổi; không có dữ liệu nửa vời ở bất kỳ bảng nào. | Abnormal |
| IT_B3_170 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chuỗi: tạo PieceRate → submit → approve; rồi `GET A/products/<PR1>` | [API]: các bước đều 200/201; `History[]` của sản phẩm phản ánh các thay đổi liên quan `[?]`.<br>[DB]: AuditLogs tăng đúng số thao tác `[?]`; `DecisionNote`, `SubmittedAt`, `ApprovedAt` nhất quán theo thứ tự thời gian. | Normal |
| IT_B3_171 | Trigger `trg_ProductMarker_Check`, `trg_ProductMarkerRatio_Check`, `trg_PieceRate_Approved` đều đang bật | Chạy lại IT_B3_054, IT_B3_057, IT_B3_141, IT_B3_161 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; body không chứa tên trigger hay `SqlException`.<br>[DB]: không có `WidthM > 3.50`; không có ratio mồ côi; không có đơn giá đã duyệt nào bị sửa. | Boundary |
