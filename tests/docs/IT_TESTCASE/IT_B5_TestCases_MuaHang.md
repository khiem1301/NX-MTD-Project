# Integration Test Cases — Batch 5: Mua hàng (`IT_B5_xxx`)

Nguồn: `IT_CONTRACT — Batch 5: Mua hàng`. Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**.

> **Phạm vi và trùng lặp với batch khác**
> - **Supplier:** CRUD (E17–E22) đã được kiểm đầy đủ ở `IT_B2_053`–`IT_B2_069`. Ở đây chỉ giữ các ca đại diện và các ca có liên hệ với đơn mua.
> - **Upload file:** batch này **không có endpoint nhận `IFormFile`**; ảnh chứng từ thanh toán được upload qua Attachment ở Batch 6. Vì vậy ca "thiếu file / file rỗng" không áp dụng, chỉ có ca `IT_B5_122` xác nhận điều này.
> - **View:** batch này không có view. Ca đối chiếu số liệu suy ra tập trung vào cột computed `PurchaseOrderLine.Amount` và tổng tiền, cùng với tính nhất quán giữa các cột thanh toán phẳng trên `PurchaseOrder` và bảng `PurchasePayment`.

## 0. Quy ước chung

**HTTP kỳ vọng**
- `401` thiếu/hết hạn/sai chữ ký token. `403` sai role ở prefix. `404` không có tài nguyên, không có route, hoặc `{id:int}` không khớp. `405` có route nhưng sai method. `400` dữ liệu sai. `409` trùng UNIQUE, sai `RowVer` hoặc xung đột trạng thái.
- **Bất kỳ `500` nào cũng là FAIL.** Body lỗi không được lộ tên constraint/trigger (`CK_PP_Amount`, `trg_PurchaseOrderLine_Check`…), `SqlException` hay stack trace.

**Quy ước cho vế [DB]**
- Giá trị trạng thái lưu dạng **UPPER_SNAKE**:
  - `Supplier.Status`: `ACTIVE`, giá trị ngừng `[?]`.
  - `GoodsStatus`: `PENDING_FINALISATION`, `ORDERED`, `PARTIALLY_RECEIVED`, và các giá trị đoán theo quy ước: `RECEIVED`, `CANCELLED` `[?]`.
  - `PaymentStatus`: `UNPAID`, và các giá trị đoán theo quy ước: `PAID`, `RETURNED` `[?]`.
  - `PaymentMethod`: lưu dạng PascalCase hay UPPER là `[?]`. Ghi lại dạng thực tế, nhưng phải **nhất quán** giữa `PurchaseOrder.PaymentMethod` và `PurchasePayment.PaymentMethod`.
- Cột computed `PurchaseOrderLine.Amount = CONVERT(decimal(18,2), OrderedQty*UnitPrice)` chỉ được SELECT để assert.
- "DB không đổi" = snapshot các bảng `PurchaseOrder` (gồm `RowVer`), `PurchaseOrderLine`, `PurchasePayment`, `DeliveryScheduleDate`, `Material`, `Supplier`, `AuditLogs` trước và sau khi gọi trùng nhau.
- Các cột ngày (`OrderDate`, `DeliveryDate`, `PaymentDate`, `CreatedAt`) không bao giờ được là `0001-01-01`. `CreatedAt` phải ≈ giờ VN (`fn_VnNow()`).

**Viết tắt URL:** `M/` = `/api/v1/manager/`, `WK/` = `/api/v1/warehouse-keeper/`, `AC/` = `/api/v1/accountant/`, `A/` = `/api/v1/admin/`. Tên controller: `PurchaseOrders`, `PurchaseOrderTracking`, `PurchasePayments`, `Suppliers`.

**Dữ liệu nền S0** (reset trước mỗi test)

- **Token:** TK_ADM, TK_MGR, TK_ACC, TK_WK, TK_TL, TK_QC, TK_WRK, TK_EXP (hết hạn), TK_FORGED (sai chữ ký).
- **Supplier:** SU1 "Vải Thành Công", SU2 "Phụ Liệu Minh" (`ACTIVE`); SU3 "NCC Cũ" (đã ngừng `[?]`).
- **Material:** MA1 `V-0001` ("Kaki", FABRIC, "Đen"); MA2 `PL-0001` ("Nút 4 lỗ", TRIM, `''`); MA4 (đã ngừng `[?]`).
- **CustomerOrder:** OD2 (đang sản xuất), có `OrderMaterial` OM1 (MA1, cần 150) và OM3 (MA2, cần 500); OD3 (đã hủy `[?]`).

**PurchaseOrder và PurchaseOrderLine**

| Alias | Mã | Đơn khách | NCC | GoodsStatus | PaymentStatus | Dòng | Ghi chú |
|---|---|---|---|---|---|---|---|
| PO1 | PO-0001 | OD2 | SU1 | `PENDING_FINALISATION` | `UNPAID` | POL1 (OM1/MA1, SL 100, giá 50000); POL2 (OM3/MA2, SL 500, giá 2000) | RowVer R1; 2 ngày giao trong `DeliveryScheduleDate` (SeqNo 1, 2) |
| PO2 | PO-0002 | — (dự trữ) | SU2 | `ORDERED` | `UNPAID` | POL3 (MA1, `OrderMaterialId` NULL, SL 200, giá 48000) | RowVer R2 |
| PO3 | PO-0003 | — | SU1 | `ORDERED` | đã trả một phần hoặc đã trả `[?]` | — | Có PP1 (tạo cách đây **30 giờ**, 5 000 000) và PP2 (tạo cách đây **1 giờ**, 3 000 000) |
| PO4 | PO-0004 | — | — | đã hủy `[?]` | — | — | `RejectReason` có giá trị |
| PO5 | PO-0005 | — | — | `PARTIALLY_RECEIVED` | `UNPAID` | — | Có `MaterialReceiptLine` |
| PO6 | PO-0006 | — | — | `ORDERED` | — | — | Có PP9 (tạo cách đây 1 giờ), dùng làm mục tiêu cho các ca chéo dữ liệu |

---

