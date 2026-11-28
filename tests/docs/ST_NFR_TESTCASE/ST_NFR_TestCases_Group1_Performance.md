# ST NFR TEST CASES — Group1_Performance (NX-MTD)

> **Nguồn duy nhất:** `ST_NFR_TARGETS.md` — mục 2 (G1-01 → G1-09) và mục 6. Mọi rủi ro trong nguồn là *giả thuyết cần kiểm chứng*.
> **Môi trường:** localhost/test, bản build production, DB reset + seed riêng cho G1, **không chạy song song nhóm khác**, Gemini dùng **stub**.
> **Công cụ đề xuất:** k6 (VU, ramp, p95), `dotnet-counters` (RAM/CPU/ThreadPool), EF Core log (đếm truy vấn). Không Assert bằng DB (Report 5.3) — kết quả kiểm qua API công khai.

## 0. Tham số chung (ĐỀ XUẤT — nhóm phải chốt trước khi chạy, theo mục 6 của nguồn)

Nguồn ghi rõ *“Nhóm cần chốt trước quy mô dữ liệu, số người dùng đồng thời và ngưỡng p95”*. Các giá trị dưới đây là placeholder; mọi test case tham chiếu tên tham số để đổi một chỗ.

| Tham số | Giá trị đề xuất | Ý nghĩa |
|---|---|---|
| `VU_STD` | 20 VU | Tải tiêu chuẩn |
| `VU_MAX` | 100 VU | Ngưỡng VU tối đa (cao điểm đầu ca) |
| `RAMP` / `HOLD` | 2 phút / 10 phút | Thời gian tăng tải / giữ tải |
| `SLA_LIST` | p95 < 800 ms | API danh sách, tra cứu |
| `SLA_HOME` | p95 < 1000 ms | Dashboard theo role |
| `SLA_REPORT` | p95 < 3000 ms | Báo cáo tổng hợp, lưới KPI/sản lượng |
| `SLA_EXPORT` | p95 < 10 s | Xuất Excel |
| `SLA_IMPORT` | < 30 s / file chuẩn | Import / preview Excel |
| `ERR_STD` / `ERR_MAX` | 0% / ≤ 1% (không có 5xx) | Tỷ lệ lỗi ở tải chuẩn / tải biên |
| `RAM_DELTA` | ≤ +500 MB và về ≤ baseline +10% sau 5 phút nghỉ | Bộ nhớ tiến trình API |
| `SEED_SMALL` / `SEED_LARGE` | 10.000 / 200.000 giao dịch kho; 500 nhân viên; 12 tháng dữ liệu | Quy mô dữ liệu |

## 1. Bảng Test Case

