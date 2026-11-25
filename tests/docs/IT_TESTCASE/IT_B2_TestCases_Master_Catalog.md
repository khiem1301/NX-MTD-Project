# Integration Test Cases — Batch 2: Master Catalog (`IT_B2_xxx`)

Phạm vi: Customer · Supplier · Material · Product (gồm ảnh, định mức, nhập Excel) · Vehicle. Nguồn: `IT_CONTRACT — Batch 2: Master Catalog`.
Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**; không kiểm lại logic tính toán đơn lẻ vốn thuộc Unit Test.

## 0. Quy ước chung

**HTTP kỳ vọng**
- `401` thiếu/hết hạn/sai chữ ký token. `403` sai role ở prefix. `404` không có tài nguyên, không có route, hoặc route constraint `{id:int}` không khớp. `405` có route nhưng sai method. `400` dữ liệu sai. `409` trùng UNIQUE hoặc xung đột trạng thái. `413` vượt `RequestSizeLimit`.
- **Bất kỳ `500` nào cũng là FAIL**, kể cả khi DB vẫn đúng. Body lỗi không được lộ chi tiết SQL: tên constraint, `SqlException`, stack trace.
- "`404/405`" = route không tồn tại cho method đó; cả hai mã đều PASS.

**Quy ước cho vế [DB]**
- `Status` trong DB viết HOA: `ACTIVE`. Giá trị "ngừng" mình đoán là `DISCONTINUED` và đánh dấu `[?]`; với Material là giá trị ngừng dùng `[?]`. Cần assert thêm rằng không có `Active`/`Discontinued` dạng PascalCase bị lưu nhầm.
- `MaterialType` trong DB là `FABRIC` / `TRIM` / `SCRAP`.
- "DB không đổi" = snapshot các bảng liên quan trước và sau khi gọi (gồm `COUNT(*)`, các cột nghiệp vụ, `AuditLogs`) trùng nhau.
- "AuditLogs +1 `[?]`" = kỳ vọng có 1 dòng nhật ký gắn với đối tượng bị tác động. Bảng/điều kiện ghi chưa xác nhận, nên ghi lại hành vi thực tế. Thao tác thất bại thì **không** được sinh nhật ký.
- `CreatedAt` phải ≈ thời điểm gọi theo giờ VN (`fn_VnNow()`), **không** được là `0001-01-01` hay giờ UTC.

**Viết tắt URL:** `A/` = `/api/v1/admin/`, `M/` = `/api/v1/manager/`, `AC/` = `/api/v1/accountant/`, `WK/` = `/api/v1/warehouse-keeper/`.

**Dữ liệu nền S0** (reset trước mỗi test)

- **Token:** TK_ADM (Admin), TK_MGR (Manager), TK_ACC (Accountant), TK_WK (WarehouseKeeper), TK_TL (TeamLeader), TK_QC (QaQc), TK_WRK (Worker), TK_EXP (hết hạn), TK_FORGED (sai chữ ký, claim Admin).
- **Customer:** CU1 "Công ty May Việt" (`ACTIVE`, Phone `0901000001`, Email `cu1@mayviet.vn`, có `CustomerOrder` OD1); CU2 "Garment ABC" (`ACTIVE`, Phone/Email NULL); CU3 "Khách Cũ" (đã ngừng `[?]`, `DiscontinueReason` = "Hết hợp tác").
- **Supplier:** SU1 "Vải Thành Công" (`ACTIVE`, Phone `0902000001`, Email `su1@tc.vn`, có 2 `PurchaseOrder`); SU2 "Phụ Liệu Minh" (`ACTIVE`, Phone/Email NULL); SU3 "NCC Cũ" (đã ngừng `[?]`).
- **Material:**
  - MA1 `V-0001` "Kaki" FABRIC, Color "Đen", WidthM 1.50. Đang được dùng ở `ProductComponentMaterial`, `OrderMaterial`, `StockBalance`.
  - MA2 `V-0002` "Kaki" FABRIC, Color `''`, WidthM 1.60. Chưa ai dùng.
  - MA3 `PL-0001` "Nút 4 lỗ" TRIM, Color `''`, WidthM NULL. Chưa ai dùng.
  - MA4 `V-0003` "Lụa" FABRIC, Color "Trắng". Đã ngừng dùng `[?]`.
- **Product:**
  - PR1 `SP001`. `ACTIVE`, có đơn OD1 nên `IsUsed=true`. Có 2 `ProductImage` (SortOrder 1, 2).
  - Spec của PR1:
    - Components: "Thân trước" (PANEL, đang được dùng trong đơn); "Túi" (PANEL, chưa dùng); "Nhãn" (TRIM).
    - Variants (do đơn tạo): (Đen, M), (Đen, L).
    - Norms: (Thân trước, Đen, MA1).
  - PR2 `SP002`. `ACTIVE`, không có đơn, không có biến thể, không có ảnh phụ.
  - PR3 `SP003`. Đã ngừng `[?]`.
  - Đơn OD2 thuộc **PR2**, dùng làm mục tiêu cho các ca chéo dữ liệu.
- **Vehicle:** VE1 `51C-123.45` (`ACTIVE`, có 1 `VehicleTrip` `STARTED`); VE2 `51C-678.90` (`ACTIVE`, không có chuyến đang chạy); VE3 `51C-000.01` (đã ngừng `[?]`); VE4 `51C-999.99` (`ACTIVE`, có **2** chuyến `STARTED`, là dữ liệu bất thường).
- **File mẫu:**
  - Ảnh: `img_ok.jpg` (1 MB); `img_limit.jpg` (6 291 456 B − 2 KB, chừa chỗ cho overhead của multipart); `img_over.jpg` (6 291 457 B); `empty.jpg` (0 B); `fake.jpg` (file .exe đổi đuôi); `note.txt`.
  - Excel: `spec_ok.xlsx` (hợp lệ cho PR1); `spec_err.xlsx` (có dòng `ParseError`); `spec_missing_mat.xlsx` (MaterialCode `V-9999` không tồn tại); `corrupt.xlsx` (hỏng); `not_excel.pdf`.

---

