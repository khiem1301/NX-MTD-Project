# Integration Test Cases — Batch 6: Nhập kho NVL & thiếu giao (`IT_B6_xxx`)

Nguồn: `IT_CONTRACT — Batch 6`. Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**.

> **Ghi chú phạm vi**
> - **Không có view:** batch này không có view SQL nào được khai báo. Phần "đối chiếu dữ liệu suy ra" kiểm cột computed (`MaterialReceipt.Source`, `OrderMaterial.Source`/`PurchaseQty`), endpoint tổng hợp `IncomingMaterials`, và chuỗi thẻ kho `StockTransaction.BalanceAfter` ↔ `StockBalance.OnHandQty`.
> - **Không có import Excel:** chỉ có export (`/excel`).
> - **Upload file:** chỉ có ở `Attachments`.

## 0. Quy ước chung

**HTTP kỳ vọng**
- Response thành công: `200 {Success:true, Data, Message}`.
- Response lỗi: `{Success:false, ErrorCode, Message, Errors?}`, map theo hợp đồng: Validation → **400**, Unauthorized → **401**, Forbidden → **403**, NotFound → **404**, Conflict → **409**.
- `405` khi có route nhưng sai method; `413` khi vượt `RequestSizeLimit`.
- **Bất kỳ `500` nào cũng là FAIL**, kể cả khi DB vẫn đúng. Body lỗi không được lộ tên trigger/constraint (`trg_*`, `UQ_*`, `usp_Stock_Post`), `SqlException` hay stack trace.
- Tuân thủ **Assert-Locking**: kỳ vọng đã khóa thì không được sửa lại để ép test PASS.

**Quy ước cho vế [DB]**
- Giá trị trạng thái/mã lưu VIẾT HOA:
  - `MaterialReceipt.Status`: `PENDING`, và các giá trị đoán theo quy ước: `CONFIRMED`, `RETURNED` `[?]`.
  - `MaterialReceipt.Source` (computed persisted): `CUSTOMER` / `SUPPLIER`.
  - `Request.RequestTypeCode`: `SHORT_DELIVERY`.
  - `Request.ShortageResolution`: `REPLENISH` / `NO_REPLENISH`.
  - `Attachment.Purpose`: `PHOTO` (DB default) — xem IT_B6_108.
  - `StockTransaction.SourceDocType` và `StockBalance.WarehouseType`: giá trị `[?]`, ghi lại giá trị thực tế.
- `StockTransaction` là bảng **append-only** (`trg_StockTransaction_AppendOnly` / `PostOnly`): chỉ assert bằng SELECT; dọn dữ liệu bằng reset DB/transaction rollback, không UPDATE/DELETE trực tiếp.
- "DB không đổi" = snapshot các bảng `MaterialReceipt` (gồm RowVer), `MaterialReceiptLine`, `StockBalance`, `StockTransaction`, `Request` (gồm RowVer), `RequestLine`, `Attachment`, `AuditLogs` trước và sau khi gọi trùng nhau.
- Các cột ngày (`ReceiptDate`, `TargetDate`, `TransactionDate`…) không bao giờ được là `0001-01-01`. Các cột `SubmittedAt`, `ConfirmedAt`, `UploadedAt`, `CreatedAt` phải ≈ giờ VN (`fn_VnNow()`).

**Viết tắt URL:** `M/` = `/api/v1/manager/`, `WK/` = `/api/v1/warehouse-keeper/`, `C/` = `/api/v1/common/`.

**Dữ liệu nền S0** (reset trước mỗi test)

- **Token:** TK_ADM (EmployeeId E_ADM), TK_MGR (E_MGR), TK_WK (E_WK), TK_WK2 (thủ kho thứ hai), TK_ACC, TK_TL, TK_WRK, TK_EXP (hết hạn), TK_FORGED (sai chữ ký).
- **Material:** MA1 `V-0001` FABRIC; MA2 `PL-0001` TRIM; MA3 `PL-0002` TRIM.

**Đơn khách và NVL của đơn**
- OD2 (đang sản xuất):
  - OM1: MA1, cần 150, khách cấp 150 → `Source='CUSTOMER'`.
  - OM2: MA2, cần 500, khách cấp 200 → `Source='BOTH'`, `PurchaseQty=300`.
- OD3: đã hủy `[?]`.
- OD7: đơn khác, có OM9 (MA3). Dùng làm mục tiêu cho các ca chéo dữ liệu.

**Đơn mua**
- PO1 (`ORDERED`, gắn OD2): POL1 (OM2/MA2, SL 300).
- PO2 (`ORDERED`, dự trữ): POL3 (MA1, `OrderMaterialId` NULL, SL 200).
- PO4: đã hủy, có POL4.

**Phiếu nhập**

| Alias | Mã phiếu | Source | Nguồn | Status | Dòng | Ghi chú |
|---|---|---|---|---|---|---|
| MR1 | PN-0001 | CUSTOMER | OD2 | `PENDING` | MRL1 (OM1, SL nhận 100) | RowVer RM1 |
| MR2 | PN-0002 | SUPPLIER | PO2 | `PENDING` | MRL2 (POL3, MA1, SL nhận 150/200) | Nhận thiếu 50 |
| MR3 | PN-0003 | SUPPLIER | PO1 | đã xác nhận `[?]` | — | Đã sinh yêu cầu thiếu giao SD1 |
| MR4 | PN-0004 | — | — | đã trả lại `[?]` | — | `ReturnReason` có giá trị |

**Yêu cầu thiếu giao (`Request` loại `SHORT_DELIVERY`)**

| Alias | Mã | Phiếu nhập gốc | Trạng thái | Phương án xử lý | Dòng |
|---|---|---|---|---|---|
| SD1 | YC-0001 | MR3 | chưa xử lý `[?]` | — | RL1 (thiếu 100) |
| SD2 | YC-0002 | — | đã xử lý | `REPLENISH` | RL2 (giao bù 40, đã bù 0); `ExpectedDeliveryCount=1` |
| SD3 | YC-0003 | — | đã đóng | `NO_REPLENISH` | — |
| SD9 | YC-0009 | — | — | — | RL9; dùng làm mục tiêu cho các ca chéo dữ liệu |

**Tồn kho:** `StockBalance` SB1 (WarehouseType NVL `[?]`, MA1, OrderLineId NULL, PCM NULL, `OnHandQty=500.000`), kèm thẻ kho hợp lệ được post qua `usp_Stock_Post`.

**Attachment**
- AT1: owner (MaterialReceipt, MR1), `PHOTO`, do TK_WK upload.
- AT2: owner (PurchaseOrder, PO3), chứng từ thanh toán, do TK_ACC upload.
- AT3: owner (MaterialReceipt, MR1), `IsCurrent=0` (đã gỡ).

**File mẫu:** `img_ok.jpg` (1 MB); `img_5mb.jpg` (5 MB − 1 KB); `img_5mb1.jpg` (5 MB + 1 B); `img_6mb1.jpg` (6 MB + 1 B); `zero.jpg` (0 B); `fake.jpg` (.exe đổi đuôi); `note.txt`.