## 1. Ma trận phân quyền chéo

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_001 | S0 | Không gửi token: `GET M/PurchaseOrders`, `GET WK/PurchaseOrders`, `GET AC/PurchasePayments`, `GET A/Suppliers` | [API]: 401 với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B5_002 | TK_EXP, TK_FORGED | `POST AC/PurchasePayments/<PO2>/confirm {…}` | [API]: 401.<br>[DB]: không có `PurchasePayment` mới; PO2 vẫn `UNPAID`. | Abnormal |
| IT_B5_003 | TK_ADM | `GET AC/PurchasePayments`; `GET AC/PurchasePayments/<PO2>` | [API]: **403** (prefix accountant chỉ cho `Accountant`, không gồm Admin).<br>[DB]: không đổi. | Abnormal |
| IT_B5_004 | TK_ADM | `POST AC/PurchasePayments/<PO2>/confirm {PaymentDate:today, PaidAmount:9600000, PaymentMethod:"BankTransfer", RowVer:R2}`; `POST AC/PurchasePayments/<PO3>/return {…}`; `PUT AC/PurchasePayments/<PO3>/payments/<PP2> {…}` | [API]: **403** với cả 3.<br>[DB]: không có `PurchasePayment` mới; PO2, PO3, PP2 không đổi. | Abnormal |
| IT_B5_005 | TK_ADM | `GET WK/PurchaseOrders`; `GET WK/PurchaseOrders/<PO2>` | [API]: **403** (prefix warehouse-keeper không gồm Admin).<br>[DB]: không đổi. | Abnormal |
| IT_B5_006 | TK_ADM | `GET M/PurchaseOrders`; `POST M/PurchaseOrders {…}`; `POST M/PurchaseOrderTracking/<PO1>/place {…}` | [API]: 200 / 201 / 200 (Admin được gọi `/manager/*`).<br>[DB]: thay đổi đúng như các ca Normal tương ứng. | Normal |
| IT_B5_007 | TK_MGR | `GET AC/PurchasePayments`; `POST AC/PurchasePayments/<PO2>/confirm {…}`; `GET WK/PurchaseOrders`; `GET A/Suppliers` | [API]: 403 với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B5_008 | TK_ACC | `GET M/PurchaseOrders`; `GET M/PurchaseOrderTracking/<PO1>`; `POST M/PurchaseOrderTracking/<PO1>/reprice {…}`; `GET WK/PurchaseOrders` | [API]: 403 với cả 4 (Kế toán không được tự đặt hàng/đổi giá).<br>[DB]: không đổi. | Abnormal |
| IT_B5_009 | TK_WK | `GET WK/PurchaseOrders`; `GET WK/PurchaseOrders/<PO2>` | [API]: 200; dữ liệu giống hệt `GET M/PurchaseOrders/<PO2>` (dùng chung service).<br>[DB]: không đổi. | Normal |
| IT_B5_010 | TK_WK | `POST WK/PurchaseOrders {…}`; `POST M/PurchaseOrders {…}`; `POST AC/PurchasePayments/<PO2>/confirm {…}` | [API]: ca 1 → 404/405 (WK chỉ có GET); ca 2 và 3 → 403.<br>[DB]: không có PO hay payment mới. | Abnormal |
| IT_B5_011 | TK_TL, TK_QC, TK_WRK (lần lượt) | `GET M/PurchaseOrders`; `GET WK/PurchaseOrders`; `GET AC/PurchasePayments`; `GET M/Suppliers` | [API]: 403 với mọi tổ hợp.<br>[DB]: không đổi. | Abnormal |
| IT_B5_012 | TK_MGR | `PUT M/PurchaseOrders/<PO1> {…}`; `DELETE M/PurchaseOrders/<PO1>`; `DELETE AC/PurchasePayments/<PO3>/payments/<PP2>` (bằng TK_ACC) | [API]: 404/405 (không có API sửa PO gốc hay xóa payment; entity ghi "không xóa").<br>[DB]: PO1 và PP2 còn nguyên. | Abnormal |
| IT_B5_013 | TK_ADM | `GET /api/v1/MANAGER/purchaseorders`; `GET /api/v1/manager/purchase-orders` | [API]: dạng chữ hoa → 200; dạng kebab → 200 hoặc 404 tùy `SlugifyParameterTransformer` `[?]`; ghi lại.<br>[DB]: không đổi. | Boundary |

## 2. Supplier — ca đại diện và liên hệ với đơn mua (bổ sung cho Batch 2)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_014 | TK_MGR | `GET M/Suppliers` | [API]: 200; `SupplierOptionDto[]` chỉ có SU1, SU2 (không có SU3); chỉ gồm các trường `{SupplierId, SupplierName}`.<br>[DB]: khớp `Supplier WHERE Status='ACTIVE'`. | Normal |
| IT_B5_015 | TK_ADM | `POST A/Suppliers {SupplierName:"Dệt Việt Thắng", Phone:"0906000001", Email:"vt@det.vn"}` | [API]: 201.<br>[DB]: `Supplier` mới với `Status='ACTIVE'`, `CreatedAt` ≈ hiện tại; AuditLogs +1 `[?]`. | Normal |
| IT_B5_016 | SU1 tồn tại | POST trùng `SupplierName` của SU1; trùng Phone; trùng Email; gửi **đồng thời** 2 POST cùng tên mới | [API]: 409 với 3 ca trùng; ca đồng thời → 1 request 201, 1 request 409; không 500.<br>[DB]: không có dòng trùng. | Abnormal |
| IT_B5_017 | S0 | POST 2 NCC với `Phone:""`, `Email:""` | [API]: cả hai 201 (chuẩn hóa thành NULL) hoặc lần 2 trả 409; **không 500** do unique filtered.<br>[DB]: không có dòng nào `Phone=''`. | Boundary |
| IT_B5_018 | TK_ADM | Phone dài 15 / 16 ký tự; Phone có ký tự có dấu `"09đ12"` (cột varchar không Unicode); SupplierName dài 200 / 201 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng hoặc Unicode trong cột varchar → 400; không 500.<br>[DB]: không có Phone chứa `?`. | Boundary |
| IT_B5_019 | SU1 có PO1, PO3 | `POST A/Suppliers/<SU1>/discontinue {Reason:"Giá cao"}` | [API]: 200 (hoặc 409 nếu còn PO chưa hoàn tất `[?]`).<br>[DB]: Status chuyển sang giá trị ngừng `[?]`; `DiscontinueReason` được lưu; PO1, PO3 không đổi (`SupplierId` vẫn là SU1, không bị SET NULL). | Normal |
| IT_B5_020 | TK_ADM | discontinue **không gửi body**; body `{}`; `{Reason:null}` (endpoint không có `[FromBody]`) | [API]: 200 hoặc 400/415 tùy cách binding của `[ApiController]`; **không 500**; ghi lại.<br>[DB]: nếu 200 thì `DiscontinueReason IS NULL`. | Boundary |
| IT_B5_021 | TK_ADM | discontinue với Reason dài 500 / 501 ký tự | [API]: 500 → 200; 501 → 400.<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B5_022 | SU3 đã ngừng | `POST A/Suppliers/<SU3>/reactivate`; reactivate SU2 (đang ACTIVE); `GET A/Suppliers/<SU1>` | [API]: SU3 → 200; SU2 → 400/409; chi tiết SU1 có `History[]` và `PurchaseOrderCount` khớp DB.<br>[DB]: SU3 chuyển `Status='ACTIVE'`. | Normal |

