# Integration Test Cases — Batch 7: Lệnh cắt & bàn cắt & bàn giao BTP (`IT_B7_xxx`)

Nguồn: `IT_CONTRACT — Batch 7`. Phân loại theo ISTQB: **Normal** · **Abnormal** · **Boundary**. Bộ này chỉ kiểm tra tương tác **API ↔ Auth ↔ DB**.

> **Ghi chú phạm vi**
> - **Không có upload hay import:** batch này không có endpoint nhận `IFormFile`. Các ca "thiếu file / file rỗng" không áp dụng; thay vào đó là ca body null hoặc JSON sai (IT_B7_131).
> - **Không có `RowVer`:** cả 7 bảng của batch đều không có cột này. Ca concurrency kiểm UNIQUE, trigger và trạng thái; ca sửa đồng thời dùng để phát hiện ghi đè lẫn nhau (finding).
> - **View:** `VwWorkOrderLineProgress` (chỉ biết qua tên file config) là nguồn đối chiếu cho `CutQty` / `HandedQty`.

## 0. Quy ước chung

**HTTP kỳ vọng**
- Response thành công: `200 {Success:true, Data, Message}`.
- Response lỗi: `{Success:false, ErrorCode, Message, Errors?}`, map theo hợp đồng: Validation → **400**, Unauthorized → **401**, Forbidden → **403**, NotFound → **404**, Conflict → **409**.
- **403 của `[CuttingTeamOnly]`** phải có `ErrorCode = WarehouseCuttingTeamOnly`.
- `{id:int}` sai kiểu (ví dụ `abc`) → **404 từ routing** (không có body `ApiResponses`). Body null hoặc JSON sai → **400 từ `[ApiController]`**.
- **Bất kỳ `500` nào cũng là FAIL.** Body lỗi không được lộ tên trigger/constraint (`trg_*`, `UQ_*`), `SqlException` hay stack trace.

**Quy ước cho vế [DB]**
- Giá trị trạng thái lưu và trả về dạng **UPPER_SNAKE**:
  - `WorkOrder.Status`: `OPEN` / `CUTTING` / `COMPLETED` / `CANCELLED`.
  - `CuttingLay.Status`: `RECORDED` / `LOCKED` (cột `varchar(10)`).
  - `CuttingLay.ShiftCode`: `MORNING` / `AFTERNOON` / `OVERTIME`.
  - `CutHandover.Status`: `PENDING` / `CONFIRMED` / `REJECTED` / `CANCELLED`.
- **Ngoại lệ:** `FabricChecks[].Status` dùng **PascalCase** (`Enough` / `Short` / `Unknown`) và không được lưu DB.
- "DB không đổi" = snapshot các bảng `WorkOrder`, `WorkOrderLine`, `CuttingLay`, `CuttingLayLine`, `CuttingLayFabric`, `CutHandover`, `CutHandoverLine`, `StockBalance`, `StockTransaction`, `Request`, `AuditLogs` trước và sau khi gọi trùng nhau.
- Các cột ngày (`DueDate`, `WorkDate`) không bao giờ được là `0001-01-01`. `CreatedAt`, `HandedAt`, `ConfirmedAt`, `CompletedAt` phải ≈ giờ VN (`fn_VnNow()`).

**Viết tắt URL** (segment dạng kebab-case như hợp đồng giả định `[?]`): `M/` = `/api/v1/manager/`, `TL/` = `/api/v1/team-leader/`, `WK/` = `/api/v1/warehouse-keeper/`.

**Dữ liệu nền S0** (reset trước mỗi test)

**Team và token**
- Team: T_CUT01, T_CUT02 (`CUTTING`, `ACTIVE`); T_SEW01 (`SEWING`); T_CUTOLD (`CUTTING`, đã ngừng).
- Token có tổ: TK_TL1 (TeamLeader T_CUT01), TK_TD1 (TeamDeputy T_CUT01), TK_TL2 (TeamLeader T_CUT02), TK_TLSEW (TeamLeader T_SEW01).
- Token không tổ: TK_TLNONE (role TeamLeader nhưng `TeamId` NULL).
- Token khác: TK_ADM, TK_MGR, TK_WK (EmployeeId E_WK), TK_ACC, TK_QC, TK_WRK, TK_EXP (hết hạn), TK_FORGED (sai chữ ký).

**Đơn khách và định mức**
- OD2 (đang sản xuất, PR1):
  - OL3 (Đen, M, đặt 200).
  - OL4 (Đen, L, đặt 100).
  - Định mức: PCM1 (Thân trước, Đen, vải MA1); PCM2 (Túi, Đen, vải MA1).
- OD1: `NEW`, chưa sản xuất.
- OD3: đã hủy.
- OD4: thuộc PR2, có OL9.

**WorkOrder (lệnh cắt)**

| Alias | Mã | Đơn | Tổ cắt | Status | Dòng | Ghi chú |
|---|---|---|---|---|---|---|
| WO1 | LC-0001 | OD2 | T_CUT01 | `OPEN` | WOL1 (OL3, 120, PCM NULL); WOL2 (OL4, 60, PCM NULL) | `FabricTolerancePercent=3.00` |
| WO2 | LC-0002 | OD2 | T_CUT01 | `CUTTING` | WOL3 (OL3, 50, NULL) | Có bàn cắt và phiếu bàn giao, chi tiết bên dưới |
| WO3 | LC-0003 | — | T_CUT02 | `OPEN` | — | Lệnh của tổ khác; có bàn CL9 và phiếu CH9 |
| WO4 | — | OD2 | T_CUT01 | `COMPLETED` | — | |
| WO5 | — | OD2 | T_CUT01 | `CANCELLED` | — | |

Bàn cắt và phiếu bàn giao của WO2:
- **CL1:** today, `MORNING`, seq 1, `RECORDED`. Dòng: OL3 có `ActualQty=40`. Vải: (MA1, `UsedM=30.50`).
- **CL_LK:** bàn cắt đã `LOCKED`.
- **CH1** `HB-0001`: `PENDING`, dòng CHL1 (OL3, giao 30, `ConfirmedQty` NULL).
- **CH2:** `CONFIRMED`, giao 10, xác nhận 10.

**Request (yêu cầu cắt bù):** RQ1 là yêu cầu cắt bù đã duyệt cho OL3, số lượng 5 `[?]`; RQ2 là yêu cầu đã có lệnh cắt bù.

---