## 1. Ma trận phân quyền 4 prefix

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_001 | S0 | Không gửi token: `GET A/customers`, `GET M/suppliers`, `GET AC/vehicles`, `GET WK/vehicles/available` | [API]: 401 với cả 4 prefix; body không có dữ liệu danh mục.<br>[DB]: không đổi. | Abnormal |
| IT_B2_002 | TK_EXP, TK_FORGED | `GET A/products`; `POST AC/vehicles {…}` | [API]: 401 với cả hai token.<br>[DB]: không có `Vehicle` mới. | Abnormal |
| IT_B2_003 | TK_ADM | `GET AC/vehicles`; `GET AC/products`; `GET AC/products/sample-image?key=<hợp lệ>` | [API]: **403 Forbidden** (prefix accountant chỉ cho `Roles="Accountant"`; Admin không có quyền bao trùm).<br>[DB]: không đổi. | Abnormal |
| IT_B2_004 | TK_ADM | `POST AC/vehicles {PlateNumber:"51C-111.11", VehicleType:"Tải 1.5T", FuelNormPer100Km:12.5}`; `POST AC/materials {…}` | [API]: **403** với cả hai.<br>[DB]: không có dòng `Vehicle` `51C-111.11`; không có `Material` mới; AuditLogs không thêm dòng. | Abnormal |
| IT_B2_005 | TK_ADM | `GET WK/vehicles/available`; `GET WK/materials`; `POST WK/materials {…}`; `PATCH WK/materials/<MA2>/status {…}` | [API]: **403** với cả 4.<br>[DB]: `Material` không đổi. | Abnormal |
| IT_B2_006 | TK_ADM | `GET M/suppliers`; `GET M/materials`; `POST M/materials {MaterialName:"Chun", MaterialType:"TRIM", Unit:"cuộn"}` | [API]: 200 / 200 / 201 (Admin được gọi `/manager/*`).<br>[DB]: có 1 `Material` mới, `Status='ACTIVE'`. | Normal |
| IT_B2_007 | TK_MGR | `GET A/customers`, `GET A/suppliers`, `GET A/materials`, `GET A/products`, `GET A/vehicles` | [API]: 403 với cả 5.<br>[DB]: không đổi. | Abnormal |
| IT_B2_008 | TK_MGR | `GET M/products`; `POST M/products/<PR1>/import-data` | [API]: **404** (không tồn tại `Manager/ProductsController`).<br>[DB]: không đổi. | Abnormal |
| IT_B2_009 | TK_MGR | `POST M/materials/reopen {MaterialCode:"V-0003"}` | [API]: 404/405 (Manager không có route `reopen`).<br>[DB]: MA4 vẫn ở trạng thái ngừng `[?]`. | Abnormal |
| IT_B2_010 | TK_MGR | `GET AC/vehicles`; `GET WK/vehicles/available`; `POST WK/materials {…}` | [API]: 403 với cả 3.<br>[DB]: không đổi. | Abnormal |
| IT_B2_011 | TK_ACC | `GET A/materials`; `GET M/materials`; `GET M/suppliers`; `GET WK/materials` | [API]: 403 với cả 4 (Accountant không thuộc `Admin,Manager`).<br>[DB]: không đổi. | Abnormal |
| IT_B2_012 | TK_ACC | `GET AC/materials`; `PUT AC/materials/<MA2> {…}`; `PATCH AC/materials/<MA2>/status {…}` | [API]: 404/405 (Accountant chỉ có POST create).<br>[DB]: MA2 không đổi. | Abnormal |
| IT_B2_013 | TK_ACC | `POST AC/products {ProductCode:"SP900"}`; `PUT AC/products/<PR2> {…}`; `PUT AC/products/<PR1>/spec {…}`; `POST AC/products/<PR1>/import-data {…}` | [API]: 404/405 với cả 4 (Accountant chỉ có quyền xem).<br>[DB]: không có Product `SP900`; spec của PR1 không đổi. | Abnormal |
| IT_B2_014 | TK_WK | `GET A/products`; `GET M/materials`; `GET AC/vehicles`; `POST AC/materials {…}` | [API]: 403 với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B2_015 | TK_WK | `POST WK/materials/reopen {MaterialCode:"V-0003"}`; `POST WK/vehicles {…}` | [API]: 404/405.<br>[DB]: MA4 không đổi; không có Vehicle mới. | Abnormal |
| IT_B2_016 | TK_TL, TK_QC, TK_WRK (lần lượt) | `GET A/customers`; `GET M/suppliers`; `GET AC/products`; `GET WK/materials` | [API]: 403 với mọi tổ hợp (12 request).<br>[DB]: không đổi. | Abnormal |
| IT_B2_017 | TK_ADM | `POST A/vehicles {…}`; `PUT A/vehicles/<VE2> {…}` | [API]: 404/405 (`Admin/Vehicles` chỉ đọc).<br>[DB]: Vehicle không đổi. | Abnormal |
| IT_B2_018 | TK_ADM | `DELETE A/customers/<CU1>`; `DELETE A/suppliers/<SU1>`; `DELETE A/materials/<MA1>`; `DELETE A/products/<PR1>`; `DELETE AC/vehicles/<VE1>` (gọi bằng TK_ACC) | [API]: 404/405 với tất cả (không có API xóa, theo GBR-14).<br>[DB]: CU1, SU1, MA1, PR1, VE1 vẫn còn; các bảng con (`CustomerOrder`, `PurchaseOrder`, `ProductComponentMaterial`, `ProductImage`, `VehicleTrip`) không đổi. | Abnormal |
| IT_B2_019 | TK_ADM | `GET /api/v1/ADMIN/Customers` (khác hoa/thường); `GET A/customers/` (có dấu `/` cuối) | [API]: 200 (route không phân biệt hoa/thường); không 500.<br>[DB]: không đổi. | Boundary |