| TC ID | Mục tiêu | Loại | Endpoint / Màn hình | Kịch bản | Kết quả mong đợi |
|---|---|---|---|---|---|
| NFR_G1_001 | G1-01 | Normal | GET `acc/accounting-reports/{workdays,meals,vehicles}` | **[Load/Attack/Env]:** `VU_STD`=20 VU, role Accountant (token hợp lệ), khoảng ngày 1 tháng, ramp 2 phút + giữ 10 phút, `SEED_LARGE`. | **[SLA/Threshold]:** p95 < 3000 ms (`SLA_REPORT`); Error Rate = 0%; không có HTTP 5xx. |
| NFR_G1_002 | G1-01 | Normal | GET `{acc,adm}/revenue[/filter-options]`, GET `adm/cost-summary`, GET `adm/break-even[/inputs]`, POST `adm/break-even/calculate` | **[Load/Attack/Env]:** 20 VU chia đều Admin/Accountant, khoảng 1 tháng, giữ 10 phút. | **[SLA/Threshold]:** p95 < 3000 ms; `filter-options`, `inputs` p95 < 800 ms; Error Rate = 0%. |
| NFR_G1_003 | G1-01 | Boundary | GET `acc/accounting-reports/*`, `{acc,adm}/revenue`, `adm/cost-summary` | **[Load/Attack/Env]:** `VU_MAX`=100 VU, khoảng ngày rộng nhất có dữ liệu (12 tháng), ramp 2 phút + giữ 10 phút. | **[SLA/Threshold]:** p95 < 4500 ms (1,5 × `SLA_REPORT`); Error Rate ≤ 1%; 0 HTTP 5xx; không timeout. |
| NFR_G1_004 | G1-01 | Boundary | GET `acc/accounting-reports/export`, `{acc,adm}/revenue/export`, `adm/cost-summary/export`, `adm/break-even/export` | **[Load/Attack/Env]:** 10 luồng `Task.WhenAll` xuất Excel (ClosedXML) khoảng 12 tháng cùng lúc, role Admin/Accountant; đo RAM bằng `dotnet-counters`. | **[SLA/Threshold]:** mỗi request < 10 s (`SLA_EXPORT`); 10/10 file mở được, > 0 byte; RAM tăng ≤ +500 MB và về ≤ baseline +10% sau 5 phút; 0 HTTP 5xx. |
| NFR_G1_005 | G1-01 | Abnormal | GET `{acc,adm}/revenue/export` | **[Load/Attack/Env]:** 1 user Accountant bấm xuất 20 lần liên tiếp trong 1 giây (double-submit), đồng thời 20 VU nền gọi `revenue` thường. | **[SLA/Threshold]:** 0 HTTP 5xx; RAM ≤ +500 MB; p95 của 20 VU nền vẫn < 3000 ms; tiến trình không bị OOM/restart. |
| NFR_G1_006 | G1-01 | Abnormal | GET `acc/accounting-reports/workdays`, `{acc,adm}/revenue` | **[Load/Attack/Env]:** 1 VU gửi khoảng ngày đảo ngược (`from > to`) và khoảng 10 năm. | **[SLA/Threshold]:** trả HTTP 400 (hoặc kết quả rỗng hợp lệ) trong < 800 ms; không có truy vấn quét toàn bảng kéo dài > 3 s; 0 HTTP 5xx. |
| NFR_G1_007 | G1-02 | Normal | GET `{acc,tl}/attendance/timesheets[/summary]`, GET `acc/payrolls` | **[Load/Attack/Env]:** 20 VU (Accountant + TeamLeader), 1 tháng, 500 nhân viên seed, giữ 10 phút. | **[SLA/Threshold]:** p95 < 3000 ms; Error Rate = 0%. |
| NFR_G1_008 | G1-02 | Boundary | GET `cm/my-estimated-pay`, GET `cm/my-estimated-pay/days/{date}` | **[Load/Attack/Env]:** `VU_MAX`=100 VU role Worker (100 tài khoản khác nhau), ramp 0→100 trong 60 s (mô phỏng nhiều công nhân cùng mở), giữ 10 phút. | **[SLA/Threshold]:** p95 < 1000 ms; Error Rate ≤ 1%; 0 HTTP 5xx; mỗi Worker chỉ nhận số liệu của mình. |
| NFR_G1_009 | G1-02 | Boundary | POST `acc/payrolls/import/preview`, POST `acc/attendance/import/preview` | **[Load/Attack/Env]:** 1 request, file chạm biên quy mô seed: 500 nhân viên × 31 ngày công; role Admin (attendance import) / Accountant (payroll). | **[SLA/Threshold]:** hoàn tất < 30 s (`SLA_IMPORT`); RAM ≤ +500 MB; trả preview đủ 500 dòng; 0 HTTP 5xx. |
| NFR_G1_010 | G1-02 | Abnormal | POST `acc/payrolls/import`, POST `acc/attendance/import` | **[Load/Attack/Env]:** gửi cùng một file import 5 lần liên tiếp cách nhau < 200 ms (double-submit), đồng thời 20 VU đọc `timesheets`. | **[SLA/Threshold]:** 0 HTTP 5xx, 0 timeout; p95 `timesheets` < 3000 ms trong lúc import; dữ liệu sau import (đọc qua API) không bị nhân đôi. |
| NFR_G1_011 | G1-03 | Normal | GET `{adm,mgr,wk}/inventories[/stock-card,/team-holdings]` | **[Load/Attack/Env]:** 20 VU (Admin/Manager/WarehouseKeeper), `SEED_SMALL` = 10.000 giao dịch, giữ 10 phút. | **[SLA/Threshold]:** `inventories` p95 < 800 ms; `stock-card`, `team-holdings` p95 < 3000 ms; Error Rate = 0%. |
| NFR_G1_012 | G1-03 | Boundary | GET `{adm,mgr,wk}/inventories/nxt-report`, `/stock-card` | **[Load/Attack/Env]:** `VU_MAX`=100 VU, `SEED_LARGE` = 200.000 giao dịch kho, khoảng 12 tháng, giữ 10 phút. | **[SLA/Threshold]:** p95 < 4500 ms; Error Rate ≤ 1%; 0 HTTP 5xx. |
| NFR_G1_013 | G1-03 | Boundary | GET `inventories`, `nxt-report`, `stock-card`, GET `tl/piece-holdings` | **[Load/Attack/Env]:** chạy cùng kịch bản 20 VU / 10 phút hai lần: lần 1 trên `SEED_SMALL`, lần 2 trên `SEED_LARGE` (đo “chậm dần theo số giao dịch tích lũy”). | **[SLA/Threshold]:** p95(lần 2) / p95(lần 1) ≤ 2,0; cả hai lần p95 trong SLA tương ứng. |
| NFR_G1_014 | G1-03 | Abnormal | GET `tl/piece-holdings`, `wk/inventories` | **[Load/Attack/Env]:** tải đột biến (spike) 0 → 100 VU trong 10 s, giữ 2 phút, hạ về 0. | **[SLA/Threshold]:** 0 HTTP 5xx; Error Rate ≤ 1%; sau khi hạ tải 60 s, p95 1 VU trở về < 800 ms. |
| NFR_G1_015 | G1-04 | Normal | GET `{adm,mgr,acc,wk,tl,qa,wrk,electrician}/home` | **[Load/Attack/Env]:** 20 VU phân bổ theo 8 role (mỗi role ≥ 2 VU), think-time 3 s, giữ 10 phút. | **[SLA/Threshold]:** p95 mỗi role < 1000 ms (`SLA_HOME`); Error Rate = 0%. |
| NFR_G1_016 | G1-04 | Boundary | GET `wrk/home`, `tl/home`, `qa/home`, `mgr/home` | **[Load/Attack/Env]:** cao điểm đầu ca: ramp 0 → `VU_MAX`=100 VU trong 60 s (70% Worker, 20% TeamLeader, 10% QA/Manager), giữ 10 phút. | **[SLA/Threshold]:** p95 < 1500 ms; Error Rate ≤ 1%; 0 HTTP 5xx; ThreadPool queue length về 0 sau khi hạ tải. |
| NFR_G1_017 | G1-04 | Boundary | GET `{adm,mgr,tl}/home` | **[Load/Attack/Env]:** 1 VU, bật EF Core log; gọi 1 lần trên `SEED_SMALL` và 1 lần trên `SEED_LARGE` (kiểm N+1). | **[SLA/Threshold]:** số câu SQL / request **không đổi** giữa 2 quy mô seed (chênh ≤ 2 câu); thời gian 1 request < 1000 ms. |
| NFR_G1_018 | G1-04 | Abnormal | GET `{wrk,tl}/home` | **[Load/Attack/Env]:** 20 user, mỗi user F5 liên tục 10 request/giây trong 60 s (lặp request liên tục). | **[SLA/Threshold]:** 0 HTTP 5xx; p95 < 2000 ms; API vẫn phục vụ request của user khác (1 VU giám sát p95 < 1000 ms). |
| NFR_G1_019 | G1-05 | Normal | GET `{mgr,tl}/output-records/grid`, GET `tl/work-allocations/{board,daily}` | **[Load/Attack/Env]:** 20 VU Manager/TeamLeader, 1 tổ × 30 ngày, giữ 10 phút. | **[SLA/Threshold]:** p95 < 3000 ms; Error Rate = 0%. |
| NFR_G1_020 | G1-05 | Boundary | GET `{mgr,tl}/output-records/grid` | **[Load/Attack/Env]:** 20 VU, tổ có nhiều công nhân nhất seed × mọi công đoạn × 31 ngày (biên lưới tổ × công nhân × công đoạn × ngày). | **[SLA/Threshold]:** p95 < 3000 ms; kích thước response ≤ 5 MB (gzip); số câu SQL/request không tăng theo số công nhân (N+1); 0 HTTP 5xx. |
| NFR_G1_021 | G1-05 | Boundary | GET `mgr/kpi-targets/{order-capacity,line-capacity,{id}/calendar,{id}/daily-results,{id}/finish-preview}`, GET `{adm,mgr}/order-progress` | **[Load/Attack/Env]:** `VU_MAX`=100 VU Admin/Manager, KPI target có lịch dài nhất seed, giữ 10 phút. | **[SLA/Threshold]:** p95 < 4500 ms; Error Rate ≤ 1%; 0 HTTP 5xx. |
| NFR_G1_022 | G1-05 | Abnormal | GET `{adm,mgr}/order-progress`, `tl/work-allocations/board` | **[Load/Attack/Env]:** spike 0 → 100 VU trong 10 s, mỗi VU F5 2 request/giây, 2 phút. | **[SLA/Threshold]:** 0 HTTP 5xx; Error Rate ≤ 1%; p95 hồi về < 3000 ms trong 60 s sau spike. |
| NFR_G1_023 | G1-06 | Normal | POST `mgr/work-orders/fabric-check`, GET `wk/work-orders/{id}/fabric-needs` | **[Load/Attack/Env]:** 5 VU Manager/WarehouseKeeper, đơn hàng cỡ trung bình seed, giữ 10 phút. | **[SLA/Threshold]:** p95 < 3000 ms; CPU API ≤ 70%; Error Rate = 0%. |
| NFR_G1_024 | G1-06 | Boundary | POST `mgr/order-markers/**` (import, excel parse/check) | **[Load/Attack/Env]:** 1 request với file sơ đồ cắt lớn nhất seed (nhiều sheet/nhiều size), thuật toán OR-Tools/Branch-and-Bound chạy hết. | **[SLA/Threshold]:** hoàn tất < 30 s; CPU đỉnh ≤ 85% và không giữ 100% > 10 s liên tục; 0 HTTP 5xx. |
| NFR_G1_025 | G1-06 | Boundary | POST `tl/grade-suggestions/generate` (Gemini **stub**) | **[Load/Attack/Env]:** stub trả trễ 10 s; 10 luồng `Task.WhenAll` TeamLeader gọi `generate`; song song 20 VU gọi `tl/home`. | **[SLA/Threshold]:** 10/10 request hoàn tất < 15 s; `tl/home` p95 < 1000 ms (không đói ThreadPool); 0 HTTP 5xx. |
| NFR_G1_026 | G1-06 | Abnormal | POST `tl/grade-suggestions/generate` (Gemini **stub**) | **[Load/Attack/Env]:** stub lần lượt trả: timeout 60 s, HTTP 429 (quota), HTTP 500, JSON sai định dạng. | **[SLA/Threshold]:** API trả lỗi nghiệp vụ (4xx/503 có thông điệp) trong < 35 s, **không** HTTP 500 không xử lý; không request treo vô thời hạn. |
| NFR_G1_027 | G1-06 | Abnormal | POST `mgr/order-markers/**`, POST `mgr/work-orders/fabric-check` | **[Load/Attack/Env]:** 10 luồng `Task.WhenAll` gửi import/parse sơ đồ cắt cùng lúc (CPU nặng), song song 20 VU gọi `mgr/home`. | **[SLA/Threshold]:** `mgr/home` p95 < 1500 ms; 0 HTTP 5xx; tiến trình không restart; CPU về < 20% trong 60 s sau khi xong. |
| NFR_G1_028 | G1-07 | Normal | POST import `adm/{products,customer-orders,order-materials,employee-import}`, `mgr/{order-materials,employee-import}`, `acc/{operations,monthly-costs}` | **[Load/Attack/Env]:** 1 request mỗi API, file 1.000 dòng, 1 sheet, dữ liệu hợp lệ, role đúng prefix. | **[SLA/Threshold]:** mỗi import < 10 s; 1.000/1.000 dòng được ghi (đếm qua API danh sách); 0 HTTP 5xx. |
| NFR_G1_029 | G1-07 | Boundary | POST `adm/products` import, POST `acc/monthly-costs/import` | **[Load/Attack/Env]:** file 10.000 dòng, nhiều sheet; trong lúc import chạy, 20 VU GET danh sách cùng bảng (`adm/products`). | **[SLA/Threshold]:** import < 30 s; p95 GET danh sách trong lúc import < 1500 ms (transaction không khóa đọc kéo dài); RAM ≤ +500 MB; 0 HTTP 5xx. |
| NFR_G1_030 | G1-07 | Normal | GET `.../{id}/excel` (phiếu nhập, phiếu xuất, phiếu giao hàng) | **[Load/Attack/Env]:** 20 VU (WarehouseKeeper/Manager/Accountant), chứng từ nhiều dòng nhất seed, giữ 10 phút. | **[SLA/Threshold]:** p95 < 10 s; file tải về mở được; Error Rate = 0%. |
| NFR_G1_031 | G1-07 | Abnormal | POST import `adm/customer-orders`, `adm/employee-import` | **[Load/Attack/Env]:** client ngắt kết nối (abort) sau 2 s khi import file 10.000 dòng, rồi gửi lại ngay. | **[SLA/Threshold]:** lần gửi lại thành công < 30 s; dữ liệu không bị ghi dở/nhân đôi (đếm qua API); 0 HTTP 5xx; không còn transaction treo (request kế tiếp < 800 ms). |
| NFR_G1_032 | G1-08 | Normal | GET `adm/audit-logs`; `cm/{notifications,requests,request-inbox}`; `{adm,mgr}/{employees,materials,order-materials}`; `adm/{customer-orders,products,grade-evaluations}`; `wk/{material-receipts,material-issues}` | **[Load/Attack/Env]:** 20 VU, page mặc định, không lọc, giữ 10 phút. | **[SLA/Threshold]:** p95 < 800 ms (`SLA_LIST`); Error Rate = 0%; mọi response có thông tin phân trang. |
| NFR_G1_033 | G1-08 | Boundary | Các API danh sách G1-08 | **[Load/Attack/Env]:** 1 VU gọi `pageSize=100` (= `MaxPageSize`) và `pageSize=101` trên bảng ≥ 100.000 bản ghi. | **[SLA/Threshold]:** `pageSize=100` → ≤ 100 item, < 800 ms; `pageSize=101` → ≤ 100 item (bị chặn) hoặc HTTP 400; **không** API nào trả > 100 item. |
| NFR_G1_034 | G1-08 | Boundary | GET `adm/audit-logs`, `{adm,mgr}/employees`, `adm/customer-orders` có `search` + lọc ngày | **[Load/Attack/Env]:** `VU_MAX`=100 VU, bảng audit-logs 200.000 dòng, từ khóa tìm ngẫu nhiên, giữ 10 phút. | **[SLA/Threshold]:** p95 < 1200 ms (1,5 × `SLA_LIST`); Error Rate ≤ 1%; 0 HTTP 5xx. |
| NFR_G1_035 | G1-08 | Abnormal | Các API danh sách G1-08 | **[Load/Attack/Env]:** 1 VU gửi `pageSize=100000`, `page=999999`, `pageSize` thiếu, kết hợp 20 VU nền. | **[SLA/Threshold]:** response ≤ 100 item hoặc HTTP 400 trong < 800 ms; page vượt → danh sách rỗng, không 5xx; p95 20 VU nền giữ < 800 ms. |
| NFR_G1_036 | G1-09 | Boundary | `DailyScheduledJob`, `WorkAllocationAutoConfirmJob`, `OrderMonitorJobs` + GET `*/home`, `tl/work-allocations/board` | **[Load/Attack/Env]:** chạy tải NFR_G1_015 (20 VU, 10 phút); tại phút thứ 5 kích hoạt các job (hoặc chỉnh lịch để chạy). | **[SLA/Threshold]:** p95 trong cửa sổ job tăng ≤ 20% so với trước job; 0 lỗi deadlock/timeout; 0 HTTP 5xx. |
| NFR_G1_037 | G1-09 | Abnormal | `MealAutoCloseJob`, `MealReminderJob`, `AttendanceSyncMonitorJob` + PATCH `cm/my-meals/today`, GET `tl/meals/confirmation` | **[Load/Attack/Env]:** 100 Worker PATCH suất ăn liên tục trong 2 phút quanh thời điểm job chốt chạy. | **[SLA/Threshold]:** 0 HTTP 5xx; không request > 30 s; job hoàn tất (trạng thái chốt đọc được qua API) trong < 60 s. |
| NFR_G1_038 | G1-01 → G1-08 | Normal | Kịch bản trộn: home, danh sách, báo cáo, tồn kho | **[Load/Attack/Env]:** soak test 20 VU trộn mọi role, 60 phút liên tục. | **[SLA/Threshold]:** p95 phút 55–60 lệch ≤ 10% so với phút 5–10; RAM không tăng liên tục (> +10% xu hướng = fail); Error Rate = 0%. |

## 2. Truy vết bao phủ

| Mục tiêu nguồn | Normal | Boundary | Abnormal |
|---|---|---|---|
| G1-01 | 001, 002 | 003, 004 | 005, 006 |
| G1-02 | 007 | 008, 009 | 010 |
| G1-03 | 011 | 012, 013 | 014 |
| G1-04 | 015 | 016, 017 | 018 |
| G1-05 | 019 | 020, 021 | 022 |
| G1-06 | 023 | 024, 025 | 026, 027 |
| G1-07 | 028, 030 | 029 | 031 |
| G1-08 | 032 | 033, 034 | 035 |
| G1-09 | 038 (soak) | 036 | 037 |