## 1. Phân quyền theo prefix và `[CuttingTeamOnly]`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_001 | S0 | Không gửi token: `GET M/work-orders`, `GET TL/cutting-lays`, `GET WK/cut-handovers` | [API]: 401 với cả 3.<br>[DB]: không đổi. | Abnormal |
| IT_B7_002 | TK_EXP, TK_FORGED | `POST WK/cut-handovers/<CH1>/confirm {…}` | [API]: 401.<br>[DB]: CH1 vẫn `PENDING`; `ConfirmedQty` NULL. | Abnormal |
| IT_B7_003 | TK_ADM | `GET WK/work-orders`; `GET WK/cut-handovers`; `POST WK/cut-handovers/<CH1>/confirm {…}`; `POST WK/cut-handovers/<CH1>/reject {…}` | [API]: **403** với cả 4 (prefix warehouse-keeper không gồm Admin).<br>[DB]: CH1 không đổi. | Abnormal |
| IT_B7_004 | TK_ADM, TK_MGR | `GET TL/work-orders`; `POST TL/cutting-lays {…}`; `POST TL/cut-handovers {…}` | [API]: 403 từ `[Authorize]` (role không khớp; Admin không có quyền bao trùm).<br>[DB]: không có bàn cắt hay phiếu mới. | Abnormal |
| IT_B7_005 | TK_ADM | `GET M/work-orders`; `POST M/work-orders {…}` | [API]: 200 (Admin được gọi `/manager/*`).<br>[DB]: thay đổi đúng như ca Normal tương ứng. | Normal |
| IT_B7_006 | TK_WK | `POST M/work-orders {…}`; `GET TL/cutting-lays`; `POST TL/cut-handovers {…}` | [API]: 403 với cả 3.<br>[DB]: không đổi. | Abnormal |
| IT_B7_007 | TK_TL1 | `GET M/work-orders`; `POST M/work-orders/<WO1>/cancel`; `GET WK/cut-handovers`; `POST WK/cut-handovers/<CH1>/confirm {…}` | [API]: 403 với cả 4 (tổ trưởng cắt không được tự hủy lệnh hay tự xác nhận phiếu bàn giao của tổ mình).<br>[DB]: WO1 vẫn `OPEN`; CH1 vẫn `PENDING`. | Abnormal |
| IT_B7_008 | TK_TLSEW (TeamLeader tổ SEWING) | `GET TL/work-orders`; `GET TL/cutting-lays`; `POST TL/cutting-lays {…}`; `POST TL/cut-handovers {…}` | [API]: **403** với `ErrorCode="WarehouseCuttingTeamOnly"` và message `CuttingTeamOnly`.<br>[DB]: không có bàn cắt hay phiếu mới. | Abnormal |
| IT_B7_009 | TK_TLNONE (TeamLeader không gắn tổ) | `GET TL/work-orders`; `POST TL/cutting-lays {…}` | [API]: **403** (`WarehouseCuttingTeamOnly`, vì `TeamTypeCode` khác `CUTTING`); không 500 do `TeamId` NULL.<br>[DB]: không đổi. | Boundary |
| IT_B7_010 | TK_TD1 (Deputy T_CUT01) | `GET TL/work-orders`; `POST TL/cutting-lays {…hợp lệ cho WO1}` | [API]: 200 (Deputy có quyền như Leader).<br>[DB]: có bàn cắt mới với `CuttingTeamId=T_CUT01`. | Normal |
| IT_B7_011 | TK_ACC, TK_QC, TK_WRK (lần lượt) | `GET M/work-orders`; `GET TL/work-orders`; `GET WK/work-orders` | [API]: 403 với mọi tổ hợp.<br>[DB]: không đổi. | Abnormal |
| IT_B7_012 | TK_TL1 | `PUT TL/cut-handovers/<CH1> {…}`; `DELETE TL/cutting-lays/<CL1>`; `POST TL/work-orders {…}` | [API]: 404/405 (không có route).<br>[DB]: không đổi. | Abnormal |
| IT_B7_013 | TK_MGR | `GET /api/v1/manager/WorkOrders`; `GET /api/v1/manager/work-orders` | [API]: một trong hai dạng trả 200, dạng kia trả 200 hoặc 404 tùy route transformer `[?]`; ghi lại dạng đúng để dùng cho toàn bộ bộ test.<br>[DB]: không đổi. | Boundary |

## 2. Cách ly dữ liệu theo tổ (IDOR)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_014 | TK_TL1 (T_CUT01) | `GET TL/work-orders` | [API]: 200; chỉ có WO1, WO2, WO4, WO5 (lệnh của T_CUT01); **không** có WO3.<br>[DB]: khớp `WorkOrder WHERE CuttingTeamId=T_CUT01`. | Normal |
| IT_B7_015 | TK_TL1 | `GET TL/work-orders/<WO3>`; `GET TL/work-orders/<WO3>/material-allowance` | [API]: **403/404**; body không có `WorkOrderCode`/`Lines` của WO3.<br>[DB]: không đổi. | Abnormal |
| IT_B7_016 | TK_TL1 | `GET TL/cutting-lays/<CL9>`; `PUT TL/cutting-lays/<CL9> {…}` | [API]: 403/404.<br>[DB]: CL9 và các dòng của nó không đổi. | Abnormal |
| IT_B7_017 | TK_TL1 | `POST TL/cutting-lays {WorkOrderId:<WO3>, …}` (ghi bàn cắt vào lệnh của tổ khác) | [API]: 403/404/400.<br>[DB]: không có `CuttingLay` nào gắn WO3 với `CuttingTeamId=T_CUT01`. | Abnormal |
| IT_B7_018 | TK_TL1 | `GET TL/cut-handovers/<CH9>`; `POST TL/cut-handovers/<CH9>/cancel`; `POST TL/cut-handovers {WorkOrderId:<WO3>, …}`; `GET TL/cut-handovers/handoverable?workOrderId=<WO3>` | [API]: 403/404 (handoverable → rỗng hoặc 403/404).<br>[DB]: CH9 không đổi; không có phiếu mới cho WO3. | Abnormal |
| IT_B7_019 | TK_TL1 | `GET TL/cutting-lays?WorkOrderId=<WO3>`; `GET TL/cut-handovers?WorkOrderId=<WO3>` | [API]: 200 với danh sách **rỗng**; không lộ CL9 hay CH9.<br>[DB]: không đổi. | Abnormal |
| IT_B7_020 | TK_TLNONE nếu vượt qua được filter `[?]`; hoặc giả lập user có `TeamId=0` | `GET TL/work-orders` | [API]: 403, hoặc 200 với danh sách rỗng; **không** trả lệnh của mọi tổ.<br>[DB]: không đổi. | Boundary |
| IT_B7_021 | TK_MGR, TK_WK | `GET M/work-orders/<WO3>`; `GET WK/work-orders/<WO3>`; `GET WK/cut-handovers/<CH9>` | [API]: 200 (manager và WK thấy mọi tổ); dữ liệu của `M/` và `WK/` cho cùng một lệnh **giống nhau**.<br>[DB]: không đổi. | Normal |
| IT_B7_022 | TK_TL1 sau khi được chuyển sang T_CUT02 (cập nhật DB, rồi login lại) | `GET TL/work-orders` bằng token **cũ**; rồi bằng token mới | [API]: token cũ → phạm vi theo `TeamId` trong claim cũ hay theo DB `[?]` (ghi lại; nếu vẫn thấy dữ liệu của T_CUT01 → finding); token mới → chỉ thấy lệnh của T_CUT02.<br>[DB]: không đổi. | Boundary |