---

## 1. Ma trận phân quyền chéo

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_001 | S0 | Không gửi token: `GET M/MaterialReceipts`, `GET WK/IncomingMaterials`, `GET M/ShortDeliveries`, `GET C/Attachments?ownerType=MaterialReceipt&ownerId=<MR1>` | [API]: 401 `{Success:false}` với cả 4.<br>[DB]: không đổi. | Abnormal |
| IT_B6_002 | TK_EXP, TK_FORGED | `POST M/MaterialReceipts/<MR1>/confirm` | [API]: 401.<br>[DB]: MR1 vẫn `PENDING`; `StockBalance`/`StockTransaction` không đổi. | Abnormal |
| IT_B6_003 | TK_ADM | `GET WK/MaterialReceipts`; `GET WK/IncomingMaterials`; `GET WK/ShortDeliveries` | [API]: **403** với cả 3 (prefix warehouse-keeper không gồm Admin).<br>[DB]: không đổi. | Abnormal |
| IT_B6_004 | TK_ADM | `POST WK/MaterialReceipts {…}`; `POST WK/MaterialReceipts/supplementary {…}`; `PUT WK/MaterialReceipts/<MR1> {…}` | [API]: **403** với cả 3.<br>[DB]: không có phiếu mới; MR1 không đổi. | Abnormal |
| IT_B6_005 | TK_MGR | Như IT_B6_003 và IT_B6_004 | [API]: 403 với tất cả.<br>[DB]: không đổi. | Abnormal |
| IT_B6_006 | TK_WK | `POST M/MaterialReceipts/<MR1>/confirm`; `POST M/MaterialReceipts/<MR1>/return {…}`; `POST M/ShortDeliveries/<SD1>/resolve {…}`; `GET M/MaterialReceipts` | [API]: **403** với cả 4 (thủ kho không được tự xác nhận phiếu do mình lập).<br>[DB]: MR1 vẫn `PENDING`; tồn kho không đổi; SD1 không đổi. | Abnormal |
| IT_B6_007 | TK_WK | `POST WK/MaterialReceipts/<MR1>/confirm`; `POST WK/ShortDeliveries/<SD1>/resolve` | [API]: 404/405 (prefix WK không có route xác nhận/xử lý).<br>[DB]: không đổi. | Abnormal |
| IT_B6_008 | TK_ADM | `GET M/MaterialReceipts`; `POST M/MaterialReceipts/<MR1>/confirm` | [API]: 200 với cả hai (Admin được gọi `/manager/*`).<br>[DB]: `ConfirmedBy = E_ADM` (xem IT_B6_052). | Normal |
| IT_B6_009 | TK_ACC, TK_TL, TK_WRK (lần lượt) | `GET M/MaterialReceipts`; `GET WK/MaterialReceipts`; `GET M/ShortDeliveries`; `GET WK/IncomingMaterials` | [API]: 403 với mọi tổ hợp.<br>[DB]: không đổi. | Abnormal |
| IT_B6_010 | TK_ACC, TK_TL, TK_WRK (lần lượt) | `GET C/Attachments?ownerType=MaterialReceipt&ownerId=<MR1>` | [API]: 200 (Controller chỉ có `[Authorize]`, không ràng buộc role). Xem các ca IDOR ở mục 9.<br>[DB]: không đổi. | Normal |
| IT_B6_011 | TK_WK | `DELETE WK/MaterialReceipts/<MR1>`; `DELETE M/MaterialReceipts/<MR1>` (bằng TK_MGR) | [API]: 404/405 (không có API xóa phiếu).<br>[DB]: MR1 và MRL1 còn nguyên. | Abnormal |
| IT_B6_012 | TK_MGR | `GET /api/v1/MANAGER/materialreceipts`; `GET /api/v1/manager/material-receipts` | [API]: dạng chữ hoa → 200; dạng kebab → 200 hoặc 404 tùy route transformer `[?]`; ghi lại.<br>[DB]: không đổi. | Boundary |