## 2. Customer — `A/customers`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_020 | TK_ADM | `POST A/customers {CustomerName:"May Sài Gòn", Phone:"0903000001", Email:"sg@may.vn", DefaultAddress:"12 Lê Lợi, Q1"}` | [API]: 201/200; body có `CustomerId`, `Status` (ghi lại dạng UPPER hay Pascal `[?]`).<br>[DB]: 1 dòng `Customer` với `Status='ACTIVE'`, `DiscontinueReason IS NULL`, tên tiếng Việt giữ nguyên dấu; AuditLogs +1 `[?]`. | Normal |
| IT_B2_021 | CU1 tồn tại | POST với `CustomerName:"Công ty May Việt"` | [API]: 409; message nghiệp vụ, không lộ tên `UQ_Customer_Name`.<br>[DB]: `COUNT(Customer WHERE CustomerName=N'Công ty May Việt') = 1`. | Abnormal |
| IT_B2_022 | CU1 tồn tại | POST với `"CÔNG TY MAY VIỆT"`; với `"Công ty May Việt "` (thừa khoảng trắng cuối) | [API]: 409 (nếu collation CI hoặc service trim) hoặc 201 `[?]`; ghi lại; không 500.<br>[DB]: nhất quán với API. | Boundary |
| IT_B2_023 | CU1 có Phone `0901000001` | POST khách mới với `Phone:"0901000001"` | [API]: 409.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_024 | CU1 có Email `cu1@mayviet.vn` | POST khách mới với `Email:"cu1@mayviet.vn"`; và `"CU1@MAYVIET.VN"` | [API]: 409 (ca hoa/thường ghi lại `[?]`).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_025 | CU2 đã có Phone/Email NULL | POST 2 khách mới (KH-N1, KH-N2), cả hai `Phone:null, Email:null` | [API]: cả hai 201.<br>[DB]: `COUNT(*) WHERE Phone IS NULL` tăng 2 (index filtered cho phép nhiều NULL). | Normal |
| IT_B2_026 | S0 | POST KH-E1 với `Phone:"", Email:""`; rồi POST KH-E2 cũng `Phone:"", Email:""` | [API]: cả hai 201 (nếu service chuẩn hóa `''` → NULL), hoặc lần 2 trả **409**; **không 500**.<br>[DB]: nếu 201 cả hai thì Phone/Email của cả hai là **NULL**, không phải `''`. Nếu lưu `''` thì ghi finding (vi phạm filtered unique). | Boundary |
| IT_B2_027 | TK_ADM | `CustomerName` dài 200 ký tự có dấu; và 201 ký tự | [API]: 200 → 201 Created; 201 → 400.<br>[DB]: chỉ ca 200 ký tự được lưu, đủ ký tự. | Boundary |
| IT_B2_028 | TK_ADM | Phone dài 15 / 16 ký tự; Email dài 100 / 101 ký tự; DefaultAddress dài 500 / 501 ký tự | [API]: các ca bằng ngưỡng → 201; các ca vượt ngưỡng → 400 (không 500 do truncation).<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B2_029 | TK_ADM | `CustomerName:null`; `""`; `"   "`; body `{}` | [API]: 400 (thiếu trường NOT NULL).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_030 | TK_ADM | `Email:"abc"`; `Phone:"09ab-12"`; Phone có ký tự tiếng Việt `"09đ"` | [API]: 400 `[?]`; không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_031 | TK_ADM | POST kèm `{CustomerId:999, Status:"DISCONTINUED", CreatedAt:"0001-01-01", DiscontinueReason:"x"}` (mass assignment) | [API]: 201, các field thừa bị bỏ qua.<br>[DB]: `CustomerId` do identity sinh, không phải 999; `Status='ACTIVE'`; `DiscontinueReason IS NULL`. | Abnormal |
| IT_B2_032 | S0 | Gửi **đồng thời** 2 POST cùng `CustomerName:"KH Đồng Thời"` | [API]: đúng 1 request 201, request còn lại **409** (lỗi unique từ DB được map thành 409, không 500).<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B2_033 | S0 | Gửi **đồng thời** 2 POST khác tên nhưng cùng `Phone:"0909999999"` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: chỉ có 1 dòng mang số điện thoại đó. | Boundary |
| IT_B2_034 | TK_ADM | `PUT A/customers/<CU2> {CustomerName:"Garment ABC Ltd", Phone:"0904000001", Email:null, DefaultAddress:"KCN Tân Bình"}` | [API]: 200.<br>[DB]: CU2 được cập nhật; `Status` giữ `ACTIVE`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_035 | CU1, CU2 tồn tại | PUT CU2 với `CustomerName:"Công ty May Việt"`; PUT CU2 với `Phone:"0901000001"` | [API]: 409 với cả hai.<br>[DB]: CU2 không đổi. | Abnormal |
| IT_B2_036 | CU1 tồn tại | PUT CU1 giữ nguyên tên/phone/email của chính nó | [API]: 200 (không tự xung đột với chính bản ghi của mình).<br>[DB]: không đổi về giá trị. | Boundary |
| IT_B2_037 | TK_ADM | `PUT A/customers/999999 {…}`; `PUT A/customers/abc {…}`; `PUT A/customers/0 {…}` | [API]: 404 với cả 3.<br>[DB]: không có dòng mới (không upsert). | Abnormal |
| IT_B2_038 | TK_ADM | PUT CU2 kèm `{Status:"DISCONTINUED"}` trong body | [API]: 200.<br>[DB]: `Status` vẫn `ACTIVE` (chỉ đổi trạng thái qua discontinue). | Abnormal |
| IT_B2_039 | Không có RowVer trên Customer | Gửi **đồng thời** 2 PUT CU2 với hai tên khác nhau ("A1", "A2") | [API]: cả hai 200 (last-write-wins), hoặc 1 request 409 `[?]`; không 500.<br>[DB]: CustomerName ∈ {A1, A2}; không có giá trị trộn. Ghi finding nếu ghi đè không kiểm soát. | Boundary |
| IT_B2_040 | CU2 `ACTIVE` | `POST A/customers/<CU2>/discontinue {Reason:"Ngừng đặt hàng"}` | [API]: 200.<br>[DB]: `Status` = `DISCONTINUED` `[?]` (HOA); `DiscontinueReason=N'Ngừng đặt hàng'`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_041 | CU2 `ACTIVE` | discontinue với `{Reason:null}`; `{Reason:""}`; body `{}`; không gửi body | [API]: 400 (theo comment DTO, Reason là bắt buộc); không gửi body → 400/415; không 500.<br>[DB]: CU2 vẫn `ACTIVE`. Nếu API trả 200 → ghi finding lệch so với comment. | Abnormal |
| IT_B2_042 | CU2 `ACTIVE` | Reason dài 500 ký tự có dấu; và 501 ký tự | [API]: 500 ký tự → 200; 501 ký tự → 400.<br>[DB]: ca 500 ký tự lưu đủ (nvarchar(500)). | Boundary |
| IT_B2_043 | CU1 có `CustomerOrder` OD1 | discontinue CU1 `{Reason:"Tạm dừng"}` | [API]: 200 (ngừng ≠ xóa) hoặc 409 nếu còn đơn mở `[?]`; ghi lại.<br>[DB]: `CustomerOrder` OD1 không đổi, vẫn trỏ CU1 (không bị cascade/SET NULL). | Normal |
| IT_B2_044 | CU3 đã ngừng | discontinue CU3 lần nữa | [API]: 400/409.<br>[DB]: `DiscontinueReason` cũ giữ nguyên. | Abnormal |
| IT_B2_045 | CU3 đã ngừng | `POST A/customers/<CU3>/reactivate` | [API]: 200.<br>[DB]: `Status='ACTIVE'`; `DiscontinueReason` về NULL hoặc giữ nguyên `[?]`, ghi lại; AuditLogs +1 `[?]`. | Normal |
| IT_B2_046 | CU1 `ACTIVE` | reactivate CU1; reactivate 999999 | [API]: CU1 → 400/409; 999999 → 404.<br>[DB]: không đổi. | Abnormal |
| IT_B2_047 | TK_ADM | `GET A/customers?Status=Active`; `?Status=Discontinued`; không truyền `Status` | [API]: Active → CU1, CU2; Discontinued → CU3; không truyền → tất cả `[?]`.<br>[DB]: số lượng khớp `WHERE Status='ACTIVE'` / giá trị ngừng (HOA). | Normal |
| IT_B2_048 | TK_ADM | `?Status=ACTIVE`; `?Status=Xyz` | [API]: 400, hoặc map không phân biệt hoa/thường `[?]`; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B2_049 | TK_ADM | `Page=0`; `Page=1`; `PageSize=0`; `PageSize=1`; `PageSize=100`; `PageSize=101` | [API]: Page=0 → 400; Page=1 → 200; PageSize=0 → 400; 1 / 100 → 200 với số phần tử ≤ PageSize; 101 → 400.<br>[DB]: không đổi. | Boundary |
| IT_B2_050 | TK_ADM | `Keyword` dài 100 ký tự; dài 101 ký tự; `Keyword:"' OR 1=1 --"`; `Keyword:"%"` | [API]: 100 → 200; 101 → 400; injection → 200 với danh sách rỗng (không trả toàn bộ, không 500); `%` → xử lý như ký tự thường `[?]`.<br>[DB]: không đổi. | Boundary |
| IT_B2_051 | TK_ADM | `SortDir=asc`; `SortDir=xyz`; `SortBy=CustomerName`; `SortBy="CustomerName; DROP TABLE Customer"` | [API]: asc → tăng dần; xyz → **giảm dần**; SortBy lạ → 400 hoặc bị bỏ qua, không 500.<br>[DB]: bảng `Customer` còn nguyên. | Boundary |
| IT_B2_052 | TK_ADM | `GET A/customers/options` | [API]: 200, mảng không phân trang; ghi lại có loại khách đã ngừng (CU3) hay không `[?]`.<br>[DB]: không đổi. | Normal |

## 3. Supplier — `A/suppliers`, `M/suppliers`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_053 | TK_ADM | `POST A/suppliers {SupplierName:"Dệt Phong Phú", Phone:"0905000001", Email:"pp@det.vn", Address:"Q9", MaterialTypes:"Vải, Chỉ", Note:"Giao 7 ngày"}` | [API]: 201.<br>[DB]: 1 dòng `Supplier` với `Status='ACTIVE'`, `CreatedAt` ≈ hiện tại (giờ VN), `DiscontinueReason IS NULL`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_054 | SU1 tồn tại | POST với `SupplierName:"Vải Thành Công"`; với `Phone:"0902000001"`; với `Email:"su1@tc.vn"` | [API]: 409 với cả ba.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_055 | SU2 có Phone/Email NULL | POST 2 NCC mới, cả hai Phone/Email null | [API]: cả hai 201.<br>[DB]: hợp lệ với filtered unique. | Normal |
| IT_B2_056 | S0 | POST 2 NCC mới, cả hai `Phone:""`, `Email:""` | [API]: cả hai 201 (chuẩn hóa thành NULL) hoặc lần 2 trả 409; **không 500**.<br>[DB]: không tồn tại dòng nào có `Phone=''` (nếu có thì ghi finding). | Boundary |
| IT_B2_057 | TK_ADM | SupplierName 200 / 201 ký tự; MaterialTypes 200 / 201; Note 500 / 501; Address 500 / 501 | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B2_058 | TK_ADM | `SupplierName` null / rỗng / chỉ khoảng trắng | [API]: 400.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_059 | S0 | Gửi **đồng thời** 2 POST cùng `SupplierName:"NCC Đồng Thời"` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B2_060 | TK_ADM | `GET A/suppliers/<SU1>` | [API]: 200; `Supplier.PurchaseOrderCount=2`; `History[]` là danh sách `AuditLogDto`.<br>[DB]: `PurchaseOrderCount` khớp `COUNT(PurchaseOrder WHERE SupplierId=SU1)`. | Normal |
| IT_B2_061 | TK_ADM | `GET A/suppliers/999999`; `GET A/suppliers/abc` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B2_062 | TK_ADM | `PUT A/suppliers/<SU2> {SupplierName:"Phụ Liệu Minh Long", …}`; PUT SU2 với `SupplierName:"Vải Thành Công"` | [API]: lần đầu 200; lần sau 409.<br>[DB]: SU2 chỉ mang giá trị của lần đầu. | Normal |
| IT_B2_063 | SU2 `ACTIVE` | `POST A/suppliers/<SU2>/discontinue {Reason:null}`; và discontinue NCC khác với body `{}` | [API]: 200 với cả hai (Reason **không bắt buộc**, khác Customer).<br>[DB]: Status chuyển sang giá trị ngừng `[?]`; `DiscontinueReason IS NULL`. | Normal |
| IT_B2_064 | SU2 `ACTIVE` | discontinue với Reason dài 500 / 501 ký tự | [API]: 500 → 200; 501 → 400.<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B2_065 | SU1 có 2 PurchaseOrder | discontinue SU1 `{Reason:"Giá cao"}` | [API]: 200 (hoặc 409 nếu còn PO mở `[?]`).<br>[DB]: các `PurchaseOrder` của SU1 không đổi, vẫn trỏ SU1; `DiscontinueReason` được lưu. | Normal |
| IT_B2_066 | Sau IT_B2_065 | `GET M/suppliers` bằng TK_MGR | [API]: 200; mảng `SupplierOptionDto` **không** có SU1 và SU3; chỉ có `{SupplierId, SupplierName}`.<br>[DB]: không đổi. | Normal |
| IT_B2_067 | SU3 đã ngừng | reactivate SU3; reactivate SU2 (đang ACTIVE) | [API]: SU3 → 200; SU2 → 400/409.<br>[DB]: SU3 `Status='ACTIVE'`; SU2 không đổi. | Normal |
| IT_B2_068 | SU3 đã ngừng | discontinue SU3 lần nữa | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B2_069 | TK_ADM | `GET A/suppliers?Status=`; `?Status=Active`; `?Status=Discontinued` | [API]: Status rỗng → tất cả (3 NCC); Active → SU1, SU2; Discontinued → SU3.<br>[DB]: khớp `Status` HOA. | Normal |

