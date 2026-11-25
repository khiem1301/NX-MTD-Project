# Integration Test Cases — Batch 4: Đơn hàng khách, Phân tổ & NVL đơn (`IT_B4_xxx`)

Nguồn: `IT_CONTRACT — Batch 4`. Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**.

Các ca có nhãn **[Đ1]–[Đ7]** là ca đánh thẳng vào 7 điểm đặc thù được yêu cầu:

| Nhãn | Điểm đặc thù |
|---|---|
| Đ1 | `UQ_OrderLine (OrderId, ProductVariantId)` |
| Đ2 | Giới hạn mềm `WidthM` (≤ 3,5 m, trong khi DB là `decimal(4,2)`) |
| Đ3 | `Status` lệch giữa DB (HOA) và DTO (PascalCase) |
| Đ4 | Trigger `trg_ProductMarker_Check`, `trg_ProductMarkerRatio_Check` và view `vw_OrderLineProgress` |
| Đ5 | Ma trận quyền chéo Admin / Manager / WarehouseKeeper |
| Đ6 | Upload `IFormFile` và rollback khi import |
| Đ7 | Phân trang và concurrency |

## 0. Quy ước chung

**HTTP kỳ vọng**
- `401` thiếu/hết hạn/sai chữ ký token. `403` sai role ở prefix. `404` không có tài nguyên, không có route, hoặc `{id:int}` không khớp. `405` có route nhưng sai method. `400` dữ liệu sai (có thể là `422` nếu `ToResponse` map như vậy, ghi lại). `409` trùng UNIQUE, sai `RowVer` hoặc xung đột trạng thái.
- **Bất kỳ `500` nào cũng là FAIL.** Body lỗi không được lộ tên constraint/trigger, `SqlException` hay stack trace.

**Quy ước cho vế [DB]**
- Giá trị trạng thái lưu VIẾT HOA:
  - `CustomerOrder.Status`: `NEW`, và các giá trị đoán theo quy ước: `IN_PRODUCTION`, `CANCELLED`, `STOPPED` `[?]`.
  - `CustomerOrder.MaterialStatus`: `AWAITING`, các giá trị khác `[?]`.
  - `ProductMarker.Status`: `ACTIVE` / `DISCONTINUED`.
  - `OrderMaterial.Source`: `SUPPLIER` / `CUSTOMER` / `BOTH`.
  - Assert thêm rằng không có giá trị PascalCase (`New`, `Active`) bị lưu nhầm.
- Cột computed (`IsDelayRisk`, `PurchaseQty`, `Source`) chỉ được **SELECT để assert**, không được INSERT/UPDATE.
- Màu/size của `OrderLine` assert qua join `ProductVariant` ↔ `ProductColor`, vì `OrderLine` không có cột Color/SizeCode.
- "DB không đổi" = snapshot các bảng liên quan trước và sau khi gọi trùng nhau (gồm `RowVer` của `CustomerOrder` và `AuditLogs`).
- Các cột ngày (`ReceivedDate`, `DueDate`, `DeliveryDate`, `CreatedAt`, `AssignedAt`) không bao giờ được là `0001-01-01`. `CreatedAt`/`AssignedAt` phải ≈ giờ VN (`fn_VnNow()`).

**Viết tắt URL:** `A/` = `/api/v1/admin/`, `M/` = `/api/v1/manager/`, `WK/` = `/api/v1/warehouse-keeper/`, `C/` = `/api/v1/common/`.

**Dữ liệu nền S0** (reset trước mỗi test)

- **Token:** TK_ADM, TK_MGR, TK_ACC, TK_WK, TK_TL (tổ trưởng T_SEW02), TK_TD, TK_QC, TK_ELEC, TK_WRK, TK_EXP (hết hạn), TK_FORGED (sai chữ ký).
- **Customer:** CU1, CU2 (`ACTIVE`); CU3 (đã ngừng `[?]`).
- **Team:** T_SEW01, T_SEW02 (Sewing, `ACTIVE`); T_CUT01 (Cutting); T_OLD (Sewing, đã ngừng).
- **Material:** MA1 `V-0001` FABRIC; MA2 `V-0002` FABRIC; MA3 `PL-0001` TRIM; MA4 (đã ngừng).

**Product**
- PR1 `SP001`: màu Đen, Trắng. Biến thể PV1 (Đen, M), PV2 (Đen, L), PV3 (Trắng, M).
- PR2 `SP002`: biến thể PV9 (Xanh, M).
- PR3: đã ngừng.

**CustomerOrder**

| Alias | Mã đơn | Khách | Status | MaterialStatus | OrderLine | Ghi chú |
|---|---|---|---|---|---|---|
| OD1 | DH-0001 | CU1 | `NEW` | `AWAITING` | OL1 (PV1, 100 cái, đơn giá 25000.00); OL2 (PV2, 50 cái) | Chưa phân tổ, chưa có bìa, chưa có NVL; RowVer = R1 |
| OD2 | DH-0002 | CU1 | `IN_PRODUCTION` `[?]` | khác `AWAITING` `[?]` | OL3 (PV3, 200 cái) | Chi tiết bên dưới |
| OD3 | DH-0003 | CU2 | `CANCELLED` `[?]` | — | — | |
| OD4 | DH-0004 | CU1 | `NEW` | — | OL9 (PV9, PR2) | Đơn của PR2, dùng cho các ca chéo dữ liệu |
| OD5 | DH-0005 | CU1 | `STOPPED` `[?]` | — | — | `StopReason` có giá trị |

Chi tiết OD2:
- **Phân tổ:** `OrderLineTeam` (OL3, T_SEW01).
- **Bìa của đơn:** `ProductMarker` MK1 (OD2, PR1, MarkerNo 1, "S1", LengthM 5.20, WidthM 1.50, `ACTIVE`). Ratio: M:2.
- **NVL của đơn:** OM1 (MA1, Required 150, Customer 0) đã có `MaterialReceiptLine`; OM2 (MA3, Required 100, Customer 100).
- **Giao hàng:** phiếu xuất `DELIVERED` 80 cái cho OL3, cộng 1 phiếu xuất **chưa** `DELIVERED` 30 cái.

**File mẫu:**
- Đơn hàng: `order_ok.xlsx`, `order_err.xlsx` (dòng 3 lỗi).
- NVL đơn: `om_ok.xlsx`, `om_err.xlsx` (3 dòng, dòng 2 lỗi).
- Bìa: `marker_ok.xlsx`, `marker_w4.xlsx` (một bìa có WidthM 4.00).
- File hỏng / sai định dạng: `zero.xlsx` (0 byte), `corrupt.xlsx`, `not_excel.pdf`.

---