## 2. Tạo phiếu nhập — `POST WK/MaterialReceipts`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_013 | TK_WK | `POST WK/MaterialReceipts {OrderId:<OD2>, Source:"CUSTOMER", PurchaseOrderId:null, ReceiptDate:today, Note:"Khách giao đợt 2", Lines:[{OrderMaterialId:<OM1>, AcceptedQty:50}, {OrderMaterialId:<OM2>, AcceptedQty:200}]}` | [API]: 200; `Data` có `ReceiptCode` tự sinh, `Status`, `ReceiptType` = "Mới" / `New`.<br>[DB]:<br>- 1 dòng `MaterialReceipt`: `Status='PENDING'`, `Source='CUSTOMER'` (computed), `OrderId=OD2`, `ShortageRequestId IS NULL`, `ConfirmedBy IS NULL`, `SubmittedAt` ≈ hiện tại.<br>- 2 dòng `MaterialReceiptLine` (`MaterialId` được điền theo OM `[?]`).<br>- **`StockBalance`/`StockTransaction` không đổi** (chưa xác nhận thì chưa tăng tồn). | Normal |
| IT_B6_014 | TK_WK | `{OrderId:null, Source:"SUPPLIER", PurchaseOrderId:<PO2>, ReceiptDate:today, Lines:[{PurchaseOrderLineId:<POL3>, AcceptedQty:200}]}` | [API]: 200.<br>[DB]: `Source='SUPPLIER'`; `PurchaseOrderId=PO2`; dòng có `PurchaseOrderLineId=POL3`, `OrderMaterialId IS NULL`, `MaterialId=MA1`. | Normal |
| IT_B6_015 | TK_WK | `{Source:"CUSTOMER", PurchaseOrderId:<PO2>, OrderId:<OD2>, …}` (Source mâu thuẫn với PO) | [API]: 400 (Source=CUSTOMER không được có PO).<br>[DB]: không có phiếu mới. Nếu tạo thành công thì DB tự tính `Source='SUPPLIER'` (computed), không theo giá trị gửi lên → ghi finding. | Abnormal |
| IT_B6_016 | TK_WK | `Source:"CUSTOMER"` nhưng `OrderId:null`; `Source:"SUPPLIER"` nhưng `PurchaseOrderId:null` | [API]: 400 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B6_017 | TK_WK | `Source:"Customer"`; `"OTHER"`; `""`; chuỗi 11 ký tự | [API]: `Customer` → 400 hoặc chuẩn hóa `[?]`; còn lại → 400; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B6_018 | OM9 thuộc OD7 | Phiếu CUSTOMER cho OD2 có dòng `OrderMaterialId:<OM9>` | [API]: 400/404 (NVL thuộc đơn khác, là IDOR chéo đơn).<br>[DB]: **Rollback**: không có phiếu, không có dòng. | Abnormal |
| IT_B6_019 | POL1 thuộc PO1 | Phiếu SUPPLIER cho PO2 có dòng `PurchaseOrderLineId:<POL1>` | [API]: 400/404.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B6_020 | TK_WK | Phiếu CUSTOMER có dòng gửi `PurchaseOrderLineId` thay cho `OrderMaterialId`; dòng gửi cả hai; dòng không gửi cả hai | [API]: 400 với từng ca.<br>[DB]: không đổi. | Abnormal |
| IT_B6_021 | TK_WK | Phiếu CUSTOMER với 2 dòng **cùng** `OrderMaterialId:<OM1>` | [API]: **409/400**; **không 500** do `UQ_MRL (MaterialReceiptId, OrderMaterialId)`.<br>[DB]: Rollback: không có `MaterialReceipt` mới. | Abnormal |
| IT_B6_022 | PO2 có POL3 (MA1) | Phiếu SUPPLIER (dòng không có `OrderMaterialId`) với 2 dòng cùng `PurchaseOrderLineId:<POL3>` (cùng MA1) | [API]: 409/400; **không 500** do `UQ_MRL_Stock (MaterialReceiptId, MaterialId) WHERE OrderMaterialId IS NULL`.<br>[DB]: Rollback. | Abnormal |
| IT_B6_023 | TK_WK | `AcceptedQty`: `0`; `-0.001`; `0.001`; `999999999999999.999`; `1000000000000000`; `1.0005` | [API]: 0 / âm → 400 (DB không có CHECK; nếu `trg_MaterialReceiptLine_Check` chặn thì vẫn phải 400, không 500); 0.001 → 200; giá trị tối đa → 200 hoặc 400 (vượt số còn phải nhận) `[?]`; vượt tối đa → 400 (tràn `decimal(18,3)`); 4 chữ số thập phân → 400 hoặc làm tròn `[?]`.<br>[DB]: không có dòng nào `AcceptedQty ≤ 0`. | Boundary |
| IT_B6_024 | OM1 cần 150, khách cấp 150, MR1 (PENDING) đã có 100 | Phiếu mới với OM1, `AcceptedQty:200` (vượt số còn phải nhận) | [API]: 400/409 `[?]` (trigger hoặc service chặn), hoặc 200 kèm cảnh báo; ghi lại; không 500.<br>[DB]: nhất quán với API. | Boundary |
| IT_B6_025 | TK_WK | `ReceiptDate:"0001-01-01"`; không gửi `ReceiptDate` (bind ra `default(DateOnly)`); `ReceiptDate` = ngày mai | [API]: 0001 / thiếu → **400**; tương lai → 400 `[?]`; không 500.<br>[DB]: không có phiếu nào `ReceiptDate='0001-01-01'`. | Boundary |
| IT_B6_026 | TK_WK | Note dài 1000 / 1001 ký tự có dấu | [API]: 1000 → 200; 1001 → 400.<br>[DB]: ca 1000 ký tự lưu đủ. | Boundary |
| IT_B6_027 | TK_WK | `Lines:[]`; `Lines:null` | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B6_028 | TK_WK | `OrderId:999999`; `OrderId:<OD3>` (đã hủy); `PurchaseOrderId:999999`; `PurchaseOrderId:<PO4>` (đã hủy) | [API]: 404/400 (không 500 do `FK_MR_Order` / `FK_MR_PO`); đơn/PO đã hủy → 400 `[?]`.<br>[DB]: không có phiếu mới. | Abnormal |
| IT_B6_029 | TK_WK | Dòng có `OrderMaterialId:999999`; `PurchaseOrderLineId:999999` | [API]: 404/400 (không 500 do `FK_MRL_OrderMaterial` / `FK_MRL_POL`).<br>[DB]: Rollback. | Abnormal |
| IT_B6_030 | TK_WK | Gửi kèm `{ReceiptCode:"HACK", Status:"CONFIRMED", ConfirmedBy:<E_ADM>, ConfirmedAt:"2026-01-01", Source:"SUPPLIER", SubmittedAt:"0001-01-01"}` (mass assignment) | [API]: 200; các field thừa bị bỏ qua.<br>[DB]: `ReceiptCode` do hệ thống sinh; `Status='PENDING'`; `ConfirmedBy`/`ConfirmedAt` NULL; `Source` theo công thức computed; `SubmittedAt` ≈ hiện tại. | Abnormal |
| IT_B6_031 | S0 | Gửi **đồng thời** 5 POST tạo phiếu hợp lệ | [API]: cả 5 đều 200 với 5 `ReceiptCode` khác nhau, hoặc một số request 409 có thể retry; **không 500** do race khi sinh mã (`UQ_MaterialReceipt_Code`).<br>[DB]: không có mã trùng; không có dòng mồ côi từ request thất bại. | Boundary |
| IT_B6_032 | Fault-injection: INSERT dòng thứ 2 bị `trg_MaterialReceiptLine_Check` (hoặc trigger test-only) chặn | POST phiếu 3 dòng | [API]: 400/409; không 500.<br>[DB]: **Rollback toàn bộ**: không có `MaterialReceipt`, không có dòng 1 (không lưu nửa vời). | Abnormal |
| IT_B6_033 | Trigger `trg_MaterialReceiptLine_Check` đang bật | Dòng có `MaterialId` khác với NVL của `OrderMaterial` tương ứng (chỉ seed được qua SQL) | [API]: 400/409; không 500; body không lộ tên trigger.<br>[DB]: không đổi. | Abnormal |