## 3. Lệnh cắt — tạo và sửa (`M/work-orders`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_023 | TK_MGR | `POST M/work-orders {OrderId:<OD2>, ProductId:<PR1>, CuttingTeamId:<T_CUT02>, DueDate:today+7, Note:"Cắt đợt 2", FabricTolerancePercent:2.50, Lines:[{OrderLineId:<OL3>, PlannedQty:30}, {OrderLineId:<OL4>, PlannedQty:40}]}` | [API]: 200; `WorkOrderCode` tự sinh; `Lines` có `Color`/`SizeCode` lấy qua join; có `FabricChecks[].Status` ∈ {`Enough`, `Short`, `Unknown`} (PascalCase).<br>[DB]: 1 dòng `WorkOrder` (`Status='OPEN'`, `FabricTolerancePercent=2.50`, `SourceRequestId` NULL, `CreatedAt` ≈ hiện tại); 2 dòng `WorkOrderLine` với `ProductComponentMaterialId` NULL. | Normal |
| IT_B7_024 | TK_MGR | Lệnh có 2 dòng **cùng** `OrderLineId:<OL3>`, cả hai `ProductComponentMaterialId:null` | [API]: **409/400**; **không 500** do `UQ_WOL` (SQL Server coi NULL bằng NULL).<br>[DB]: **Rollback**: không có `WorkOrder` mới. | Abnormal |
| IT_B7_025 | TK_MGR | Hai dòng cùng OL3: một dòng cả bộ (NULL), một dòng cắt lẻ `PCM1` | [API]: 200 (khác `ProductComponentMaterialId`).<br>[DB]: có 2 dòng; dòng cắt lẻ **không** tính vào trần số lượng đặt. | Normal |
| IT_B7_026 | TK_MGR | Hai dòng cùng (OL3, PCM1) | [API]: 409/400; không 500.<br>[DB]: Rollback. | Abnormal |
| IT_B7_027 | OL3 đặt 200; WO1 (120) + WO2 (50) = 170 cả bộ | Lệnh mới có OL3 cả bộ `PlannedQty:31` (tổng thành 201) | [API]: **400/409** (vượt số lượng đặt; `trg_WorkOrderLine_Check` hoặc service chặn); không 500.<br>[DB]: Rollback, không có lệnh mới. | Boundary |
| IT_B7_028 | Như IT_B7_027 | Lệnh mới có OL3 cả bộ `PlannedQty:30` (tổng đúng 200) | [API]: 200.<br>[DB]: tổng kế hoạch cả bộ của OL3 = 200. | Boundary |
| IT_B7_029 | Như IT_B7_027 | Lệnh mới có OL3 cắt lẻ (PCM2) `PlannedQty:500` | [API]: 200 (cắt lẻ không tính vào trần) hoặc 400 theo giới hạn riêng `[?]`.<br>[DB]: nhất quán với API. | Boundary |
| IT_B7_030 | TK_MGR | `PlannedQty`: `0`; `-1`; `1`; `2147483647`; `2147483648` | [API]: 0 / −1 → **400** (DB không có CHECK, nên API hoặc trigger phải chặn); 1 → 200; max → 400 (vượt số đặt); 2147483648 → 400 (tràn `int` khi bind); không 500.<br>[DB]: không có dòng nào `PlannedQty ≤ 0`. | Boundary |
| IT_B7_031 | TK_MGR | `FabricTolerancePercent`: `0`; `999.99`; `1000.00`; `-0.01`; `2.555`; `100.01` | [API]: 0 → 200; 1000 → 400, không 500 do tràn `decimal(5,2)`; âm → 400; 2.555 → 400 hoặc làm tròn `[?]`; 999.99 / 100.01 → 400 theo giới hạn nghiệp vụ `[?]` (ghi lại).<br>[DB]: không có giá trị ngoài miền kiểu. | Boundary |
| IT_B7_032 | TK_MGR | Không gửi `FabricTolerancePercent` | [API]: 200.<br>[DB]: `FabricTolerancePercent=0.00` (default CLR; DB không có default). | Normal |
| IT_B7_033 | TK_MGR | `DueDate:"0001-01-01"`; không gửi `DueDate`; `DueDate` = hôm qua; `DueDate` sau `CustomerOrder.DueDate` | [API]: 0001 / thiếu → **400**; quá khứ → 400 `[?]`; sau hạn của đơn → 400 hoặc cảnh báo `[?]`; không 500.<br>[DB]: không có `WorkOrder.DueDate='0001-01-01'`. | Boundary |
| IT_B7_034 | TK_MGR | `CuttingTeamId:<T_SEW01>`; `CuttingTeamId:<T_CUTOLD>`; `CuttingTeamId:999999` | [API]: SEWING → 400 (chỉ tổ CUTTING); đã ngừng → 400; 999999 → 400/404 (không 500 do `FK_WO_Team`).<br>[DB]: không có lệnh mới. | Abnormal |
| IT_B7_035 | OL9 thuộc OD4 | Lệnh cho OD2 có dòng `OrderLineId:<OL9>` | [API]: 400/404 (dòng đơn của đơn khác).<br>[DB]: Rollback. | Abnormal |
| IT_B7_036 | TK_MGR | `ProductId:<PR2>` trong khi `OrderId:<OD2>` (đơn của PR1) | [API]: 400.<br>[DB]: không có lệnh mới. | Abnormal |
| IT_B7_037 | TK_MGR | Dòng có `ProductComponentMaterialId` là PCM của PR2; `ProductComponentMaterialId:999999` | [API]: 400/404 (không 500 do `FK_WOL_PCM`).<br>[DB]: Rollback. | Abnormal |
| IT_B7_038 | TK_MGR | `OrderId:<OD1>` (`NEW`, chưa sản xuất); `OrderId:<OD3>` (đã hủy); `OrderId:999999` | [API]: 400 / 400 / 404 `[?]`; không 500.<br>[DB]: không có lệnh mới. | Abnormal |
| IT_B7_039 | TK_MGR | `Lines:[]`; `Lines:null`; Note dài 1000 / 1001 ký tự | [API]: `[]` / null → 400; 1000 → 200; 1001 → 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B7_040 | TK_MGR | Gửi kèm `{WorkOrderCode:"HACK", Status:"COMPLETED", SourceRequestId:<RQ1>, CompletedAt:"2026-01-01", CreatedAt:"0001-01-01"}` (mass assignment) | [API]: 200; các field thừa bị bỏ qua.<br>[DB]: mã do hệ thống sinh; `Status='OPEN'`; `SourceRequestId` NULL; `CompletedAt` NULL; `CreatedAt` ≈ hiện tại. | Abnormal |
| IT_B7_041 | S0 | Gửi **đồng thời** 5 POST tạo lệnh hợp lệ (mỗi lệnh có OL4 cắt lẻ để không đụng trần) | [API]: cả 5 đều 200 với 5 `WorkOrderCode` khác nhau, hoặc một số request 409 có thể retry; **không 500** do `UQ_WorkOrder_Code`.<br>[DB]: không có mã trùng; không có `WorkOrderLine` mồ côi. | Boundary |
| IT_B7_042 | OL3 còn 30 cái chưa lập lệnh cả bộ | Gửi **đồng thời** 2 POST, mỗi lệnh OL3 cả bộ 30 | [API]: 1 request 200, 1 request 400/409; không 500.<br>[DB]: tổng kế hoạch cả bộ của OL3 ≤ 200 (race không được làm vượt trần). | Boundary |
| IT_B7_043 | Fault-injection: dòng thứ 2 bị `trg_WorkOrderLine_Check` chặn | POST lệnh 2 dòng | [API]: 400/409; không 500.<br>[DB]: **Rollback**: không có `WorkOrder` và không có dòng 1. | Abnormal |
| IT_B7_044 | WO1 `OPEN` | `PUT M/work-orders/<WO1>` đổi `PlannedQty` của WOL1 thành 100, bỏ WOL2, thêm OL4 cắt lẻ PCM1 20, đổi `DueDate` | [API]: 200.<br>[DB]: WOL1 = 100; WOL2 bị xóa `[?]`; có dòng mới (OL4, PCM1, 20); `DueDate` được cập nhật; `Status` vẫn `OPEN`. | Normal |
| IT_B7_045 | WO2 `CUTTING`, đã có bàn cắt cho OL3 (cắt 40) | PUT WO2 giảm `PlannedQty` của OL3 xuống 30 (nhỏ hơn số đã cắt) | [API]: 400/409 `[?]`; không 500 (do trigger hoặc service).<br>[DB]: WOL3 vẫn 50. | Abnormal |
| IT_B7_046 | WO2 có bàn cắt và phiếu bàn giao cho OL3 | PUT WO2 bỏ dòng OL3 | [API]: 400/409 (dòng đã có bàn cắt/bàn giao); **không 500** do FK.<br>[DB]: WOL3 còn nguyên. | Abnormal |
| IT_B7_047 | WO4 `COMPLETED`; WO5 `CANCELLED` | PUT WO4; PUT WO5 | [API]: 409/400.<br>[DB]: không đổi. | Abnormal |
| IT_B7_048 | WO1 `OPEN` | PUT WO1 đổi `CuttingTeamId` sang T_CUT02 | [API]: 200 (lệnh chưa cắt thì được đổi tổ) hoặc 400 `[?]`.<br>[DB]: nếu 200 thì TK_TL1 không còn thấy WO1 và TK_TL2 thấy (kiểm lại bằng IT_B7_014). | Normal |
| IT_B7_049 | WO1 `OPEN`; bảng không có RowVer | Gửi **đồng thời** 2 PUT WO1 (`PlannedQty` 100 và 110) | [API]: cả hai 200 (last-write-wins) hoặc 1 request 409; không 500.<br>[DB]: WOL1 ∈ {100, 110}; không có dòng trùng. Ghi **finding**: không có khóa lạc quan. | Boundary |
| IT_B7_050 | TK_MGR | `PUT M/work-orders/999999 {…}`; `PUT M/work-orders/abc {…}` | [API]: 404 (ApiResponses); 404 (routing).<br>[DB]: không có lệnh mới (không upsert). | Abnormal |