## 4. Material — 4 controller (A / M / AC / WK)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_070 | Mã FABRIC lớn nhất là V-0003 | `POST A/materials {MaterialCode:null, MaterialName:"Jean", MaterialType:"FABRIC", Unit:"m", Color:"Xanh", WidthM:1.45}` | [API]: 201; `MaterialCode="V-0004"`.<br>[DB]: `Material(V-0004, 'Jean', 'FABRIC', 'Xanh', Unit='m', WidthM=1.45, Status='ACTIVE')`; `CreatedAt` ≈ hiện tại; AuditLogs +1 `[?]`. | Normal |
| IT_B2_071 | Mã TRIM lớn nhất là PL-0001 | `POST M/materials {MaterialCode:"", MaterialName:"Khóa kéo", MaterialType:"TRIM", Unit:"cái"}` | [API]: 201; `MaterialCode="PL-0002"`.<br>[DB]: `MaterialType='TRIM'`, `Color=''`, `WidthM IS NULL`. | Normal |
| IT_B2_072 | TK_WK | `POST WK/materials {MaterialCode:"V-CUSTOM-01", MaterialName:"Thun", MaterialType:"FABRIC", Unit:"m", WidthM:1.80}` | [API]: 201; giữ nguyên mã đã gửi.<br>[DB]: `MaterialCode='V-CUSTOM-01'`. | Normal |
| IT_B2_073 | TK_ACC | `POST AC/materials {MaterialName:"Chỉ may", MaterialType:"TRIM", Unit:"cuộn"}` | [API]: 201 (Accountant được tạo NVL).<br>[DB]: dòng `Material` mới, `Status='ACTIVE'`. | Normal |
| IT_B2_074 | V-0001 tồn tại | POST với `MaterialCode:"V-0001"`; và `"v-0001"` | [API]: 409 (ca chữ thường ghi lại theo collation `[?]`); không 500.<br>[DB]: `COUNT(MaterialCode='V-0001') = 1`. | Abnormal |
| IT_B2_075 | TK_ADM | MaterialCode dài 30 / 31 ký tự | [API]: 30 → 201; 31 → 400.<br>[DB]: chỉ ca 30 ký tự được lưu. | Boundary |
| IT_B2_076 | MA1 = ("Kaki", FABRIC, "Đen") | POST `{MaterialName:"Kaki", MaterialType:"FABRIC", Color:"Đen", Unit:"m", WidthM:1.5}` | [API]: 409 (trùng `UQ_Material_NameType`).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_077 | MA2 = ("Kaki", FABRIC, `''`) | POST ("Kaki", FABRIC, `Color:null`, WidthM:1.5) | [API]: 409 (Color null → mặc định `''`, trùng MA2); không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_078 | MA1, MA2 tồn tại | POST ("Kaki", FABRIC, "Đỏ"); POST ("Kaki", TRIM, `''`) | [API]: cả hai 201 (khác màu hoặc khác loại là NVL khác).<br>[DB]: có 4 dòng tên "Kaki" với các tổ hợp (Type, Color) khác nhau. | Normal |
| IT_B2_079 | MA1 có Color "Đen" | POST ("Kaki", FABRIC, "đen") — khác hoa/thường | [API]: 409 hoặc 201 tùy collation `[?]`; ghi lại; không 500.<br>[DB]: nhất quán với API. | Boundary |
| IT_B2_080 | TK_ADM | FABRIC không gửi `WidthM` (null) | [API]: 400 (WidthM bắt buộc với vải, DEV-ALL-102).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_081 | TK_ADM | TRIM gửi kèm `WidthM:1.20` | [API]: 201.<br>[DB]: `WidthM IS NULL` (giá trị bị bỏ qua với phụ liệu). | Normal |
| IT_B2_082 | TK_ADM | FABRIC gửi `Unit:"cuộn"` | [API]: 201.<br>[DB]: `Unit='m'` (vải luôn tính bằng mét). | Normal |
| IT_B2_083 | TK_ADM | FABRIC với `WidthM`: `99.99`; `100.00`; `0.01`; `0`; `-1` | [API]: 99.99 → 201; 100.00 → 400 (không 500 do tràn `decimal(4,2)`); 0.01 → 201; 0 / −1 → 400 `[?]`.<br>[DB]: chỉ ca hợp lệ được lưu, đúng giá trị. | Boundary |
| IT_B2_084 | TK_ADM | FABRIC với `WidthM:1.555` | [API]: 400, hoặc 201 kèm làm tròn `[?]`; ghi lại.<br>[DB]: nếu lưu thì giá trị là `1.56` (hoặc `1.55`); ghi lại cách làm tròn. | Boundary |
| IT_B2_085 | TK_ADM | `MaterialType:"OTHER"` (giá trị "nhóm OTHER" lấy từ comment PO); `"XYZ"`; `"fabric"` | [API]: OTHER / XYZ → 400 (không có CHECK ở DB nên Service phải chặn; nếu 201 → finding nghiêm trọng); `fabric` → 400 hoặc chuẩn hóa `[?]`.<br>[DB]: không tồn tại `MaterialType` nào ngoài {`FABRIC`, `TRIM`, `SCRAP`}. | Abnormal |
| IT_B2_086 | TK_ADM | `MaterialType:"SCRAP"` | [API]: 400 hoặc 201 `[?]` (SCRAP chỉ dành cho trả kho); ghi lại.<br>[DB]: nhất quán với API. | Abnormal |
| IT_B2_087 | TK_ADM | MaterialType dài 11 ký tự `"FABRICXXXXX"` | [API]: 400; không 500 (cột `varchar(10)`).<br>[DB]: không có dòng mới. | Boundary |
| IT_B2_088 | TK_ADM | MaterialName 200 / 201 ký tự; Color 50 / 51 ký tự; Unit 20 / 21 ký tự (dùng TRIM) | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu, đúng dấu tiếng Việt. | Boundary |
| IT_B2_089 | TK_ADM | Thiếu `MaterialName`; thiếu `MaterialType`; thiếu `Unit` (TRIM) | [API]: 400.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_090 | TK_ADM | POST kèm `{MaterialId:1, Status:"INACTIVE", CreatedAt:"0001-01-01T00:00:00"}` | [API]: 201; field thừa bị bỏ qua.<br>[DB]: `Status='ACTIVE'`; `CreatedAt` ≈ hiện tại, **không** phải `0001-01-01`; MaterialId do identity sinh. | Abnormal |
| IT_B2_091 | Mã FABRIC lớn nhất V-0003 | Gửi **đồng thời** 2 POST FABRIC `MaterialCode:null` ("Jean A", "Jean B") | [API]: cả hai 201 với 2 mã khác nhau (V-0004, V-0005), hoặc 1 request 409 có thể retry; **không 500** (race khi tự sinh mã).<br>[DB]: không có hai dòng trùng `MaterialCode`; không có khoảng trống bất thường `[?]`. | Boundary |
| IT_B2_092 | S0 | Gửi **đồng thời** 2 POST cùng ("Lanh", FABRIC, "Be"), một qua `A/`, một qua `WK/` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B2_093 | MA2 chưa được dùng | `PUT A/materials/<MA2> {MaterialName:"Kaki", MaterialType:"FABRIC", Unit:"m", Color:"Nâu", WidthM:1.60}` | [API]: 200.<br>[DB]: `Color=N'Nâu'`; `MaterialCode` vẫn `V-0002`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_094 | MA1 đang được dùng ở định mức/đơn/tồn kho | PUT MA1 đổi `Color:"Xám"` | [API]: 400/409 (không được đổi màu NVL đã dùng).<br>[DB]: MA1 vẫn Color "Đen"; các dòng `ProductComponentMaterial`, `OrderMaterial`, `StockBalance` không đổi. | Abnormal |
| IT_B2_095 | MA1 đang được dùng | PUT MA1 giữ màu "Đen", đổi `MaterialName:"Kaki cao cấp"`, `WidthM:1.52` | [API]: 200 `[?]` (chỉ cấm đổi màu); ghi lại.<br>[DB]: Name/WidthM đổi; Color không đổi. | Normal |
| IT_B2_096 | MA1 ("Kaki", FABRIC, "Đen") và MA2 tồn tại | PUT MA2 sang `Color:"Đen"` | [API]: 409 (trùng khóa ghép).<br>[DB]: MA2 không đổi. | Abnormal |
| IT_B2_097 | MA2 tồn tại | PUT MA2 kèm `MaterialCode:"V-9000"` trong body | [API]: 200 (field bị bỏ qua) hoặc 400.<br>[DB]: `MaterialCode` vẫn `V-0002`. | Abnormal |
| IT_B2_098 | MA3 là TRIM | PUT MA3 đổi `MaterialType:"FABRIC"` nhưng không gửi WidthM | [API]: 400.<br>[DB]: MA3 không đổi. | Abnormal |
| IT_B2_099 | TK_ADM | `PUT A/materials/999999 {…}`; `GET A/materials/999999`; `GET A/materials/abc` | [API]: 404.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_100 | MA2 `ACTIVE` | `PATCH A/materials/<MA2>/status {Status:<giá trị ngừng hợp lệ [?]>}` | [API]: 200.<br>[DB]: `Status` = giá trị ngừng (HOA) `[?]`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_101 | TK_WK | `PATCH WK/materials/<MA2>/status {Status:"Xyz"}`; `{Status:""}`; body `{}` | [API]: 400.<br>[DB]: MA2 vẫn `ACTIVE`. | Abnormal |
| IT_B2_102 | MA1 đang có tồn kho trong `StockBalance` | PATCH MA1 sang trạng thái ngừng | [API]: 200 hoặc 409 `[?]`; ghi lại; không 500.<br>[DB]: `StockBalance` của MA1 không đổi. | Abnormal |
| IT_B2_103 | MA4 `V-0003` đã ngừng | `POST A/materials/reopen {MaterialCode:"V-0003"}` | [API]: 200.<br>[DB]: MA4 `Status='ACTIVE'`; không có dòng Material mới; AuditLogs +1 `[?]`. | Normal |
| IT_B2_104 | TK_ADM | reopen `{MaterialCode:"V-0001"}` (đang ACTIVE); `{MaterialCode:"V-9999"}`; `{MaterialCode:""}` | [API]: 400/409; 404; 400 tương ứng.<br>[DB]: không đổi. | Abnormal |
| IT_B2_105 | TK_MGR | `GET M/materials?MaterialType=FABRIC&Status=Active` | [API]: 200; chỉ có MA1, MA2 (không có MA4).<br>[DB]: khớp `WHERE MaterialType='FABRIC' AND Status='ACTIVE'`. | Normal |
| IT_B2_106 | TK_WK | `GET WK/materials/<MA1>` | [API]: 200; `WidthM=1.50` (kiểu decimal, không bị lỗi số thực).<br>[DB]: không đổi. | Normal |