## 3. Sửa phiếu nhập — `PUT WK/MaterialReceipts/{id}`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_034 | MR1 `PENDING` | `PUT WK/MaterialReceipts/<MR1> {OrderId:<OD2>, Source:"CUSTOMER", ReceiptDate:today, Note:"Sửa SL", Lines:[{OrderMaterialId:<OM1>, AcceptedQty:120}, {OrderMaterialId:<OM2>, AcceptedQty:100}]}` | [API]: 200.<br>[DB]: MRL1 có `AcceptedQty=120.000`; thêm dòng OM2; RowVer của MR1 đổi; `Status` vẫn `PENDING`; tồn kho không đổi. | Normal |
| IT_B6_035 | MR1 có MRL1 (OM1) | PUT MR1 với Lines chỉ còn OM2 | [API]: 200.<br>[DB]: MRL1 bị DELETE `[?]`; chỉ còn dòng OM2. | Normal |
| IT_B6_036 | MR3 đã xác nhận | PUT MR3 | [API]: **409** (không sửa phiếu đã xác nhận).<br>[DB]: MR3, các dòng của MR3 và thẻ kho không đổi. | Abnormal |
| IT_B6_037 | MR4 đã bị trả lại | PUT MR4 với dữ liệu hợp lệ (nộp lại) | [API]: 200 `[?]`.<br>[DB]: `Status` quay về `PENDING` `[?]`; `ReturnReason` được giữ hoặc xóa `[?]`, ghi lại; `SubmittedAt` cập nhật hay giữ nguyên `[?]`. | Normal |
| IT_B6_038 | MR1 là phiếu CUSTOMER của OD2 | PUT MR1 đổi sang `Source:"SUPPLIER", PurchaseOrderId:<PO2>, OrderId:null`, Lines POL3 | [API]: 200 hoặc 400 (không cho đổi nguồn) `[?]`.<br>[DB]: nếu 200 thì `Source` (computed) tự chuyển thành `SUPPLIER`; không còn dòng OM1 trỏ sai nguồn. | Boundary |
| IT_B6_039 | `SaveMaterialReceiptDto` **không có** `RowVer` | Gửi **đồng thời** 2 PUT MR1 với AcceptedQty khác nhau (120 và 130) | [API]: cả hai 200 (last-write-wins) hoặc 1 request 409; không 500.<br>[DB]: MRL1 ∈ {120, 130}; không có dòng trùng. Ghi **finding**: DB có RowVer nhưng API sửa không dùng tới, nên không có khóa lạc quan. | Boundary |
| IT_B6_040 | MR1 do TK_WK tạo | TK_WK2 gửi PUT MR1 | [API]: 200 (không có ràng buộc theo người tạo), hoặc 403/404 nếu Service kiểm người tạo `[?]`; ghi lại.<br>[DB]: nhất quán với API. | Abnormal |
| IT_B6_041 | TK_WK | `PUT WK/MaterialReceipts/999999 {…}`; `PUT WK/MaterialReceipts/abc {…}` | [API]: 404.<br>[DB]: không có phiếu mới (không upsert). | Abnormal |
| IT_B6_042 | MR1 `PENDING` | PUT MR1 với dòng trùng OM1 hai lần; với `AcceptedQty:-1` | [API]: 409/400; 400.<br>[DB]: **Rollback**: MRL1 vẫn 100 (dòng cũ không bị xóa trước khi lệnh thêm thất bại). | Abnormal |
| IT_B6_043 | MR1 `PENDING` | Gửi **đồng thời** PUT MR1 (WK) và confirm MR1 (Manager) | [API]: confirm 200 trước thì PUT phải trả 409; hoặc PUT trước thì confirm dùng dữ liệu mới; không 500.<br>[DB]: số lượng ghi vào thẻ kho **khớp đúng** `AcceptedQty` của phiếu ở thời điểm được xác nhận (không có trạng thái "đã xác nhận" với dòng bị sửa sau đó). | Boundary |

## 4. Xác nhận và trả lại phiếu — `M/MaterialReceipts/{id}/confirm`, `/return`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_044 | MR1 `PENDING` (OM1 nhận 100); NVL MA1 của đơn OD2 chưa có `StockBalance` riêng `[?]` | `POST M/MaterialReceipts/<MR1>/confirm` bằng TK_MGR (không body) | [API]: 200.<br>[DB]:<br>- MR1: `Status='CONFIRMED'` `[?]`, `ConfirmedBy=E_MGR` (lấy từ token), `ConfirmedAt` ≈ hiện tại, RowVer đổi.<br>- `StockBalance`: tạo mới hoặc cộng `OnHandQty += 100.000`, `UpdatedAt` ≈ hiện tại.<br>- 1 dòng `StockTransaction`: `SourceDocType` = loại phiếu nhập `[?]`, `SourceDocId=MR1`, `SourceLineId=MRL1`, `QtyChange=100.000`, `BalanceAfter` = OnHand mới, `TransactionDate` = ReceiptDate `[?]`. | Normal |
| IT_B6_045 | MR2 nhận 150/200 của POL3 (thiếu 50) | confirm MR2 | [API]: 200; `Data.ShortDeliveryRequestId` khác null `[?]`.<br>[DB]:<br>- SB1 (MA1, kho dự trữ): `OnHandQty` 500 → 650; có thẻ kho tương ứng.<br>- 1 dòng `Request(RequestTypeCode='SHORT_DELIVERY', MaterialReceiptId=MR2, Status` = chưa xử lý `[?]`) với `RequestCode` duy nhất.<br>- 1 dòng `RequestLine(Quantity=50.000, MaterialReceiptLineId=MRL2, MaterialId=MA1)`.<br>- Tất cả trong **cùng 1 giao dịch**. | Normal |
| IT_B6_046 | MR1 đã được xác nhận (sau IT_B6_044) | confirm MR1 lần nữa | [API]: **409**.<br>[DB]: `StockBalance` **không** bị cộng thêm; không có thẻ kho mới (`UQ_ST_SourceLine` không bị chạm tới hay báo lỗi thành 500). | Abnormal |
| IT_B6_047 | MR1 `PENDING` | Gửi **đồng thời** 2 request confirm MR1 (TK_MGR và TK_ADM) | [API]: đúng 1 request 200, request còn lại 409; **không 500** (nếu `UQ_ST_SourceLine` hay trigger là bên chặn thì cũng phải map thành 409).<br>[DB]: `OnHandQty` chỉ tăng **một lần** (+100); đúng 1 dòng `StockTransaction` cho (MR1, MRL1). | Boundary |
| IT_B6_048 | MR2 thiếu giao | Gửi **đồng thời** 2 request confirm MR2 | [API]: 1 request 200, 1 request 409; không 500 do `UX_Request_Receipt`.<br>[DB]: đúng **1** `Request` `SHORT_DELIVERY` cho MR2; tồn chỉ tăng một lần. | Boundary |
| IT_B6_049 | Fault-injection: `usp_Stock_Post` lỗi ở dòng thứ 2 của phiếu 2 dòng | confirm phiếu đó | [API]: lỗi có kiểm soát (409/400, ghi lại mã); không 500 thô.<br>[DB]: **Rollback toàn bộ**: phiếu vẫn `PENDING`, `ConfirmedBy` NULL, RowVer không đổi; `StockBalance` của dòng 1 **không** tăng; không có `StockTransaction` nào; không có `Request` thiếu giao. | Abnormal |
| IT_B6_050 | Trigger `trg_StockBalance_Check` đang bật | confirm một phiếu mà dữ liệu seed khiến trigger từ chối (ví dụ WarehouseType hoặc tổ hợp khóa không hợp lệ; đối chiếu nội dung trigger) | [API]: 400/409; không 500; body không lộ tên trigger.<br>[DB]: Rollback như IT_B6_049. | Abnormal |
| IT_B6_051 | SB1 đã tồn tại cho (WarehouseType, MA1, NULL, NULL) | confirm phiếu SUPPLIER dự trữ thứ hai cũng nhận MA1 | [API]: 200.<br>[DB]: **cộng vào SB1**, không INSERT dòng `StockBalance` thứ hai (với `UQ_SB_Item`, các giá trị NULL bị coi là bằng nhau); không 500 do trùng UNIQUE. | Boundary |
| IT_B6_052 | MR1 `PENDING` | confirm bằng **TK_ADM** | [API]: 200.<br>[DB]: `ConfirmedBy = E_ADM` (EmployeeId của Admin lấy từ `ICurrentUser`, không lấy từ request). | Normal |
| IT_B6_053 | MR4 đã bị trả lại | confirm MR4 | [API]: 409/400.<br>[DB]: không đổi. | Abnormal |
| IT_B6_054 | TK_MGR | confirm `id=999999`; `id=abc` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B6_055 | MR1 `PENDING` | `POST M/MaterialReceipts/<MR1>/return {Reason:"Số lượng không khớp thực tế"}` | [API]: 200.<br>[DB]: `Status` = giá trị trả lại viết HOA (`RETURNED` `[?]`); `ReturnReason` được lưu; RowVer đổi; **tồn kho không đổi**. | Normal |
| IT_B6_056 | MR1 `PENDING` | return với `{Reason:""}`; `{Reason:null}`; body `{}`; không gửi body | [API]: 400 (không gửi body → 400/415); không 500.<br>[DB]: MR1 vẫn `PENDING`. | Abnormal |
| IT_B6_057 | MR1 `PENDING` | return với Reason dài 500 / 501 ký tự có dấu | [API]: 500 → 200; 501 → 400 (không 500 do truncation `nvarchar(500)`).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B6_058 | MR3 đã xác nhận | return MR3 | [API]: 409.<br>[DB]: MR3 và tồn kho không đổi (không có giao dịch đảo chiều). | Abnormal |
| IT_B6_059 | MR1 `PENDING` | Gửi **đồng thời** confirm và return cho MR1 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: MR1 ở **một** trạng thái cuối duy nhất; chỉ khi trạng thái cuối là đã xác nhận thì tồn kho mới tăng. | Boundary |
| IT_B6_060 | MR1 đã xác nhận | `GET M/MaterialReceipts/<MR1>`; `GET WK/MaterialReceipts/<MR1>` | [API]: 200; `ConfirmedByName` = tên của E_MGR; `Lines[].MaterialCode/Name/Unit/MaterialColor` đầy đủ.<br>[DB]: khớp join với `Employee` và `Material`. | Normal |
| IT_B6_061 | Seed bằng SQL một `MaterialReceiptLine` có `MaterialId IS NULL` (DB cho phép, DTO lại non-null) | `GET M/MaterialReceipts/<phiếu đó>` | [API]: 200, **không 500** khi map NULL sang `int`; `MaterialId` được lấy từ OM/POL hoặc trả 0 `[?]`.<br>[DB]: không đổi. Nếu API trả 500 → ghi finding. | Boundary |