## 1. Ma trận phân quyền chéo [Đ5]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_001 | S0 | Không gửi token: `GET A/CustomerOrders`, `GET M/OrderLineTeams`, `GET WK/OrderMaterials?orderId=<OD2>`, `GET C/OrderLookups/orders` | [API]: 401 với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B4_002 | TK_EXP, TK_FORGED | `POST A/CustomerOrders/<OD1>/start-production {RowVer:R1}` | [API]: 401.<br>[DB]: OD1 vẫn `Status='NEW'`, RowVer = R1. | Abnormal |
| IT_B4_003 **[Đ5]** | TK_ADM | `GET WK/OrderMaterials?orderId=<OD2>` | [API]: **403 Forbidden** (prefix warehouse-keeper chỉ cho `WarehouseKeeper`; Admin không có quyền bao trùm).<br>[DB]: không đổi. | Abnormal |
| IT_B4_004 **[Đ5]** | TK_ADM | `POST WK/OrderMaterials {…}`; `DELETE WK/OrderMaterials/<OM2>` | [API]: 403 (hoặc 404/405 nếu không có route; ghi lại, không được 200).<br>[DB]: OM2 vẫn còn. | Abnormal |
| IT_B4_005 **[Đ5]** | TK_MGR | `POST M/OrderMaterials/import/preview-data?orderId=<OD1>` (endpoint chỉ có ở Admin) | [API]: **404/405** (route không tồn tại ở prefix manager, không phải 403).<br>[DB]: không đổi. | Abnormal |
| IT_B4_006 **[Đ5]** | TK_MGR | `POST A/OrderMaterials/import/preview-data?orderId=<OD1>`; `POST A/OrderMaterials/import-data?orderId=<OD1>`; `POST A/OrderMaterials/import?orderId=<OD1>` | [API]: **403** với cả 3 (prefix admin chỉ cho `Admin`).<br>[DB]: `OrderMaterial` của OD1 không đổi. | Abnormal |
| IT_B4_007 | TK_MGR | `POST M/OrderMaterials/import-data?orderId=<OD1>` với 1 dòng hợp lệ | [API]: 200 (Manager có `import-data` ở prefix manager).<br>[DB]: có 1 `OrderMaterial` mới cho OD1. | Normal |
| IT_B4_008 | TK_MGR | `GET A/CustomerOrders`; `POST A/CustomerOrders {…}`; `POST A/CustomerOrders/<OD1>/cancel {…}` | [API]: 403 với cả 3.<br>[DB]: không có đơn mới; OD1 không đổi. | Abnormal |
| IT_B4_009 | TK_ADM | `GET M/CustomerOrders`; `PUT M/OrderLineTeams {…}`; `PUT M/OrderMarkers/<OD1>/products/<PR1> {…}` | [API]: 200 (Admin được gọi `/manager/*`).<br>[DB]: thay đổi đúng như các ca Normal tương ứng. | Normal |
| IT_B4_010 | TK_WK | `GET WK/OrderMaterials?orderId=<OD2>` | [API]: 200; trả OM1, OM2.<br>[DB]: không đổi. | Normal |
| IT_B4_011 | TK_WK | `POST WK/OrderMaterials {…}`; `GET WK/OrderMaterials/paged`; `GET M/OrderMaterials?orderId=<OD2>`; `GET A/OrderMaterials?orderId=<OD2>` | [API]: 2 ca đầu → 404/405 (WK chỉ có O2); 2 ca sau → 403.<br>[DB]: không đổi. | Abnormal |
| IT_B4_012 | TK_ACC, TK_TL, TK_QC, TK_ELEC (lần lượt) | `GET M/CustomerOrders`; `GET M/OrderLineTeams`; `GET M/OrderMarkers/<OD2>`; `GET WK/OrderMaterials?orderId=<OD2>` | [API]: 403 với mọi tổ hợp.<br>[DB]: không đổi. | Abnormal |
| IT_B4_013 | TK_ACC, TK_WK, TK_TL, TK_TD, TK_QC, TK_ELEC (lần lượt) | `GET C/OrderLookups/orders`; `GET C/OrderLookups/products` | [API]: 200 với 6 role (thuộc `StaffExceptWorker`).<br>[DB]: không đổi. | Normal |
| IT_B4_014 | TK_WRK | `GET C/OrderLookups/orders`; `/orders/<OD2>/lines`; `/products`; `/product-colors` | [API]: **403** với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B4_015 | TK_ADM | `DELETE A/CustomerOrders/<OD1>` | [API]: 404/405 (không có API xóa đơn).<br>[DB]: OD1, OL1, OL2 vẫn còn. | Abnormal |
| IT_B4_016 | TK_ADM | `GET /api/v1/ADMIN/customerorders` (khác hoa/thường) | [API]: 200 (route không phân biệt hoa/thường).<br>[DB]: không đổi. | Boundary |