## 4. Đóng thiếu và hủy lệnh

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_051 | WO2 `CUTTING` | `POST M/work-orders/<WO2>/close-short {Reason:"Hết vải, khách đồng ý"}` | [API]: 200.<br>[DB]: `Status='COMPLETED'` `[?]`; `CompletedAt` ≈ hiện tại; Reason được lưu ở `Note` / `AuditLogs` / bảng khác `[?]` (bảng `WorkOrder` **không có cột Reason**: ghi lại nơi lưu thực tế; nếu Reason bị mất hoàn toàn → finding). | Normal |
| IT_B7_052 | WO2 `CUTTING` | close-short với `{Reason:""}`; `{Reason:null}`; body `null`; không gửi body | [API]: 400 (Reason bắt buộc; body null bị `[ApiController]` chặn); không 500.<br>[DB]: WO2 vẫn `CUTTING`. | Abnormal |
| IT_B7_053 | WO2 có `Note` dài 900 ký tự | close-short với Reason dài 500 ký tự (nếu service nối Reason vào `Note` thì sẽ vượt 1000) | [API]: 200 kèm cắt bớt có kiểm soát, hoặc 400; **không 500** do truncation `nvarchar(1000)`.<br>[DB]: `Note` không vượt quá 1000 ký tự. | Boundary |
| IT_B7_054 | WO1 `OPEN` (chưa cắt); WO4 `COMPLETED`; WO5 `CANCELLED` | close-short WO1, WO4, WO5 | [API]: WO1 → 400/409 hoặc 200 `[?]`; WO4, WO5 → 409.<br>[DB]: WO4, WO5 không đổi. | Abnormal |
| IT_B7_055 | WO1 `OPEN`, chưa có bàn cắt | `POST M/work-orders/<WO1>/cancel` (không body) | [API]: 200.<br>[DB]: `WO1.Status='CANCELLED'`; các dòng `WorkOrderLine` được giữ nguyên (không cascade xóa). | Normal |
| IT_B7_056 | WO2 `CUTTING`, đã có bàn cắt; WO4 `COMPLETED` | cancel WO2; cancel WO4 | [API]: 409/400 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B7_057 | WO1 `OPEN` | Gửi **đồng thời**: cancel WO1 (Manager) và `POST TL/cutting-lays` cho WO1 (TK_TL1) | [API]: 1 request 200, request còn lại 409/400; không 500.<br>[DB]: không tồn tại trạng thái lệnh `CANCELLED` mà lại có bàn cắt `RECORDED` mới. | Boundary |
| IT_B7_058 | TK_MGR | cancel `999999`; close-short `999999` | [API]: 404.<br>[DB]: không đổi. | Abnormal |