## 5. Phiếu nhập bổ sung — `POST WK/MaterialReceipts/supplementary`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_062 | SD2 `REPLENISH`, RL2 giao bù 40 | `POST WK/MaterialReceipts/supplementary {ShortageRequestId:<SD2>, ReceiptDate:today, Note:"Bù đợt 1", Lines:[{RequestLineId:<RL2>, AcceptedQty:40}]}` | [API]: 200; `ReceiptType` = "Bổ sung" / `Supplementary`; `ShortageRequestId=SD2`; có `OriginalReceiptId` `[?]`.<br>[DB]: `MaterialReceipt` mới với `ShortageRequestId=SD2`, `Status='PENDING'`, `Source` computed theo PO gốc; dòng có `OriginalReceiptLineId` trỏ dòng của phiếu gốc `[?]`; tồn kho **chưa** đổi. | Normal |
| IT_B6_063 | Phiếu bổ sung của IT_B6_062 đang `PENDING` | confirm phiếu bổ sung đó | [API]: 200; `GET M/ShortDeliveries/<SD2>` có `ReplenishedQty=40` cho RL2.<br>[DB]: tồn kho +40; có thẻ kho; `Request` SD2 chuyển sang hoàn tất (`CompletedAt` khác NULL) nếu đã bù đủ `[?]`; **không** sinh thêm `SHORT_DELIVERY` mới. | Normal |
| IT_B6_064 | SD1 chưa được xử lý; SD3 là `NO_REPLENISH` | supplementary với `ShortageRequestId:<SD1>`; với `<SD3>` | [API]: 400/409 với cả hai (chỉ phiếu `REPLENISH` mới được giao bù).<br>[DB]: không có phiếu mới. | Abnormal |
| IT_B6_065 | RL9 thuộc SD9 | supplementary SD2 với dòng `RequestLineId:<RL9>` | [API]: 400/404 (dòng thuộc yêu cầu khác, là IDOR chéo yêu cầu).<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B6_066 | RL2 giao bù 40 | `AcceptedQty:41` (vượt số giao bù); `0`; `-1` | [API]: 41 → 400 `[?]` (hoặc 200 kèm cảnh báo); 0 / −1 → 400; không 500 (`trg_RequestLine_Check` / `trg_MaterialReceiptLine_Check`).<br>[DB]: không có dòng `AcceptedQty ≤ 0`. | Boundary |
| IT_B6_067 | TK_WK | Hai dòng cùng `RequestLineId:<RL2>`; `Lines:[]`; `ShortageRequestId:999999` | [API]: 400/409; 400; 404.<br>[DB]: Rollback, không đổi. | Abnormal |
| IT_B6_068 | TK_WK | supplementary với `ReceiptDate:"0001-01-01"`; Note dài 1001 ký tự | [API]: 400 với cả hai.<br>[DB]: không đổi. | Boundary |
| IT_B6_069 | RL2 giao bù 40 | Gửi **đồng thời** 2 request supplementary, mỗi request 40 cho RL2, rồi confirm cả hai | [API]: tạo phiếu có thể đều 200; khi confirm thì phiếu thứ hai phải 400/409 (đã bù đủ) `[?]`; không 500.<br>[DB]: `ReplenishedQty` không vượt 40; tồn kho chỉ tăng đúng số đã được chấp nhận. | Boundary |

