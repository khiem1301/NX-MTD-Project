# ST NFR TEST CASES — Group4_Compatibility (NX-MTD)

> **Nguồn duy nhất:** `ST_NFR_TARGETS.md` — mục 5 (G4-01 → G4-06) và mục 6.
> **Môi trường:** localhost/test, **bản build production** của FE, DB reset + seed riêng cho G4.
> **Công cụ:** Playwright — Chromium, Firefox, WebKit. Giả lập viewport không thay thế kiểm tra trên thiết bị thật.

## 0. Tham số chung

| Tham số | Giá trị | Nguồn |
|---|---|---|
| `VP_D1` / `VP_D2` | 1920×1080 / 1366×768 | Viewport đề xuất của nguồn |
| `VP_T1` / `VP_T2` | 768×1024 / 1024×768 | Viewport đề xuất của nguồn |
| `VP_M1` / `VP_M2` | 390×844 / 360×800 (nhỏ nhất) | Viewport đề xuất của nguồn |
| `LOAD_FIRST` | ≤ 3 s (Desktop) / ≤ 5 s (Mobile) | **Đề xuất** — nguồn ghi “ngưỡng thời gian cần nhóm chốt” |
| `LOAD_NAV` | ≤ 1,5 s chuyển trang (lazy chunk) | **Đề xuất** |
| `TAP_MIN` | vùng bấm ≥ 44×44 px | **Đề xuất** (cho rủi ro “nút khó bấm”) |

**Tiêu chí chung (áp dụng cho mọi TC, theo nguồn):**
- **C1 – Không tràn ngang:** `document.documentElement.scrollWidth <= window.innerWidth` (bỏ qua phần tử bên trong vùng bảng có `overflow-x: auto`).
- **C2 – Tương tác:** menu, modal, nút thao tác hiển thị trong viewport và bấm được (`toBeVisible()` + `click()` thành công).
- **C3 – Console:** 0 message `console.error` / `pageerror`.
- **C4 – Network:** 0 response HTTP 5xx.
- **C5 – Thời gian:** đo tải lần đầu và khi chuyển trang theo `LOAD_FIRST`, `LOAD_NAV`.

## 1. Bảng Test Case