## 2. CustomerOrders — tạo và sửa đơn, `UQ_OrderLine` [Đ1]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_017 | Mã lớn nhất hiện có là DH-0005 | `GET A/CustomerOrders/next-code` | [API]: 200; trả mã kế tiếp (ví dụ `DH-0006`).<br>[DB]: không đổi (chỉ gợi ý, không "giữ chỗ" mã). | Normal |
| IT_B4_018 **[Đ1]** | TK_ADM | `POST A/CustomerOrders {OrderCode:"DH-0100", CustomerId:<CU1>, ReceivedDate:today, DueDate:today+30, DeliveryAddress:"KCN Tân Tạo", Lines:[{ProductId:<PR1>, Color:"Đen", SizeCode:"M", OrderedQty:100, CmtUnitPrice:25000}, {ProductId:<PR1>, Color:"Trắng", SizeCode:"M", OrderedQty:80, CmtUnitPrice:25000}]}` | [API]: 201; body `OrderSaveResultDto{OrderId, Products:[]}`.<br>[DB]:<br>- 1 dòng `CustomerOrder`: `Status='NEW'`, `MaterialStatus='AWAITING'`, `CreatedAt` ≈ hiện tại, `IsDelayRisk=0`.<br>- 2 dòng `OrderLine` trỏ PV1, PV3 (hai biến thể khác nhau trong cùng đơn đều được chấp nhận).<br>- AuditLogs +1 `[?]`. | Normal |
| IT_B4_019 **[Đ1]** | TK_ADM | POST đơn `DH-0101` với 2 dòng **cùng** (PR1, Đen, M), số lượng 100 và 50 | [API]: **409 Conflict** (hoặc 400 với message "trùng biến thể"); **không 500** do `UQ_OrderLine`.<br>[DB]: **Rollback**: không có `CustomerOrder` DH-0101, không có `OrderLine` mồ côi. | Abnormal |
| IT_B4_020 **[Đ1]** | TK_ADM | POST với 2 dòng (PR1, "Đen", "M") và (PR1, "đen", "m") — khác hoa/thường | [API]: 409/400 nếu cả hai map về cùng PV1, hoặc tạo biến thể mới `[?]`; không 500.<br>[DB]: không có hai `OrderLine` cùng (OrderId, ProductVariantId). | Boundary |
| IT_B4_021 **[Đ1]** | OD1 có OL1 (PV1) | `PUT A/CustomerOrders/<OD1>` với RowVer = R1, giữ OL1 và thêm dòng mới (PR1, Đen, M) không có `OrderLineId` | [API]: **409** (hoặc 400); không 500.<br>[DB]: OD1 vẫn có 2 dòng; RowVer vẫn là R1. | Abnormal |
| IT_B4_022 **[Đ1]** | OD1 có OL1 (PV1), OL2 (PV2) | PUT OD1 đổi OL2 sang (Đen, M) — trùng biến thể của OL1 | [API]: 409/400.<br>[DB]: OL2 vẫn trỏ PV2. | Abnormal |
| IT_B4_023 **[Đ1]** | PV1 đã có trong OD1 | POST đơn mới `DH-0102` có dòng (PR1, Đen, M) | [API]: 201 (UNIQUE chỉ áp dụng trong cùng một đơn).<br>[DB]: có 2 `OrderLine` trỏ PV1 ở hai đơn khác nhau. | Normal |
| IT_B4_024 | PR1 chưa có biến thể (Đen, XL) | POST đơn có dòng (PR1, Đen, XL) | [API]: 201, hoặc 400 nếu biến thể bắt buộc phải có sẵn `[?]`.<br>[DB]: nếu 201 thì có `ProductVariant` (Đen, XL) mới với `Status='ACTIVE'`, cùng giao dịch với đơn; `UQ_PV` không bị vi phạm. | Normal |
| IT_B4_025 | TK_ADM | POST `DH-0103` với `StagedProducts:[{TempId:-1, ProductCode:"SP500"}]` và `Lines:[{ProductId:-1, Color:"Đỏ", SizeCode:"S", OrderedQty:10, CmtUnitPrice:30000}]` | [API]: 201; `Products=[{TempId:-1, ProductId:<mới>, ProductCode:"SP500"}]`.<br>[DB]: có `Product` SP500, `ProductColor` "Đỏ", `ProductVariant` (Đỏ, S), `OrderLine` trỏ đúng ProductVariantId + ProductId mới, tất cả trong cùng 1 giao dịch. | Normal |
| IT_B4_026 | TK_ADM | Staged với `TempId:-1` nhưng dòng đơn dùng `ProductId:-2` (không có TempId tương ứng) | [API]: 400.<br>[DB]: **Rollback**: không có Product SP500, không có đơn. | Abnormal |
| IT_B4_027 | SP001 đã tồn tại | Staged `{TempId:-1, ProductCode:"SP001"}` | [API]: 409/400.<br>[DB]: **Rollback**: không có đơn mới, không có Product trùng. | Abnormal |
| IT_B4_028 | TK_ADM | Staged SP501 hợp lệ + một dòng đơn có `OrderedQty:0` | [API]: 400.<br>[DB]: **Rollback toàn bộ**: không có Product SP501 (staged product không được lưu nửa vời). | Abnormal |
| IT_B4_029 | DH-0001 tồn tại | POST `OrderCode:"DH-0001"`; `"dh-0001"` | [API]: 409 (ca chữ thường ghi lại theo collation `[?]`).<br>[DB]: không có đơn mới. | Abnormal |
| IT_B4_030 **[Đ7]** | S0 | Gửi **đồng thời** 2 POST cùng `OrderCode:"DH-0200"` (mỗi request 2 dòng) | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: 1 đơn với đúng 2 `OrderLine`; không có dòng mồ côi từ request thua. | Boundary |
| IT_B4_031 **[Đ7]** | S0 | Hai client cùng lấy next-code (`DH-0006`), rồi gửi **đồng thời** 2 POST với mã đó | [API]: 1 request 201, 1 request 409 (thông báo mã đã dùng); không 500.<br>[DB]: `COUNT(DH-0006) = 1`. | Boundary |
| IT_B4_032 | TK_ADM | OrderCode dài 30 / 31 ký tự; DeliveryAddress dài 500 / 501 ký tự có dấu; Note dài 1000 / 1001 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400.<br>[DB]: chỉ ca hợp lệ được lưu, đúng dấu. | Boundary |
| IT_B4_033 | TK_ADM | Thiếu `DeliveryAddress` (NOT NULL, không có default DB); `DeliveryAddress:""`; thiếu `OrderCode`; `Lines:[]` | [API]: 400 với từng ca.<br>[DB]: không có đơn mới. | Abnormal |
| IT_B4_034 | TK_ADM | `CustomerId:999999`; `CustomerId:<CU3>` (đã ngừng) | [API]: 999999 → 400/404 (không 500 do `FK_CustomerOrder_Customer`); CU3 → 400 `[?]`.<br>[DB]: không có đơn mới. | Abnormal |
| IT_B4_035 | TK_ADM | `ReceivedDate:"0001-01-01"`; không gửi `DueDate` (bind ra `default(DateOnly)`); `DueDate` < `ReceivedDate` | [API]: **400** với cả 3; không 500.<br>[DB]: không có đơn nào có ngày `0001-01-01`. | Boundary |
| IT_B4_036 | TK_ADM | `DueDate` = `ReceivedDate`; `DueDate` = hôm qua | [API]: bằng ngày nhận → 201 `[?]`; quá khứ → 400 hoặc 201 kèm `IsDelayRisk` `[?]`; ghi lại.<br>[DB]: nhất quán với API. | Boundary |
| IT_B4_037 | TK_ADM | `OrderedQty`: `0`; `-1`; `1`; `2147483647`; `2147483648` | [API]: 0 / −1 → 400 (DB không có CHECK, nên API phải chặn); 1 / max → 201; 2147483648 → 400 (tràn `int` khi bind); không 500.<br>[DB]: không có `OrderLine.OrderedQty ≤ 0`. | Boundary |
| IT_B4_038 | TK_ADM | `CmtUnitPrice`: `-1`; `0`; `0.01`; `9999999999999999.99`; `10000000000000000`; `100.555` | [API]: âm → 400; 0 → 400 hoặc 201 `[?]`; 0.01 / giá trị tối đa → 201; vượt tối đa → 400 (tràn `decimal(18,2)`); 3 chữ số thập phân → 400 hoặc làm tròn `[?]`; không 500.<br>[DB]: chỉ giá trị hợp lệ được lưu. | Boundary |
| IT_B4_039 | PV9 thuộc PR2 | POST dòng `{ProductId:<PR1>, Color:"Xanh", SizeCode:"M"}` (màu chỉ có ở PR2) | [API]: 400, hoặc 201 kèm tạo màu mới cho PR1 `[?]`; **không 500** do `FK_OrderLine_Variant` (FK ghép ProductVariantId + ProductId).<br>[DB]: không có `OrderLine` nào trỏ PV9 nhưng `ProductId=PR1`. | Abnormal |
| IT_B4_040 | TK_ADM | Dòng có `ProductId:999999`; `ProductId:<PR3>` (đã ngừng) | [API]: 404/400; PR3 → 400 `[?]`.<br>[DB]: không có đơn mới. | Abnormal |
| IT_B4_041 | TK_ADM | POST kèm `{Status:"IN_PRODUCTION", MaterialStatus:"RECEIVED", FinalAmount:1, IsDelayRisk:true, CreatedAt:"0001-01-01"}` (mass assignment) | [API]: 201; các field thừa bị bỏ qua.<br>[DB]: `Status='NEW'`, `MaterialStatus='AWAITING'`, `FinalAmount IS NULL`, `DelayRiskSince IS NULL`, `CreatedAt` ≈ hiện tại. | Abnormal |
| IT_B4_042 | OD1 như S0, RowVer = R1 | `PUT A/CustomerOrders/<OD1>` với RowVer = R1: sửa `DueDate`, OL1 đổi `OrderedQty` 120, giữ OL2, thêm (Trắng, M) 30 cái | [API]: 200.<br>[DB]: RowVer đổi (≠ R1); `UpdatedAt` khác NULL; OL1 có qty 120; có thêm 1 `OrderLine` PV3; AuditLogs +1 `[?]`. | Normal |
| IT_B4_043 **[Đ7]** | RowVer của OD1 đã bị đổi bởi một lần sửa khác | PUT OD1 với RowVer = R1 (cũ) | [API]: **409**.<br>[DB]: OD1 giữ đúng dữ liệu của lần sửa trước; không có dòng đơn mới. | Abnormal |
| IT_B4_044 | TK_ADM | PUT thiếu `RowVer`; `RowVer:"không-phải-base64"`; `RowVer:""` | [API]: 400 (hoặc 409); không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B4_045 **[Đ7]** | 2 client cùng đọc RowVer = R1 của OD1 | Gửi **đồng thời** 2 PUT OD1 với R1 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: chỉ dữ liệu của request thắng được lưu. | Boundary |
| IT_B4_046 | OD1 `NEW`; giả lập OL2 đã có `OrderLineTeam` (OL2, T_SEW02) | PUT OD1 bỏ OL2 khỏi danh sách | [API]: 200.<br>[DB]: OL2 bị DELETE; `OrderLineTeam` của OL2 bị xóa theo (**ON DELETE CASCADE**); OL1 không đổi. | Normal |
| IT_B4_047 | OD2 đang sản xuất | PUT OD2 bỏ OL3 | [API]: 400/409 (chỉ xóa được dòng khi đơn còn `NEW`).<br>[DB]: OL3 và `OrderLineTeam` của OL3 còn nguyên. | Abnormal |
| IT_B4_048 | OD1 thuộc PR1; OL9 thuộc OD4 | PUT OD1 với `OrderLineId:<OL9>` (dòng của đơn khác) | [API]: 400/404 (IDOR chéo đơn); không lộ dữ liệu của OD4.<br>[DB]: OL9 vẫn thuộc OD4, không đổi. | Abnormal |
| IT_B4_049 | OD3 đã hủy; OD5 đã dừng | PUT OD3; PUT OD5 (RowVer đúng) | [API]: 400/409 `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B4_050 | TK_ADM | `PUT A/CustomerOrders/999999 {…}`; `GET A/CustomerOrders/abc` | [API]: 404.<br>[DB]: không có đơn mới. | Abnormal |

## 3. CustomerOrders — chuyển trạng thái [Đ3]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_051 **[Đ3]** | OD1 `NEW` nhưng `MaterialStatus='AWAITING'` | `POST A/CustomerOrders/<OD1>/start-production {RowVer:R1}` | [API]: 400/409 (chỉ bắt đầu sản xuất được khi `MaterialStatus` khác `AWAITING`).<br>[DB]: `Status='NEW'`; RowVer vẫn R1. | Abnormal |
| IT_B4_052 **[Đ3]** | OD1 đã có NVL và `MaterialStatus` khác `AWAITING` (seed) | start-production với RowVer hiện tại | [API]: 200; `Status` trả về dạng PascalCase hay HOA `[?]`, ghi lại.<br>[DB]: `Status` = giá trị đang sản xuất viết HOA (`IN_PRODUCTION` `[?]`), không phải PascalCase; RowVer đổi; AuditLogs +1 `[?]`. | Normal |
| IT_B4_053 | OD2 đang sản xuất | start-production OD2 lần nữa | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B4_054 **[Đ3]** | OD1 `NEW` | `POST A/CustomerOrders/<OD1>/cancel {Reason:"Khách hủy", RowVer:R1}` | [API]: 200.<br>[DB]: `Status` = giá trị hủy viết HOA (`CANCELLED` `[?]`); lý do được lưu vào `StopReason` `[?]`; RowVer đổi. | Normal |
| IT_B4_055 | OD1 `NEW` | cancel với `{Reason:null, RowVer:R1}` | [API]: 200 (Reason của Cancel là nullable).<br>[DB]: Status chuyển sang hủy; `StopReason IS NULL`. | Normal |
| IT_B4_056 | OD2 đang sản xuất; OD3 đã hủy | cancel OD2; cancel OD3 | [API]: OD2 → 400/409 hoặc 200 `[?]` (ghi lại quy tắc); OD3 → 400/409.<br>[DB]: OD3 không đổi. | Abnormal |
| IT_B4_057 **[Đ3]** | OD2 đang sản xuất | `POST A/CustomerOrders/<OD2>/stop {Reason:"Thiếu vải", RowVer}` | [API]: 200.<br>[DB]: `Status` = giá trị dừng viết HOA (`STOPPED` `[?]`); `StopReason=N'Thiếu vải'`; `OrderLineTeam` và `ProductMarker` giữ nguyên. | Normal |
| IT_B4_058 | OD2 đang sản xuất | stop với `{Reason:""}`; `{Reason:null}`; thiếu `Reason` | [API]: 400 (Reason của Stop là non-null).<br>[DB]: OD2 không đổi. | Abnormal |
| IT_B4_059 | OD2 đang sản xuất | stop với Reason dài 500 / 501 ký tự có dấu; cancel OD1 với Reason dài 501 ký tự | [API]: 500 → 200; 501 → 400 (không 500 do truncation `nvarchar(500)`).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B4_060 | OD1 `NEW` | stop OD1 (đơn chưa sản xuất) | [API]: 400/409 `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B4_061 **[Đ7]** | OD1 sẵn sàng sản xuất, RowVer = Rx | Gửi **đồng thời** `start-production` và `cancel` với cùng Rx | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: OD1 ở **một** trạng thái cuối duy nhất. | Boundary |
| IT_B4_062 | TK_ADM | cancel / stop / start-production với RowVer cũ; với `id=999999` | [API]: 409; 404.<br>[DB]: không đổi. | Abnormal |
| IT_B4_063 **[Đ3]** | Có đơn ở đủ các trạng thái | `GET A/CustomerOrders?Status=New`; `?Status=NEW`; `?Status=Xyz` | [API]: `New` → OD1, OD4; `NEW` → cùng kết quả hoặc 400 (ghi lại để biết filter có phân biệt hoa/thường không); `Xyz` → 400 hoặc rỗng; không 500.<br>[DB]: kết quả khớp `WHERE Status='NEW'`. | Boundary |