## 3. Tạo đơn mua — `POST M/PurchaseOrders` (E03)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_023 | TK_MGR | `POST M/PurchaseOrders {OrderId:null, SupplierId:<SU1>, OrderDate:today, ExpectedDeliveryCount:2, Note:"Dự trữ quý 1", Lines:[{MaterialId:<MA1>, OrderedQty:120.5, UnitPrice:49000}, {MaterialId:<MA2>, OrderedQty:1000, UnitPrice:1500}], ExpectedDeliveryDates:[today+7, today+14]}` | [API]: 201; `PurchaseOrderDto` có `PoCode` tự sinh, 2 dòng, `ExpectedDeliveryCount=2`.<br>[DB]:<br>- 1 dòng `PurchaseOrder`: `OrderId IS NULL`, `SupplierId=SU1`, `GoodsStatus` = `PENDING_FINALISATION` hoặc `ORDERED` (ghi lại: comment nói "tạo đơn = đặt hàng luôn" nhưng default DB là `PENDING_FINALISATION`), `PaymentStatus='UNPAID'`, `CreatedAt` ≈ hiện tại.<br>- 2 dòng `PurchaseOrderLine` với `OrderMaterialId IS NULL`, `NotReplenishedQty` khác NULL; `Amount` lần lượt = 5904500.00 và 1500000.00.<br>- 2 dòng `DeliveryScheduleDate` với `PurchaseOrderId` = PO mới, `OrderId` và `RequestId` NULL, SeqNo 1, 2.<br>- AuditLogs +1 `[?]`. | Normal |
| IT_B5_024 | OD2 có OM1 (MA1), OM3 (MA2) | POST với `OrderId:<OD2>`, Lines MA1 (80) và MA2 (300) | [API]: 201.<br>[DB]: các dòng được gắn `OrderMaterialId` = OM1, OM3 `[?]`; `PurchaseOrder.OrderId=OD2`; `OrderMaterial` được cập nhật liên kết `[?]`. | Normal |
| IT_B5_025 | TK_MGR | Đơn dự trữ (`OrderId:null`) với 2 dòng **cùng** `MaterialId:<MA1>` | [API]: 400/409 (hoặc gộp thành 1 dòng `[?]`); **không 500** do `UQ_POL_Stock (PurchaseOrderId, MaterialId) WHERE OrderMaterialId IS NULL`.<br>[DB]: **Rollback**: không có PO mới, không có dòng mồ côi, không có `DeliveryScheduleDate` mồ côi. | Abnormal |
| IT_B5_026 | OD2 có OM1 (MA1) | Đơn gắn OD2 với 2 dòng cùng MA1 (cùng map vào OM1) | [API]: 400/409; **không 500** do `UQ_POL (PurchaseOrderId, OrderMaterialId)`.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B5_027 | PO2 (dự trữ) đã có dòng MA1 | Tạo PO dự trữ mới cũng có MA1 | [API]: 201 (UNIQUE tính theo từng PO).<br>[DB]: có 2 dòng MA1 thuộc hai PO khác nhau. | Normal |
| IT_B5_028 | Chưa có NVL ("Chun bản 2cm", TRIM, `''`) | Dòng `{MaterialId:null, NewMaterialName:"Chun bản 2cm", NewMaterialUnit:"cuộn", NewMaterialColor:null, OrderedQty:50, UnitPrice:12000}` | [API]: 201; dòng có `MaterialCode` mới (dạng `PL-xxxx`).<br>[DB]: có `Material` mới với `MaterialType='TRIM'` (comment ghi "nhóm OTHER" nhưng không có cột nhóm; ghi finding nếu khác), `Color=''` (không phải NULL), `Status='ACTIVE'`; `PurchaseOrderLine.MaterialId` trỏ tới nó, trong cùng giao dịch. | Normal |
| IT_B5_029 | MA2 = ("Nút 4 lỗ", TRIM, `''`) | Dòng NVL mới `{NewMaterialName:"Nút 4 lỗ", NewMaterialColor:""}` | [API]: 201.<br>[DB]: **dùng lại** MA2 (trùng tên + màu); **không** INSERT `Material` trùng, không 500 do `UQ_Material_NameType`. | Normal |
| IT_B5_030 | TK_MGR | Dòng có **cả** `MaterialId:<MA1>` và `NewMaterialName:"X"`; dòng có **cả hai** đều null | [API]: 400 với cả hai.<br>[DB]: không có PO mới, không có Material mới. | Abnormal |
| IT_B5_031 | TK_MGR | Dòng 1 là NVL mới hợp lệ "Ren trang trí"; dòng 2 có `OrderedQty:-1` | [API]: 400.<br>[DB]: **Rollback toàn bộ**: không có `Material` "Ren trang trí", không có PO, không có dòng lịch giao. | Abnormal |
| IT_B5_032 | TK_MGR | NewMaterialName dài 200 / 201 ký tự; NewMaterialUnit dài 20 / 21 ký tự; NewMaterialColor dài 50 / 51 ký tự | [API]: bằng ngưỡng → 201; vượt ngưỡng → 400; không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B5_033 | TK_MGR | `SupplierId:<SU3>` (đã ngừng); `SupplierId:999999`; `SupplierId:0` | [API]: SU3 → 400 ("bắt buộc NCC đang hợp tác"); 999999 → 400/404 (không 500 do `FK_PO_Supplier`); 0 → 400.<br>[DB]: không có PO mới. | Abnormal |
| IT_B5_034 | TK_MGR | `OrderId:999999`; `OrderId:<OD3>` (đã hủy) | [API]: 404/400 (không 500 do `FK_PO_Order`); OD3 → 400 `[?]`.<br>[DB]: không có PO mới. | Abnormal |
| IT_B5_035 | TK_MGR | Dòng có `MaterialId:999999`; `MaterialId:<MA4>` (đã ngừng) | [API]: 404/400 (không 500 do `FK_POL_Material`); MA4 → 400 `[?]`.<br>[DB]: Rollback. | Abnormal |
| IT_B5_036 | TK_MGR | `OrderedQty`: `0`; `-0.001`; `0.001`; `999999999999999.999`; `1000000000000000`; `1.0005` | [API]: 0 / âm → 400 (DB không có CHECK, nên API phải chặn; nếu `trg_PurchaseOrderLine_Check` chặn thì vẫn phải trả 400, không 500); 0.001 / giá trị tối đa → 201; vượt tối đa → 400 (tràn `decimal(18,3)`); 4 chữ số thập phân → 400 hoặc làm tròn `[?]`.<br>[DB]: không có dòng nào `OrderedQty ≤ 0`. | Boundary |
| IT_B5_037 | TK_MGR | `UnitPrice`: `-1`; `0`; `0.01`; `9999999999999999.99`; `10000000000000000`; `100.555` | [API]: âm → 400; 0 → 400 hoặc 201 `[?]`; 0.01 / giá trị tối đa → 201; vượt tối đa → 400; 3 chữ số thập phân → 400 hoặc làm tròn `[?]`; không 500.<br>[DB]: không có dòng nào `UnitPrice < 0`. | Boundary |
| IT_B5_038 | TK_MGR | `OrderedQty:999999999999999.999` và `UnitPrice:9999999999999999.99` (tích vượt `decimal(18,2)` của cột computed `Amount`) | [API]: 400 (chặn trước khi tràn); hoặc 201 nhưng khi GET chi tiết/danh sách thì **không 500** do lỗi arithmetic overflow khi tính `Amount`.<br>[DB]: không có dòng nào khiến `SELECT Amount` báo lỗi tràn. | Boundary |
| IT_B5_039 | TK_MGR | `Lines:[]`; `Lines:null` | [API]: 400.<br>[DB]: không có PO mới. | Abnormal |
| IT_B5_040 | TK_MGR | `OrderDate:"0001-01-01"`; không gửi `OrderDate` (bind ra `default(DateOnly)`); `OrderDate:"2026-02-30"` | [API]: **400** với cả 3; không 500.<br>[DB]: không có PO nào `OrderDate='0001-01-01'`. | Boundary |
| IT_B5_041 | TK_MGR | `ExpectedDeliveryCount:3` nhưng `ExpectedDeliveryDates` chỉ có 2 ngày (DEV-ALL-113) | [API]: 400 (số lượng không khớp) `[?]`; ghi lại nếu được chấp nhận.<br>[DB]: không có PO mới. | Abnormal |
| IT_B5_042 | TK_MGR | `ExpectedDeliveryCount:0` với `[]`; `-1`; `32767`; `32768`; `ExpectedDeliveryDates:null` khi Count = 1 | [API]: 0 → 400 hoặc 201 `[?]`; −1 → 400 (nếu lọt xuống thì `CK_DSD_Seq` sẽ vi phạm, không được 500); 32768 → 400 (tràn `short`); null → 400 hoặc 201 không có lịch `[?]`.<br>[DB]: không có `DeliveryScheduleDate.SeqNo < 1`. | Boundary |
| IT_B5_043 | TK_MGR | ExpectedDeliveryDates chứa `"0001-01-01"`; chứa 2 ngày trùng nhau; có ngày trước `OrderDate` | [API]: 400 với `0001-01-01`; trùng ngày / ngày trước OrderDate → 400 `[?]`; không 500.<br>[DB]: không có `DeliveryDate='0001-01-01'`; không có `(PurchaseOrderId, SeqNo)` trùng. | Boundary |
| IT_B5_044 | TK_MGR | Note dài 1000 / 1001 ký tự có dấu | [API]: 1000 → 201; 1001 → 400.<br>[DB]: ca 1000 ký tự lưu đủ. | Boundary |
| IT_B5_045 | TK_MGR | POST kèm `{PoCode:"HACK-01", GoodsStatus:"RECEIVED", PaymentStatus:"PAID", PaidAmount:1, SourceRequestId:1, CreatedAt:"0001-01-01"}` (mass assignment) | [API]: 201; các field thừa bị bỏ qua.<br>[DB]: `PoCode` do hệ thống sinh, không phải "HACK-01"; `PaymentStatus='UNPAID'`; `PaidAmount IS NULL`; `SourceRequestId IS NULL`; `CreatedAt` ≈ hiện tại. | Abnormal |
| IT_B5_046 | S0 | Gửi **đồng thời** 5 POST tạo PO dự trữ hợp lệ | [API]: cả 5 đều 201 với 5 `PoCode` **khác nhau**, hoặc một số request trả 409 có thể retry; **không 500** do race khi sinh mã (`UQ_PurchaseOrder_Code`).<br>[DB]: không có `PoCode` trùng; mỗi PO có đủ dòng và lịch giao, không có dữ liệu nửa vời từ request thất bại. | Boundary |
| IT_B5_047 | Fault-injection: INSERT `DeliveryScheduleDate` lỗi (trigger test-only) | POST hợp lệ như IT_B5_023 | [API]: lỗi có kiểm soát (ghi lại mã).<br>[DB]: **Rollback**: không có `PurchaseOrder` hay `PurchaseOrderLine` mới. | Abnormal |
| IT_B5_048 | Trigger `trg_PurchaseOrderLine_Check` đang bật | POST với dữ liệu vi phạm điều kiện của trigger (đối chiếu nội dung trigger, ví dụ dòng gắn `OrderMaterialId` nhưng `MaterialId` khác với NVL của `OrderMaterial` đó, chỉ seed được qua SQL) | [API]: 400/409; **không 500**; body không lộ tên trigger.<br>[DB]: Rollback, không có PO mới. | Abnormal |