## 6. Thiếu giao — `ShortDeliveries` (Manager: xử lý; WK: chỉ xem các yêu cầu đã xử lý)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_070 | S0 | `GET M/ShortDeliveries`; `?AwaitingReplenish=true`; `?ResolvedOnly=true` | [API]: 200; không lọc → SD1, SD2, SD3; AwaitingReplenish → SD2; ResolvedOnly → SD2, SD3.<br>[DB]: khớp `Request WHERE RequestTypeCode='SHORT_DELIVERY'` và các điều kiện tương ứng. | Normal |
| IT_B6_071 | S0 | `GET WK/ShortDeliveries?ResolvedOnly=false` | [API]: 200; **chỉ** có SD2, SD3 (controller ép `ResolvedOnly=true`, bỏ qua giá trị client gửi); không có SD1.<br>[DB]: không đổi. | Boundary |
| IT_B6_072 | SD1 chưa xử lý | `GET WK/ShortDeliveries/<SD1>` | [API]: 404 (nếu Service áp cùng bộ lọc) hoặc 200 `[?]`; ghi lại. Nếu 200 thì danh sách và chi tiết **không nhất quán** về phạm vi dữ liệu → ghi finding.<br>[DB]: không đổi. | Abnormal |
| IT_B6_073 | SD1 chưa xử lý (RL1 thiếu 100) | `POST M/ShortDeliveries/<SD1>/resolve {Resolution:"REPLENISH", Lines:[{RequestLineId:<RL1>, ReplenishQty:100}], Note:"NCC giao bù", ExpectedDeliveryCount:2, ExpectedDeliveryDates:[today+5, today+10]}` | [API]: 200.<br>[DB]: SD1 có `ShortageResolution='REPLENISH'`, `Status` = đã xử lý `[?]`, `ExpectedDeliveryCount=2`, `TargetDate=today+5`, `TargetDateTo=today+10`, `ResultNote` được lưu, `DecidedAt` ≈ hiện tại, RowVer đổi; `RL1.ReplenishQty=100.000`; lịch giao trong `DeliveryScheduleDate` (`RequestId=SD1`, SeqNo 1, 2) `[?]`. | Normal |
| IT_B6_074 | SD1 chưa xử lý | resolve `{Resolution:"NO_REPLENISH", Note:"Chấp nhận thiếu"}` | [API]: 200.<br>[DB]: `ShortageResolution='NO_REPLENISH'`; `CompletedAt` khác NULL `[?]`; `RL1.ReplenishQty` NULL hoặc 0; không có lịch giao. | Normal |
| IT_B6_075 | TK_MGR | `Resolution:"Replenish"`; `"PARTIAL"`; `""` | [API]: `Replenish` → 400 hoặc chuẩn hóa `[?]`; còn lại → 400.<br>[DB]: không tồn tại `ShortageResolution` nào ngoài {`REPLENISH`, `NO_REPLENISH`}. | Boundary |
| IT_B6_076 | TK_MGR | `REPLENISH` với `ExpectedDeliveryCount`: `null`; `0`; `1`; `99`; `100`; `-1`; `32768` | [API]: null / 0 / 100 / −1 → **400** (bắt buộc, trong khoảng 1–99); 1 / 99 → 200 (kèm đúng số ngày tương ứng); 32768 → 400 (tràn `short`); không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B6_077 | TK_MGR | `ExpectedDeliveryCount:2` nhưng chỉ gửi 1 ngày; gửi 2 ngày trùng nhau; một ngày là `0001-01-01`; một ngày trong quá khứ | [API]: 400 với từng ca (`0001-01-01` tuyệt đối không được lưu); quá khứ → 400 `[?]`.<br>[DB]: SD1 không đổi. | Boundary |
| IT_B6_078 | RL1 thiếu 100 | `ReplenishQty:101`; `0`; `-1`; `1000000000000000` | [API]: 101 → 400 (vượt số thiếu) `[?]`; 0 / âm → 400; tràn → 400; không 500 (`trg_RequestLine_Check`).<br>[DB]: không đổi. | Boundary |
| IT_B6_079 | RL9 thuộc SD9 | resolve SD1 với `Lines:[{RequestLineId:<RL9>, …}]` | [API]: 400/404 (IDOR chéo yêu cầu).<br>[DB]: RL9 và SD9 không đổi. | Abnormal |
| IT_B6_080 | TK_MGR | `REPLENISH` với `Lines:null` / `[]`; thiếu dòng RL1 | [API]: 400 (giao bù phải có số lượng) `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B6_081 | TK_MGR | Note (lưu vào `ResultNote`, nvarchar(1000)) dài 1000 / 1001 ký tự | [API]: 1000 → 200; 1001 → 400.<br>[DB]: ca 1000 ký tự lưu đủ. | Boundary |
| IT_B6_082 | SD3 đã đóng (`NO_REPLENISH`) | resolve SD3 | [API]: 409/400.<br>[DB]: không đổi. | Abnormal |
| IT_B6_083 | SD2 đang `REPLENISH` và đã giao bù một phần | resolve SD2 lần nữa với `REPLENISH`, `ExpectedDeliveryCount:1` | [API]: 200 (chọn giao bù thêm) hoặc 409 `[?]`.<br>[DB]: nếu 200 thì `Request.ExpectedDeliveryCount` được **cộng dồn** (1 → 2), không phải ghi đè bằng giá trị gửi lên. | Boundary |
| IT_B6_084 | SD1 chưa xử lý | Gửi **đồng thời** 2 request resolve SD1 (`REPLENISH` và `NO_REPLENISH`) | [API]: 1 request 200, 1 request 409 (dựa trên RowVer của `Request`); không 500.<br>[DB]: SD1 có đúng **một** phương án xử lý; không có lịch giao mồ côi. | Boundary |
| IT_B6_085 | TK_MGR | resolve `id=999999`; resolve id của một `Request` không phải loại `SHORT_DELIVERY` | [API]: 404 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B6_086 | Fault-injection: INSERT lịch giao lỗi khi resolve `REPLENISH` | resolve hợp lệ | [API]: lỗi có kiểm soát.<br>[DB]: **Rollback**: SD1 vẫn chưa xử lý; `RL1.ReplenishQty` không đổi. | Abnormal |
| IT_B6_087 | MR3 đã có SD1 | Chèn trực tiếp bằng SQL một `Request` `SHORT_DELIVERY` thứ hai cho MR3 | [API]: —<br>[DB]: bị `UX_Request_Receipt` chặn (chứng minh ràng buộc mỗi phiếu nhập gốc tối đa 1 yêu cầu thiếu giao); bảo đảm luồng xác nhận phiếu không thể sinh trùng (xem IT_B6_048). | Boundary |

## 7. `IncomingMaterials` và export Excel (đối chiếu dữ liệu suy ra)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_088 | S0 | `GET WK/IncomingMaterials` | [API]: 200; `Customer[]` có NVL khách cấp còn chờ nhận của OD2 (OM1, phần khách cấp của OM2); `Supplier[]` có các dòng của PO1/PO2 còn chờ nhận; `ClosedShort[]` có các PO đã đóng phần thiếu (SD3).<br>[DB]: số lượng còn phải nhận khớp với `CustomerQty` (hoặc `OrderedQty`) trừ `SUM(AcceptedQty)` của các phiếu **đã xác nhận** (phiếu `PENDING`/đã trả lại có được tính hay không `[?]`, ghi lại). | Normal |
| IT_B6_089 | Sau khi confirm MR1 (IT_B6_044) | `GET WK/IncomingMaterials` | [API]: số còn chờ nhận của OM1 giảm đi 100.<br>[DB]: khớp với DB sau khi xác nhận. | Normal |
| IT_B6_090 | Sau khi trả lại MR1 (IT_B6_055) | `GET WK/IncomingMaterials` | [API]: số còn chờ nhận của OM1 **không** bị trừ bởi phiếu đã trả lại.<br>[DB]: nhất quán. | Boundary |
| IT_B6_091 | OM2 cần 500, khách cấp 200 | Đối chiếu `GET M/MaterialReceipts?OrderId=<OD2>&Source=CUSTOMER` với các cột computed của OM2 | [API]: các phiếu khách cấp được lọc đúng.<br>[DB]: `SELECT PurchaseQty, Source FROM OrderMaterial WHERE OrderMaterialId=OM2` = (300.000, `BOTH`); `MaterialReceipt.Source` của các phiếu khớp công thức computed. | Normal |
| IT_B6_092 | MR1 tồn tại | `GET M/MaterialReceipts/<MR1>/excel`; `GET WK/MaterialReceipts/<MR1>/excel` | [API]: 200; `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; nội dung là file .xlsx mở được, số dòng khớp với phiếu (không parse như JSON).<br>[DB]: không đổi. | Normal |
| IT_B6_093 | TK_MGR | `GET M/MaterialReceipts/999999/excel`; `/abc/excel` | [API]: 999999 → 404 với body **JSON** `{Success:false, ErrorCode}` (không trả file rỗng); abc → 404.<br>[DB]: không đổi. | Abnormal |
| IT_B6_094 | TK_ADM | `GET WK/MaterialReceipts/<MR1>/excel` | [API]: 403.<br>[DB]: không đổi. | Abnormal |