## 5. Product — thông tin chung và ảnh phụ (`ProductImage` không có CHECK)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_107 | TK_ADM; có sẵn 5 ImageKey hợp lệ K1–K5 (upload qua P7) | `POST A/products {ProductCode:"SP100", SampleImageUrl:K0, Description:"Áo sơ mi nam", ExtraImages:[K1,K2,K3,K4,K5]}` | [API]: 201.<br>[DB]: `Product(SP100, Status='ACTIVE')`; đúng **5** dòng `ProductImage` với `SortOrder` = 1, 2, 3, 4, 5 theo thứ tự gửi lên, `ProductId` khớp; AuditLogs +1 `[?]`. | Normal |
| IT_B2_108 | TK_ADM | POST `SP101` với `ExtraImages:[K1..K6]` (**6 ảnh**) | [API]: **400** ("tối đa 5 ảnh").<br>[DB]: **Rollback**: không có `Product SP101`; không có `ProductImage` mồ côi. Kiểm tra trực tiếp `COUNT(ProductImage)` không đổi (vì DB không có CHECK để chặn). | Abnormal |
| IT_B2_109 | TK_ADM | POST `SP102` với `ExtraImages:[]`; POST `SP103` không gửi ExtraImages | [API]: 201 với cả hai.<br>[DB]: không có dòng `ProductImage` nào cho SP102, SP103. | Normal |
| IT_B2_110 | PR1 có 2 ảnh phụ | `PUT A/products/<PR1> {ProductCode:"SP001", …, ExtraImages:null}` | [API]: 200.<br>[DB]: 2 dòng `ProductImage` giữ nguyên (cùng `ImageKey` và `SortOrder`). | Normal |
| IT_B2_111 | PR1 có 2 ảnh phụ | PUT PR1 với `ExtraImages:[]` | [API]: 200.<br>[DB]: `COUNT(ProductImage WHERE ProductId=PR1) = 0`. | Normal |
| IT_B2_112 | PR1 có 2 ảnh phụ | PUT PR1 với `ExtraImages:[K3,K1,K4]` | [API]: 200.<br>[DB]: đúng 3 dòng, `SortOrder` liên tục 1–3 theo thứ tự K3, K1, K4; không vi phạm `UQ_ProductImage_Sort`; không 500 do việc sắp lại SortOrder va chạm UNIQUE giữa chừng. | Normal |
| IT_B2_113 | PR1 có 2 ảnh phụ | PUT PR1 với 6 ảnh | [API]: 400.<br>[DB]: **Rollback**: PR1 vẫn đúng 2 ảnh cũ; `Product` (Description…) cũng không đổi. | Abnormal |
| IT_B2_114 | Nếu `ExtraImages` có cho phép gửi `SortOrder` `[?]` | Gửi ảnh với `SortOrder:0`; `SortOrder:6`; hai ảnh cùng `SortOrder:2` | [API]: 400 với cả ba (DB không có CHECK nên API phải chặn).<br>[DB]: không tồn tại dòng nào có `SortOrder` ∉ [1..5] hoặc trùng. Nếu DTO không có field SortOrder → field bị bỏ qua và SortOrder tự sinh 1..n. | Boundary |
| IT_B2_115 | Chèn trực tiếp bằng SQL cho PR2: 1 dòng `SortOrder=0`, 1 dòng `SortOrder=6` (chứng minh DB không có CHECK) | `GET A/products/<PR2>`; rồi `PUT A/products/<PR2> {…, ExtraImages:[K1]}` | [API]: GET → 200, không 500 khi gặp dữ liệu bẩn; PUT → 200.<br>[DB]: INSERT trực tiếp thành công (ghi **finding**: thiếu CHECK 1–5). Sau PUT, PR2 chỉ còn đúng 1 ảnh, `SortOrder=1`. | Boundary |
| IT_B2_116 | TK_ADM | ExtraImages có `ImageKey` dài 500 / 501 ký tự; ExtraImages chứa key không tồn tại trong storage; chứa `null` | [API]: 500 ký tự → 201 (nếu key hợp lệ); 501 → 400; key không tồn tại → 400 `[?]`; `null` → 400; không 500.<br>[DB]: **Rollback**: không có Product mới nào ở các ca lỗi. | Boundary |
| IT_B2_117 | TK_ADM | ExtraImages `[K1, K1]` (trùng key) | [API]: 400 hoặc 201 `[?]`; ghi lại.<br>[DB]: nếu 201 thì có 2 dòng `SortOrder` 1 và 2, hợp lệ với UQ. | Boundary |
| IT_B2_118 | PR1 có 2 ảnh | Gửi **đồng thời** 2 PUT PR1: một với `[K1,K2,K3]`, một với `[K4,K5]` | [API]: cả hai 200, hoặc 1 request 409; **không 500** dù có va chạm `UQ_ProductImage_Sort`.<br>[DB]: kết quả cuối đúng bằng **một** trong hai tập (3 hoặc 2 ảnh), `SortOrder` liên tục từ 1; không có ảnh trộn của cả hai. | Boundary |
| IT_B2_119 | SP001 tồn tại | POST `ProductCode:"SP001"`; `"sp001"`; PUT PR2 với `ProductCode:"SP001"` | [API]: 409 với cả 3 (ca chữ thường ghi lại theo collation `[?]`).<br>[DB]: không đổi. | Abnormal |
| IT_B2_120 | TK_ADM | ProductCode 30 / 31 ký tự; Description 1000 / 1001 ký tự; SampleImageUrl 500 / 501 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B2_121 | TK_ADM | `ProductCode` null / rỗng | [API]: 400.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_122 | TK_ADM | `SampleImageUrl:"https://cdn.x.vn/a.jpg"`; `"javascript:alert(1)"`; `"file:///etc/passwd"` | [API]: https → 201 (dữ liệu cũ được phép); javascript / file → 400 `[?]`.<br>[DB]: không lưu scheme nguy hiểm. | Abnormal |
| IT_B2_123 | S0 | Gửi **đồng thời** 2 POST `ProductCode:"SP200"` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: 1 Product; ảnh phụ chỉ thuộc bản ghi thắng, không có `ProductImage` mồ côi. | Boundary |
| IT_B2_124 | PR2 `ACTIVE` | `POST A/products/<PR2>/discontinue` (không body) | [API]: 200.<br>[DB]: `Status` = `DISCONTINUED` `[?]`; `ProductImage` không đổi; AuditLogs +1 `[?]`. | Normal |
| IT_B2_125 | PR1 đang được dùng trong OD1 | discontinue PR1 | [API]: 200 hoặc 409 `[?]`; ghi lại; không 500.<br>[DB]: `CustomerOrder` OD1, `ProductVariant`, `ProductComponent` không đổi. | Abnormal |
| IT_B2_126 | PR3 đã ngừng | discontinue PR3; reactivate PR1 (đang ACTIVE); reactivate 999999 | [API]: 400/409; 400/409; 404.<br>[DB]: không đổi. | Abnormal |
| IT_B2_127 | PR3 đã ngừng | reactivate PR3 | [API]: 200.<br>[DB]: `Status='ACTIVE'`. | Normal |
| IT_B2_128 | TK_ADM | `GET A/products` (không truyền Status); `?Status=Active`; `?Status=Discontinued` | [API]: null → tất cả (PR1–PR3); PR1 có `IsUsed=true`, PR2 có `IsUsed=false`.<br>[DB]: `IsUsed` khớp việc có tham chiếu từ đơn hàng. | Normal |
| IT_B2_129 | TK_ACC | `GET AC/products`; `GET AC/products/<PR1>/spec` | [API]: 200 với cả hai; dữ liệu giống endpoint của Admin.<br>[DB]: không đổi. | Normal |