## 4. Danh sách và chi tiết (E01, E02, E04–E07)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_049 | TK_MGR | `GET M/PurchaseOrders/<PO1>`; `GET M/PurchaseOrderTracking/<PO1>` | [API]: 200 với cả hai (hai shape khác nhau). Mỗi dòng có `MaterialCode/Name/Unit/MaterialColor` (màu rỗng là `""`, không phải null) và `Amount`. Tracking có `ExpectedDeliveryDate/To` và `ExpectedDeliveryDates[]` (2 ngày).<br>[DB]: `Amount` khớp `SELECT Amount FROM PurchaseOrderLine`; `TotalAmount` (ở danh sách) = `SUM(Amount)`. | Normal |
| IT_B5_050 | Seed bằng SQL một `PurchaseOrderLine` có `MaterialId IS NULL` (DB cho phép, nhưng DTO có `MaterialId` kiểu `int` non-null) | `GET M/PurchaseOrders/<PO đó>`; `GET M/PurchaseOrders` | [API]: 200, **không 500** khi map NULL sang `int`; ghi lại giá trị trả về.<br>[DB]: không đổi. Nếu API trả 500 → ghi finding. | Boundary |
| IT_B5_051 | TK_MGR | `GET M/PurchaseOrders?goodsStatus=ORDERED`; `?goodsStatus=Ordered`; `?goodsStatus=Xyz` | [API]: dạng HOA → PO2, PO3, PO6 (theo comment của `PurchaseOrderQuery`); dạng Pascal → cùng kết quả hoặc 400 (ghi lại); Xyz → 400 hoặc rỗng; không 500.<br>[DB]: kết quả khớp `WHERE GoodsStatus='ORDERED'`. | Boundary |
| IT_B5_052 | TK_MGR | `GET M/PurchaseOrderTracking?goodsStatus=PendingFinalisation`; `?goodsStatus=PENDING_FINALISATION`; `?paymentStatus=Unpaid` | [API]: dạng Pascal → PO1 (theo comment của `PurchaseOrderFlowQuery`); dạng HOA → cùng kết quả hoặc 400 (ghi lại để so với IT_B5_051, vì hai query dùng quy ước khác nhau); `Unpaid` → các PO `UNPAID`.<br>[DB]: khớp `GoodsStatus` / `PaymentStatus` HOA. | Boundary |
| IT_B5_053 | TK_MGR | `GET M/PurchaseOrders?stockOnly=true`; `?stockOnly=false`; `?receivable=true` | [API]: stockOnly=true → chỉ PO có `OrderId IS NULL` (PO2, PO3…); receivable → chỉ PO đang chờ nhận hàng `[?]`.<br>[DB]: khớp điều kiện tương ứng. | Normal |
| IT_B5_054 | TK_MGR | `GET M/PurchaseOrderTracking?payableOnly=true` | [API]: 200; **vẫn** có PO1 (chưa đặt hàng), vì server ghi đè `PayableOnly=false`.<br>[DB]: không đổi. | Boundary |
| IT_B5_055 | TK_ACC | `GET AC/PurchasePayments?payableOnly=false` | [API]: 200; **không** có PO1 (chưa đặt) và PO4 (đã hủy), vì server ghi đè `PayableOnly=true`.<br>[DB]: không đổi. | Boundary |
| IT_B5_056 | TK_MGR | `PageSize`: `0`; `1`; `100`; `101`; `-5`. `Page`: `0`; `-1` | [API]: 200 với mọi ca (theo hợp đồng thì `SafePageSize` tự kẹp: < 1 → 20, > 100 → 100; `Page` < 1 → 1); số phần tử ≤ PageSize sau kẹp. Nếu validator lại trả 400 → ghi finding vì không nhất quán với các batch khác.<br>[DB]: không đổi. | Boundary |
| IT_B5_057 | TK_MGR | `keyword` dài 100 / 101 ký tự; `keyword:"' OR 1=1--"`; `sortDir=ASC` / `xyz`; `sortBy` chứa câu lệnh SQL | [API]: 101 ký tự → 400 hoặc chấp nhận `[?]`; injection → rỗng, không 500; `ASC` → tăng dần; `xyz` → giảm dần; sortBy lạ → 400 hoặc bị bỏ qua.<br>[DB]: các bảng còn nguyên. | Boundary |
| IT_B5_058 | TK_MGR / TK_WK / TK_ACC | `GET M/PurchaseOrders/999999`; `GET WK/PurchaseOrders/abc`; `GET AC/PurchasePayments/999999` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B5_059 | TK_ACC | `GET AC/PurchasePayments/<PO1>` (PO chưa đặt, không thuộc nhóm "cần thanh toán") | [API]: 404, hoặc 200 chỉ đọc `[?]`; ghi lại (danh sách ẩn PO1 nhưng chi tiết có thể vẫn trả về).<br>[DB]: không đổi. | Abnormal |

