# ST NFR TEST CASES — Group3_Security (NX-MTD)

> **Nguồn duy nhất:** `ST_NFR_TARGETS.md` — mục 4 (G3-01 → G3-10) và mục 6. Mọi rủi ro trong nguồn là *giả thuyết cần kiểm chứng*.
> **Môi trường:** **chỉ localhost/test**, không thử bảo mật trên môi trường đang triển khai. DB reset + seed riêng cho G3; dữ liệu tạo ra có tiền tố `ST_AUTO_`.
> **Cách chạy:** xUnit + `HttpClient` (API), Playwright (G3-10). Kết quả kiểm qua HTTP status + API công khai, không Assert bằng DB.

## 0. Quy ước

| Ký hiệu | Ý nghĩa |
|---|---|
| Role (9) | Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, QaQc, Worker, Electrician |
| `TOK_<ROLE>` | Access token hợp lệ của role đó (seed `ST_AUTO_`) |
| `A` / `B` | Hai người dùng cùng role, thuộc hai tổ khác nhau (`TEAM_A`, `TEAM_B`) |
| Chặn hợp lệ cho IDOR | HTTP 403 **hoặc** 404 (không lộ sự tồn tại); body không chứa dữ liệu của bản ghi đích |
| Lỗi an toàn | HTTP 4xx, body ProblemDetails **không** chứa stack trace, tên bảng, chuỗi kết nối |

## 1. Bảng Test Case

