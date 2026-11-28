# BẢN ĐỒ MỤC TIÊU KIỂM THỬ PHI CHỨC NĂNG (NFR SYSTEM TEST) — NXMTD

> **Cơ sở:** tên file, route, `[Authorize]`/filter, khai báo khóa và token đồng thời. Chưa đọc logic xử lý, nên mọi mục "Rủi ro" là **giả thuyết cần kiểm chứng**, không phải lỗi đã xác nhận.
> **Quy ước:** mọi API có tiền tố `/api/v1/`; `{a,b}` là viết gộp nhiều đường dẫn. `adm`=admin, `mgr`=manager, `acc`=accountant, `wk`=warehouse-keeper, `tl`=team-leader, `qa`=qa-qc, `cm`=common, `wrk`=worker.

## 1. Tổng quan

| Group ID | Tên Nhóm | Danh sách Endpoint / Màn hình trọng điểm | Rủi ro NFR chính |
|---|---|---|---|
| G1 | Group1_Performance | Báo cáo, xuất/nhập Excel, tồn kho, Home, danh sách lớn | Phản hồi chậm, tốn RAM, timeout |
| G2 | Group2_Concurrency | Kho, mã chứng từ, duyệt/xác nhận, phân công, sản lượng, công, lương, thanh toán | Ghi trùng, mất cập nhật, tồn âm, vượt hạn mức, deadlock |
| G3 | Group3_Security | Toàn bộ API theo role, API có `{id}`, auth, agent, upload | Vượt quyền, IDOR, lộ dữ liệu, DoS do payload |
| G4 | Group4_Compatibility | Màn hình chính của từng role trên Desktop/Tablet/Mobile | Vỡ layout, tải chậm, lỗi Console, response 5xx |

## 2. Group1_Performance

| Group ID | Tên Nhóm | Danh sách Endpoint / Màn hình trọng điểm | Rủi ro NFR chính |
|---|---|---|---|
| G1-01 | Báo cáo kế toán & quản trị | GET `acc/accounting-reports/{workdays,meals,vehicles,export}`; GET `{acc,adm}/revenue[/export,/filter-options]`; GET `adm/cost-summary[/export]`; GET `adm/break-even[/inputs,/export]`, POST `adm/break-even/calculate` | Tổng hợp nhiều bảng, khoảng ngày rộng; xuất Excel (ClosedXML) tốn RAM |
| G1-02 | Lương & công | GET `acc/payrolls[/export,/{id}/file]`; POST `acc/payrolls/import[/preview]`; GET `{acc,tl}/attendance/timesheets[/summary]`; POST `acc/attendance/import[/preview]`; GET `cm/my-estimated-pay[/days/{date}]` | File nhiều nhân viên/ngày công; lương tạm tính có thể bị nhiều công nhân gọi cùng lúc |
| G1-03 | Tồn kho | GET `{adm,mgr,wk}/inventories[/stock-card,/nxt-report,/team-holdings]`; GET `tl/piece-holdings` | Chậm dần theo số giao dịch kho tích lũy |
| G1-04 | Dashboard theo role | GET `{adm,mgr,acc,wk,tl,qa,wrk,electrician}/home` | Nhiều truy vấn tổng hợp mỗi lần mở; tải cao điểm đầu ca; N+1 |
| G1-05 | Sản xuất & KPI | GET `mgr/kpi-targets/{order-capacity,line-capacity,{id}/calendar,{id}/daily-results,{id}/finish-preview}`; GET `{mgr,tl}/output-records/grid`; GET `tl/work-allocations/{board,daily}`; GET `{adm,mgr}/order-progress` | Lưới tổ × công nhân × công đoạn × ngày; N+1; payload lớn |
| G1-06 | Thuật toán & dịch vụ nặng | POST `mgr/order-markers/**` (import, excel parse/check); GET `{wk}/work-orders/{id}/fabric-needs`; POST `mgr/work-orders/fabric-check`; POST `tl/grade-suggestions/generate` | CPU cao (OR-Tools, Branch-and-Bound); gọi Gemini bên ngoài: độ trễ, timeout, quota |
| G1-07 | Import & xuất chứng từ | POST import tại `adm/{products,customer-orders,order-materials,employee-import}`, `mgr/{order-materials,employee-import}`, `acc/{operations,monthly-costs}`; GET `.../{id}/excel` (phiếu nhập, phiếu xuất, phiếu giao hàng) | File nhiều dòng/nhiều sheet; request kéo dài, giữ transaction lâu |
| G1-08 | Danh sách lớn | GET `adm/audit-logs`; `cm/{notifications,requests,request-inbox}`; `{adm,mgr}/{employees,materials,order-materials}`; `adm/{customer-orders,products,grade-evaluations}`; `wk/{material-receipts,material-issues}` | Cần xác nhận phân trang thực sự áp dụng (có hằng `MaxPageSize = 100` nhưng chưa kiểm từng API); lọc/tìm kiếm thiếu index |
| G1-09 | Job nền (đo ảnh hưởng gián tiếp) | `DailyScheduledJob`, `OrderMonitorJobs`, `MealAutoCloseJob`, `MealReminderJob`, `WorkAllocationAutoConfirmJob`, `AttendanceSyncMonitorJob` | Tranh khóa/tài nguyên với request người dùng khi job chạy |