## 5. Đặt hàng — `POST M/PurchaseOrderTracking/{id}/place` (E08)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_060 | PO1 `PENDING_FINALISATION`, RowVer R1 | `place {SupplierId:<SU1>, OrderDate:today, ExpectedDeliveryCount:3, Note:"Chốt giá", Lines:[{PurchaseOrderLineId:<POL1>, OrderedQty:110, UnitPrice:49500}, {PurchaseOrderLineId:<POL2>, OrderedQty:500, UnitPrice:2000}], RowVer:R1, ExpectedDeliveryDates:[d1, d2, d3]}` | [API]: 200.<br>[DB]:<br>- PO1: `GoodsStatus='ORDERED'`, RowVer đổi, `ExpectedDeliveryDate=d1`, `ExpectedDeliveryDateTo=d3`.<br>- POL1: SL 110, giá 49500, `Amount=5445000.00`.<br>- Lịch giao: 2 dòng cũ được thay bằng 3 dòng SeqNo 1..3; không còn dòng cũ.<br>- AuditLogs +1 `[?]`. | Normal |
| IT_B5_061 | RowVer của PO1 đã bị đổi bởi lần sửa khác | place với RowVer = R1 (cũ) | [API]: **409**.<br>[DB]: PO1 giữ dữ liệu của lần sửa trước; lịch giao không đổi. | Abnormal |
| IT_B5_062 | TK_MGR | place thiếu `RowVer`; `RowVer:"abc"`; `RowVer:""` | [API]: 400/409; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B5_063 | 2 client cùng đọc R1 | Gửi **đồng thời** 2 request place PO1 với R1 (giá khác nhau) | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: giá trên các dòng khớp **một** request; lịch giao không bị trộn hay nhân đôi (`UQ_DSD_PurchaseOrder` không bị vi phạm). | Boundary |
| IT_B5_064 | POL3 thuộc PO2 | place PO1 với `Lines:[{PurchaseOrderLineId:<POL3>, …}]` (dòng của PO khác) | [API]: 400/404 (IDOR chéo PO); không lộ dữ liệu PO2.<br>[DB]: POL3 không đổi; PO1 không đổi. | Abnormal |
| IT_B5_065 | TK_MGR | place PO1 thiếu POL2 trong `Lines` (chỉ gửi POL1) | [API]: 400 (phải chốt đủ các dòng), hoặc 200 giữ nguyên POL2 `[?]`; ghi lại.<br>[DB]: không có dòng nào bị xóa ngoài ý muốn. | Abnormal |
| IT_B5_066 | TK_MGR | place với `PurchaseOrderLineId:999999`; với POL1 xuất hiện 2 lần | [API]: 400/404.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B5_067 | TK_MGR | place với `OrderedQty:0` / `-1` / `1000000000000000`; `UnitPrice:-1` / `10000000000000000` | [API]: 400 với từng ca (do trigger hoặc validator); không 500.<br>[DB]: Rollback: cả `PurchaseOrder` (Note, SupplierId) lẫn các dòng khác cũng không đổi. | Boundary |
| IT_B5_068 | TK_MGR | place với `SupplierId:<SU3>` (đã ngừng); `SupplierId:999999` | [API]: 400; 400/404 (không 500 do FK).<br>[DB]: PO1 vẫn SU1. | Abnormal |
| IT_B5_069 | TK_MGR | place với `OrderDate:"0001-01-01"`; `ExpectedDeliveryDates` chứa `0001-01-01`; Count = 2 nhưng gửi 3 ngày | [API]: 400 với từng ca.<br>[DB]: lịch giao cũ (2 dòng) còn nguyên (không bị DELETE trước khi phần INSERT thất bại). | Boundary |
| IT_B5_070 | PO2 đã `ORDERED`; PO4 đã hủy; PO5 `PARTIALLY_RECEIVED` | place PO2; place PO4; place PO5 (với RowVer đúng) | [API]: 400/409 với cả 3 (chỉ PO `PENDING_FINALISATION` mới được đặt hàng).<br>[DB]: không đổi. | Abnormal |
| IT_B5_071 | TK_MGR | `POST M/PurchaseOrderTracking/999999/place {…}`; `/abc/place` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B5_072 | Fault-injection: INSERT lịch giao mới lỗi trong lúc place | place hợp lệ | [API]: lỗi có kiểm soát.<br>[DB]: **Rollback**: PO1 vẫn `PENDING_FINALISATION`, RowVer = R1; giá trên các dòng không đổi; 2 dòng lịch giao cũ còn nguyên. | Abnormal |
| IT_B5_073 | TK_MGR | place với Note dài 1001 ký tự | [API]: 400.<br>[DB]: không đổi. | Boundary |