## 5. Lệnh cắt bù, kiểm tra vải, endpoint đọc của WK và tổ trưởng

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_059 | RQ1 đã duyệt, chưa có lệnh cắt bù | `GET M/work-orders/replacement-requests` | [API]: 200; có RQ1, không có RQ2.<br>[DB]: không đổi. | Normal |
| IT_B7_060 | RQ1 như trên | `POST M/work-orders/replacement {RequestId:<RQ1>, CuttingTeamId:<T_CUT01>, DueDate:today+3, Note:"Cắt bù lỗi"}` | [API]: 200.<br>[DB]: `WorkOrder` mới (`Status='OPEN'`, `SourceRequestId=RQ1`); `WorkOrderLine` lấy theo nội dung yêu cầu; RQ1 được đánh dấu đã có lệnh `[?]`; RQ1 biến mất khỏi IT_B7_059. | Normal |
| IT_B7_061 | RQ2 đã có lệnh cắt bù | replacement với RQ2 | [API]: 409.<br>[DB]: không có lệnh mới. | Abnormal |
| IT_B7_062 | RQ1 chưa có lệnh | Gửi **đồng thời** 2 request replacement với RQ1 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: đúng 1 `WorkOrder` có `SourceRequestId=RQ1`. | Boundary |
| IT_B7_063 | TK_MGR | replacement với `RequestId:999999`; với `RequestId` của yêu cầu chưa duyệt hoặc không phải loại cắt bù; `CuttingTeamId:<T_SEW01>`; `DueDate:"0001-01-01"` | [API]: 404; 400/409; 400; 400; không 500 (`FK_WO_Request`).<br>[DB]: không có lệnh mới. | Abnormal |
| IT_B7_064 | TK_MGR | `POST M/work-orders/fabric-check {OrderId:<OD2>, CuttingTeamId:<T_CUT01>, Lines:[{OrderLineId:<OL3>, PlannedQty:30}]}` | [API]: 200; `FabricChecks[].Status` có giá trị ∈ {`Enough`, `Short`, `Unknown`} (PascalCase).<br>[DB]: **không đổi** (chỉ cảnh báo, không ghi). | Normal |
| IT_B7_065 | TK_MGR | fabric-check với `Lines:[]`; `OrderId:999999`; `PlannedQty:-1` | [API]: 400 / 404 / 400; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B7_066 | TK_WK | `GET WK/work-orders/<WO1>/fabrics`; `GET WK/work-orders/<WO1>/fabric-needs?color=Đen&sizeCode=M` | [API]: 200; nhu cầu vải tính theo `PlannedQty` × định mức `[?]`; lọc theo color/size đúng.<br>[DB]: không đổi. | Normal |
| IT_B7_067 | TK_WK | fabric-needs với `color=Xanh` (không có trong lệnh); `sizeCode` dài 11 ký tự; `GET WK/work-orders/999999/fabrics` | [API]: 200 rỗng; 200 rỗng hoặc 400; 404.<br>[DB]: không đổi. | Boundary |
| IT_B7_068 | TK_TL1 | `GET TL/work-orders/<WO2>/material-allowance`; `?exceptRequestId=<RQ1>`; `?exceptRequestId=999999`; `?exceptRequestId=abc` | [API]: 200 kèm hạn mức NVL; exceptRequestId hợp lệ hoặc không tồn tại → 200 (bỏ qua); `abc` → 400 (bind lỗi); không 500.<br>[DB]: không đổi. | Normal |
| IT_B7_069 | TK_MGR | `GET M/work-orders?Status=OPEN`; `?Status=Open`; `?Status=Xyz`; `?OrderId=<OD2>` | [API]: dạng HOA → WO1, WO3; dạng Pascal → cùng kết quả hoặc rỗng (DTO truyền nguyên chuỗi, ghi lại); Xyz → rỗng hoặc 400; không 500.<br>[DB]: khớp `WHERE Status='OPEN'`. | Boundary |
| IT_B7_070 | TK_MGR / TK_TL1 | `PageSize`: `0`; `1`; `100`; `101`; `-1`. `Page`: `0`. `SortDir`: `ASC` / `xyz`. `Keyword` dài 101 ký tự hoặc chứa câu lệnh SQL | [API]: 200 với các ca PageSize/Page (`Safe*` tự kẹp; nếu validator trả 400 → ghi lại); `ASC` → tăng dần; khác → giảm dần; Keyword 101 ký tự → 400 hoặc chấp nhận `[?]`; injection → rỗng, không 500.<br>[DB]: không đổi. | Boundary |