## 3. Group2_Concurrency

**Dấu hiệu bảo vệ tìm thấy:**
- `sp_getapplock` (qua `IAppLock`): mã chứng từ, mã vật tư, tồn kho, bàn giao cắt, thứ tự bàn cắt, bàn giao thành phẩm, phiếu giao hàng, trả NVL, chuyến xe, phân công/sản lượng.
- RowVersion/concurrency token: `CustomerOrder`, `DefectRecord`, `DispatchNote`, `Employee`, `MaterialIssue`, `MaterialReceipt`, `OutputRecord`, `Payroll`, `PurchaseOrder`, `QcBatch`, `Request`.

Có khai báo không đồng nghĩa mọi luồng đều được bảo vệ. "Không thấy" chỉ có nghĩa là lượt quét này không tìm thấy; unique index hoặc kiểm tra trạng thái vẫn có thể đang bảo vệ.

| Group ID | Tên Nhóm | Danh sách Endpoint / Màn hình trọng điểm | Rủi ro NFR chính |
|---|---|---|---|
| G2-01 | Xuất/nhập/trả kho | POST `wk/material-issues`, `tl/material-issues/{id}/{confirm,reject}`; POST/PUT `wk/material-receipts[/supplementary,/{id}]`, `mgr/material-receipts/{id}/{confirm,return}`; POST `tl/material-returns[/{id}/cancel]`, `wk/material-returns/{id}/{confirm,reject}` | Tồn âm, trừ/cộng hai lần, confirm đua với reject/sửa, rollback thiếu |
| G2-02 | Kiểm kê | POST/PUT `wk/stocktakes`; POST `{adm,mgr}/stocktakes/{id}/{approve,return}` | Duyệt hai lần; duyệt khi đang xuất/nhập làm lệch tồn. Không thấy RowVersion trên `Stocktake` |
| G2-03 | Thành phẩm & giao hàng | POST `qa/fg-handovers[/handover-all,/{id}/cancel]`; `wk/fg-handovers/{id}/{confirm,reject}`; POST `mgr/dispatch-notes[/{id}/cancel]`; `wk/dispatch-notes/{id}/{export,reject}`; `acc/dispatch-notes/{id}/confirm-delivery` | Bàn giao/xuất vượt số có; cancel đua với confirm/export |
| G2-04 | Cắt | POST/PUT `mgr/work-orders[/replacement,/{id}/close-short,/{id}/cancel]`; POST/PUT `tl/cutting-lays`; `tl/cut-handovers[/{id}/cancel]`; `wk/cut-handovers/{id}/{confirm,reject}` | Trùng số bàn cắt, bàn giao vượt sản lượng, đóng/hủy lệnh khi đang cắt |
| G2-05 | Phân công, sản lượng, QC | POST/PUT `tl/work-allocations[/daily]`, các `confirm-on-behalf`; `wrk/work-allocations/{{id}/confirm,confirm-batch,{id}/reject}`; `tl/operation-assignments[/bulk,/copy,/{id}/replace,/{id}/close]`; `{tl,mgr}/output-records`; `tl/defect-records`, `qa/qc-batches`, `mgr/{defect-records,qc-batches}` (tạo/sửa/hủy) | Vượt hạn mức sản lượng, phân công trùng, đua với job tự xác nhận, deadlock khi khóa nhiều dòng. Không thấy app lock cho `operation-assignments` |
| G2-06 | Duyệt đơn & trạng thái | POST `cm/request-inbox/{id}/{approve,reject,complete,not-complete}`; PUT/POST `cm/requests/{id}[/cancel]`; `adm/customer-orders/{id}[/cancel,/start-production,/stop]`; `mgr/purchase-orders`, `mgr/purchase-order-tracking/{id}/{place,cancel,reprice}`; `mgr/short-deliveries/{id}/resolve`; `adm/piece-rates/{approve-many,{id}/approve,{id}/reject}`, `acc/piece-rates/{submit-many,{id}/submit}`; `{adm,mgr}/grade-evaluations/{set,{id}/apply,{id}/reject}`; `mgr/kpi-targets/{id}/{finish,cancel}` | Hai người duyệt cùng lúc, chạy side effect hai lần (đơn kéo theo kho/lệnh cắt), sửa đè sau khi đã duyệt |
| G2-07 | Chấm công & suất ăn | POST `acc/attendance/{adjustments,import}`; POST `agent/attendance-sync/{punches,employees,heartbeat}`; POST `acc/attendance/rj-employees/sync`; PATCH `cm/my-meals/today`; `tl/meals/{confirmation,members}`; `acc/meals/{guest-count,price}` | Punch trùng khi agent retry, import đua với sync, sửa suất ăn đúng lúc job chốt. Không thấy app lock |
| G2-08 | Chốt lương & công nợ | POST `acc/payrolls/{import,{id}/submit}`; `adm/payrolls/{id}/{approve,reject,reopen}`; POST/PUT `acc/order-payments/{id}/{payments,final-amount}`; `acc/purchase-payments/{id}/{confirm,payments,actual-amount,return}`; `acc/monthly-costs/import[/rows]` | Duyệt đua với import lại/reopen, ghi thanh toán hai lần, lost update tổng công nợ. Không thấy app lock; `OrderPayment` không có trong danh sách token |
| G2-09 | Mã tự sinh, xe, nhân sự | GET `adm/customer-orders/next-code` + POST tạo đơn; POST `{wk,mgr,adm,acc}/materials`; `acc/vehicle-trips[/{id}/complete]`; `{adm,mgr}/employees[/from-rj]`; `{adm,mgr}/teams/{id}/{transfer-in,add-members,remove-members,appoint,dismiss}` | Trùng mã/username (trả 500 thay vì lỗi nghiệp vụ), một xe hai chuyến, một người hai tổ/hai tổ trưởng |