## 4. Danh sách, chi tiết, GBR-02, phân trang [Đ7]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_064 **[Đ7]** | TK_ADM | `GET A/CustomerOrders?PageSize=0`; `?PageSize=1`; `?PageSize=100`; `?PageSize=101` | [API]: 0 → **400** (lỗi validator); 1 → 200, 1 phần tử; 100 → 200, ≤ 100 phần tử; 101 → **400**. Hành vi phải **nhất quán** giữa các endpoint có phân trang (không được nơi báo lỗi, nơi tự kẹp). Nếu thấy tự kẹp → ghi finding vì lệch với validator.<br>[DB]: không đổi. | Boundary |
| IT_B4_065 **[Đ7]** | TK_MGR | Lặp lại bộ PageSize 0 / 1 / 100 / 101 trên `M/OrderLineTeams`, `M/OrderMaterials/paged?orderId=<OD2>`, `C/OrderLookups/orders` | [API]: cùng quy tắc như IT_B4_064 ở cả 3 endpoint.<br>[DB]: không đổi. | Boundary |
| IT_B4_066 | TK_ADM | `Page=0`; `Page=-1`; `Page=999` | [API]: 0 / −1 → 400; 999 → 200 với danh sách rỗng.<br>[DB]: không đổi. | Boundary |
| IT_B4_067 | TK_ADM | `Keyword` dài 100 / 101 ký tự; `Keyword:"' OR 1=1--"`; `SortDir=ASC` / `xyz`; `SortBy="OrderCode;DROP TABLE CustomerOrder"` | [API]: 100 → 200; 101 → 400; injection → rỗng, không 500; `ASC` → tăng dần; `xyz` → giảm dần; SortBy lạ → 400 hoặc bị bỏ qua.<br>[DB]: bảng còn nguyên. | Boundary |
| IT_B4_068 | TK_ADM | `?DueFrom=2026-12-31&DueTo=2026-01-01` (đảo ngược); `?CustomerId=999999`; `?DueFrom=0001-01-01` | [API]: khoảng đảo ngược → 400 hoặc rỗng `[?]`; khách không tồn tại → rỗng; `0001-01-01` → 200, xử lý như không có cận dưới; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B4_069 | TK_ADM | `GET A/CustomerOrders/<OD2>` | [API]: 200; có `Lines[].CmtUnitPrice`, `History[]`, `RowVer` (base64), `HasOrderMarkers=true`, `HasOrderMaterials=true`, `StopReason`; `Lines[].Color` / `SizeCode` lấy qua join.<br>[DB]: khớp `OrderLine` join `ProductVariant` join `ProductColor`. | Normal |
| IT_B4_070 | TK_MGR | `GET M/CustomerOrders/<OD2>`; `GET M/CustomerOrders` | [API]: 200; **không** có các trường `CmtUnitPrice`, `TotalAmount`, `History`, `RowVer` (GBR-02): kiểm tra key không tồn tại trong JSON, không chỉ là null.<br>[DB]: không đổi. | Normal |
| IT_B4_071 | TK_ACC / TK_TL | `GET C/OrderLookups/orders`; `/orders/<OD2>/lines`; `/products`; `/product-colors?productId=<PR1>` | [API]: 200; **không** có `CmtUnitPrice` ở bất kỳ phần tử nào (D26/GBR-02); `orders` không truyền `Status` → chỉ đơn đang sản xuất (OD2).<br>[DB]: không đổi. | Normal |
| IT_B4_072 | TK_TL (tổ trưởng **T_SEW02**) | `GET C/OrderLookups/orders/<OD2>/lines?sewingTeamId=<T_SEW01>` (tổ khác) | [API]: 403, hoặc danh sách rỗng/chỉ dữ liệu của tổ mình `[?]`; không lộ dòng đơn của T_SEW01 cho tổ trưởng tổ khác (IDOR theo tổ).<br>[DB]: không đổi. Nếu API trả đủ dữ liệu của T_SEW01 → ghi **finding** IDOR. | Abnormal |
| IT_B4_073 | TK_ACC | `GET C/OrderLookups/orders/999999/lines`; `/product-colors?productId=999999`; `/orders/abc/lines` | [API]: 404 hoặc 200 rỗng; 200 rỗng; 404 (route).<br>[DB]: không đổi. | Abnormal |
| IT_B4_074 | TK_ADM | `GET A/CustomerOrders/<OD2>` sau khi seed `DelayRiskSince = hôm qua` trực tiếp trong DB | [API]: `IsDelayRisk=true` (DTO kiểu `bool`, entity kiểu `bool?` computed, không 500 khi map).<br>[DB]: cột computed trả `1`; UPDATE trực tiếp vào `IsDelayRisk` bị từ chối (chứng minh là cột chỉ đọc). | Boundary |