## 6. Hủy đơn mua — `…/{id}/cancel` (E09)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_074 | PO1 `PENDING_FINALISATION` | `cancel {Reason:"NCC hết hàng", RowVer:R1}` | [API]: 200.<br>[DB]: `GoodsStatus` = giá trị hủy viết HOA (`CANCELLED` `[?]`); `RejectReason=N'NCC hết hàng'` `[?]`; RowVer đổi; các dòng và lịch giao không bị xóa; AuditLogs +1. | Normal |
| IT_B5_075 | TK_MGR | cancel với `{Reason:""}`; `{Reason:null}`; thiếu `Reason` | [API]: 400 (Reason non-null).<br>[DB]: không đổi. | Abnormal |
| IT_B5_076 | TK_MGR | cancel với Reason dài 500 / 501 ký tự có dấu | [API]: 500 → 200; 501 → 400 (không 500 do truncation `nvarchar(500)`).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B5_077 | PO5 đã nhận một phần (có `MaterialReceiptLine`); PO3 đã thanh toán | cancel PO5; cancel PO3 | [API]: 400/409 với cả hai `[?]`; không 500.<br>[DB]: không đổi; `MaterialReceiptLine` và `PurchasePayment` còn nguyên. | Abnormal |
| IT_B5_078 | PO4 đã hủy | cancel PO4 lần nữa | [API]: 400/409.<br>[DB]: `RejectReason` cũ giữ nguyên. | Abnormal |
| IT_B5_079 | TK_MGR | cancel với RowVer cũ; cancel `id=999999` | [API]: 409; 404.<br>[DB]: không đổi. | Abnormal |
| IT_B5_080 | PO1, RowVer R1 | Gửi **đồng thời** `place` và `cancel` cho PO1 với cùng R1 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: PO1 ở **một** trạng thái cuối duy nhất (`ORDERED` hoặc giá trị hủy). | Boundary |

## 7. Đổi giá — `…/{id}/reprice` (E10)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_081 | PO2 `ORDERED`, RowVer R2 | `reprice {Note:"NCC giảm giá", Lines:[{PurchaseOrderLineId:<POL3>, UnitPrice:46000}], RowVer:R2}` | [API]: 200.<br>[DB]: `POL3.UnitPrice=46000.00`; `Amount=9200000.00`; `OrderedQty` **không đổi**; `PurchaseOrder.Note` được cập nhật; RowVer đổi; AuditLogs +1. | Normal |
| IT_B5_082 | PO3 đã có thanh toán | reprice PO3 làm tổng tiền thay đổi | [API]: 200 (`PaymentStatus` được tính lại `[?]`), hoặc 409 (không đổi giá sau khi đã trả tiền) `[?]`; ghi lại.<br>[DB]: nhất quán: nếu 200 thì `PaymentStatus` khớp với tổng mới so với `PaidAmount`; `PurchasePayment` không bị sửa. | Abnormal |
| IT_B5_083 | POL1 thuộc PO1 | reprice PO2 với `PurchaseOrderLineId:<POL1>` | [API]: 400/404 (IDOR chéo PO).<br>[DB]: POL1 không đổi. | Abnormal |
| IT_B5_084 | TK_MGR | reprice với `UnitPrice:-1`; `10000000000000000`; `100.555`; `Lines:[]` | [API]: 400 với từng ca (`[]` → 400 hoặc chỉ đổi Note `[?]`); không 500.<br>[DB]: Rollback, không đổi. | Boundary |
| IT_B5_085 | PO2 có 1 dòng | reprice với 2 phần tử cùng `PurchaseOrderLineId` (giá khác nhau) | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B5_086 | PO1 `PENDING_FINALISATION`; PO4 đã hủy | reprice PO1; reprice PO4 | [API]: PO1 → 400/409 hoặc 200 `[?]` (đổi giá có thể chỉ áp dụng sau khi đặt); PO4 → 400/409.<br>[DB]: PO4 không đổi. | Abnormal |
| IT_B5_087 | TK_MGR | reprice với RowVer cũ; thiếu RowVer | [API]: 409; 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B5_088 | PO2, RowVer R2 | Gửi **đồng thời** `reprice` (Manager) và `confirm` thanh toán (Accountant) với cùng R2 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: không tồn tại trạng thái "đã trả" mà giá đã bị đổi **sau** khi xác nhận thanh toán mà không được ghi nhận. | Boundary |