## 4. Group3_Security

| Group ID | Tên Nhóm | Danh sách Endpoint / Màn hình trọng điểm | Rủi ro NFR chính |
|---|---|---|---|
| G3-01 | Ma trận role theo prefix | `adm/*`=Admin; `mgr/*`=Admin, Manager; `acc/*`=Accountant; `wk/*`=WarehouseKeeper; `tl/*`=TeamLeader, TeamDeputy; `qa/*`=QaQc; `wrk/*`=Worker; `electrician/*`=Electrician | Test 9 role × từng nhóm API (sai role → 403). Không thấy `FallbackPolicy`, nên controller mới thiếu attribute sẽ mở công khai |
| G3-02 | Ngoại lệ quyền | `acc/attendance/*`=Accountant, Admin, Manager; riêng `sync-sessions`, `rj-employees/sync`, `import[/preview]`=Admin. `cm/request-inbox`=Handlers + TeamLeader/Deputy; `cm/{grade-code-options,hr-lookups,order-lookups}`=mọi role trừ Worker; `cm/rj-employees`=Admin, Manager, Accountant; `cm/import-templates`=Admin, Manager, Accountant; `tl/{cut-handovers,cutting-lays,work-orders}` thêm `[CuttingTeamOnly]` | Kế toán gọi được API Admin-only; Manager xem chấm công ngoài phạm vi; tổ trưởng tổ may gọi được API tổ cắt |
| G3-03 | IDOR: dữ liệu cá nhân/theo tổ | `cm/requests/{id}[/cancel]`; `cm/request-inbox/{id}/*`; `cm/notifications/{id}/read`; `cm/my-payslips/{periodMonth}`; `wrk/work-allocations/{id}/*`, `confirm-batch` (ID trong body); `tl/{material-issues,material-returns,cutting-lays,cut-handovers,operation-assignments,defect-records,work-allocations}/{id}`; `tl/grade-suggestions/items/{workerId}`; `tl/work-allocations/workers/{workerId}/confirm-on-behalf`; query `teamId`/`workerId` | Đọc hoặc sửa dữ liệu của người khác/tổ khác. Nhóm `my-*` cần thử chèn thêm tham số định danh người dùng |
| G3-04 | IDOR: `cm` chỉ yêu cầu đăng nhập | `cm/attachments` (POST `ownerType`+`ownerId`, GET, GET `{id}/content`, DELETE `{id}`); `cm/lookups/*` (đơn hàng, tổ, NVL) | **Ưu tiên cao:** class chỉ có `[Authorize]`; cần kiểm quyền theo đối tượng sở hữu và dữ liệu mà Worker đọc được |
| G3-05 | IDOR: chứng từ & tài chính | `{adm,acc}/payrolls/{id}[/file,/submit,/approve]`; `acc/{order-payments,purchase-payments}/{id}[/payments/{paymentId}]`; `qa/qc-batches/{id}`, `qa/fg-handovers/{id}`; `mgr/order-markers/{orderId}/products/{productId}` | `paymentId`/`productId` không thuộc `{id}`/`orderId` cha; sửa chứng từ do người khác tạo |
| G3-06 | Xác thực & token | `cm/auth/{login,refresh}` (anonymous), `logout`, `me`; `cm/profile/change-password`; `{adm,mgr}/employees/{id}/account/{lock,unlock,reset-password}`; route `/login` | Thiếu token, token hết hạn (ClockSkew 1 phút), sửa chữ ký → 401; dùng lại refresh token sau logout/khóa/reset. Không thấy rate limiter (brute-force). Manager có thể reset mật khẩu tài khoản Admin (cần test) |
| G3-07 | Agent API | `agent/attendance-sync/{cursor,punches,employees,heartbeat}`: `[AllowAnonymous]` + `[AgentApiKey]` (header `X-Agent-Key`, có `FixedTimeEquals`) | Thiếu/sai key; JWT người dùng không thay được key; batch lớn; replay |
| G3-08 | Upload & payload lớn | `cm/attachments`; `adm/products/sample-image`; mọi API import/parse Excel ở G1-07, G1-02, `mgr/order-markers` | Chỉ thấy `RequestSizeLimit` ở attachments và sample-image; API khác cần thử file quá cỡ, zip-bomb xlsx, sai MIME, tên file path traversal, formula injection khi xuất lại |
| G3-09 | Input validation & cấu hình | Query `search`, `pageSize`, khoảng ngày ở danh sách G1; body mảng của các API `bulk`/`batch`/`*-many`; CORS policy `Web` | SQLi, stored XSS, số âm/tràn, mảng khổng lồ, 500 lộ stack trace. Không thấy HSTS/HTTPS redirect trong code API (kiểm thêm ở reverse proxy) |
| G3-10 | Guard phía Frontend | `ProtectedRoute` + `roles` trong `fe/src/features/*/routes.tsx`; gõ thẳng URL trái role | Lộ màn hình. `/profile` và `/my/payslips` không khai báo `roles` (mọi người đã đăng nhập) |