## 5. View `vw_OrderLineProgress` — tiến độ đơn [Đ4]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_075 **[Đ4]** | OL3 có phiếu xuất `DELIVERED` 80 cái và phiếu chưa `DELIVERED` 30 cái | `GET A/CustomerOrders/<OD2>`; `GET M/CustomerOrders/<OD2>` | [API]: `DeliveredQty` của OL3 = **80** ở cả hai (không tính phiếu chưa `DELIVERED`).<br>[DB]: khớp `SELECT DeliveredQty FROM vw_OrderLineProgress WHERE OrderLineId=OL3` và khớp tổng phiếu xuất `DELIVERED` của OL3. | Normal |
| IT_B4_076 **[Đ4]** | Như trên | INSERT thêm 1 phiếu xuất `DELIVERED` 20 cái cho OL3, rồi GET lại | [API]: `DeliveredQty=100`.<br>[DB]: view phản ánh ngay (không cache sai). | Normal |
| IT_B4_077 **[Đ4]** | OL1 của OD1 chưa có phiếu xuất | `GET A/CustomerOrders/<OD1>` | [API]: `DeliveredQty=0` (không phải null, không lỗi khi view không có dòng).<br>[DB]: view không có dòng hoặc trả 0 cho OL1. | Boundary |
| IT_B4_078 **[Đ4]** | Seed phiếu xuất `DELIVERED` 250 cái cho OL3 (vượt `OrderedQty` 200) | GET chi tiết OD2 | [API]: 200; `DeliveredQty=250`, các trường tiến độ hoặc % hiển thị đúng hoặc được giới hạn `[?]`; không 500 (không chia cho 0, không tràn số).<br>[DB]: view trả 250. | Boundary |
| IT_B4_079 **[Đ4]** | OD2 phân OL3 cho T_SEW01 | `GET M/OrderLineTeams` và `GET C/OrderLookups/orders/<OD2>/lines?sewingTeamId=<T_SEW01>` (gọi bằng tổ trưởng T_SEW01) | [API]: số tổ, tên tổ và số lượng đã xuất hiển thị khớp nhau giữa các endpoint.<br>[DB]: khớp `OrderLineTeam` và `vw_OrderLineProgress`. | Normal |