## 8. Thanh toán — `AC/PurchasePayments` (E11–E15)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_089 | PO2 `ORDERED`, `UNPAID`, tổng 9 600 000, RowVer R2 | `POST AC/PurchasePayments/<PO2>/confirm {PaymentDate:today, PaidAmount:9600000, PaymentMethod:"BankTransfer", PaymentNote:"UNC 123", RowVer:R2, ActualAmount:9600000}` | [API]: 200; `Payments[]` có 1 phần tử, `CanEdit=true`.<br>[DB]:<br>- 1 dòng `PurchasePayment` (`Amount=9600000.00`, `PaymentDate=today`, `CreatedAt` ≈ hiện tại giờ VN).<br>- PO2: `PaymentStatus='PAID'` `[?]`; các cột phẳng `PaidAmount`, `PaymentDate`, `PaymentMethod`, `PaymentNote`, `ActualAmount` **khớp** với payment; RowVer đổi.<br>- AuditLogs +1 `[?]`. | Normal |
| IT_B5_090 | PO2 tổng 9 600 000 | confirm với `PaidAmount:5000000` (trả một phần) | [API]: 200.<br>[DB]: `PurchasePayment.Amount=5000000.00`; `PaymentStatus` = trả một phần hoặc vẫn `UNPAID` `[?]`; `PurchaseOrder.PaidAmount` = tổng các payment `[?]`. | Normal |
| IT_B5_091 | TK_ACC | confirm với `PaidAmount:0`; `PaidAmount:-1` | [API]: **400**; **không 500** do `CK_PP_Amount (Amount > 0)`.<br>[DB]: không có `PurchasePayment` mới; PO2 không đổi. | Boundary |
| IT_B5_092 | TK_ACC | `PaidAmount:0.01`; `9999999999999999.99`; `10000000000000000`; `100.555` | [API]: 0.01 → 200; giá trị tối đa → 200 hoặc 400 (vượt tổng tiền đơn) `[?]`; vượt tối đa → 400 (tràn `decimal(18,2)`); 3 chữ số thập phân → 400 hoặc làm tròn `[?]`.<br>[DB]: không có payment nào `Amount ≤ 0`. | Boundary |
| IT_B5_093 | PO2 tổng 9 600 000 | confirm `PaidAmount:20000000` (vượt tổng) | [API]: 400 `[?]` (hoặc chấp nhận và ghi chênh lệch qua `ActualAmount`); ghi lại.<br>[DB]: nhất quán với API. | Boundary |
| IT_B5_094 | TK_ACC | `PaymentMethod:"BankTransfer"`; `"Cash"`; `"BANKTRANSFER"`; `"Crypto"`; `""`; chuỗi 21 ký tự | [API]: hai giá trị hợp lệ → 200; dạng HOA → 200 hoặc 400 `[?]`; `Crypto` / rỗng → 400; 21 ký tự → 400, không 500 (cột `varchar(20)`).<br>[DB]: chỉ có giá trị hợp lệ; ghi lại dạng lưu thực tế (Pascal hay HOA) và giá trị này phải **giống nhau** ở `PurchaseOrder` và `PurchasePayment`. | Boundary |
| IT_B5_095 | TK_ACC | `PaymentDate:"0001-01-01"`; thiếu `PaymentDate`; `PaymentDate` = ngày mai; `PaymentDate` trước `OrderDate` | [API]: 0001 / thiếu → **400**; tương lai / trước ngày đặt → 400 `[?]`; không 500.<br>[DB]: không có payment nào `PaymentDate='0001-01-01'`. | Boundary |
| IT_B5_096 | TK_ACC | `PaymentNote` dài 500 / 501 ký tự; `ActualAmount:-1` | [API]: 500 → 200; 501 → 400; `ActualAmount` âm → 400 `[?]`.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B5_097 | TK_ACC | confirm với RowVer cũ; thiếu RowVer | [API]: 409; 400/409.<br>[DB]: không có `PurchasePayment` mới (không được INSERT payment khi PO không cập nhật được). | Abnormal |
| IT_B5_098 | PO1 chưa đặt; PO4 đã hủy | confirm PO1; confirm PO4 | [API]: 400/409 (hoặc 404 nếu không thuộc nhóm cần thanh toán).<br>[DB]: không đổi. | Abnormal |
| IT_B5_099 | PO2, RowVer R2 | Gửi **đồng thời** 2 request confirm PO2 với cùng R2 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: đúng **1** dòng `PurchasePayment` mới (không có thanh toán kép). | Boundary |
| IT_B5_100 | Fault-injection: UPDATE `PurchaseOrder` lỗi sau khi đã INSERT `PurchasePayment` | confirm hợp lệ | [API]: lỗi có kiểm soát.<br>[DB]: **Rollback**: không có `PurchasePayment` mồ côi; PO2 vẫn `UNPAID`, các cột phẳng vẫn NULL. | Abnormal |
| IT_B5_101 | PP2 thuộc PO3, tạo cách đây 1 giờ (`CanEdit=true`) | `PUT AC/PurchasePayments/<PO3>/payments/<PP2> {PaymentDate:yesterday, Amount:3500000, PaymentMethod:"Cash", PaymentNote:"Sửa số", Reason:"Nhập sai số tiền", RowVer:<RowVer PO3>}` | [API]: 200.<br>[DB]: PP2 có `Amount=3500000.00`, `UpdatedAt` khác NULL, `EditReason=N'Nhập sai số tiền'`, `CreatedAt` **không đổi**; các cột phẳng của PO3 (`PaidAmount`…) được tính lại cho khớp `[?]`; RowVer của PO3 đổi. | Normal |
| IT_B5_102 | PP1 thuộc PO3, tạo cách đây **30 giờ** (`CanEdit=false`) | PUT PP1 với body hợp lệ | [API]: **400/409** (đã quá 24 giờ).<br>[DB]: PP1 không đổi. | Abnormal |
| IT_B5_103 | Seed `CreatedAt` của PP2 = `fn_VnNow()` − 23 giờ 59 phút; và một payment khác = − 24 giờ 01 phút | PUT cả hai | [API]: 23h59 → 200; 24h01 → 400/409 (biên 24 giờ phải tính theo cùng nguồn giờ `fn_VnNow`, không lệch 7 giờ do dùng UTC).<br>[DB]: chỉ payment 23h59 được sửa. | Boundary |
| IT_B5_104 | PP9 thuộc PO6 | `PUT AC/PurchasePayments/<PO3>/payments/<PP9> {…, RowVer:<RowVer PO3>}` (paymentId thuộc PO khác) | [API]: 400/404 (IDOR chéo PO); không lộ dữ liệu PP9.<br>[DB]: PP9 không đổi; PO6 không đổi. | Abnormal |
| IT_B5_105 | TK_ACC | PUT với `Amount:0`; `Amount:-1` | [API]: **400**; **không 500** do `CK_PP_Amount`.<br>[DB]: PP2 không đổi. | Boundary |
| IT_B5_106 | TK_ACC | PUT với `Reason:""` / null; Reason dài 500 / 501 ký tự; PaymentNote dài 501 ký tự | [API]: rỗng / null → 400 (Reason bắt buộc khi sửa); 500 → 200; 501 → 400.<br>[DB]: chỉ ca hợp lệ được lưu vào `EditReason`. | Boundary |
| IT_B5_107 | TK_ACC | PUT với RowVer cũ của PO3; thiếu RowVer; `paymentId=999999`; `paymentId=abc` | [API]: 409; 400/409; 404; 404.<br>[DB]: không đổi. | Abnormal |
| IT_B5_108 | PP2 có thể sửa | Gửi **đồng thời** 2 PUT PP2 với cùng RowVer của PO3 | [API]: 1 request 200, 1 request 409; không 500 (`PurchasePayment` không có RowVer riêng, dựa vào RowVer của PO).<br>[DB]: PP2 khớp **một** request; các cột phẳng của PO3 nhất quán với PP2. | Boundary |
| IT_B5_109 | PO2 đã được confirm (sau IT_B5_089) | `POST AC/PurchasePayments/<PO2>/return {Reason:"Sai số tài khoản", RowVer}` | [API]: 200.<br>[DB]: `PaymentStatus` = giá trị trả lại viết HOA (`RETURNED` `[?]`); `PaymentReturnReason=N'Sai số tài khoản'`; `PurchasePayment` **không bị xóa** (theo entity); RowVer đổi. | Normal |
| IT_B5_110 | TK_ACC | return với `Reason:""` / null; Reason dài 500 / 501 ký tự | [API]: 400 / 400 / 200 / 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B5_111 | PO2 `UNPAID` (chưa confirm) | return PO2 | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B5_112 | TK_ACC | return với RowVer cũ; `id=999999` | [API]: 409; 404.<br>[DB]: không đổi. | Abnormal |
| IT_B5_113 | PO2 đã confirm | Gửi **đồng thời** `return` và `PUT payments/<PP mới>` với cùng RowVer | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: PO2 ở một trạng thái nhất quán duy nhất. | Boundary |
| IT_B5_114 | PO3 có PP1, PP2 | `GET AC/PurchasePayments/<PO3>` | [API]: 200; `Payments[]` gồm PP1 (`CanEdit=false`) và PP2 (`CanEdit=true`); `PaidAmount` phẳng nhất quán với `SUM(Payments.Amount)` `[?]`.<br>[DB]: khớp `PurchasePayment WHERE PurchaseOrderId=PO3`. | Normal |
| IT_B5_115 | TK_ACC | `GET AC/PurchasePayments?paymentStatus=Paid`; `?paymentStatus=PAID`; `?paymentStatus=Xyz` | [API]: dạng Pascal → các PO đã trả; dạng HOA → cùng kết quả hoặc 400 (ghi lại); Xyz → 400 hoặc rỗng; không 500.<br>[DB]: khớp `PaymentStatus` HOA. | Boundary |