## 6. Bàn cắt — `TL/cutting-lays`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_071 | WO1 `OPEN` | TK_TL1 gửi `POST TL/cutting-lays {WorkOrderId:<WO1>, WorkDate:today, ShiftCode:"AFTERNOON", Fabrics:[{MaterialId:<MA1>, UsedM:45.50}], Note:"Bàn 1", Lines:[{OrderLineId:<OL3>, ProductComponentMaterialId:null, ActualQty:60}]}` | [API]: 200; `Data` có `LaySequence`, `SizeSummary` (ví dụ "M:60").<br>[DB]:<br>- `CuttingLay`: `CuttingTeamId=T_CUT01` (lấy từ token, không lấy từ body), `LaySequence` do server cấp (1 nếu là bàn đầu của tổ trong ca đó), `Status='RECORDED'`, `CreatedAt` ≈ hiện tại.<br>- 1 dòng `CuttingLayLine` (`ActualQty=60`) và 1 dòng `CuttingLayFabric` (`UsedM=45.50`).<br>- `WO1.Status` chuyển `OPEN` → `CUTTING` `[?]`. | Normal |
| IT_B7_072 | Tổ T_CUT01 đã có CL1 (today, MORNING, seq 1) | POST bàn mới cho WO2 cùng `WorkDate=today`, `ShiftCode="MORNING"` | [API]: 200.<br>[DB]: `LaySequence=2` (server tự tăng); không vi phạm `UQ_CL`. | Normal |
| IT_B7_073 | T_CUT01 chưa có bàn nào trong ca `OVERTIME` hôm nay | Gửi **đồng thời** 3 POST bàn cắt cho T_CUT01, cùng ngày và cùng ca `OVERTIME` | [API]: cả 3 đều 200 với `LaySequence` 1, 2, 3 (không trùng), hoặc một số request 409 có thể retry; **không 500** do `UQ_CL (CuttingTeamId, WorkDate, ShiftCode, LaySequence)`.<br>[DB]: không có hai bàn trùng khóa; không có dòng bàn cắt hay vải mồ côi. | Boundary |
| IT_B7_074 | T_CUT01 và T_CUT02 cùng chưa có bàn trong ca `MORNING` ngày mai | TK_TL1 và TK_TL2 cùng tạo bàn đầu tiên cho tổ của mình | [API]: cả hai 200.<br>[DB]: cả hai bàn đều có `LaySequence=1` (UNIQUE tính theo từng tổ). | Normal |
| IT_B7_075 | TK_TL1 | `ShiftCode:"NIGHT"`; `"morning"`; `""`; chuỗi 11 ký tự | [API]: NIGHT / rỗng / 11 ký tự → 400 (cột `varchar(10)`, không 500); `morning` → 400 hoặc chuẩn hóa `[?]`.<br>[DB]: không tồn tại `ShiftCode` nào ngoài {`MORNING`, `AFTERNOON`, `OVERTIME`}. | Boundary |
| IT_B7_076 | TK_TL1 | Bàn có 2 dòng cùng (OL3, NULL) | [API]: 409/400; **không 500** do `UQ_CLL`.<br>[DB]: **Rollback**: không có `CuttingLay` mới. | Abnormal |
| IT_B7_077 | TK_TL1 | Bàn có 2 vải cùng `MaterialId:<MA1>` | [API]: 409/400; không 500 do `UQ_CLF`.<br>[DB]: Rollback. | Abnormal |
| IT_B7_078 | WOL3 của WO2 kế hoạch 50, đã cắt 40 | Bàn mới cho WO2 với OL3 `ActualQty:11` (tổng thành 51) | [API]: 400/409 (vượt kế hoạch; `trg_CuttingLayLine_Check` hoặc service chặn) `[?]`; **không 500**.<br>[DB]: Rollback; tổng `ActualQty` của OL3 trong WO2 vẫn là 40. | Boundary |
| IT_B7_079 | Như IT_B7_078 | Bàn mới với OL3 `ActualQty:10` (tổng đúng 50) | [API]: 200.<br>[DB]: tổng đã cắt = 50. | Boundary |
| IT_B7_080 | TK_TL1 | `ActualQty`: `0`; `-1`; `2147483648` | [API]: 400 với cả 3; không 500.<br>[DB]: không có dòng `ActualQty ≤ 0`. | Boundary |
| IT_B7_081 | TK_TL1 | Dòng có `OrderLineId:<OL4>` nhưng WO2 không có OL4; `OrderLineId:<OL9>` (đơn khác) | [API]: 400/404; không 500 do FK.<br>[DB]: Rollback. | Abnormal |
| IT_B7_082 | TK_TL1 | Vải có `UsedM`: `9999999.99`; `10000000.00`; `0`; `-1`; `12.345`; `null` | [API]: 9999999.99 → 200 hoặc 400 (vượt lượng vải còn lại) `[?]`; 10000000 → 400 (tràn `decimal(9,2)`); 0 / −1 → 400; 3 chữ số thập phân → 400 hoặc làm tròn; `null` khi tạo mới → 400 hoặc 200 `[?]` (DB cho phép NULL chỉ dành cho bàn cũ); không 500.<br>[DB]: không có `UsedM` âm. | Boundary |
| IT_B7_083 | Lệnh cần vải MA1 | `Fabrics:[]` (thiếu vải); `Fabrics` có MA2 (vải không thuộc định mức); `MaterialId:999999` | [API]: 400 ("phải khai báo đủ mọi vải"); 400; 400/404; không 500 do `FK_CLF_Material`.<br>[DB]: Rollback. | Abnormal |
| IT_B7_084 | TK_TL1 | `WorkDate:"0001-01-01"`; không gửi `WorkDate`; `WorkDate` = ngày mai; `WorkDate` trước ngày tạo lệnh | [API]: 0001 / thiếu → **400**; tương lai / trước ngày tạo lệnh → 400 `[?]`; không 500.<br>[DB]: không có `WorkDate='0001-01-01'`. | Boundary |
| IT_B7_085 | WO4 `COMPLETED`; WO5 `CANCELLED` | POST bàn cắt cho WO4; cho WO5 | [API]: 409/400.<br>[DB]: không đổi. | Abnormal |
| IT_B7_086 | TK_TL1 | `Lines:[]`; Note dài 500 / 501 ký tự | [API]: 400; 200; 400.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B7_087 | TK_TL1 | POST kèm `{CuttingTeamId:<T_CUT02>, LaySequence:99, Status:"LOCKED"}` (mass assignment) | [API]: 200; các field thừa bị bỏ qua.<br>[DB]: `CuttingTeamId=T_CUT01` (theo token); `LaySequence` do server cấp; `Status='RECORDED'`. | Abnormal |
| IT_B7_088 | Fault-injection: INSERT `CuttingLayFabric` lỗi | POST bàn hợp lệ | [API]: lỗi có kiểm soát.<br>[DB]: **Rollback**: không có `CuttingLay` và `CuttingLayLine`; `WO1.Status` **không** bị đổi sang `CUTTING`. | Abnormal |
| IT_B7_089 | CL1 `RECORDED` (OL3 cắt 40, vải 30.50) | `PUT TL/cutting-lays/<CL1>` với `ActualQty:45`, `UsedM:32.00`, Note mới | [API]: 200.<br>[DB]: dòng của CL1 có `ActualQty=45`, vải có `UsedM=32.00`; `LaySequence` và `CuttingTeamId` **không đổi**; không có dòng trùng. | Normal |
| IT_B7_090 | CL_LK `LOCKED` | PUT CL_LK | [API]: **409** (bàn đã khóa).<br>[DB]: CL_LK và các dòng của nó không đổi. | Abnormal |
| IT_B7_091 | CL1 OL3 cắt 40; đã có phiếu bàn giao 30 + 10 = 40 | PUT CL1 giảm `ActualQty` xuống 35 (nhỏ hơn số đã bàn giao) | [API]: 400/409 `[?]` (trigger hoặc service); không 500.<br>[DB]: CL1 vẫn 40. | Abnormal |
| IT_B7_092 | CL1 thuộc WO2 | PUT CL1 đổi `WorkOrderId` sang WO1; đổi `ShiftCode` sang `AFTERNOON` | [API]: 400 hoặc 200 `[?]`; nếu đổi ca thì `LaySequence` được cấp lại, không 500 do `UQ_CL`.<br>[DB]: nhất quán; không có hai bàn trùng khóa. | Boundary |
| IT_B7_093 | CL1 có thể sửa | Gửi **đồng thời** 2 PUT CL1 (`ActualQty` 42 và 44) | [API]: cả hai 200 (last-write-wins) hoặc 1 request 409; không 500.<br>[DB]: dòng của CL1 có `ActualQty` ∈ {42, 44}; không có dòng trùng hay mồ côi. Ghi **finding** nếu ghi đè không kiểm soát. | Boundary |
| IT_B7_094 | TK_TL1 | `GET TL/cutting-lays?WorkDate=<today>&Status=RECORDED`; `?WorkDate=0001-01-01`; `?WorkDate=abc` | [API]: lọc đúng; 0001 → rỗng; abc → 400 (bind lỗi); không 500.<br>[DB]: không đổi. | Boundary |
| IT_B7_095 | TK_TL1 | `PUT TL/cutting-lays/999999 {…}`; `GET TL/cutting-lays/abc` | [API]: 404 (ApiResponses); 404 (routing).<br>[DB]: không có bàn mới. | Abnormal |