## 8. Danh sách phiếu và phân trang

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_095 | TK_MGR | `GET M/MaterialReceipts?Status=PENDING`; `?Status=Pending`; `?Status=Xyz` | [API]: dạng HOA → MR1, MR2; dạng Pascal → cùng kết quả hoặc rỗng (DTO truyền nguyên chuỗi, ghi lại); Xyz → rỗng hoặc 400; không 500.<br>[DB]: khớp `WHERE Status='PENDING'`. | Boundary |
| IT_B6_096 | TK_MGR | `?Source=SUPPLIER&PurchaseOrderId=<PO2>`; `?OrderId=<OD2>`; `?Source=supplier` | [API]: lọc đúng; chữ thường → cùng kết quả hoặc rỗng `[?]`.<br>[DB]: khớp cột computed `Source`. | Normal |
| IT_B6_097 | TK_WK | `PageSize`: `0`; `1`; `100`; `101`; `-1`. `Page`: `0`; `-5` | [API]: 200 với mọi ca (`SafePageSize`: < 1 → 20, > 100 → 100; `SafePage` ≥ 1), hoặc 400 nếu `PageQueryValidatorBase` được áp dụng `[?]`; hành vi phải nhất quán giữa `M/` và `WK/`.<br>[DB]: không đổi. | Boundary |
| IT_B6_098 | TK_MGR | `Keyword` dài 100 / 101 ký tự; `Keyword:"' OR 1=1--"`; `SortDir=ASC` / `xyz` | [API]: 101 ký tự → 400 hoặc chấp nhận `[?]`; injection → rỗng, không 500; `ASC` → tăng dần; `xyz` → giảm dần.<br>[DB]: bảng còn nguyên. | Boundary |
| IT_B6_099 | TK_MGR | `GET M/MaterialReceipts/999999`; `GET M/ShortDeliveries/999999` | [API]: 404.<br>[DB]: không đổi. | Abnormal |