## 9. Kiểm tra chéo: trạng thái, số liệu suy ra, nhật ký

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B5_116 | Đã chạy các ca ở mục 3–8 | `SELECT DISTINCT` các cột `GoodsStatus`, `PaymentStatus` của `PurchaseOrder` và `Status` của `Supplier` | [API]: —<br>[DB]: mọi giá trị đều UPPER_SNAKE (`PENDING_FINALISATION`, `ORDERED`, `PARTIALLY_RECEIVED`, `UNPAID`, `ACTIVE`, cùng các giá trị `[?]` khác); không có PascalCase. | Normal |
| IT_B5_117 | Có các PO đã confirm/update | So sánh `PurchaseOrder.PaymentMethod` với `PurchasePayment.PaymentMethod` mới nhất, và `PurchaseOrder.PaidAmount` với `SUM(PurchasePayment.Amount)` của từng PO | [API]: E07 và E12 trả `PaidAmount` và `Payments[]` nhất quán.<br>[DB]: hai nguồn dữ liệu thanh toán (cột phẳng và bảng) **khớp nhau** ở mọi PO; nếu lệch → ghi finding. | Normal |
| IT_B5_118 | Có các PO với nhiều dòng | So `TotalAmount` (E01/E06/E11) với `SUM(PurchaseOrderLine.Amount)`; so `Amount` với `CONVERT(decimal(18,2), OrderedQty*UnitPrice)` | [API]: các giá trị tiền trong danh sách và chi tiết khớp nhau.<br>[DB]: khớp đến 2 chữ số thập phân (không bị sai số làm tròn do tính ở client). | Normal |
| IT_B5_119 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chạy các ca thất bại IT_B5_025, IT_B5_031, IT_B5_061, IT_B5_091, IT_B5_102, IT_B5_104 | [API]: 4xx đúng như từng ca.<br>[DB]: `MAX(AuditLogs.Id)` không đổi; không có dữ liệu nửa vời ở `Material`, `PurchaseOrder`, `PurchaseOrderLine`, `DeliveryScheduleDate`, `PurchasePayment`. | Abnormal |
| IT_B5_120 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Chuỗi: tạo PO → place → reprice → confirm → sửa payment → return | [API]: mỗi bước 200/201.<br>[DB]: AuditLogs tăng đúng số bước `[?]`; RowVer của PO đổi sau mỗi bước ghi; trạng thái cuối là `ORDERED` + giá trị trả lại `[?]`. | Normal |
| IT_B5_121 | Trigger `trg_PurchaseOrderLine_Check` đang bật | Chạy lại IT_B5_036, IT_B5_046, IT_B5_063, IT_B5_099 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; body không chứa tên trigger hay constraint.<br>[DB]: không có `PoCode` trùng; không có payment kép; không có dòng `OrderedQty ≤ 0`. | Boundary |
| IT_B5_122 | — | Rà soát toàn bộ endpoint của batch (E01–E22) xem có endpoint nào nhận `multipart/form-data` | [API]: không có endpoint nào nhận `IFormFile` (chứng từ thanh toán upload qua Attachment ở Batch 6). Gửi multipart tới E13 → 415/400, không 500.<br>[DB]: không đổi. | Boundary |