## 6. OrderLineTeams — phân tổ [Đ7 concurrency]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_080 | OL1, OL2 chưa phân tổ; OL3 đã phân | `GET M/OrderLineTeams?OnlyUnallotted=true`; `?OnlyUnallotted=false` | [API]: true → chỉ OL1, OL2, OL9 (các dòng của đơn đã hủy/dừng có được tính hay không `[?]`); false → có cả OL3.<br>[DB]: khớp các dòng `OrderLine` không có `OrderLineTeam`. | Normal |
| IT_B4_081 | TK_MGR | `GET M/OrderLineTeams/teams?orderId=<OD2>&productId=<PR1>`; thiếu `orderId`; `productId=<PR2>` (không có trong OD2) | [API]: 200 với T_SEW01; thiếu tham số → 400; productId sai → 200 rỗng hoặc 400.<br>[DB]: không đổi. | Normal |
| IT_B4_082 | OL1 chưa phân tổ | `PUT M/OrderLineTeams {OrderLineId:<OL1>, TeamIds:[<T_SEW01>, <T_SEW02>]}` | [API]: 200.<br>[DB]: có 2 dòng `OrderLineTeam` (OL1, T_SEW01), (OL1, T_SEW02); `AssignedAt` ≈ hiện tại (giờ VN). | Normal |
| IT_B4_083 | OL3 đang được phân cho T_SEW01 | PUT `{OrderLineId:<OL3>, TeamIds:[<T_SEW02>]}` | [API]: 200.<br>[DB]: (OL3, T_SEW01) bị DELETE; có (OL3, T_SEW02) mới (thay thế toàn bộ danh sách). | Normal |
| IT_B4_084 | OL3 có T_SEW01 | PUT `{OrderLineId:<OL3>, TeamIds:[]}` | [API]: 200 (danh sách rỗng = bỏ chia tổ).<br>[DB]: `COUNT(OrderLineTeam WHERE OrderLineId=OL3) = 0`. | Normal |
| IT_B4_085 | TK_MGR | PUT OL1 với `TeamIds:[<T_CUT01>]` | [API]: 400 (tổ cắt không được chia theo dòng) `[?]`; không 500.<br>[DB]: không có dòng nào với `SewingTeamId=T_CUT01`. | Abnormal |
| IT_B4_086 | TK_MGR | PUT OL1 với `TeamIds:[<T_OLD>]`; `TeamIds:[999999]` | [API]: T_OLD → 400; 999999 → 400/404 (không 500 do `FK_OLT_Team`).<br>[DB]: không đổi. | Abnormal |
| IT_B4_087 | TK_MGR | PUT OL1 với `TeamIds:[<T_SEW01>, <T_SEW01>]` | [API]: 200 (khử trùng) hoặc 400; **không 500** do `UQ_OrderLineTeam`.<br>[DB]: tối đa 1 dòng (OL1, T_SEW01). | Boundary |
| IT_B4_088 | OL1 có T_SEW01 | PUT OL1 với `TeamIds:[<T_SEW02>, 999999]` | [API]: 400/404.<br>[DB]: **Rollback**: OL1 vẫn đúng {T_SEW01} (tổ cũ không bị xóa trước khi lệnh thêm thất bại). | Abnormal |
| IT_B4_089 | TK_MGR | PUT `{OrderLineId:999999, TeamIds:[…]}`; PUT OL của OD3 (đơn đã hủy) | [API]: 404; OD3 → 400/409 `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B4_090 | OL3 có T_SEW01; OL1 chưa có tổ | `POST M/OrderLineTeams/bulk {OrderLineIds:[<OL1>, <OL3>], TeamIds:[<T_SEW01>, <T_SEW02>]}` | [API]: 200.<br>[DB]: OL1 có {T_SEW01, T_SEW02}; OL3 có {T_SEW01 (giữ nguyên, không INSERT lại), T_SEW02}; không vi phạm UQ. | Normal |
| IT_B4_091 | TK_MGR | bulk `{OrderLineIds:[<OL1>, 999999], TeamIds:[<T_SEW01>]}` | [API]: 400/404.<br>[DB]: **Rollback toàn bộ lô**: OL1 không được gán tổ nào. | Abnormal |
| IT_B4_092 | TK_MGR | bulk `{OrderLineIds:[<OL1>], TeamIds:[<T_SEW01>, <T_CUT01>]}` | [API]: 400.<br>[DB]: **Rollback**: OL1 không có tổ nào (kể cả T_SEW01). | Abnormal |
| IT_B4_093 | TK_MGR | bulk với `OrderLineIds:[]`; `TeamIds:[]`; `OrderLineIds:null` | [API]: 400.<br>[DB]: không đổi. | Boundary |
| IT_B4_094 **[Đ7]** | OL1 chưa có tổ | Gửi **đồng thời** 2 request bulk cùng `{OL1, T_SEW01}` | [API]: cả hai 200 (idempotent) hoặc 1 request 409; **không 500** do `UQ_OrderLineTeam`.<br>[DB]: đúng 1 dòng (OL1, T_SEW01). | Boundary |
| IT_B4_095 **[Đ7]** | OL1 chưa có tổ | Gửi **đồng thời** `PUT {OL1, [T_SEW01]}` và `PUT {OL1, [T_SEW02]}` | [API]: cả hai 200 theo thứ tự tuần tự hóa, hoặc 1 request 409; không 500 (không deadlock).<br>[DB]: OL1 có đúng **một** trong hai tập tổ; không có tập trộn {T_SEW01, T_SEW02}. | Boundary |
| IT_B4_096 **[Đ7]** | OL1 có T_SEW01 | Gửi **đồng thời** `PUT {OL1, []}` và `POST bulk {OL1, T_SEW02}` | [API]: không 500.<br>[DB]: trạng thái cuối nhất quán với một thứ tự tuần tự hóa nào đó; không có dòng `OrderLineTeam` trỏ OrderLine không tồn tại. | Boundary |

## 7. OrderMarkers — bìa của đơn, `WidthM`, trigger [Đ2][Đ3][Đ4][Đ6]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_097 | TK_MGR | `GET M/OrderMarkers/product-spec/<PR1>`; `GET M/OrderMarkers/<OD2>` | [API]: 200; OD2 có MK1 với Ratios (M:2); `Status` trả về dạng PascalCase hay HOA `[?]`, ghi lại.<br>[DB]: không đổi. | Normal |
| IT_B4_098 **[Đ3]** | OD1 chưa có bìa | `PUT M/OrderMarkers/<OD1>/products/<PR1> {Markers:[{ProductMarkerId:null, MarkerNo:1, MarkerCode:"S1", LengthM:6.00, WidthM:1.50, Status:"Active", Ratios:[{SizeCode:"M", PiecesPerPly:2}, {SizeCode:"L", PiecesPerPly:1}]}]}` | [API]: 200.<br>[DB]: `ProductMarker(OD1, PR1, 1, 'S1', 6.00, 1.50, Status='ACTIVE')` (lưu dạng **HOA**, không phải `Active`); có 2 dòng `ProductMarkerRatio`; cả hai trigger chạy không lỗi. | Normal |
| IT_B4_099 **[Đ3]** | TK_MGR | `Status:"ACTIVE"`; `"active"`; `"Discontinued"`; `"DISCONTINUED"` | [API]: 200 với các giá trị hợp lệ (map không phân biệt hoa/thường `[?]`).<br>[DB]: chỉ lưu `ACTIVE` hoặc `DISCONTINUED` viết HOA. | Boundary |
| IT_B4_100 **[Đ3]** | TK_MGR | `Status:"Paused"`; `Status:""`; `Status:null` | [API]: Paused → **400**; rỗng / null → 400, hoặc mặc định `ACTIVE` `[?]`; không 500.<br>[DB]: không tồn tại `ProductMarker.Status` nào ngoài {`ACTIVE`, `DISCONTINUED`}. | Abnormal |
| IT_B4_101 **[Đ2]** | TK_MGR | `WidthM: 3.50` | [API]: **200** (đúng biên hợp lệ).<br>[DB]: `WidthM=3.50`. | Boundary |
| IT_B4_102 **[Đ2]** | TK_MGR | `WidthM: 3.51` | [API]: **400 Bad Request** (tầng ứng dụng chặn, DEV-ALL-102); không 500 kể cả khi trigger là bên chặn.<br>[DB]: **không** có dòng nào với `WidthM=3.51`. | Boundary |
| IT_B4_103 **[Đ2]** | TK_MGR | `WidthM: 99.99` (giá trị tối đa của kiểu `decimal(4,2)`) | [API]: **400** (DB nhận được giá trị này, nhưng tầng ứng dụng bắt buộc phải chặn trước).<br>[DB]: không có dòng nào với `WidthM > 3.50`. Nếu dòng này lọt xuống DB → **finding** nghiêm trọng. | Boundary |
| IT_B4_104 **[Đ2]** | TK_MGR | `WidthM: 100.00` (vượt kiểu dữ liệu) | [API]: 400; **không 500** do tràn `decimal(4,2)`.<br>[DB]: không đổi. | Boundary |
| IT_B4_105 **[Đ2]** | TK_MGR | `WidthM: 0`; `WidthM: -0.01`; `WidthM: 0.01` | [API]: 0 / âm → **400**; 0.01 → 200.<br>[DB]: không có dòng nào với `WidthM ≤ 0`. | Boundary |
| IT_B4_106 **[Đ2]** | TK_MGR | `WidthM: null` (DTO cho phép null, nhưng DB NOT NULL và comment nói "bắt buộc") | [API]: **400**; không được tới DB gây lỗi NOT NULL (500).<br>[DB]: không đổi. | Abnormal |
| IT_B4_107 **[Đ2]** | TK_MGR | `WidthM: 3.505`; `WidthM: 1.555` | [API]: 3.505 → 400 (hoặc làm tròn lên 3.51 thì vẫn phải 400); 1.555 → 400 hoặc làm tròn `[?]`.<br>[DB]: không có `WidthM > 3.50` sau khi làm tròn. | Boundary |
| IT_B4_108 | TK_MGR | `LengthM`: `99999.99`; `100000.00`; `0`; `-1` | [API]: 99999.99 → 200 (hoặc 400 theo nghiệp vụ); 100000 → 400 (tràn `decimal(7,2)`); 0 / −1 → 400; không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B4_109 | TK_MGR | `MarkerNo`: `0`; `255`; `256`; `-1` (DTO là int, DB là tinyint) | [API]: 256 / −1 → **400**, không 500 do tràn `tinyint`; 255 → 200 hoặc 400 theo nghiệp vụ; 0 → 400 `[?]`.<br>[DB]: không có `MarkerNo` ngoài [0..255]. | Boundary |
| IT_B4_110 | TK_MGR | MarkerCode dài 20 / 21 ký tự; `MarkerCode:""` | [API]: 20 → 200; 21 / rỗng → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B4_111 | TK_MGR | 2 bìa cùng `(MarkerNo:1, MarkerCode:"S1")` trong cùng request | [API]: 400/409; **không 500** do `UQ_PM`.<br>[DB]: không đổi. | Abnormal |
| IT_B4_112 **[Đ4]** | OD1 chỉ có size M, L | Ratios có `SizeCode:"XXL"` (không có trong đơn) | [API]: **400/409** (`trg_ProductMarkerRatio_Check` chặn, hoặc validator chặn trước); **không 500**; body không lộ tên trigger.<br>[DB]: **Rollback**: không có `ProductMarker` mới và không có ratio mồ côi (assert trực tiếp DB). | Abnormal |
| IT_B4_113 **[Đ4]** | TK_MGR | Ratios có `M` xuất hiện 2 lần; `PiecesPerPly:0`; `PiecesPerPly:-1`; `PiecesPerPly:32768`; `Ratios:[]` | [API]: 400 với từng ca (do trigger hoặc validator); ca trùng `M` không được 500 do PK ghép; 32768 → 400 (tràn `short`).<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B4_114 **[Đ4]** | TK_MGR | Một bìa hợp lệ + một bìa vi phạm `trg_ProductMarker_Check` (ví dụ WidthM 4.00 nếu validator bị bypass, hoặc điều kiện khác của trigger) trong cùng request | [API]: **400/409**; không 500.<br>[DB]: **Rollback toàn bộ**: bìa hợp lệ cũng **không** được lưu. | Abnormal |
| IT_B4_115 | OD2 có MK1 | PUT OD2/PR1 với danh sách chỉ có một bìa mới (MK1 không có trong danh sách) | [API]: 200, hoặc 409 nếu MK1 đã được dùng trong lệnh cắt `[?]`.<br>[DB]: nếu 200 thì MK1 và ratio của nó bị DELETE (hoặc chuyển `DISCONTINUED` nếu đang dùng `[?]`); bìa mới được INSERT. | Normal |
| IT_B4_116 | OD2 có MK1 | PUT với `ProductMarkerId:<MK1>`, đổi `LengthM:5.50` và ratio M:3 | [API]: 200.<br>[DB]: MK1 có `LengthM=5.50`; ratio M được cập nhật thành 3 (không tạo dòng trùng). | Normal |
| IT_B4_117 | MK1 thuộc OD2 | PUT `M/OrderMarkers/<OD1>/products/<PR1>` với `ProductMarkerId:<MK1>` (bìa của đơn khác) | [API]: 400/404 (IDOR chéo đơn).<br>[DB]: MK1 vẫn thuộc OD2, không đổi. | Abnormal |
| IT_B4_118 | OD4 thuộc PR2 | `PUT M/OrderMarkers/<OD4>/products/<PR1> {…}`; `PUT M/OrderMarkers/999999/products/<PR1>` | [API]: 400/404 (mã hàng không có trong đơn) / 404; không 500 do FK.<br>[DB]: không có `ProductMarker` (OD4, PR1). | Abnormal |
| IT_B4_119 **[Đ7]** | OD1 chưa có bìa | Gửi **đồng thời** 2 PUT OD1/PR1 với cùng bìa mới "S1" | [API]: cả hai 200 hoặc 1 request 409; không 500 (`UQ_PM`, trigger).<br>[DB]: đúng 1 dòng "S1"; ratio không bị nhân đôi. | Boundary |
| IT_B4_120 | TK_MGR | `POST M/OrderMarkers/excel/parse` với `marker_ok.xlsx`; `POST M/OrderMarkers/excel/check {OrderId:<OD1>, ProductId:<PR1>, Bundle:<kết quả parse>}` | [API]: 200 với cả hai.<br>[DB]: **không đổi**. | Normal |
| IT_B4_121 **[Đ6]** | TK_MGR | parse / import/preview / import **thiếu `file`** | [API]: **400** (model validation, `IFormFile` non-nullable); không 500 / NullReference.<br>[DB]: không đổi. | Abnormal |
| IT_B4_122 **[Đ6]** | TK_MGR | Gửi `zero.xlsx` (0 byte); `corrupt.xlsx`; `not_excel.pdf` tới parse và import | [API]: 400 với tất cả; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B4_123 **[Đ6]** | TK_MGR | `POST M/OrderMarkers/<OD1>/products/<PR1>/import/preview` với `marker_ok.xlsx` | [API]: 200; mỗi dòng có `Action` ∈ {New, Update, Unchanged}; `CanImport=true`.<br>[DB]: **không đổi**. | Normal |
| IT_B4_124 **[Đ6][Đ4]** | TK_MGR | `POST …/<OD1>/products/<PR1>/import` với `marker_w4.xlsx` (dòng 1 hợp lệ, dòng 2 WidthM 4.00, dòng 3 hợp lệ) | [API]: **400**; báo lỗi theo dòng 2; không 500.<br>[DB]: **Rollback toàn bộ** (1 giao dịch gồm định mức + bìa): không có `ProductMarker` nào từ dòng 1 hay dòng 3; `ProductComponent`/Norm không đổi. | Abnormal |
| IT_B4_125 | TK_MGR | import `marker_ok.xlsx` với `replace=false`, rồi với `replace=true` (file không còn MK cũ "S9" đã seed) | [API]: 200 với cả hai.<br>[DB]: lần 1 → INSERT bìa mới, "S9" giữ nguyên; lần 2 → "S9" bị DELETE (hoặc `DISCONTINUED` nếu đang dùng `[?]`). | Normal |
| IT_B4_126 | TK_MGR | `POST …/import-data` **không có** `parts`; `parts=components,norms` (không có `markers`) | [API]: **400** (`SpecImportInvalid`, bị chặn trước khi gọi Service).<br>[DB]: không đổi. | Abnormal |
| IT_B4_127 | TK_MGR | import-data `parts=markers,components` với body có cả Components (thêm "Cổ") và OrderMarkers | [API]: 200.<br>[DB]: chỉ `ProductMarker`/`Ratio` thay đổi; `ProductComponent` **không** có "Cổ" (phần ngoài `markers` bị bỏ qua). | Boundary |
| IT_B4_128 | TK_MGR | import-data `parts=MARKERS` (chữ HOA) | [API]: 200 (controller tự chuyển về chữ thường).<br>[DB]: bìa được ghi. | Boundary |

## 8. OrderMaterials — NVL của đơn, lịch giao, import [Đ6]

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_129 | OD1 chưa có NVL | `POST A/OrderMaterials {OrderId:<OD1>, MaterialId:<MA1>, RequiredQty:150.5, CustomerQty:50, Note:"Vải chính"}` | [API]: 201.<br>[DB]: `OrderMaterial` mới; cột computed `PurchaseQty=100.500`, `Source='BOTH'`; `CustomerOrder.MaterialStatus` cập nhật hay giữ `AWAITING` `[?]`, ghi lại. | Normal |
| IT_B4_130 | TK_MGR | POST với `CustomerQty:0`; với `CustomerQty` = `RequiredQty`; với `CustomerQty` > `RequiredQty` | [API]: 201 với cả ba.<br>[DB]: `Source` lần lượt là `SUPPLIER` / `CUSTOMER` / `CUSTOMER`; `PurchaseQty` lần lượt là Required / 0 / 0 (viết HOA, chỉ SELECT để kiểm). | Normal |
| IT_B4_131 | OM1 (OD2, MA1) tồn tại | POST `{OrderId:<OD2>, MaterialId:<MA1>, …}` | [API]: **409**; không 500 do `UQ_OM`.<br>[DB]: vẫn 1 dòng (OD2, MA1). | Abnormal |
| IT_B4_132 **[Đ7]** | OD1 chưa có MA2 | Gửi **đồng thời** 2 POST (OD1, MA2) | [API]: 1 request 201, 1 request 409; không 500.<br>[DB]: `COUNT = 1`. | Boundary |
| IT_B4_133 | TK_ADM | `RequiredQty`: `0`; `-1`; `0.001`; `999999999999999.999`; `1000000000000000.000`; `1.0005` | [API]: 0 / âm → 400; 0.001 / giá trị tối đa → 201; vượt tối đa → 400 (tràn `decimal(18,3)`); 4 chữ số thập phân → 400 hoặc làm tròn `[?]`; không 500.<br>[DB]: chỉ giá trị hợp lệ được lưu. | Boundary |
| IT_B4_134 | TK_ADM | `CustomerQty:-1`; Note dài 500 / 501 ký tự | [API]: âm → 400; 500 → 201; 501 → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B4_135 | TK_ADM | `MaterialId:999999`; `MaterialId:<MA4>` (đã ngừng); `OrderId:999999`; `OrderId:<OD3>` (đã hủy) | [API]: 404/400 (không 500 do FK); MA4 / OD3 → 400 `[?]`.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B4_136 | OD1 chưa có NVL | `POST A/OrderMaterials/batch {OrderId:<OD1>, Lines:[MA1, MA2, MA3 đều hợp lệ]}` | [API]: 201.<br>[DB]: 3 dòng `OrderMaterial`; Source/PurchaseQty đúng theo từng dòng. | Normal |
| IT_B4_137 | OD1 chưa có NVL | batch với 3 dòng, dòng giữa có `RequiredQty:-5` | [API]: 400.<br>[DB]: **Rollback (all-or-nothing)**: không có dòng nào cho OD1. | Abnormal |
| IT_B4_138 | OD1 chưa có NVL | batch có MA1 xuất hiện 2 lần | [API]: 400/409; không 500 do `UQ_OM`.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B4_139 | OM1 tồn tại trong OD2 | batch OD2 có MA1 (đã có) và MA2 (mới) | [API]: 409/400.<br>[DB]: **Rollback**: MA2 không được thêm. | Abnormal |
| IT_B4_140 | TK_ADM | batch với `Lines:[]`; `Lines:null` | [API]: 400.<br>[DB]: không đổi. | Boundary |
| IT_B4_141 | OM2 tồn tại (MA3, 100/100) | `PUT A/OrderMaterials/<OM2> {MaterialId:<MA3>, RequiredQty:120, CustomerQty:100}` | [API]: 200.<br>[DB]: `RequiredQty=120.000`; `PurchaseQty=20.000`; `Source='BOTH'`. | Normal |
| IT_B4_142 | OM1 (MA1), OM2 (MA3) cùng thuộc OD2 | PUT OM2 đổi `MaterialId:<MA1>` | [API]: 409; không 500.<br>[DB]: OM2 không đổi. | Abnormal |
| IT_B4_143 | OM1 đã có `MaterialReceiptLine` | PUT OM1 đổi `MaterialId:<MA2>`; PUT OM1 giảm `RequiredQty` xuống dưới số đã nhận `[?]` | [API]: 400/409 `[?]`; không 500.<br>[DB]: OM1 không đổi. | Abnormal |
| IT_B4_144 | OM1 đã có `MaterialReceiptLine` (FK) | `DELETE A/OrderMaterials/<OM1>` | [API]: **409**; **không 500** do vi phạm FK.<br>[DB]: OM1 vẫn còn. | Abnormal |
| IT_B4_145 | Dòng NVL OM-x của OD1 chưa có phiếu nhập/đơn mua | DELETE OM-x | [API]: 200/204.<br>[DB]: OM-x bị xóa; `MaterialStatus` của OD1 được tính lại `[?]`. | Normal |
| IT_B4_146 | TK_ADM | DELETE 999999; PUT 999999 | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B4_147 | TK_MGR | `GET M/OrderMaterials/paged?OrderId=<OD2>&Supply=<giá trị hợp lệ [?]>`; `?Supply=Xyz`; `GET M/OrderMaterials/product-materials?orderId=<OD2>` | [API]: 200 có lọc đúng; `Xyz` → 400 hoặc rỗng; `ReceivedQty` = nhận từ khách + nhận từ mua, `ShortQty = max(Required − Received, 0)`.<br>[DB]: khớp với các dòng nhận của OM. | Normal |
| IT_B4_148 | OD1 | `PUT A/OrderMaterials/customer-delivery/<OD1> {DeliveryCount:2, DeliveryDates:["2026-12-10","2026-12-20"]}` | [API]: 200; `GET …/customer-delivery/<OD1>` trả đúng 2 ngày.<br>[DB]: 2 dòng `DeliveryScheduleDate` với `OrderId=OD1`, `PurchaseOrderId` và `RequestId` NULL, `SeqNo` = 1, 2; `CustomerOrder.CustomerDeliveryCount=2`, `CustomerDeliveryDate`/`DateTo` là ngày đầu/cuối `[?]`. | Normal |
| IT_B4_149 | OD1 đã có 2 ngày giao | PUT lại với `{DeliveryCount:1, DeliveryDates:["2026-12-15"]}` | [API]: 200.<br>[DB]: chỉ còn 1 dòng với `SeqNo=1` (dòng cũ bị xóa); không vi phạm `UQ_DSD_Order`. | Normal |
| IT_B4_150 | TK_ADM | `DeliveryCount:3` nhưng chỉ gửi 2 ngày; `DeliveryCount:0` với `[]`; `DeliveryCount:-1` | [API]: lệch số lượng → 400; 0 với `[]` → 200 (xóa lịch) hoặc 400 `[?]`; −1 → 400.<br>[DB]: ca lỗi thì không đổi; không có dòng nào với `SeqNo < 1` (`CK_DSD_Seq`). | Boundary |
| IT_B4_151 | TK_ADM | DeliveryDates chứa `"0001-01-01"`; chứa 2 ngày trùng nhau; ngày trước `ReceivedDate` | [API]: 400 với `0001-01-01`; trùng ngày → 400 `[?]`; trước ngày nhận → 400 `[?]`; không 500.<br>[DB]: không có `DeliveryDate='0001-01-01'`. | Boundary |
| IT_B4_152 | TK_ADM | `DeliveryCount:32767` kèm 32767 ngày; `DeliveryCount:32768` | [API]: 32768 → 400 (tràn `short`); 32767 → 400 theo giới hạn nghiệp vụ, hoặc 200 không timeout `[?]`; không 500.<br>[DB]: nhất quán với API. | Boundary |
| IT_B4_153 **[Đ7]** | OD1 | Gửi **đồng thời** 2 PUT customer-delivery với 2 bộ ngày khác nhau | [API]: cả hai 200 hoặc 1 request 409; **không 500** do `UQ_DSD_Order (OrderId, SeqNo)`.<br>[DB]: OD1 có đúng **một** bộ ngày; `SeqNo` liên tục 1..n, không trùng. | Boundary |
| IT_B4_154 | TK_ADM | `PUT …/customer-delivery/999999 {…}` | [API]: 404; không 500 do FK hay `CK_DSD_Owner`.<br>[DB]: không có `DeliveryScheduleDate` mồ côi. | Abnormal |
| IT_B4_155 **[Đ6]** | TK_ADM | `POST A/OrderMaterials/import/preview?orderId=<OD1>` với `om_ok.xlsx` | [API]: 200; mỗi dòng có `Action` ∈ {New, Update, Unchanged}.<br>[DB]: **không đổi**. | Normal |
| IT_B4_156 **[Đ6]** | TK_ADM | import/preview và import **thiếu `file`**; gửi `zero.xlsx`; `corrupt.xlsx`; `not_excel.pdf` | [API]: **400** với tất cả; không 500 / NullReference.<br>[DB]: không đổi. | Abnormal |
| IT_B4_157 **[Đ6]** | OD1 chưa có NVL | `POST A/OrderMaterials/import?orderId=<OD1>` với `om_err.xlsx` (dòng 1 và 3 hợp lệ, dòng 2 có `RequiredQty` âm) | [API]: **400**; báo lỗi ở dòng 2.<br>[DB]: **Rollback toàn bộ**: không có `OrderMaterial` nào từ dòng 1 hay dòng 3; không có `Material` mới. | Abnormal |
| IT_B4_158 | OD1 chưa có NVL | import `om_ok.xlsx` (3 dòng hợp lệ) | [API]: 200.<br>[DB]: 3 dòng `OrderMaterial` đúng số lượng; Source/PurchaseQty đúng. | Normal |
| IT_B4_159 | TK_ADM | `POST A/OrderMaterials/import/preview-data?orderId=<OD1>` với dòng `{MaterialCode:"V-9999", MaterialName:"Vải mới", Unit:"m", RequiredQty:10}` | [API]: 200; dòng được báo là NVL chưa có (gợi ý thêm vào danh mục, DEV-ALL-123).<br>[DB]: **không** có `Material` V-9999 (preview không ghi). | Normal |
| IT_B4_160 | TK_ADM | `POST A/OrderMaterials/import-data?orderId=<OD1>` với cùng dòng V-9999 | [API]: 200 kèm tự thêm NVL, hoặc 400 yêu cầu tạo NVL trước `[?]`.<br>[DB]: nếu 200 thì có `Material` V-9999 và `OrderMaterial` trỏ tới nó trong **cùng giao dịch**. | Normal |
| IT_B4_161 | Dòng V-9999 hợp lệ + một dòng lỗi | import-data | [API]: 400.<br>[DB]: **Rollback**: không có `Material` V-9999, không có `OrderMaterial` (danh mục NVL không được tạo nửa vời). | Abnormal |
| IT_B4_162 | OD2 có OM1 (đã nhập kho, bị "Blocked") và OM2; file chỉ có MA2 | import-data `orderId=<OD2>&replace=true` | [API]: 200; preview trước đó báo OM1 là "Blocked".<br>[DB]: OM2 bị xóa (không có trong file); **OM1 vẫn còn** (không xóa được do đã có phiếu nhập); có thêm MA2. | Normal |
| IT_B4_163 | TK_ADM | import-data với hai dòng cùng `MaterialCode:"V-0001"`; `RequiredQty:null`; `MaterialCode:""` | [API]: 400 (các dòng có `Action="Error"`).<br>[DB]: Rollback. | Abnormal |
| IT_B4_164 | TK_ADM | import-data với body `[]`; với body `null` | [API]: `[]` → 200 với 0 thay đổi (kể cả khi `replace=true`, không được xóa toàn bộ NVL `[?]`) hoặc 400; null → 400.<br>[DB]: không mất `OrderMaterial` ngoài ý muốn. | Boundary |
| IT_B4_165 **[Đ7]** | OD1 chưa có NVL | Gửi **đồng thời** 2 request import `om_ok.xlsx` vào OD1 | [API]: cả hai 200 (request sau thành `Unchanged`) hoặc 1 request 409; không 500.<br>[DB]: không có dòng trùng (OrderId, MaterialId). | Boundary |
| IT_B4_166 | TK_ADM | import / import-data **không truyền** `orderId`; `orderId=999999` | [API]: 400; 404.<br>[DB]: không đổi. | Abnormal |

## 9. Import đơn hàng (A1, A2) và kiểm tra chéo

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B4_167 **[Đ6]** | TK_ADM | `POST A/CustomerOrders/import/preview` với `order_ok.xlsx`; `POST …/import/preview-data` với `OrderImportExcelDto` tương ứng | [API]: 200 với cả hai; trả dữ liệu đơn và dòng đơn đã parse.<br>[DB]: **không đổi** (comment ghi "Không ghi gì"): không có CustomerOrder, OrderLine, Product, ProductVariant mới. | Normal |
| IT_B4_168 **[Đ6]** | TK_ADM | import/preview **thiếu file**; `zero.xlsx`; `not_excel.pdf`; `order_err.xlsx` | [API]: 400 / 400 / 400 / 200 kèm dòng lỗi (preview không fail cả file `[?]`); không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B4_169 **[Đ3]** | Đã chạy các ca ở mục 2–8 | `SELECT DISTINCT` các cột `CustomerOrder.Status`, `MaterialStatus`, `ProductMarker.Status`, `OrderMaterial.Source` | [API]: —<br>[DB]: mọi giá trị đều VIẾT HOA (`NEW`, `AWAITING`, `ACTIVE`, `DISCONTINUED`, `SUPPLIER`, `CUSTOMER`, `BOTH`, cùng các giá trị `[?]` khác); không có PascalCase. | Normal |
| IT_B4_170 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chạy các ca thất bại IT_B4_019, IT_B4_043, IT_B4_088, IT_B4_112, IT_B4_137, IT_B4_157 | [API]: 4xx đúng như từng ca.<br>[DB]: `MAX(AuditLogs.Id)` không đổi; không có dữ liệu nửa vời ở bất kỳ bảng nào. | Abnormal |
| IT_B4_171 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chuỗi trên OD1: PUT → start-production → stop; rồi `GET A/CustomerOrders/<OD1>` | [API]: `History[]` có 3 mục mới theo đúng thứ tự thời gian; mục stop có Reason.<br>[DB]: AuditLogs +3 `[?]`; RowVer đổi sau mỗi bước. | Normal |
| IT_B4_172 **[Đ4]** | Hai trigger đang bật | Chạy lại IT_B4_102, IT_B4_112, IT_B4_114, IT_B4_119 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; body không chứa tên trigger hay `SqlException`.<br>[DB]: không có `WidthM > 3.50`; không có ratio mồ côi; không có bìa trùng `UQ_PM`. | Boundary |