## 7. Bàn giao BTP — tổ trưởng tạo và hủy (`TL/cut-handovers`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_096 | WO2: OL3 đã cắt 40, đã giao 40 (CH1 30 + CH2 10) | `GET TL/cut-handovers/handoverable?workOrderId=<WO2>` | [API]: 200; OL3 có số còn được giao = 0 (đã cắt 40 − đã giao 40, trong đó `HandedQty` gồm cả phiếu chờ nhận và đã nhận).<br>[DB]: khớp `SUM(ActualQty)` − `SUM(HandedQty)` của các phiếu `PENDING` + `CONFIRMED`. | Normal |
| IT_B7_097 | TK_TL1 | `GET …/handoverable` (thiếu `workOrderId`, model binding rơi về 0); `?workOrderId=999999` | [API]: 200 với danh sách rỗng, hoặc 400/404; **không 500** khi `workOrderId=0`.<br>[DB]: không đổi. | Boundary |
| IT_B7_098 | WO2: thêm bàn CL2 cắt OL3 thêm 10 (sau IT_B7_079, tổng cắt 50) | `POST TL/cut-handovers {WorkOrderId:<WO2>, Lines:[{OrderLineId:<OL3>, ProductComponentMaterialId:null, HandedQty:10}]}` | [API]: 200; `HandoverCode` tự sinh.<br>[DB]: `CutHandover` mới (`Status='PENDING'`, `HandedAt` ≈ hiện tại, `ConfirmedBy` NULL); `CutHandoverLine` có `HandedQty=10`, `ConfirmedQty` NULL. | Normal |
| IT_B7_099 | OL3 còn được giao 10 | Phiếu với `HandedQty:11` | [API]: **400/409** (vượt số đã cắt; `trg_CutHandoverLine_Check` hoặc service chặn); không 500.<br>[DB]: Rollback, không có phiếu mới. | Boundary |
| IT_B7_100 | OL3 còn được giao 10 | Gửi **đồng thời** 2 POST, mỗi phiếu giao 10 cho OL3 | [API]: 1 request 200, 1 request 400/409; không 500.<br>[DB]: tổng `HandedQty` (phiếu `PENDING` + `CONFIRMED`) ≤ tổng đã cắt. | Boundary |
| IT_B7_101 | TK_TL1 | Phiếu có 2 dòng cùng (OL3, NULL) | [API]: 409/400; **không 500** do `UQ_CHL`.<br>[DB]: Rollback. | Abnormal |
| IT_B7_102 | TK_TL1 | `HandedQty`: `0`; `-1`; `2147483648`; `Lines:[]` | [API]: 400 với tất cả; không 500.<br>[DB]: không có dòng `HandedQty ≤ 0`. | Boundary |
| IT_B7_103 | TK_TL1 | Dòng có OL4 (chưa có bàn cắt nào cho OL4 trong WO2); OL9 (đơn khác); `ProductComponentMaterialId:999999` | [API]: 400/404; không 500 do FK.<br>[DB]: Rollback. | Abnormal |
| IT_B7_104 | WO5 `CANCELLED`; WO1 chưa có bàn cắt | POST phiếu cho WO5; cho WO1 | [API]: 409/400 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B7_105 | S0 | Gửi **đồng thời** 5 POST phiếu hợp lệ (mỗi phiếu giao 1 cái, sau khi seed đủ số đã cắt) | [API]: cả 5 đều 200 với `HandoverCode` khác nhau, hoặc một số 409 có thể retry; **không 500** do `UQ_CutHandover_Code`.<br>[DB]: không có mã trùng. | Boundary |
| IT_B7_106 | Fault-injection: dòng thứ 2 bị `trg_CutHandoverLine_Check` chặn | POST phiếu 2 dòng | [API]: 400/409; không 500.<br>[DB]: **Rollback**: không có `CutHandover`, không có dòng 1. | Abnormal |
| IT_B7_107 | CH1 `PENDING` | `POST TL/cut-handovers/<CH1>/cancel` (không body) | [API]: 200.<br>[DB]: `CH1.Status='CANCELLED'`; các dòng vẫn còn; số `handoverable` của OL3 tăng thêm 30 (phiếu đã hủy không còn được tính vào đã giao). | Normal |
| IT_B7_108 | CH2 `CONFIRMED`; một phiếu `REJECTED`; một phiếu `CANCELLED` | cancel lần lượt từng phiếu | [API]: 409 với cả 3.<br>[DB]: không đổi. | Abnormal |
| IT_B7_109 | CH1 `PENDING` | Gửi **đồng thời** `TL/…/<CH1>/cancel` (tổ trưởng) và `WK/…/<CH1>/confirm` (thủ kho) | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: CH1 ở **một** trạng thái cuối duy nhất; chỉ khi trạng thái cuối là `CONFIRMED` thì mới có giao dịch tồn kho. | Boundary |