## 6. Product — upload và đọc ảnh mẫu (P7, P8, P19)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_130 | TK_ADM | `POST A/products/sample-image` multipart `file=img_ok.jpg` | [API]: 200; trả về khóa ảnh (string khác rỗng).<br>[DB]: **không** có INSERT nào vào `Product`/`ProductImage` (chỉ ghi vào storage). | Normal |
| IT_B2_131 | TK_ADM | Multipart không có `file` (`file == null`) | [API]: **400**; **không 500 / NullReference** (controller truyền byte rỗng xuống service).<br>[DB]: không đổi; storage không có file rác. | Abnormal |
| IT_B2_132 | TK_ADM | `file=empty.jpg` (0 byte) | [API]: 400.<br>[DB]: không đổi. | Boundary |
| IT_B2_133 | TK_ADM | `file=img_limit.jpg` (vừa dưới 6 MiB, tính cả overhead multipart) | [API]: 200.<br>[DB]: không đổi. | Boundary |
| IT_B2_134 | TK_ADM | `file=img_over.jpg` (6 291 457 B) | [API]: **413 Payload Too Large** (hoặc 400); không 500.<br>[DB]: không đổi; không lưu file. | Boundary |
| IT_B2_135 | TK_ADM | `file=fake.jpg` (.exe đổi đuôi); `file=note.txt` | [API]: 400 (kiểm tra theo nội dung/magic bytes `[?]`).<br>[DB]: không đổi. | Abnormal |
| IT_B2_136 | K1 đã upload | `GET A/products/sample-image?key=K1` | [API]: 200; trả **file** (Content-Type image/*), header `Cache-Control: private, max-age=3600`.<br>[DB]: không đổi. | Normal |
| IT_B2_137 | TK_ADM | `GET A/products/sample-image` (thiếu key); `?key=` (rỗng); `?key=khong-ton-tai` | [API]: 400 / 400 / 404; lỗi trả **JSON** qua `ToResponse`, không trả file.<br>[DB]: không đổi. | Abnormal |
| IT_B2_138 | TK_ADM / TK_ACC | `?key=../../appsettings.json`; `?key=..%2F..%2Fweb.config`; `?key=/etc/passwd`; `?key=C:\Windows\win.ini` | [API]: 400/404 với tất cả; **không** trả nội dung file hệ thống (chống path traversal, cũng là một dạng IDOR); không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B2_139 | K1 đã upload | `GET AC/products/sample-image?key=K1` bằng TK_ACC; cùng URL bằng TK_MGR | [API]: Accountant → 200 (file); Manager → 403.<br>[DB]: không đổi. | Normal |

## 7. Product — định mức (P9, P10). Toàn vẹn transaction

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_140 | Spec của PR1 như S0 | `GET A/products/<PR1>/spec` | [API]: 200; có `Components` (3 dòng, kèm `Status`), `Variants` (Đen-M, Đen-L), `Norms`, `MarkerComponents`, `MarkerFabrics`.<br>[DB]: khớp các bảng `ProductComponent`, `ProductVariant`, `ProductComponentMaterial`. | Normal |
| IT_B2_141 | TK_ADM | `GET A/products/999999/spec`; `PUT A/products/999999/spec {…}` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B2_142 | PR1 như S0 | `PUT A/products/<PR1>/spec` với Components = {Thân trước (PANEL, 1), Túi (PANEL, 2), Nhãn (TRIM, 1), **Cổ** (PANEL, 1)}; Variants = 2 biến thể hiện có; Norms = {(Thân trước, Đen, MA1), (Cổ, Đen, MA2)}; MarkerComponents = {(1, Thân trước), (1, Cổ)}; MarkerColors = {(1, Đen)} | [API]: 200.<br>[DB]: thêm `ProductComponent` "Cổ"; `ProductComponentMaterial` của biến thể màu Đen được thay bằng đúng 2 dòng gửi lên; nhóm bìa được thay nguyên; `ProductVariant` **không đổi** (COUNT và giá trị). | Normal |
| IT_B2_143 | "Túi" chưa được dùng; "Thân trước" đang dùng trong đơn | PUT spec **bỏ** "Túi" và **bỏ** "Thân trước" khỏi Components (đồng thời bỏ các norm/marker liên quan) | [API]: 200.<br>[DB]: dòng "Túi" bị **DELETE**; dòng "Thân trước" **không** bị xóa mà chuyển `Status` sang ngừng dùng `[?]` (HOA); dữ liệu của đơn OD1 không đổi. | Normal |
| IT_B2_144 | PR1 chỉ có màu Đen | PUT spec với Norm `(Thân trước, "Trắng", MA1)` — màu không có trong Variants | [API]: 400.<br>[DB]: **Rollback toàn bộ**: Components/Norms/Markers của PR1 không đổi, dù trong request có cả phần hợp lệ. | Abnormal |
| IT_B2_145 | TK_ADM | Norm với `MaterialId:999999` (FK không tồn tại) | [API]: 400/404; **không 500** do lỗi FK.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B2_146 | MA4 đã ngừng dùng | Norm với `MaterialId:<MA4>` | [API]: 400 `[?]`; ghi lại.<br>[DB]: không đổi nếu bị từ chối. | Abnormal |
| IT_B2_147 | TK_ADM | Variants có thêm biến thể mới `(Xanh, XL)` chưa tồn tại | [API]: 400, hoặc 200 nhưng bỏ qua biến thể mới `[?]`.<br>[DB]: `ProductVariant` **không** có dòng (Xanh, XL) (spec không được tạo/sửa biến thể). | Abnormal |
| IT_B2_148 | TK_ADM | Norm tham chiếu `ComponentName:"Tay áo"` không có trong Components; MarkerComponents trỏ tới thành phần không tồn tại | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B2_149 | TK_ADM | `ComponentType:"PANEL2"`; `ComponentType:"panel"`; ComponentName trùng nhau 2 lần trong cùng request | [API]: 400 (chữ thường ghi lại `[?]`).<br>[DB]: không đổi. | Abnormal |
| IT_B2_150 | TK_ADM | `PiecesPerGarment`: `0`; `-1`; `1`; `32767`; `32768` (tràn `short`) | [API]: 0 / −1 → 400; 1 → 200; 32767 → 200 hoặc 400 theo giới hạn nghiệp vụ `[?]`; 32768 → 400 (lỗi deserialize), **không 500**.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B2_151 | TK_ADM | `MarkerNo`: `0`; `-1`; `2147483648` | [API]: 400; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B2_152 | TK_ADM | PUT spec với **tất cả** mảng rỗng `[]` | [API]: 200 hoặc 400 `[?]`; ghi lại.<br>[DB]: nếu 200 thì thành phần chưa dùng bị xóa, thành phần đang dùng ("Thân trước") chỉ bị ngừng dùng; `ProductVariant` không đổi. | Boundary |
| IT_B2_153 | Fault-injection: làm INSERT vào `ProductMarker` lỗi (ví dụ trigger test-only có `THROW`) | PUT spec hợp lệ như IT_B2_142 | [API]: lỗi 4xx/5xx có kiểm soát; ghi lại mã.<br>[DB]: **Rollback sạch**: "Cổ" không được thêm; Norms cũ còn nguyên (không bị xóa nửa chừng). | Abnormal |
| IT_B2_154 | PR1 như S0 | Gửi **đồng thời** 2 PUT spec khác nhau (A thêm "Cổ", B thêm "Vai") | [API]: cả hai 200, hoặc 1 request 409; không 500 (deadlock phải được xử lý).<br>[DB]: trạng thái cuối khớp đúng **một** request; không có Components/Norms trộn, không có dòng Norm mồ côi. | Boundary |
| IT_B2_155 | PR3 đã ngừng | PUT spec cho PR3 | [API]: 400 hoặc 200 `[?]`; ghi lại.<br>[DB]: nhất quán với API. | Abnormal |

## 8. Product — Excel: parse, check, preview (chỉ đọc) và import (1 giao dịch)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_156 | TK_ADM | `POST A/products/excel/parse` với `file=spec_ok.xlsx` | [API]: 200; trả các dòng đã parse.<br>[DB]: **không đổi** (snapshot toàn bộ bảng Product/Spec/Material). | Normal |
| IT_B2_157 | TK_ADM | parse với `corrupt.xlsx`; `not_excel.pdf`; không gửi file | [API]: 400 với cả ba; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B2_158 | TK_ADM | `POST A/products/excel/check {ProductId:<PR1>, OrderId:null, Bundle:<có Norm với MaterialCode "V-9999">, Replace:false}` | [API]: 200; `MissingMaterials` có `{MaterialCode:"V-9999", SuggestedType:"FABRIC"\|"TRIM", Colors[]}`.<br>[DB]: **không** có INSERT vào `Material` (không tự tạo NVL). | Normal |
| IT_B2_159 | TK_ADM | check với `ProductId:999999`; với `OrderId:999999` | [API]: 404/400; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B2_160 | TK_ADM | `POST A/products/<PR1>/import/preview?replace=false` với `file=spec_ok.xlsx` | [API]: 200; mỗi dòng có `Action` ∈ {`New`, `Update`, `Unchanged`} (PascalCase); `CanImport=true`; `ErrorCount=0`.<br>[DB]: **không đổi**. | Normal |
| IT_B2_161 | TK_ADM | preview-data với `SpecWorkbookDto` có 1 dòng `ParseError`, và một Norm trỏ tới thành phần không có (gây `CrossErrors`) | [API]: 200; `CanImport=false`; `ErrorCount ≥ 1`; `CrossErrors` khác rỗng; dòng lỗi có `Action="Error"`.<br>[DB]: không đổi. | Abnormal |
| IT_B2_162 | TK_ADM | preview với `replace=true` | [API]: 200; `RemovedCount` = số dòng cũ không có trong file.<br>[DB]: **không đổi** (preview không được xóa thật). | Normal |
| IT_B2_163 | TK_ADM | preview với `parts=" Norms , ,NORMS,components"`; với `parts=""`; với `parts="abc"` | [API]: lần 1 → chuẩn hóa thành {norms, components}, chỉ preview 2 phần này; `""` → mọi phần; `"abc"` → 400 hoặc bị bỏ qua `[?]`; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B2_164 | TK_ADM | `POST A/products/<PR1>/import?replace=false` với `file=spec_ok.xlsx` | [API]: 200; `SpecImportResultDto` có số đếm `Components`, `Norms`, `MarkerGroups` khớp với preview; `Removed=0`.<br>[DB]: `ProductComponent`, `ProductComponentMaterial`, `ProductMarker`/`ProductMarkerFabric` `[?]` thay đổi đúng như preview; `ProductVariant` không đổi; AuditLogs +1 `[?]`. | Normal |
| IT_B2_165 | TK_ADM | import với `spec_err.xlsx` (có dòng lỗi lẫn dòng hợp lệ) | [API]: 400/409; liệt kê lỗi theo `RowNumber`.<br>[DB]: **Rollback toàn bộ (1 giao dịch)**: không nhập dòng hợp lệ nào. | Abnormal |
| IT_B2_166 | TK_ADM | import-data với Norm có `MaterialCode:"V-9999"` (không tồn tại) | [API]: 400.<br>[DB]: không đổi; **không** tự tạo `Material` V-9999. | Abnormal |
| IT_B2_167 | PR1 có "Túi" (chưa dùng) và "Thân trước" (đang dùng); file không chứa cả hai | import-data `?replace=true&parts=components` | [API]: 200; `Removed=2`.<br>[DB]: "Túi" bị **DELETE**; "Thân trước" chỉ UPDATE `Status` sang ngừng dùng `[?]`; dữ liệu đơn OD1 không đổi. | Normal |
| IT_B2_168 | Như IT_B2_167 | import-data `?replace=false` với cùng nội dung | [API]: 200; `Removed=0`.<br>[DB]: "Túi" và "Thân trước" vẫn còn, giữ nguyên `Status`. | Normal |
| IT_B2_169 | TK_ADM | import-data `?parts=norms` (file có cả components lẫn norms) | [API]: 200; chỉ phần Norms được xử lý.<br>[DB]: chỉ `ProductComponentMaterial` thay đổi; `ProductComponent` và nhóm bìa không đổi. | Normal |
| IT_B2_170 | TK_ADM | import-data có `HasOrderMarkers=true` nhưng **không** truyền `orderId` | [API]: 200; `Notes` có ghi chú rằng bảng bìa của đơn đã bị bỏ qua.<br>[DB]: không có thay đổi ở bảng bìa cắt của đơn `[?]`. | Normal |
| IT_B2_171 | OD1 thuộc PR1 | import-data `?orderId=<OD1>` với OrderMarkers hợp lệ (`Status` để trống) | [API]: 200; `OrderMarkers > 0`.<br>[DB]: bìa cắt của OD1 được tạo, `Status='ACTIVE'` (để trống = ACTIVE). | Normal |
| IT_B2_172 | OD2 thuộc **PR2** | `POST A/products/<PR1>/import-data?orderId=<OD2>` | [API]: 400/404 (đơn không thuộc mã hàng này, là truy cập chéo dữ liệu); không lộ thông tin của OD2.<br>[DB]: không có thay đổi gì ở OD2 hay PR1. | Abnormal |
| IT_B2_173 | TK_ADM | import-data `?orderId=999999`; import vào `products/999999` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B2_174 | TK_ADM | OrderMarker có `Status:"Active"` (PascalCase); `"PAUSED"`; `LengthM:-1`; `WidthM:100.00` | [API]: 400 (Status chỉ nhận `ACTIVE`/`DISCONTINUED`; số âm / vượt độ chính xác bị từ chối `[?]`); không 500.<br>[DB]: không đổi. | Boundary |
| IT_B2_175 | TK_ADM | import-data với `SpecWorkbookDto` toàn mảng rỗng, mọi cờ `Has* = false` | [API]: 200 với số đếm = 0, hoặc 400 `[?]`.<br>[DB]: không đổi (kể cả khi `replace=true`, không được xóa dữ liệu khi file rỗng `[?]`). | Boundary |
| IT_B2_176 | TK_ADM | Gửi **đồng thời** 2 request import-data cùng file vào PR1 | [API]: cả hai 200 (idempotent), hoặc 1 request 409; không 500.<br>[DB]: không có `ProductComponent` trùng tên trong PR1; số dòng bằng với khi import 1 lần. | Boundary |
| IT_B2_177 | TK_ADM | `POST A/products/<PR1>/import` với `img_over.jpg` / `corrupt.xlsx` | [API]: 400/413; không 500.<br>[DB]: không đổi. | Abnormal |

## 9. Vehicle — `A/vehicles` (chỉ đọc), `WK/vehicles/available`, `AC/vehicles` (CRUD)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_178 | TK_ACC | `POST AC/vehicles {PlateNumber:"51C-222.22", VehicleType:"Tải 2.5 tấn", FuelNormPer100Km:14.50}` | [API]: 201.<br>[DB]: `Vehicle('51C-222.22', N'Tải 2.5 tấn', 14.50, Status='ACTIVE')`; AuditLogs +1 `[?]`. | Normal |
| IT_B2_179 | VE1 tồn tại | POST `PlateNumber:"51C-123.45"`; `"51c-123.45"` | [API]: 409 (ca chữ thường ghi lại theo collation `[?]`).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_180 | TK_ACC | PlateNumber dài 20 / 21 ký tự; VehicleType dài 50 / 51 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B2_181 | TK_ACC | `PlateNumber:"51Đ-123.45"` (có ký tự Unicode trong cột `varchar`) | [API]: 400 (không được để lưu thành `51?-123.45`); không 500.<br>[DB]: không tồn tại biển số bị mất dấu; nếu có → ghi finding. | Boundary |
| IT_B2_182 | TK_ACC | `FuelNormPer100Km`: `9999.99`; `10000.00`; `0`; `-0.01`; `12.345` | [API]: 9999.99 → 201; 10000 → 400 (không 500 do tràn `decimal(6,2)`); 0 / −0.01 → 400 `[?]`; 12.345 → 400 hoặc làm tròn `[?]`.<br>[DB]: chỉ ca hợp lệ được lưu, đúng giá trị. | Boundary |
| IT_B2_183 | TK_ACC | Thiếu `PlateNumber`; thiếu `VehicleType`; `FuelNormPer100Km:null`; `FuelNormPer100Km:"abc"` | [API]: 400.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_184 | S0 | Gửi **đồng thời** 2 POST cùng `PlateNumber:"51C-333.33"` | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B2_185 | VE2 tồn tại | `PUT AC/vehicles/<VE2> {PlateNumber:"51C-678.90", VehicleType:"Tải 3.5 tấn", FuelNormPer100Km:16}`; PUT VE2 với `PlateNumber:"51C-123.45"` | [API]: lần đầu 200; lần sau 409.<br>[DB]: VE2 chỉ mang giá trị của lần đầu. | Normal |
| IT_B2_186 | TK_ACC | `PUT AC/vehicles/999999 {…}`; `PUT AC/vehicles/abc {…}` | [API]: 404.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B2_187 | VE2 không có chuyến đang chạy | `POST AC/vehicles/<VE2>/discontinue` | [API]: 200.<br>[DB]: `Status` = giá trị ngừng (HOA) `[?]`; `VehicleTrip` không đổi. | Normal |
| IT_B2_188 | VE1 có chuyến `STARTED` | discontinue VE1 | [API]: 409/400 (xe đang chạy), hoặc 200 `[?]`; ghi lại; không 500.<br>[DB]: chuyến `STARTED` của VE1 không đổi. | Abnormal |
| IT_B2_189 | VE3 đã ngừng | discontinue VE3; reactivate VE3; reactivate VE2 (đang ACTIVE) | [API]: 400/409; 200; 400/409.<br>[DB]: VE3 chuyển `Status='ACTIVE'`; các xe khác không đổi. | Normal |
| IT_B2_190 | TK_ADM | `GET A/vehicles?Running=true`; `?Running=false`; không truyền `Running` | [API]: true → VE1, VE4 (`IsRunning=true`, có `CurrentTripId`, `DriverName`, `DispatchCode`); false → VE2, VE3; null → tất cả.<br>[DB]: `IsRunning` khớp việc có `VehicleTrip.Status='STARTED'`. | Normal |
| IT_B2_191 | VE4 có 2 chuyến `STARTED` | `GET A/vehicles?Running=true` | [API]: 200, **không 500**; VE4 có `OpenTripCount=2` (đánh dấu bất thường); `CurrentTripId` lấy theo quy tắc xác định `[?]`; VE4 không bị nhân đôi trong danh sách.<br>[DB]: không đổi. | Boundary |
| IT_B2_192 | TK_ADM | `?Running=abc` | [API]: 400; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B2_193 | TK_WK | `GET WK/vehicles/available` | [API]: 200; **chỉ có VE2** (ACTIVE và không có chuyến STARTED); không có VE1, VE3, VE4.<br>[DB]: không đổi. | Normal |
| IT_B2_194 | TK_ACC | `GET AC/vehicles?Status=Active` | [API]: 200; mỗi xe có `TripCount`, `LastOdometer` khớp `VehicleTrip`.<br>[DB]: `TripCount` khớp `COUNT(VehicleTrip WHERE VehicleId=…)`. | Normal |

## 10. Kiểm tra chéo: trạng thái, nhật ký, dữ liệu đang được tham chiếu

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B2_195 | Đã chạy các ca discontinue / reactivate ở trên | `SELECT DISTINCT Status` trên `Customer`, `Supplier`, `Material`, `Product`, `Vehicle` | [API]: —<br>[DB]: mọi giá trị đều VIẾT HOA (`ACTIVE`, giá trị ngừng `[?]`); không có `Active`/`Discontinued` dạng PascalCase. | Normal |
| IT_B2_196 | CU2 vừa bị ngừng (IT_B2_040) | `GET A/customers?Status=Discontinued`; `GET A/customers/options` | [API]: response trả `Status` nhất quán ở mọi endpoint (cùng một dạng UPPER hoặc Pascal) `[?]`.<br>[DB]: `Status` HOA; dữ liệu chuyển đổi DB ↔ DTO nhất quán 2 chiều. | Normal |
| IT_B2_197 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chạy các thao tác thất bại: IT_B2_021, IT_B2_076, IT_B2_108, IT_B2_144, IT_B2_165 | [API]: 4xx đúng như từng ca.<br>[DB]: `MAX(AuditLogs.Id)` **không đổi** (nhật ký không được ghi khi thao tác thất bại). | Abnormal |
| IT_B2_198 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chuỗi trên SU2: PUT → discontinue (có Reason) → reactivate; rồi `GET A/suppliers/<SU2>` | [API]: `History[]` có 3 mục mới, theo đúng thứ tự thời gian; mục discontinue chứa Reason.<br>[DB]: AuditLogs +3 `[?]`. | Normal |
| IT_B2_199 | CU1 có đơn, SU1 có PO, MA1 có tồn kho/định mức, PR1 có đơn, VE1 có chuyến | Discontinue lần lượt CU1, SU1, MA1 (PATCH status), PR1, VE1 | [API]: mỗi request trả 200 hoặc 409 theo quy tắc nghiệp vụ `[?]`; **không request nào 500**.<br>[DB]: các bảng con (`CustomerOrder`, `PurchaseOrder`, `StockBalance`, `ProductComponentMaterial`, `OrderMaterial`, `VehicleTrip`) **giữ nguyên**: không cascade xóa, không SET NULL FK. | Abnormal |
| IT_B2_200 | Không có trigger nào trên các bảng danh mục (theo Config) | Chạy lại các ca IT_B2_032, IT_B2_091, IT_B2_118, IT_B2_154, IT_B2_184 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; mọi lỗi đồng thời đều được map thành 409.<br>[DB]: không có bản ghi trùng; không có dòng con mồ côi (`ProductImage`, `ProductComponentMaterial`). | Boundary |
