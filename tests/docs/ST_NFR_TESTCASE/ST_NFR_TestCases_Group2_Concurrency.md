# ST NFR TEST CASES — Group2_Concurrency (NX-MTD)

> **Nguồn duy nhất:** `ST_NFR_TARGETS.md` — mục 3 (G2-01 → G2-09) và mục 6. Mọi rủi ro trong nguồn là *giả thuyết cần kiểm chứng*.
> **Môi trường:** localhost/test, DB reset + seed riêng cho G2; dữ liệu do test tạo có tiền tố `ST_AUTO_` và được dọn sau khi chạy.
> **Cách chạy:** xUnit + `HttpClient`, bắn đồng thời bằng `Task.WhenAll` sau một `Barrier`/`TaskCompletionSource` chung để các request xuất phát cùng lúc.
> **Quy chuẩn Report 5.3:** **không Assert bằng DB** — tồn kho, trạng thái, số bản ghi đều đọc lại qua **API công khai** (hoặc giao diện).

## 0. Quy ước kết quả

| Ký hiệu | Ý nghĩa |
|---|---|
| **Thắng** | HTTP 2xx |
| **Thua hợp lệ** | HTTP 400 / 409 / 422 có thông điệp nghiệp vụ (bao gồm `DbUpdateConcurrencyException` đã được ánh xạ thành 409) |
| **Fail** | Bất kỳ HTTP 500, timeout > 30 s, deadlock không xử lý, hoặc trạng thái cuối sai |
| `N_PAIR` / `N_BURST` / `N_FLOOD` | 2 / 10 / 50 luồng `Task.WhenAll` |

Bảo vệ đã khai báo theo nguồn: `sp_getapplock` (mã chứng từ, mã vật tư, tồn kho, bàn giao cắt, thứ tự bàn cắt, bàn giao thành phẩm, phiếu giao hàng, trả NVL, chuyến xe, phân công/sản lượng); RowVersion trên `CustomerOrder`, `DefectRecord`, `DispatchNote`, `Employee`, `MaterialIssue`, `MaterialReceipt`, `OutputRecord`, `Payroll`, `PurchaseOrder`, `QcBatch`, `Request`. Các luồng *“không thấy”* bảo vệ được đánh dấu **(ưu tiên)**.

## 1. Bảng Test Case