## 8. Bàn giao BTP — kho xác nhận và từ chối (`WK/cut-handovers`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_110 | CH1 `PENDING`, CHL1 giao 30 | `POST WK/cut-handovers/<CH1>/confirm {Lines:[{CutHandoverLineId:<CHL1>, ConfirmedQty:30, DiscrepancyReason:null}]}` | [API]: 200; `ConfirmedByName` = tên của E_WK.<br>[DB]:<br>- CH1: `Status='CONFIRMED'`, `ConfirmedBy=E_WK` (lấy từ token), `ConfirmedAt` ≈ hiện tại.<br>- CHL1: `ConfirmedQty=30`.<br>- Tồn kho BTP: `StockBalance` +30 và có thẻ kho `StockTransaction` `[?]`. | Normal |
| IT_B7_111 | CH1 `PENDING`, giao 30 | confirm với `ConfirmedQty:28, DiscrepancyReason:"Thiếu 2 chi tiết"` | [API]: 200.<br>[DB]: `ConfirmedQty=28`; `DiscrepancyReason` được lưu; tồn kho chỉ tăng 28. | Normal |
| IT_B7_112 | CH1 giao 30 | confirm với `ConfirmedQty:28` nhưng `DiscrepancyReason:null` hoặc `""` | [API]: 400 (có chênh lệch thì phải có lý do) `[?]`.<br>[DB]: CH1 vẫn `PENDING`. | Abnormal |
| IT_B7_113 | CH1 giao 30 | `ConfirmedQty`: `31` (lớn hơn số giao); `-1`; `0`; `2147483648` | [API]: 31 / −1 → 400 (`trg_CutHandoverLine_Check` hoặc service); 0 → 200 kèm lý do hoặc 400 `[?]`; tràn → 400; không 500.<br>[DB]: không có `ConfirmedQty` âm hay lớn hơn `HandedQty`. | Boundary |
| IT_B7_114 | TK_WK | `DiscrepancyReason` dài 500 / 501 ký tự | [API]: 500 → 200; 501 → 400 (không 500 do truncation).<br>[DB]: ca 500 ký tự lưu đủ. | Boundary |
| IT_B7_115 | Phiếu CHx có 2 dòng | confirm chỉ gửi 1 dòng; gửi `Lines:[]` | [API]: 400 (phải xác nhận đủ các dòng) `[?]`.<br>[DB]: phiếu vẫn `PENDING`. | Abnormal |
| IT_B7_116 | CHL9 thuộc CH9 | confirm CH1 với `CutHandoverLineId:<CHL9>` | [API]: 400/404 (dòng thuộc phiếu khác).<br>[DB]: CHL9 và CH9 không đổi. | Abnormal |
| IT_B7_117 | CH1 `PENDING` | confirm với cùng `CutHandoverLineId` xuất hiện 2 lần | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B7_118 | CH2 `CONFIRMED` | confirm CH2 lần nữa | [API]: **409**.<br>[DB]: tồn kho không bị cộng thêm; không có thẻ kho trùng (`UQ_ST_SourceLine`). | Abnormal |
| IT_B7_119 | CH1 `PENDING` | Gửi **đồng thời** 2 request confirm CH1 (2 phiên của thủ kho) | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: tồn kho chỉ tăng **một lần**; `ConfirmedAt` chỉ được ghi một lần. | Boundary |
| IT_B7_120 | WO2 kế hoạch OL3 50; đã cắt 50; đây là phiếu cuối để đủ 50 cái được xác nhận | confirm phiếu cuối | [API]: 200.<br>[DB]: `WO2.Status='COMPLETED'`, `CompletedAt` ≈ hiện tại (khi đã bàn giao đủ) `[?]`; ghi lại điều kiện thực tế. | Normal |
| IT_B7_121 | Fault-injection: post tồn kho (`usp_Stock_Post`) lỗi khi confirm | confirm CH1 | [API]: lỗi có kiểm soát (409/400); không 500 thô.<br>[DB]: **Rollback**: CH1 vẫn `PENDING`; `ConfirmedBy` và `ConfirmedQty` vẫn NULL; không có thẻ kho; WO2 không bị chuyển sang `COMPLETED`. | Abnormal |
| IT_B7_122 | CH1 `PENDING` | `POST WK/cut-handovers/<CH1>/reject {Reason:"Sai size, đóng gói lẫn"}` | [API]: 200.<br>[DB]: `Status='REJECTED'`; `RejectReason` được lưu; `ConfirmedBy`/`ConfirmedAt` được ghi hay để NULL `[?]`; tồn kho không đổi; số `handoverable` của OL3 tăng lại thêm 30. | Normal |
| IT_B7_123 | CH1 `PENDING` | reject với `{Reason:""}`; `{Reason:null}`; không gửi body; Reason dài 500 / 501 ký tự | [API]: rỗng / null / không body → 400; 500 → 200; 501 → 400; không 500.<br>[DB]: chỉ ca hợp lệ được lưu. | Boundary |
| IT_B7_124 | CH2 `CONFIRMED`; một phiếu đã `CANCELLED` | reject từng phiếu | [API]: 409.<br>[DB]: không đổi. | Abnormal |
| IT_B7_125 | CH1 `PENDING` | Gửi **đồng thời** confirm và reject cho CH1 | [API]: 1 request 200, 1 request 409; không 500.<br>[DB]: CH1 ở một trạng thái cuối duy nhất. | Boundary |
| IT_B7_126 | TK_WK | confirm hoặc reject với `999999`; `abc` | [API]: 404 (ApiResponses); 404 (routing).<br>[DB]: không đổi. | Abnormal |

## 9. View `VwWorkOrderLineProgress` và kiểm tra chéo

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B7_127 | WO2: OL3 đã cắt 40 (CL1); bàn giao CH1 30 (`PENDING`) + CH2 10 (`CONFIRMED`) | `GET M/work-orders/<WO2>`; `GET WK/work-orders/<WO2>`; `GET TL/work-orders/<WO2>` | [API]: cả 3 trả giống nhau: `Lines[OL3].CutQty=40`, `HandedQty=40`.<br>[DB]:<br>- Khớp `SELECT CutQty, HandedQty FROM VwWorkOrderLineProgress WHERE WorkOrderLineId=WOL3`.<br>- Khớp `SUM(CuttingLayLine.ActualQty)` và `SUM(CutHandoverLine.HandedQty)` tính trên các phiếu `PENDING` + `CONFIRMED`. | Normal |
| IT_B7_128 | Sau khi CH1 bị hủy (IT_B7_107) hoặc bị từ chối (IT_B7_122) | GET chi tiết WO2 | [API]: `HandedQty` của OL3 = 10 (phiếu `CANCELLED`/`REJECTED` không được tính).<br>[DB]: view phản ánh ngay sau khi trạng thái phiếu đổi. | Boundary |
| IT_B7_129 | Bàn cắt có OL3 vừa cả bộ vừa cắt lẻ (PCM1) | GET chi tiết lệnh | [API]: `CutQty` của dòng cả bộ và dòng cắt lẻ được tách riêng theo `ProductComponentMaterialId`; không cộng lẫn vào nhau.<br>[DB]: view nhóm theo (WorkOrderLine, PCM) đúng. | Boundary |
| IT_B7_130 | Đã chạy các ca ở mục 3–8 | `SELECT DISTINCT` `WorkOrder.Status`, `CuttingLay.Status`, `CuttingLay.ShiftCode`, `CutHandover.Status` | [API]: —<br>[DB]: mọi giá trị đều UPPER_SNAKE (`OPEN`, `CUTTING`, `COMPLETED`, `CANCELLED`, `RECORDED`, `LOCKED`, `MORNING`, `AFTERNOON`, `OVERTIME`, `PENDING`, `CONFIRMED`, `REJECTED`); không có `Enough`/`Short` (FabricCheck) bị lưu nhầm vào DB. | Normal |
| IT_B7_131 | TK_MGR, TK_TL1, TK_WK | Gửi body `null`; JSON sai cú pháp; `Content-Type: text/plain` tới `POST M/work-orders`, `POST TL/cutting-lays`, `POST WK/cut-handovers/<CH1>/confirm` | [API]: 400 (do `[ApiController]`) hoặc 415; không 500 / NullReference.<br>[DB]: không đổi. | Abnormal |
| IT_B7_132 | Ghi `MAX(AuditLogs.Id)` và `MAX(StockTransactionId)` trước khi chạy | Chạy các ca thất bại IT_B7_024, IT_B7_043, IT_B7_076, IT_B7_088, IT_B7_099, IT_B7_106, IT_B7_118, IT_B7_121 | [API]: 4xx đúng như từng ca.<br>[DB]: hai giá trị MAX không đổi; không có dữ liệu nửa vời ở 7 bảng của batch. | Abnormal |
| IT_B7_133 | Trigger `trg_WorkOrderLine_Check`, `trg_CuttingLayLine_Check`, `trg_CutHandoverLine_Check` đang bật | Chạy lại IT_B7_027, IT_B7_042, IT_B7_073, IT_B7_078, IT_B7_100, IT_B7_119 trong 10 vòng liên tiếp | [API]: không vòng nào trả 500; body không chứa tên trigger hay `SqlException`.<br>[DB]: không vượt trần số đặt / kế hoạch / số đã cắt; không có bàn cắt trùng `UQ_CL`; tồn kho không bị cộng hai lần. | Boundary |