| TC ID | Mục tiêu | Loại | Endpoint / Màn hình | Kịch bản | Kết quả mong đợi |
|---|---|---|---|---|---|
| NFR_G3_001 | G3-01 | Normal | `adm/*`, `mgr/*`, `acc/*`, `wk/*`, `tl/*`, `qa/*`, `wrk/*`, `electrician/*` (GET `home` + 1 GET danh sách mỗi prefix) | **[Load/Attack/Env]:** mỗi role dùng `TOK_<ROLE>` gọi prefix được phép (Admin→`adm`,`mgr`; Manager→`mgr`; TeamLeader & TeamDeputy→`tl`; …). | **[SLA/Threshold]:** HTTP 200 cho mọi cặp role–prefix hợp lệ. |
| NFR_G3_002 | G3-01 | Abnormal | Như 001 | **[Load/Attack/Env]:** ma trận 9 role × 8 prefix; với mọi cặp **không** được phép, gọi GET + 1 POST đại diện bằng `TOK_<ROLE>`. | **[SLA/Threshold]:** HTTP **403 Forbidden** cho 100% cặp sai role; không có thay đổi dữ liệu do POST (đọc lại qua API). |
| NFR_G3_003 | G3-01 | Abnormal | Toàn bộ API không `[AllowAnonymous]` | **[Load/Attack/Env]:** gọi không header `Authorization`; và `Authorization: Bearer ` (rỗng). | **[SLA/Threshold]:** HTTP **401 Unauthorized** cho 100% endpoint; ngoại lệ duy nhất: `cm/auth/{login,refresh}` và `agent/attendance-sync/*` (có key riêng, xem G3-07). |
| NFR_G3_004 | G3-01 | Boundary | Mọi controller (liệt kê qua Swagger/ApiExplorer) | **[Load/Attack/Env]:** quét tự động toàn bộ route; gọi từng route không token. Không thấy `FallbackPolicy`. | **[SLA/Threshold]:** số route trả 2xx khi không token = số route trong whitelist anonymous (`login`, `refresh`); mọi route ngoài whitelist → 401. Route mới phát sinh mà mở công khai = Fail. |
| NFR_G3_005 | G3-02 | Normal | GET `acc/attendance/timesheets` | **[Load/Attack/Env]:** gọi bằng `TOK_ACCOUNTANT`, `TOK_ADMIN`, `TOK_MANAGER`. | **[SLA/Threshold]:** HTTP 200 cho cả 3 role. |
| NFR_G3_006 | G3-02 | Abnormal | `acc/attendance/sync-sessions`, POST `acc/attendance/rj-employees/sync`, POST `acc/attendance/import[/preview]` | **[Load/Attack/Env]:** gọi bằng `TOK_ACCOUNTANT` và `TOK_MANAGER` (Admin-only). | **[SLA/Threshold]:** HTTP **403**; `TOK_ADMIN` → 2xx. |
| NFR_G3_007 | G3-02 | Abnormal | `cm/{grade-code-options,hr-lookups,order-lookups}` | **[Load/Attack/Env]:** gọi bằng `TOK_WORKER`. | **[SLA/Threshold]:** HTTP **403**; 8 role còn lại → 200. |
| NFR_G3_008 | G3-02 | Abnormal | `cm/rj-employees`, `cm/import-templates`, `cm/request-inbox` | **[Load/Attack/Env]:** `rj-employees`, `import-templates` gọi bằng WarehouseKeeper/TeamLeader/QaQc/Worker/Electrician; `request-inbox` gọi bằng Worker/Electrician (không phải Handlers/TeamLeader/Deputy). | **[SLA/Threshold]:** HTTP **403** cho mọi role ngoài danh sách cho phép. |
| NFR_G3_009 | G3-02 | Abnormal | `tl/{cut-handovers,cutting-lays,work-orders}` (`[CuttingTeamOnly]`) | **[Load/Attack/Env]:** `TOK_TEAMLEADER` của **tổ may** gọi GET + POST. | **[SLA/Threshold]:** HTTP **403**; TeamLeader tổ cắt → 2xx. |
| NFR_G3_010 | G3-02 | Boundary | GET `acc/attendance/timesheets?teamId=…` | **[Load/Attack/Env]:** `TOK_MANAGER` truyền `teamId` ngoài phạm vi quản lý (nếu phạm vi được định nghĩa) và `teamId` không tồn tại. | **[SLA/Threshold]:** HTTP 403 hoặc danh sách rỗng; không trả chấm công ngoài phạm vi; `teamId` không tồn tại → 404/rỗng, không 500. |
| NFR_G3_011 | G3-03 | Normal | GET `cm/requests/{id}`, `cm/my-payslips/{periodMonth}` | **[Load/Attack/Env]:** user `A` đọc đơn và phiếu lương của chính mình. | **[SLA/Threshold]:** HTTP 200; dữ liệu đúng của `A`. |
| NFR_G3_012 | G3-03 | Abnormal | GET/PUT `cm/requests/{id}`, POST `cm/requests/{id}/cancel`, POST `cm/request-inbox/{id}/*` | **[Load/Attack/Env]:** `A` đổi `{id}` sang đơn của `B`; thử thêm `id` = id lân cận ±1…±50. | **[SLA/Threshold]:** Chặn hợp lệ (403/404) 100%; đơn của `B` không đổi trạng thái. |
| NFR_G3_013 | G3-03 | Abnormal | POST `cm/notifications/{id}/read` | **[Load/Attack/Env]:** `A` đánh dấu đã đọc thông báo của `B`. | **[SLA/Threshold]:** 403/404; trạng thái thông báo của `B` vẫn chưa đọc (đọc lại bằng `TOK_B`). |
| NFR_G3_014 | G3-03 | Abnormal | GET `cm/my-payslips/{periodMonth}`, `cm/my-estimated-pay`, các API `my-*` | **[Load/Attack/Env]:** `TOK_WORKER_A` chèn thêm `?userId=`, `?employeeId=`, `?workerId=` của `B` (query và body). | **[SLA/Threshold]:** tham số bị bỏ qua — response luôn là dữ liệu của `A`, hoặc HTTP 400; **không** trả dữ liệu `B`. |
| NFR_G3_015 | G3-03 | Abnormal | POST `wrk/work-allocations/{id}/{confirm,reject}`, POST `wrk/work-allocations/confirm-batch` | **[Load/Attack/Env]:** Worker `A` confirm phân công của `B`; `confirm-batch` body gồm 2 ID của `A` + 1 ID của `B`. | **[SLA/Threshold]:** đơn lẻ → 403/404; batch → từ chối toàn bộ (400/403) **hoặc** chỉ xử lý ID của `A`; phân công của `B` không đổi. |
| NFR_G3_016 | G3-03 | Abnormal | GET/PUT/POST `tl/{material-issues,material-returns,cutting-lays,cut-handovers,operation-assignments,defect-records,work-allocations}/{id}` | **[Load/Attack/Env]:** TeamLeader `TEAM_A` thao tác bằng `{id}` thuộc `TEAM_B`. | **[SLA/Threshold]:** 403/404 cho 100% endpoint; dữ liệu `TEAM_B` không đổi. |
| NFR_G3_017 | G3-03 | Abnormal | GET `tl/grade-suggestions/items/{workerId}`, POST `tl/work-allocations/workers/{workerId}/confirm-on-behalf` | **[Load/Attack/Env]:** TeamLeader `TEAM_A` truyền `workerId` thuộc `TEAM_B`. | **[SLA/Threshold]:** 403/404; không xác nhận hộ được worker tổ khác. |
| NFR_G3_018 | G3-03 | Boundary | Các API danh sách `tl/*` nhận query `teamId`/`workerId` | **[Load/Attack/Env]:** TeamLeader `TEAM_A` truyền `teamId=TEAM_B`, `teamId` rỗng, `teamId=0`, `teamId=-1`. | **[SLA/Threshold]:** chỉ trả dữ liệu `TEAM_A` hoặc 403/400; không 500. |
| NFR_G3_019 | G3-04 | Abnormal **(ưu tiên cao)** | GET `cm/attachments`, GET `cm/attachments/{id}/content` | **[Load/Attack/Env]:** `TOK_WORKER` liệt kê và tải file đính kèm thuộc chứng từ tài chính/đơn của người khác (ID lấy từ seed). Class chỉ có `[Authorize]`. | **[SLA/Threshold]:** 403/404; không trả nội dung file; danh sách chỉ gồm file Worker được phép xem. |
| NFR_G3_020 | G3-04 | Abnormal **(ưu tiên cao)** | POST `cm/attachments` (`ownerType` + `ownerId`) | **[Load/Attack/Env]:** `TOK_WORKER` đính kèm file vào `ownerType`/`ownerId` của đơn hàng/bảng lương không thuộc mình; thử `ownerType` không hợp lệ. | **[SLA/Threshold]:** 403 (không có quyền với đối tượng) hoặc 400 (`ownerType` sai); file không xuất hiện ở đối tượng đích. |
| NFR_G3_021 | G3-04 | Abnormal **(ưu tiên cao)** | DELETE `cm/attachments/{id}` | **[Load/Attack/Env]:** `TOK_WORKER_A` xóa file do `B`/Accountant tải lên. | **[SLA/Threshold]:** 403/404; file vẫn tải được bằng token chủ sở hữu. |
| NFR_G3_022 | G3-04 | Boundary | GET `cm/lookups/*` (đơn hàng, tổ, NVL) | **[Load/Attack/Env]:** `TOK_WORKER` gọi mọi lookup, so sánh trường trả về với `TOK_ADMIN`. | **[SLA/Threshold]:** Worker chỉ nhận dữ liệu tối thiểu cho chức năng của mình (danh sách trường được phép do nhóm chốt); trường nhạy cảm (giá, chi phí) = Fail nếu xuất hiện. |
| NFR_G3_023 | G3-05 | Abnormal | GET/PUT `acc/{order-payments,purchase-payments}/{id}/payments/{paymentId}` | **[Load/Attack/Env]:** `TOK_ACCOUNTANT` dùng `{id}` = chứng từ X, `paymentId` thuộc chứng từ Y. | **[SLA/Threshold]:** HTTP 404 (không khớp cha–con); payment của Y không đổi. |
| NFR_G3_024 | G3-05 | Abnormal | GET/PUT `mgr/order-markers/{orderId}/products/{productId}` | **[Load/Attack/Env]:** `productId` không thuộc `orderId`. | **[SLA/Threshold]:** HTTP 404; không đọc/sửa được sản phẩm của đơn khác. |
| NFR_G3_025 | G3-05 | Abnormal | GET/PUT `qa/qc-batches/{id}`, `qa/fg-handovers/{id}` | **[Load/Attack/Env]:** QaQc `A` sửa/hủy chứng từ do QaQc `B` tạo. | **[SLA/Threshold]:** 403 (nếu quy tắc chỉ người tạo được sửa) — **nhóm chốt quy tắc**; tối thiểu: không 500 và có audit log ghi người sửa. |
| NFR_G3_026 | G3-05 | Abnormal | POST `{adm,acc}/payrolls/{id}/{submit,approve}`, GET `.../{id}/file` | **[Load/Attack/Env]:** `TOK_ACCOUNTANT` gọi `adm/payrolls/{id}/approve`; `TOK_MANAGER`/`TOK_WORKER` gọi `acc/payrolls/{id}/file`; `{id}` không tồn tại. | **[SLA/Threshold]:** sai role → 403; `{id}` không tồn tại → 404; không lộ file lương. |
| NFR_G3_027 | G3-06 | Normal | POST `cm/auth/login`, POST `cm/auth/refresh`, GET `cm/auth/me`, POST `cm/auth/logout` | **[Load/Attack/Env]:** đăng nhập đúng → dùng access token gọi `me` → refresh → logout. | **[SLA/Threshold]:** login 200 + access/refresh token; `me` 200 đúng user; refresh 200 token mới; logout 2xx. |
| NFR_G3_028 | G3-06 | Boundary | GET `cm/auth/me` | **[Load/Attack/Env]:** token hết hạn: `exp` + 30 s (trong ClockSkew 1 phút) và `exp` + 61 s (ngoài ClockSkew). | **[SLA/Threshold]:** +30 s → 200; +61 s → **HTTP 401**. |
| NFR_G3_029 | G3-06 | Abnormal | Bất kỳ API có `[Authorize]` | **[Load/Attack/Env]:** token bị sửa: đổi 1 ký tự chữ ký; đổi payload role `Worker`→`Admin` giữ chữ ký cũ; header `alg: none`; token ký bằng khóa khác. | **[SLA/Threshold]:** HTTP **401** cho 100% biến thể. |
| NFR_G3_030 | G3-06 | Abnormal | POST `cm/auth/refresh` | **[Load/Attack/Env]:** dùng lại refresh token (a) sau logout, (b) sau `{adm,mgr}/employees/{id}/account/lock`, (c) sau `reset-password`, (d) sau `cm/profile/change-password`, (e) dùng cùng refresh token 2 lần. | **[SLA/Threshold]:** HTTP **401** cho (a)–(d); (e) lần 2 → 401 (nếu xoay vòng token) — ghi nhận rủi ro nếu 200. |
| NFR_G3_031 | G3-06 | Abnormal | POST `cm/auth/login` | **[Load/Attack/Env]:** brute-force: 100 lần sai mật khẩu cùng username trong 60 s từ 1 IP; vòng 2: 100 username khác nhau. Không thấy rate limiter. | **[SLA/Threshold]:** sau ≤ 10 lần sai → HTTP 429 hoặc tài khoản tạm khóa (ngưỡng nhóm chốt); 0 HTTP 500. **Dự kiến Fail theo giả thuyết** → ghi nhận lỗ hổng. |
| NFR_G3_032 | G3-06 | Abnormal | POST `mgr/employees/{id}/account/{reset-password,lock,unlock}` | **[Load/Attack/Env]:** `TOK_MANAGER` reset mật khẩu / khóa tài khoản **Admin**. | **[SLA/Threshold]:** HTTP **403**; Admin vẫn đăng nhập được bằng mật khẩu cũ. |
| NFR_G3_033 | G3-06 | Abnormal | POST `cm/auth/login`, POST `cm/profile/change-password` | **[Load/Attack/Env]:** login với username `' OR 1=1--`, `admin'--`, chuỗi 10.000 ký tự; change-password với mật khẩu cũ sai. | **[SLA/Threshold]:** login → 401/400, **không** đăng nhập được; chuỗi dài → 400; change-password sai → 400; 0 HTTP 500. |
| NFR_G3_034 | G3-06 | Abnormal | Route FE `/login` | **[Load/Attack/Env]:** đăng nhập sai liên tiếp 5 lần trên giao diện. | **[SLA/Threshold]:** thông báo lỗi chung (không tiết lộ “sai username” hay “sai mật khẩu”); không có lỗi Console chứa token. |
| NFR_G3_035 | G3-07 | Normal | `agent/attendance-sync/{cursor,punches,employees,heartbeat}` | **[Load/Attack/Env]:** header `X-Agent-Key` đúng, batch 100 punch. | **[SLA/Threshold]:** HTTP 2xx. |
| NFR_G3_036 | G3-07 | Abnormal | `agent/attendance-sync/*` | **[Load/Attack/Env]:** (a) thiếu `X-Agent-Key`; (b) key sai 1 ký tự cuối; (c) key rỗng; (d) chỉ gửi `TOK_ADMIN` (JWT người dùng) không key. | **[SLA/Threshold]:** HTTP **401** cho (a)–(d); không ghi punch. |
| NFR_G3_037 | G3-07 | Boundary | POST `agent/attendance-sync/punches` | **[Load/Attack/Env]:** key đúng, batch 10.000 punch; batch 100.000 punch. | **[SLA/Threshold]:** 10.000 → xử lý < 30 s hoặc 413/400 có thông điệp; 100.000 → 413/400; 0 HTTP 500; RAM ≤ +500 MB. |
| NFR_G3_038 | G3-07 | Abnormal | POST `agent/attendance-sync/punches` | **[Load/Attack/Env]:** replay: gửi lại nguyên request đã thành công 10 lần (cùng key, cùng body). | **[SLA/Threshold]:** không tạo punch trùng (đọc qua `timesheets`); 2xx idempotent hoặc 409. |
| NFR_G3_039 | G3-08 | Boundary | POST `cm/attachments`, POST `adm/products/sample-image` | **[Load/Attack/Env]:** file kích thước = `RequestSizeLimit` (chạm ngưỡng) và = limit + 1 byte. | **[SLA/Threshold]:** = limit → 2xx; limit + 1 byte → HTTP **413 Payload Too Large**; 0 HTTP 500. |
| NFR_G3_040 | G3-08 | Abnormal | Mọi API import G1-07, G1-02, `mgr/order-markers` (không thấy `RequestSizeLimit`) | **[Load/Attack/Env]:** upload file `.xlsx` 50 MB và 200 MB. | **[SLA/Threshold]:** HTTP 413/400 trong < 10 s; RAM ≤ +500 MB; tiến trình không restart. |
| NFR_G3_041 | G3-08 | Abnormal | Như 040 | **[Load/Attack/Env]:** zip-bomb `.xlsx` (file nén ~1 MB, giải nén > 1 GB). | **[SLA/Threshold]:** HTTP 400 trong < 10 s; RAM ≤ +200 MB; 0 HTTP 500. |
| NFR_G3_042 | G3-08 | Abnormal | `cm/attachments`, `adm/products/sample-image`, các API import | **[Load/Attack/Env]:** file `.exe` đổi đuôi `.xlsx`/`.png`; `Content-Type` giả; tên file `../../appsettings.json`, `..\\..\\web.config`, `a.png%00.exe`. | **[SLA/Threshold]:** sai MIME/nội dung → 400; tên file được làm sạch (tải lại thấy tên không chứa `..`, `/`, `\`); không ghi file ra ngoài thư mục lưu trữ. |
| NFR_G3_043 | G3-08 | Abnormal | Import `adm/products`, `adm/customer-orders` → export tương ứng | **[Load/Attack/Env]:** ô dữ liệu bắt đầu bằng `=HYPERLINK("http://evil")`, `+SUM(1)`, `-1+1`, `@SUM(1)`. | **[SLA/Threshold]:** file xuất lại lưu các ô này dưới dạng **text** (escape/tiền tố `'`), không thành công thức. |
| NFR_G3_044 | G3-09 | Abnormal | Query `search` của các danh sách G1-08 | **[Load/Attack/Env]:** `' OR 1=1--`, `'; WAITFOR DELAY '0:0:5'--`, `%' AND 1=CONVERT(int,@@version)--`. | **[SLA/Threshold]:** HTTP 200 (rỗng/đúng lọc) hoặc 400; thời gian < 800 ms (không trễ 5 s); không trả thêm bản ghi; 0 HTTP 500. |
| NFR_G3_045 | G3-09 | Abnormal | Trường text: tên NVL, ghi chú đơn, lý do từ chối, tên đính kèm | **[Load/Attack/Env]:** lưu `<script>alert(1)</script>`, `<img src=x onerror=alert(1)>`, `"><svg onload=alert(1)>`; mở màn hình hiển thị tương ứng bằng Playwright. | **[SLA/Threshold]:** không dialog `alert` xuất hiện; chuỗi hiển thị dạng text; 0 lỗi Console. |
| NFR_G3_046 | G3-09 | Boundary | `pageSize`, khoảng ngày, số lượng/số tiền trong body | **[Load/Attack/Env]:** `pageSize` = −1, 0, 2147483647; số lượng = −1, 0, 1e18; số tiền decimal tràn; ngày `0001-01-01`, `9999-12-31`. | **[SLA/Threshold]:** HTTP 400 với thông điệp validation; 0 HTTP 500. |
| NFR_G3_047 | G3-09 | Abnormal | Body mảng của `*/bulk`, `*/batch`, `*-many`, `confirm-batch` | **[Load/Attack/Env]:** mảng 100.000 phần tử; mảng rỗng; phần tử trùng lặp. | **[SLA/Threshold]:** mảng lớn → 400/413 trong < 5 s; rỗng → 400; 0 HTTP 500. |
| NFR_G3_048 | G3-09 | Abnormal | Bất kỳ POST/PUT | **[Load/Attack/Env]:** JSON sai cú pháp, sai kiểu (`"qty":"abc"`), `Content-Type: text/plain`. | **[SLA/Threshold]:** HTTP 400/415; body là Lỗi an toàn (không stack trace, không đường dẫn file, không tên bảng). |
| NFR_G3_049 | G3-09 | Abnormal | CORS policy `Web` | **[Load/Attack/Env]:** preflight `OPTIONS` với `Origin: http://evil.test`; kiểm tra header HSTS ở reverse proxy test. | **[SLA/Threshold]:** không trả `Access-Control-Allow-Origin` cho origin lạ; không có `*` kèm `Allow-Credentials: true`; HSTS có mặt ở proxy (nếu thiếu → ghi nhận). |
| NFR_G3_050 | G3-10 | Normal | `fe/src/features/*/routes.tsx` | **[Load/Attack/Env]:** Playwright, Chromium 1920×1080; mỗi role đăng nhập và mở các route của mình. | **[SLA/Threshold]:** màn hình hiển thị; 0 lỗi Console; 0 response 4xx/5xx. |
| NFR_G3_051 | G3-10 | Abnormal | Gõ thẳng URL trái role (vd. Worker mở `/admin/orders`, `/payrolls/approval`, `/admin/audit-logs`) | **[Load/Attack/Env]:** Playwright, `TOK_WORKER`, điều hướng trực tiếp; ghi lại network. | **[SLA/Threshold]:** chuyển về trang 403/trang chủ; không có response 200 nào từ `adm/*`/`acc/*` trong network log. |
| NFR_G3_052 | G3-10 | Boundary | `/profile`, `/my/payslips` (không khai báo `roles`) | **[Load/Attack/Env]:** mở bằng cả 9 role. | **[SLA/Threshold]:** mỗi role chỉ thấy dữ liệu của chính mình; role không có phiếu lương → trạng thái rỗng, không lỗi. |
| NFR_G3_053 | G3-10 | Abnormal | Mọi route có `ProtectedRoute` | **[Load/Attack/Env]:** chưa đăng nhập, mở deep link; xóa token khỏi storage giữa phiên rồi bấm chuyển trang. | **[SLA/Threshold]:** chuyển về `/login`; không hiển thị dữ liệu đã cache của phiên trước. |

## 2. Truy vết bao phủ

| Mục tiêu nguồn | Normal | Boundary | Abnormal |
|---|---|---|---|
| G3-01 | 001 | 004 | 002, 003 |
| G3-02 | 005 | 010 | 006, 007, 008, 009 |
| G3-03 | 011 | 018 | 012–017 |
| G3-04 | — | 022 | 019, 020, 021 |
| G3-05 | — | — | 023, 024, 025, 026 |
| G3-06 | 027 | 028 | 029–034 |
| G3-07 | 035 | 037 | 036, 038 |
| G3-08 | — | 039 | 040–043 |
| G3-09 | — | 046 | 044, 045, 047, 048, 049 |
| G3-10 | 050 | 052 | 051, 053 |