## 9. Attachments — `C/Attachments` (upload, IDOR, xóa mềm)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_100 | TK_WK | `POST C/Attachments` multipart `{ownerType:"MaterialReceipt", ownerId:<MR2>, purpose:"Photo", file:img_ok.jpg}` | [API]: 200; `AttachmentDto{AttachmentId (long), OwnerType, OwnerId, Purpose, FileName:"img_ok.jpg", ContentType:"image/jpeg", UploadedAt}`; **không** trả `FileUrl`.<br>[DB]: 1 dòng `Attachment` với `IsCurrent=1`, `RemovedAt IS NULL`, `FileUrl` khác rỗng, `UploadedAt` ≈ hiện tại. | Normal |
| IT_B6_101 | TK_WK | Multipart **không có** `file` (file == null) | [API]: **400**; không 500 / NullReference (controller chuyển nội dung rỗng và `FileName=""` xuống Service).<br>[DB]: không có `Attachment` mới; storage không có file rác. | Abnormal |
| IT_B6_102 | TK_WK | `file=zero.jpg` (0 byte) | [API]: 400.<br>[DB]: không đổi. | Boundary |
| IT_B6_103 | TK_WK | `file=img_5mb.jpg` (5 MB − 1 KB); `file=img_5mb1.jpg` (5 MB + 1 B) | [API]: 5 MB − 1 KB → 200; 5 MB + 1 B → **400** (giới hạn 5 MB do Service kiểm) `[?]`.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B6_104 | TK_WK | `file=img_6mb1.jpg` (vượt `RequestSizeLimit` 6 MB) | [API]: **413** (hoặc 400); không 500.<br>[DB]: không đổi. | Boundary |
| IT_B6_105 | TK_WK | `file=fake.jpg` (.exe đổi đuôi); `file=note.txt` | [API]: 400 (kiểm nội dung/MIME) `[?]`.<br>[DB]: không đổi. | Abnormal |
| IT_B6_106 | TK_WK | `ownerType:"Xyz"`; `ownerType:""`; ownerType dài 31 ký tự | [API]: 400; không 500 (cột `varchar(30)`).<br>[DB]: không đổi. | Boundary |
| IT_B6_107 | TK_WK | `ownerId:999999` (không có FK vì là quan hệ đa hình); `ownerId:0`; `ownerId:-1`; `ownerId:"abc"` | [API]: 999999 → 404/400 (Service phải tự kiểm owner tồn tại; nếu 200 → ghi finding vì tạo được ảnh mồ côi); 0 / −1 / abc → 400.<br>[DB]: không có `Attachment` trỏ owner không tồn tại. | Abnormal |
| IT_B6_108 | TK_WK | `purpose:"Photo"`; không gửi `purpose`; `purpose:"PHOTO"`; purpose dài 31 ký tự | [API]: 3 ca đầu → 200; 31 ký tự → 400.<br>[DB]: `Purpose` được lưu **nhất quán** (`PHOTO` viết HOA theo DB default, hoặc `Photo` nếu không chuẩn hóa `[?]`; ghi lại). Lệch giữa controller (`"Photo"`) và DB (`'PHOTO'`) là điểm cần xác nhận. | Boundary |
| IT_B6_109 | AT1 là ảnh hiện hành của (MaterialReceipt, MR1, PHOTO) | Upload ảnh mới cho cùng owner và purpose | [API]: 200.<br>[DB]: ảnh mới có `IsCurrent=1`; AT1 chuyển `IsCurrent=0`, `RemovedAt` ≈ hiện tại `[?]` (hoặc cả hai cùng `IsCurrent=1` nếu cho phép nhiều ảnh, ghi lại). | Normal |
| IT_B6_110 | AT1 hiện hành; AT3 đã gỡ | `GET C/Attachments?ownerType=MaterialReceipt&ownerId=<MR1>` | [API]: 200; có AT1, **không** có AT3 (`IsCurrent=0`).<br>[DB]: khớp `WHERE OwnerType=… AND OwnerId=… AND IsCurrent=1`. | Normal |
| IT_B6_111 | TK_WK | `GET C/Attachments/<AT1>/content` | [API]: 200; trả file binary, `Content-Type` = `ContentType` đã lưu, header `Cache-Control: private, max-age=3600`.<br>[DB]: không đổi. | Normal |
| IT_B6_112 | TK_WK | `GET …/999999999999/content`; `GET …/abc/content`; `GET …/<AT3>/content` (đã gỡ) | [API]: 404; 404; AT3 → 404 hoặc 200 `[?]` (ghi lại); lỗi trả JSON, không trả file rỗng.<br>[DB]: không đổi. | Abnormal |
| IT_B6_113 | AT2 là chứng từ thanh toán của PO3 (do TK_ACC upload) | TK_WRK gửi `GET C/Attachments?ownerType=PurchaseOrder&ownerId=<PO3>` và `GET C/Attachments/<AT2>/content` | [API]: **403/404** (IDOR: công nhân không được xem chứng từ thanh toán).<br>[DB]: không đổi. Nếu trả 200 kèm nội dung file → ghi **finding** IDOR (Controller chỉ có `[Authorize]`). | Abnormal |
| IT_B6_114 | AT1 do TK_WK upload cho MR1 | TK_WRK gửi `DELETE C/Attachments/<AT1>`; TK_TL gửi DELETE AT2 | [API]: **403/404**.<br>[DB]: AT1, AT2 vẫn `IsCurrent=1`. Nếu xóa được → ghi **finding** IDOR. | Abnormal |
| IT_B6_115 | MR3 đã xác nhận (chứng từ của phiếu đã chốt) | TK_WRK upload ảnh cho `(MaterialReceipt, MR3)` | [API]: 403/400 `[?]` (không được ghi đè chứng từ của phiếu đã chốt bằng tài khoản không có quyền).<br>[DB]: ảnh hiện hành của MR3 không bị đổi `IsCurrent`. Nếu thành công → ghi **finding**. | Abnormal |
| IT_B6_116 | AT1 hiện hành | `DELETE C/Attachments/<AT1>` bằng TK_WK | [API]: 200.<br>[DB]: **xóa mềm**: dòng vẫn còn với `IsCurrent=0`, `RemovedAt` ≈ hiện tại (giờ VN); file vật lý chưa bị xóa (do job dọn sau 15 ngày). | Normal |
| IT_B6_117 | AT1 đã bị gỡ | DELETE AT1 lần nữa; DELETE `999999999999`; DELETE `abc` | [API]: 404 hoặc 409 (đã gỡ) / 404 / 404; không 500.<br>[DB]: `RemovedAt` của AT1 **không** bị ghi đè. | Abnormal |
| IT_B6_118 | TK_WK | `id` = `9223372036854775807` (giá trị long lớn nhất); `9223372036854775808` (tràn long) | [API]: 404; tràn → 404 (route `:long` không khớp); không 500.<br>[DB]: không đổi. | Boundary |
| IT_B6_119 | (MaterialReceipt, MR2) chưa có ảnh | Gửi **đồng thời** 3 request upload cho cùng owner và purpose | [API]: cả 3 đều 200; không 500.<br>[DB]: nếu thiết kế là 1 ảnh hiện hành thì đúng **1** dòng `IsCurrent=1`, 2 dòng còn lại `IsCurrent=0` (không có tình trạng nhiều ảnh cùng là hiện hành do race). | Boundary |
| IT_B6_120 | Fault-injection: lưu file vào storage thành công nhưng INSERT `Attachment` lỗi | upload hợp lệ | [API]: lỗi có kiểm soát.<br>[DB]: không có dòng `Attachment`; ảnh cũ **không** bị chuyển `IsCurrent=0` (không gỡ ảnh cũ khi ảnh mới chưa lưu được); file mồ côi trong storage được ghi nhận `[?]`. | Abnormal |

## 10. Thẻ kho append-only và kiểm tra chéo

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B6_121 | Có thẻ kho của MR1 (sau IT_B6_044) | Chạy trực tiếp bằng SQL: `UPDATE StockTransaction SET QtyChange=0 WHERE …`; `DELETE FROM StockTransaction WHERE …`; `INSERT` không đi qua `usp_Stock_Post` | [API]: —<br>[DB]: cả 3 lệnh **bị chặn** bởi `trg_StockTransaction_AppendOnly` / `PostOnly` (chứng minh API không có đường nào sửa thẻ kho). | Boundary |
| IT_B6_122 | Sau chuỗi xác nhận ở mục 4 và 5 | Với mỗi `StockBalance`: so `OnHandQty` với `SUM(QtyChange)` của các thẻ kho thuộc dòng đó, và `BalanceAfter` của thẻ kho mới nhất | [API]: —<br>[DB]: `OnHandQty` = tổng thay đổi tính từ số dư đầu kỳ = `BalanceAfter` mới nhất; chuỗi `BalanceAfter` tăng đúng theo `QtyChange` (không lệch do race ở IT_B6_047). | Normal |
| IT_B6_123 | Đã chạy các ca ở mục 2–9 | `SELECT DISTINCT` `MaterialReceipt.Status`, `Source`; `Request.Status`, `ShortageResolution`, `RequestTypeCode`; `Attachment.Purpose` | [API]: —<br>[DB]: mọi giá trị đều VIẾT HOA (`PENDING`, `CUSTOMER`, `SUPPLIER`, `REPLENISH`, `NO_REPLENISH`, `SHORT_DELIVERY`, `PHOTO`, cùng các giá trị `[?]` khác); nếu có `Photo` lẫn `PHOTO` → ghi finding. | Normal |
| IT_B6_124 | Ghi `MAX(AuditLogs.Id)`, `MAX(StockTransactionId)` trước khi chạy | Chạy các ca thất bại IT_B6_021, IT_B6_032, IT_B6_046, IT_B6_049, IT_B6_078, IT_B6_101 | [API]: 4xx đúng như từng ca.<br>[DB]: hai giá trị MAX không đổi; không có dữ liệu nửa vời ở `MaterialReceipt`, `MaterialReceiptLine`, `StockBalance`, `Request`, `RequestLine`, `Attachment`. | Abnormal |
| IT_B6_125 | Trigger `trg_MaterialReceiptLine_Check`, `trg_StockBalance_Check`, `trg_RequestLine_Check` đang bật | Chạy lại IT_B6_023, IT_B6_047, IT_B6_048, IT_B6_078, IT_B6_119 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; body không chứa tên trigger hay `SqlException`.<br>[DB]: không có post trùng thẻ kho; không có `SHORT_DELIVERY` trùng; không có nhiều ảnh cùng là hiện hành. | Boundary |