## 5. Group4_Compatibility

**Viewport đề xuất** (chưa đối chiếu breakpoint của dự án): Desktop 1920×1080 và 1366×768; Tablet 768×1024 và 1024×768; Mobile 390×844 và 360×800.
**Tiêu chí chung:** không tràn ngang ngoài vùng bảng; menu, modal, nút thao tác dùng được; không có lỗi Console; không có response 5xx; đo thời gian tải lần đầu và khi chuyển trang (trang được lazy load). Ngưỡng thời gian cần nhóm chốt, nên đo trên bản build production.

| Group ID | Tên Nhóm | Danh sách Endpoint / Màn hình trọng điểm | Rủi ro NFR chính |
|---|---|---|---|
| G4-01 | Chung (mọi role) | `/login`, `/`, `/profile`, `/notifications`, `/requests`, `/requests/inbox` | Layout chính và sidebar vỡ trên Mobile; dashboard tải chậm |
| G4-02 | Công nhân (ưu tiên Mobile) | `/my/attendance`, `/my/meals`, `/my/payslips`, `/my/estimated-pay`, `/my-piece-movements`, `/my-defects` | Nút khó bấm, bảng không đọc được trên màn nhỏ |
| G4-03 | Tổ trưởng & QA (ưu tiên Tablet/Mobile) | `/operation-assignments`, `/output-records`, `/defect-records`, `/cutting/lays`, `/cutting/handovers`, `/team/attendance`, `/team/meals`, `/qc-batches`, `/warehouse/fg-handovers`, `/warehouse/piece-movements`, `/warehouse/piece-holdings`, `/hr/grades` | Lưới nhập liệu rộng; bàn phím ảo che form; submit lặp khi mạng yếu |
| G4-04 | Kho | `/warehouse/{materials,incoming-materials,material-receipts,material-issues,material-returns,short-deliveries,inventories,stocktakes,dispatch-notes}`, `/cutting/work-orders` | Bảng nhiều cột, modal nhiều dòng |
| G4-05 | Quản lý & Admin (ưu tiên Desktop) | `/admin/{orders,orders/new,orders/:id,products,employees,vehicles,audit-logs,meal-settings}`, `/manager/{orders,employees,output-records,defect-records,qc-batches}`, `/teams`, `/kpi/targets`, `/order-line-teams`, `/warehouse/order-materials`, `/suppliers`, `/purchase-orders`, `/admin/piece-rates`, `/operations` | Chi tiết đơn nhiều tab, lịch KPI, form lớn; render chậm |
| G4-06 | Kế toán & báo cáo | `/attendance`, `/meals/summary`, `/payrolls`, `/payrolls/approval`, `/purchase-payments`, `/accounting/{order-payments,vehicle-log,products}`, `/monthly-costs`, `/revenue`, `/admin/revenue`, `/admin/break-even`, `/admin/cost-summary`, `/accountant/accounting-reports` | Biểu đồ không co giãn; bảng rộng; tải file Excel lỗi trên một số trình duyệt |

Trình duyệt đề xuất: Chromium, Firefox, WebKit (Playwright). Giả lập viewport không thay thế được kiểm tra trên thiết bị thật.

## 6. Thứ tự chạy cuốn chiếu

Chạy G3 → G2 → G1 → G4. Mỗi nhóm bắt đầu từ DB test đã reset và seed riêng để chạy độc lập được.

- **G2, G3:** kiểm qua API công khai. Dữ liệu tạo ra có tiền tố `ST_AUTO_` và được dọn sau khi chạy.
- **G1:** cần seed khối lượng lớn. Nhóm cần chốt trước quy mô dữ liệu, số người dùng đồng thời và ngưỡng p95. Không chạy song song với nhóm khác.
- **Phạm vi môi trường:** chỉ chạy trên localhost/test, không chạy tải hoặc thử bảo mật trên môi trường đang triển khai. Dùng stub cho Gemini khi chạy tải.
- **Quy chuẩn Report 5.3:** `CLAUDE.md` cấm Assert bằng DB. Vì vậy G2 kiểm kết quả (tồn, trạng thái, số bản ghi) qua giao diện hoặc API công khai.