| TC ID | Mục tiêu | Loại | Endpoint / Màn hình | Kịch bản | Kết quả mong đợi |
|---|---|---|---|---|---|
| NFR_G4_001 | G4-01 | Normal | `/login`, `/`, `/profile`, `/notifications`, `/requests`, `/requests/inbox` | **[Load/Attack/Env]:** Chromium, `VP_D1` 1920×1080, mỗi role 1 lượt đăng nhập rồi mở lần lượt các trang. | **[SLA/Threshold]:** C1–C4 đạt; tải lần đầu `/` ≤ 3 s; chuyển trang ≤ 1,5 s. |
| NFR_G4_002 | G4-01 | Normal | Như 001 | **[Load/Attack/Env]:** Firefox và WebKit, `VP_D2` 1366×768. | **[SLA/Threshold]:** C1–C4 đạt trên cả 2 trình duyệt; sidebar không che nội dung chính. |
| NFR_G4_003 | G4-01 | Boundary | Như 001 | **[Load/Attack/Env]:** Chromium + WebKit, `VP_M1` 390×844. | **[SLA/Threshold]:** `scrollWidth <= innerWidth`; sidebar thu thành menu/drawer mở–đóng được; C3, C4 đạt; tải `/` ≤ 5 s. |
| NFR_G4_004 | G4-01 | Boundary | Như 001 | **[Load/Attack/Env]:** Chromium, `VP_M2` 360×800 (nhỏ nhất). | **[SLA/Threshold]:** `scrollWidth <= innerWidth`; form `/login` hiển thị trọn, nút Đăng nhập bấm được; C3, C4 đạt. |
| NFR_G4_005 | G4-01 | Abnormal | `/login` | **[Load/Attack/Env]:** Chromium `VP_M1`, giả lập mạng chậm (Slow 3G); double-click nút Đăng nhập. | **[SLA/Threshold]:** chỉ 1 request `cm/auth/login` được gửi (hoặc nút bị khóa khi đang gửi); không lỗi Console; đăng nhập thành công. |
| NFR_G4_006 | G4-02 | Normal | `/my/attendance`, `/my/meals`, `/my/payslips`, `/my/estimated-pay`, `/my-piece-movements`, `/my-defects` | **[Load/Attack/Env]:** Chromium `VP_D1`, role Worker. | **[SLA/Threshold]:** C1–C4 đạt; chuyển trang ≤ 1,5 s. |
| NFR_G4_007 | G4-02 | Boundary | Như 006 (ưu tiên Mobile) | **[Load/Attack/Env]:** Chromium + WebKit, `VP_M1` 390×844, role Worker. | **[SLA/Threshold]:** `scrollWidth <= innerWidth`; mọi nút thao tác ≥ 44×44 px; dữ liệu bảng đọc được (cuộn trong vùng bảng hoặc dạng thẻ); C3, C4 đạt; tải ≤ 5 s. |
| NFR_G4_008 | G4-02 | Boundary | Như 006 | **[Load/Attack/Env]:** Chromium, `VP_M2` 360×800, role Worker. | **[SLA/Threshold]:** như 007 tại viewport nhỏ nhất; không chữ bị cắt mất ở cột số tiền/số lượng. |
| NFR_G4_009 | G4-02 | Abnormal | `/my/meals` | **[Load/Attack/Env]:** `VP_M1`, Slow 3G; bấm đổi suất ăn 5 lần liên tiếp (double-submit). | **[SLA/Threshold]:** trạng thái cuối hiển thị khớp với dữ liệu trả về từ `cm/my-meals/today`; không có request bị lặp ngoài ý muốn (≤ 1 request đang chạy); 0 HTTP 5xx. |
| NFR_G4_010 | G4-02 | Abnormal | `/my/payslips`, `/my/estimated-pay` | **[Load/Attack/Env]:** WebKit `VP_M1`; xoay 390×844 → 844×390 khi đang xem. | **[SLA/Threshold]:** bố cục tự co giãn; C1, C3 đạt sau khi xoay; dữ liệu không mất. |
| NFR_G4_011 | G4-03 | Normal | `/operation-assignments`, `/output-records`, `/defect-records`, `/cutting/lays`, `/cutting/handovers`, `/team/attendance`, `/team/meals`, `/qc-batches`, `/warehouse/fg-handovers`, `/warehouse/piece-movements`, `/warehouse/piece-holdings`, `/hr/grades` | **[Load/Attack/Env]:** Chromium `VP_D1`, role TeamLeader (tổ may & tổ cắt) và QaQc. | **[SLA/Threshold]:** C1–C4 đạt; tải lưới ≤ 3 s. |
| NFR_G4_012 | G4-03 | Boundary | Như 011 (ưu tiên Tablet) | **[Load/Attack/Env]:** Chromium + WebKit, `VP_T1` 768×1024 và `VP_T2` 1024×768. | **[SLA/Threshold]:** lưới nhập liệu cuộn ngang **bên trong** vùng bảng, trang không tràn (C1); ô nhập bấm được; C3, C4 đạt. |
| NFR_G4_013 | G4-03 | Boundary | `/output-records`, `/defect-records`, `/qc-batches` | **[Load/Attack/Env]:** `VP_M1` 390×844; mô phỏng bàn phím ảo bằng cách thu viewport còn 390×500 khi focus ô nhập cuối form. | **[SLA/Threshold]:** ô đang focus và nút Lưu vẫn nằm trong viewport (tự cuộn tới); C1, C3 đạt. |
| NFR_G4_014 | G4-03 | Abnormal | `/output-records`, `/operation-assignments`, `/cutting/handovers` | **[Load/Attack/Env]:** `VP_T1`, Slow 3G; bấm Lưu 3 lần liên tiếp (submit lặp khi mạng yếu). | **[SLA/Threshold]:** chỉ 1 bản ghi được tạo (đếm qua API danh sách); nút bị vô hiệu khi đang gửi hoặc request trùng bị từ chối hợp lệ; 0 HTTP 5xx. |
| NFR_G4_015 | G4-03 | Abnormal | `/team/attendance`, `/team/meals` | **[Load/Attack/Env]:** `VP_T2`, ngắt mạng (offline) giữa lúc lưu rồi bật lại. | **[SLA/Threshold]:** hiển thị thông báo lỗi kết nối, không trắng trang; dữ liệu đang nhập không mất; 0 `pageerror`. |
| NFR_G4_016 | G4-04 | Normal | `/warehouse/{materials,incoming-materials,material-receipts,material-issues,material-returns,short-deliveries,inventories,stocktakes,dispatch-notes}`, `/cutting/work-orders` | **[Load/Attack/Env]:** Chromium `VP_D2` 1366×768, role WarehouseKeeper. | **[SLA/Threshold]:** C1–C4 đạt; bảng nhiều cột cuộn trong vùng bảng; tải ≤ 3 s. |
| NFR_G4_017 | G4-04 | Boundary | Như 016 | **[Load/Attack/Env]:** `VP_T1` 768×1024; mở modal tạo phiếu với số dòng lớn nhất seed. | **[SLA/Threshold]:** modal cao ≤ viewport, cuộn bên trong; nút Lưu/Hủy luôn hiển thị; C1, C3, C4 đạt. |
| NFR_G4_018 | G4-04 | Boundary | `/warehouse/material-issues`, `/warehouse/material-receipts`, `/warehouse/inventories` | **[Load/Attack/Env]:** `VP_M2` 360×800. | **[SLA/Threshold]:** `scrollWidth <= innerWidth`; modal nhiều dòng dùng được; C3, C4 đạt. |
| NFR_G4_019 | G4-04 | Abnormal | Như 016 | **[Load/Attack/Env]:** Firefox + WebKit `VP_D2`; refresh (F5) khi modal đang mở và khi đang ở trang chi tiết. | **[SLA/Threshold]:** trang tải lại không lỗi, không trắng trang; C3, C4 đạt. |
| NFR_G4_020 | G4-05 | Normal | `/admin/{orders,orders/new,orders/:id,products,employees,vehicles,audit-logs,meal-settings}`, `/manager/{orders,employees,output-records,defect-records,qc-batches}`, `/teams`, `/kpi/targets`, `/order-line-teams`, `/warehouse/order-materials`, `/suppliers`, `/purchase-orders`, `/admin/piece-rates`, `/operations` | **[Load/Attack/Env]:** Chromium `VP_D1`, role Admin/Manager (ưu tiên Desktop). | **[SLA/Threshold]:** C1–C4 đạt; chuyển tab chi tiết đơn ≤ 1,5 s; lịch KPI render ≤ 3 s. |
| NFR_G4_021 | G4-05 | Boundary | `/admin/orders/new`, `/admin/orders/:id`, `/kpi/targets` | **[Load/Attack/Env]:** Chromium + Firefox + WebKit, `VP_D2` 1366×768; đơn có nhiều sản phẩm nhất seed. | **[SLA/Threshold]:** form lớn và các tab hiển thị đủ, không chồng chéo; C1, C3, C4 đạt; render ≤ 3 s. |
| NFR_G4_022 | G4-05 | Boundary | Như 020 | **[Load/Attack/Env]:** `VP_T2` 1024×768 và `VP_M1` 390×844 (kiểm khả dụng tối thiểu). | **[SLA/Threshold]:** không trắng trang, không `pageerror`; C1 đạt (bảng cuộn trong vùng); thao tác chính (xem, lưu) bấm được. |
| NFR_G4_023 | G4-05 | Abnormal | `/admin/orders/:id`, `/manager/orders` | **[Load/Attack/Env]:** deep link + F5 trực tiếp `/admin/orders/:id`; `:id` không tồn tại và `:id=abc`. | **[SLA/Threshold]:** id hợp lệ → hiển thị đúng; id sai → thông báo “không tìm thấy”, không trắng trang; 0 `pageerror`; 0 HTTP 5xx. |
| NFR_G4_024 | G4-06 | Normal | `/attendance`, `/meals/summary`, `/payrolls`, `/payrolls/approval`, `/purchase-payments`, `/accounting/{order-payments,vehicle-log,products}`, `/monthly-costs`, `/revenue`, `/admin/revenue`, `/admin/break-even`, `/admin/cost-summary`, `/accountant/accounting-reports` | **[Load/Attack/Env]:** Chromium `VP_D1`, role Accountant/Admin. | **[SLA/Threshold]:** C1–C4 đạt; biểu đồ render ≤ 3 s. |
| NFR_G4_025 | G4-06 | Boundary | `/revenue`, `/admin/revenue`, `/admin/break-even`, `/admin/cost-summary` | **[Load/Attack/Env]:** `VP_D2` 1366×768 và `VP_T1` 768×1024. | **[SLA/Threshold]:** chiều rộng biểu đồ ≤ chiều rộng container cha; C1 đạt; nhãn trục không chồng nhau. |
| NFR_G4_026 | G4-06 | Boundary | Như 024 | **[Load/Attack/Env]:** `VP_M2` 360×800. | **[SLA/Threshold]:** `scrollWidth <= innerWidth`; bảng rộng cuộn trong vùng bảng; biểu đồ co theo màn hình; C3, C4 đạt. |
| NFR_G4_027 | G4-06 | Abnormal | Nút tải Excel tại `/payrolls`, `/revenue`, `/admin/cost-summary`, `/admin/break-even`, `/accountant/accounting-reports` | **[Load/Attack/Env]:** Chromium + Firefox + WebKit, `VP_D2`; bấm tải 2 lần liên tiếp. | **[SLA/Threshold]:** mỗi trình duyệt nhận sự kiện `download`; file `.xlsx` > 0 byte, đúng tên/đuôi; không mở tab trắng; 0 HTTP 5xx. |
| NFR_G4_028 | G4-06 | Abnormal | `/revenue`, `/admin/break-even` | **[Load/Attack/Env]:** Chromium; resize trực tiếp 1920×1080 → 390×844 → 1920×1080 khi biểu đồ đang hiển thị. | **[SLA/Threshold]:** biểu đồ vẽ lại theo kích thước mới; C1, C3 đạt sau mỗi lần resize. |
| NFR_G4_029 | G4-01 → G4-06 | Normal | Toàn bộ route mục 5 của nguồn | **[Load/Attack/Env]:** Chromium `VP_D1`, bản build production; điều hướng tuần tự qua mọi route bằng menu (đo lazy load). | **[SLA/Threshold]:** mỗi lần chuyển trang ≤ 1,5 s (`LOAD_NAV`); không chunk JS nào lỗi tải (0 `ChunkLoadError`). |
| NFR_G4_030 | G4-01 → G4-06 | Abnormal | Toàn bộ route mục 5 của nguồn | **[Load/Attack/Env]:** Chromium; sau khi deploy bản build mới (đổi hash chunk) giữ nguyên tab cũ và bấm chuyển trang. | **[SLA/Threshold]:** ứng dụng tự tải lại hoặc báo cập nhật; không trắng trang; 0 `pageerror` chưa xử lý. |

## 2. Truy vết bao phủ

| Mục tiêu nguồn | Normal | Boundary | Abnormal |
|---|---|---|---|
| G4-01 | 001, 002 | 003, 004 | 005 |
| G4-02 | 006 | 007, 008 | 009, 010 |
| G4-03 | 011 | 012, 013 | 014, 015 |
| G4-04 | 016 | 017, 018 | 019 |
| G4-05 | 020 | 021, 022 | 023 |
| G4-06 | 024 | 025, 026 | 027, 028 |
| Toàn bộ | 029 | — | 030 |