| TC ID | Mục tiêu | Loại | Endpoint / Màn hình | Kịch bản | Kết quả mong đợi |
|---|---|---|---|---|---|
| NFR_G2_001 | G2-01 | Normal | POST `wk/material-issues`, POST `tl/material-issues/{id}/confirm` | **[Load/Attack/Env]:** 1 luồng, WarehouseKeeper tạo phiếu xuất 10 đơn vị NVL `ST_AUTO_` (tồn 100), TeamLeader confirm. | **[SLA/Threshold]:** HTTP 2xx cả 2 bước; tồn đọc qua `wk/inventories` = 90; trạng thái phiếu = đã xác nhận; mỗi request < 800 ms. |
| NFR_G2_002 | G2-01 | Boundary | POST `wk/material-issues` | **[Load/Attack/Env]:** tồn = 10 (biên); `N_PAIR`=2 luồng `Task.WhenAll`, mỗi luồng xuất 10, cùng NVL, token WarehouseKeeper. | **[SLA/Threshold]:** đúng 1 Thắng + 1 Thua hợp lệ; tồn cuối = 0 (**≥ 0**); không phát sinh HTTP 500. |
| NFR_G2_003 | G2-01 | Boundary | POST `wk/material-issues` | **[Load/Attack/Env]:** tồn = 10; `N_FLOOD`=50 luồng `Task.WhenAll`, mỗi luồng xuất 1. | **[SLA/Threshold]:** đúng 10 Thắng, 40 Thua hợp lệ; tồn cuối = 0; số phiếu xuất mới (đếm qua API danh sách) = 10; 0 HTTP 500. |
| NFR_G2_004 | G2-01 | Abnormal | POST `tl/material-issues/{id}/confirm` ∥ POST `tl/material-issues/{id}/reject` | **[Load/Attack/Env]:** `N_PAIR`: 1 luồng confirm + 1 luồng reject cùng phiếu, cùng TeamLeader, xuất phát đồng thời; lặp 20 vòng. | **[SLA/Threshold]:** mỗi vòng đúng 1 Thắng; trạng thái cuối khớp với luồng thắng; tồn chỉ thay đổi nếu confirm thắng (đúng 1 lần); 0 HTTP 500. |
| NFR_G2_005 | G2-01 | Abnormal | POST `mgr/material-receipts/{id}/confirm` | **[Load/Attack/Env]:** double-submit: `N_BURST`=10 luồng confirm cùng phiếu nhập 50 đơn vị, token Manager. | **[SLA/Threshold]:** đúng 1 Thắng, 9 Thua hợp lệ; tồn tăng **đúng 50** (không cộng hai lần); 0 HTTP 500. |
| NFR_G2_006 | G2-01 | Abnormal | PUT `wk/material-receipts/{id}` ∥ POST `mgr/material-receipts/{id}/{confirm,return}` | **[Load/Attack/Env]:** `N_PAIR`: WarehouseKeeper sửa số lượng phiếu với RowVersion cũ, Manager confirm cùng lúc; vòng 2 thay confirm bằng return. | **[SLA/Threshold]:** đúng 1 Thắng; bên thua nhận HTTP 409; không có phiếu “đã xác nhận nhưng số lượng bị sửa sau”; tồn khớp số lượng của phiếu đã confirm. |
| NFR_G2_007 | G2-01 | Boundary | POST `tl/material-returns/{id}/cancel` ∥ POST `wk/material-returns/{id}/{confirm,reject}` | **[Load/Attack/Env]:** `N_PAIR`: TeamLeader hủy phiếu trả NVL trong lúc WarehouseKeeper confirm; lặp 20 vòng (10 vòng confirm, 10 vòng reject). | **[SLA/Threshold]:** mỗi vòng đúng 1 Thắng; tồn chỉ cộng khi confirm thắng; không có phiếu vừa hủy vừa được cộng tồn; 0 HTTP 500. |
| NFR_G2_008 | G2-02 | Normal | POST `wk/stocktakes`, POST `mgr/stocktakes/{id}/approve` | **[Load/Attack/Env]:** 1 luồng: WarehouseKeeper tạo phiếu kiểm kê lệch −5, Manager duyệt. | **[SLA/Threshold]:** HTTP 2xx; tồn sau duyệt = tồn sổ − 5; trạng thái = đã duyệt. |
| NFR_G2_009 | G2-02 | Abnormal **(ưu tiên)** | POST `{adm,mgr}/stocktakes/{id}/approve` | **[Load/Attack/Env]:** `N_PAIR`: Admin và Manager duyệt cùng phiếu đồng thời; vòng 2: `N_BURST`=10 luồng approve (double-submit). `Stocktake` không thấy RowVersion. | **[SLA/Threshold]:** đúng 1 Thắng; tồn chỉ điều chỉnh **1 lần**; 0 HTTP 500. |
| NFR_G2_010 | G2-02 | Boundary | POST `mgr/stocktakes/{id}/approve` ∥ POST `wk/material-issues` | **[Load/Attack/Env]:** phiếu kiểm kê ghi tồn thực = 20; cùng lúc `N_BURST`=10 luồng xuất 2/luồng cùng NVL. | **[SLA/Threshold]:** tồn cuối = (giá trị theo thứ tự xử lý thực tế, tính lại từ lịch sử thẻ kho qua `stock-card`) và **≥ 0**; tổng biến động trên thẻ kho khớp tồn cuối; 0 HTTP 500. |
| NFR_G2_011 | G2-03 | Normal | POST `qa/fg-handovers`, POST `wk/fg-handovers/{id}/confirm` | **[Load/Attack/Env]:** 1 luồng: QaQc bàn giao 100 thành phẩm, WarehouseKeeper confirm. | **[SLA/Threshold]:** HTTP 2xx; số thành phẩm trong kho tăng đúng 100. |
| NFR_G2_012 | G2-03 | Boundary | POST `qa/fg-handovers/handover-all` ∥ POST `qa/fg-handovers` | **[Load/Attack/Env]:** số có thể bàn giao = 100 (biên); `N_PAIR`: 1 luồng `handover-all` + 1 luồng bàn giao lẻ 100. | **[SLA/Threshold]:** tổng số đã bàn giao (đọc qua API) **≤ 100**; đúng 1 Thắng; 0 HTTP 500. |
| NFR_G2_013 | G2-03 | Abnormal | POST `mgr/dispatch-notes/{id}/cancel` ∥ POST `wk/dispatch-notes/{id}/export` | **[Load/Attack/Env]:** `N_PAIR`: Manager hủy phiếu giao hàng trong lúc WarehouseKeeper xuất; lặp 20 vòng. | **[SLA/Threshold]:** mỗi vòng đúng 1 Thắng; nếu export thắng, tồn thành phẩm giảm đúng 1 lần; nếu cancel thắng, tồn không đổi; 0 HTTP 500. |
| NFR_G2_014 | G2-03 | Abnormal | POST `acc/dispatch-notes/{id}/confirm-delivery`, POST `wk/dispatch-notes/{id}/reject` | **[Load/Attack/Env]:** double-submit: `N_BURST`=10 luồng confirm-delivery; vòng 2: confirm-delivery ∥ reject. | **[SLA/Threshold]:** đúng 1 Thắng mỗi vòng; trạng thái cuối nhất quán; 0 HTTP 500. |
| NFR_G2_015 | G2-04 | Boundary | POST `tl/cutting-lays` | **[Load/Attack/Env]:** `N_BURST`=10 luồng TeamLeader tổ cắt tạo bàn cắt cho cùng lệnh cắt. | **[SLA/Threshold]:** 10 số bàn cắt **khác nhau** (đọc qua API); không trùng thứ tự; 0 HTTP 500. |
| NFR_G2_016 | G2-04 | Boundary | POST `tl/cut-handovers`, POST `wk/cut-handovers/{id}/confirm` | **[Load/Attack/Env]:** sản lượng cắt còn lại = 200 (biên); `N_PAIR`: 2 luồng mỗi luồng bàn giao 200. | **[SLA/Threshold]:** tổng bàn giao **≤ 200**; đúng 1 Thắng; 0 HTTP 500. |
| NFR_G2_017 | G2-04 | Abnormal | POST `mgr/work-orders/{id}/{close-short,cancel}` ∥ POST `tl/cutting-lays`, `tl/cut-handovers` | **[Load/Attack/Env]:** `N_PAIR`: Manager đóng thiếu/hủy lệnh cắt trong lúc TeamLeader tạo bàn cắt/bàn giao; lặp 10 vòng mỗi cặp. | **[SLA/Threshold]:** không có bàn cắt/bàn giao mới gắn với lệnh đã hủy/đóng sau thời điểm đóng; bên thua nhận Thua hợp lệ; 0 HTTP 500. |
| NFR_G2_018 | G2-05 | Normal | POST `tl/work-allocations`, POST `wrk/work-allocations/{id}/confirm` | **[Load/Attack/Env]:** 1 luồng: TeamLeader phân công 50 sp, Worker xác nhận. | **[SLA/Threshold]:** HTTP 2xx; trạng thái = đã xác nhận; số lượng = 50. |
| NFR_G2_019 | G2-05 | Boundary | POST `{tl,mgr}/output-records` | **[Load/Attack/Env]:** hạn mức sản lượng còn lại = 5 (biên); `N_PAIR`: TeamLeader và Manager cùng ghi 5 cho cùng công nhân/công đoạn/ngày. | **[SLA/Threshold]:** tổng sản lượng (đọc qua `output-records/grid`) **≤ hạn mức**; đúng 1 Thắng; 0 HTTP 500. |
| NFR_G2_020 | G2-05 | Abnormal **(ưu tiên)** | POST `tl/operation-assignments/bulk` ∥ POST `tl/operation-assignments/copy` | **[Load/Attack/Env]:** `N_PAIR` + vòng `N_BURST`=10: cùng tổ, cùng công nhân, cùng ngày, cùng công đoạn. Không thấy app lock cho `operation-assignments`. | **[SLA/Threshold]:** không có phân công trùng (công nhân × công đoạn × ngày) trong danh sách API; bên thua Thua hợp lệ; 0 HTTP 500. |
| NFR_G2_021 | G2-05 | Abnormal | POST `wrk/work-allocations/{id}/confirm` ∥ `WorkAllocationAutoConfirmJob` | **[Load/Attack/Env]:** tạo phân công sát mốc tự xác nhận; Worker bấm confirm tại T−1 s, T, T+1 s so với lúc job chạy. | **[SLA/Threshold]:** mỗi phân công chỉ được xác nhận **1 lần**, số lượng không nhân đôi; Worker nhận 2xx hoặc Thua hợp lệ (“đã xác nhận”); 0 HTTP 500. |
| NFR_G2_022 | G2-05 | Abnormal | POST `wrk/work-allocations/confirm-batch`, `tl/work-allocations/workers/{workerId}/confirm-on-behalf` | **[Load/Attack/Env]:** `N_BURST`=10 luồng: 5 luồng Worker `confirm-batch` danh sách ID chồng lấn (đảo thứ tự ID), 5 luồng TeamLeader `confirm-on-behalf` cùng worker. | **[SLA/Threshold]:** không deadlock (0 HTTP 500, 0 timeout > 30 s); mỗi phân công xác nhận đúng 1 lần. |
| NFR_G2_023 | G2-05 | Abnormal | POST `tl/defect-records`, POST `qa/qc-batches`, sửa/hủy tại `mgr/{defect-records,qc-batches}` | **[Load/Attack/Env]:** double-submit tạo: `N_BURST`=10 request giống hệt; vòng 2: `N_PAIR` PUT cùng bản ghi với RowVersion cũ. | **[SLA/Threshold]:** vòng 1: số bản ghi mới = 1 hoặc hệ thống trả Thua hợp lệ cho bản trùng (ghi nhận nếu tạo 10 bản — giả thuyết rủi ro); vòng 2: đúng 1 Thắng, 1 HTTP 409; 0 HTTP 500. |
| NFR_G2_024 | G2-06 | Boundary | POST `cm/request-inbox/{id}/approve` | **[Load/Attack/Env]:** `N_PAIR`: 2 handler khác nhau duyệt cùng đơn đồng thời. | **[SLA/Threshold]:** đúng 1 Thắng, 1 Thua hợp lệ/409; side effect (thông báo, phiếu liên quan) phát sinh **1 lần**; 0 HTTP 500. |
| NFR_G2_025 | G2-06 | Abnormal | POST `adm/customer-orders/{id}/start-production` | **[Load/Attack/Env]:** double-submit: `N_BURST`=10 luồng Admin cùng đơn. | **[SLA/Threshold]:** đúng 1 Thắng; side effect (kho/lệnh cắt) tạo **1 lần** (đếm qua API); 0 HTTP 500. |
| NFR_G2_026 | G2-06 | Abnormal | PUT `cm/requests/{id}` / POST `cm/requests/{id}/cancel` ∥ POST `cm/request-inbox/{id}/{approve,reject}` | **[Load/Attack/Env]:** `N_PAIR`: người tạo sửa/hủy đơn trong lúc handler duyệt; lặp 20 vòng. | **[SLA/Threshold]:** không có đơn “đã duyệt nhưng nội dung bị sửa sau duyệt”; bên thua HTTP 409/Thua hợp lệ; 0 HTTP 500. |
| NFR_G2_027 | G2-06 | Abnormal | POST `adm/piece-rates/approve-many` ∥ POST `adm/piece-rates/{id}/reject` ∥ POST `acc/piece-rates/submit-many` | **[Load/Attack/Env]:** `N_BURST`=10 luồng với tập ID chồng lấn: 4 approve-many, 3 reject đơn lẻ, 3 submit-many. | **[SLA/Threshold]:** mỗi đơn giá có đúng 1 trạng thái cuối hợp lệ; không deadlock; 0 HTTP 500. |
| NFR_G2_028 | G2-06 | Abnormal | POST `mgr/purchase-order-tracking/{id}/{place,cancel,reprice}`, `adm/customer-orders/{id}/{cancel,stop}`, `mgr/kpi-targets/{id}/{finish,cancel}`, `mgr/short-deliveries/{id}/resolve`, `{adm,mgr}/grade-evaluations/{id}/{apply,reject}` | **[Load/Attack/Env]:** với mỗi cặp hành động đối nghịch, `N_PAIR` đồng thời; với `reprice`, 2 luồng gửi giá khác nhau cùng RowVersion. | **[SLA/Threshold]:** mỗi cặp đúng 1 Thắng; `reprice` bên thua HTTP 409 (không lost update); 0 HTTP 500. |
| NFR_G2_029 | G2-07 | Abnormal **(ưu tiên)** | POST `agent/attendance-sync/punches` | **[Load/Attack/Env]:** agent retry: gửi cùng batch 100 punch `N_BURST`=10 lần đồng thời, header `X-Agent-Key` hợp lệ. Không thấy app lock. | **[SLA/Threshold]:** số punch lưu (đọc qua `timesheets`) = 100, **không trùng**; 0 HTTP 500. |
| NFR_G2_030 | G2-07 | Boundary | PATCH `cm/my-meals/today` ∥ `MealAutoCloseJob` | **[Load/Attack/Env]:** `N_FLOOD`=50 Worker PATCH tại T−1 s, T, T+1 s so với mốc chốt suất ăn. | **[SLA/Threshold]:** request trước mốc → Thắng; sau mốc → Thua hợp lệ; tổng suất ăn (`tl/meals/confirmation`) khớp số Thắng; 0 HTTP 500. |
| NFR_G2_031 | G2-07 | Abnormal | POST `acc/attendance/import` ∥ POST `agent/attendance-sync/punches` ∥ POST `acc/attendance/adjustments` | **[Load/Attack/Env]:** `N_PAIR`×3 đồng thời cho cùng nhân viên/ngày; vòng 2 thêm `acc/attendance/rj-employees/sync` ∥ `agent/attendance-sync/employees`. | **[SLA/Threshold]:** công ngày đó có đúng 1 kết quả cuối, không bản ghi trùng; 0 HTTP 500. |
| NFR_G2_032 | G2-08 | Boundary **(ưu tiên)** | POST `acc/order-payments/{id}/payments` | **[Load/Attack/Env]:** công nợ còn lại = 1.000.000 (biên); `N_PAIR`: 2 luồng mỗi luồng thanh toán 1.000.000. `OrderPayment` không có concurrency token. | **[SLA/Threshold]:** tổng đã thanh toán **≤ final amount**; công nợ còn lại **≥ 0**; đúng 1 Thắng; 0 HTTP 500. |
| NFR_G2_033 | G2-08 | Abnormal | POST `acc/order-payments/{id}/payments`, `acc/purchase-payments/{id}/payments` | **[Load/Attack/Env]:** double-submit: `N_BURST`=10 request giống hệt (cùng số tiền 100.000). | **[SLA/Threshold]:** số lần thanh toán ghi nhận = 1 (hoặc ghi nhận rủi ro nếu 10); tổng công nợ khớp; 0 HTTP 500. |
| NFR_G2_034 | G2-08 | Abnormal | PUT `acc/order-payments/{id}/final-amount` ∥ POST `.../payments`; PUT `acc/purchase-payments/{id}/actual-amount` ∥ POST `.../{confirm,return}` | **[Load/Attack/Env]:** `N_PAIR` mỗi cặp, lặp 20 vòng. | **[SLA/Threshold]:** không lost update tổng công nợ (công nợ = final − Σ payments đọc qua API); 0 HTTP 500. |
| NFR_G2_035 | G2-08 | Abnormal | POST `adm/payrolls/{id}/approve` ∥ POST `adm/payrolls/{id}/reopen` ∥ POST `acc/payrolls/import` | **[Load/Attack/Env]:** `N_PAIR`: approve ∥ import lại cùng kỳ; vòng 2: approve ∥ reopen; vòng 3: `N_BURST`=10 approve. | **[SLA/Threshold]:** bảng lương đã duyệt không bị ghi đè bởi import; bên thua HTTP 409/Thua hợp lệ; đúng 1 Thắng; 0 HTTP 500. |
| NFR_G2_036 | G2-09 | Boundary | GET `adm/customer-orders/next-code` + POST `adm/customer-orders` | **[Load/Attack/Env]:** `N_FLOOD`=50 luồng cùng lấy `next-code` rồi tạo đơn với mã đó. | **[SLA/Threshold]:** mọi đơn tạo thành công có mã **duy nhất**; luồng trùng nhận Thua hợp lệ (**không phải HTTP 500**). |
| NFR_G2_037 | G2-09 | Boundary | POST `{wk,mgr,adm,acc}/materials` | **[Load/Attack/Env]:** `N_FLOOD`=50 luồng (trộn 4 role) tạo vật tư mã tự sinh. | **[SLA/Threshold]:** 50 mã vật tư duy nhất; 0 HTTP 500. |
| NFR_G2_038 | G2-09 | Abnormal | POST `{adm,mgr}/employees[/from-rj]` | **[Load/Attack/Env]:** `N_BURST`=10 luồng tạo nhân viên cùng username `ST_AUTO_dup`. | **[SLA/Threshold]:** đúng 1 HTTP 201; 9 Thua hợp lệ (400/409); **0 HTTP 500**. |
| NFR_G2_039 | G2-09 | Abnormal | POST `acc/vehicle-trips`, POST `acc/vehicle-trips/{id}/complete` | **[Load/Attack/Env]:** `N_PAIR`: 2 chuyến cùng xe, cùng khung giờ; vòng 2: `N_BURST`=10 complete cùng chuyến. | **[SLA/Threshold]:** không có 1 xe 2 chuyến đang chạy; complete thắng 1 lần; 0 HTTP 500. |
| NFR_G2_040 | G2-09 | Abnormal | POST `{adm,mgr}/teams/{id}/{transfer-in,add-members,remove-members,appoint,dismiss}` | **[Load/Attack/Env]:** `N_PAIR`: chuyển cùng 1 người vào 2 tổ khác nhau; vòng 2: bổ nhiệm 2 tổ trưởng khác nhau cho cùng tổ. | **[SLA/Threshold]:** người đó thuộc đúng 1 tổ; tổ có đúng 1 tổ trưởng; bên thua Thua hợp lệ; 0 HTTP 500. |

## 2. Truy vết bao phủ

| Mục tiêu nguồn | Normal | Boundary | Abnormal |
|---|---|---|---|
| G2-01 | 001 | 002, 003, 007 | 004, 005, 006 |
| G2-02 | 008 | 010 | 009 |
| G2-03 | 011 | 012 | 013, 014 |
| G2-04 | — (bao trong 015) | 015, 016 | 017 |
| G2-05 | 018 | 019 | 020, 021, 022, 023 |
| G2-06 | — (bao trong 024) | 024 | 025, 026, 027, 028 |
| G2-07 | — | 030 | 029, 031 |
| G2-08 | — | 032 | 033, 034, 035 |
| G2-09 | — | 036, 037 | 038, 039, 040 |

Ghi chú: với G2, “Normal” đơn luồng chủ yếu là baseline để so sánh; giá trị kiểm thử nằm ở Boundary/Abnormal.
