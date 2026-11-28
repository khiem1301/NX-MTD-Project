# ST_CONTRACT — Module 14: Chi phí, KPI, Doanh thu & Báo cáo

> Nguồn: `fe/src/features/{monthly-cost,kpi,revenue,break-even,cost-summary,accounting-reports}/*`, `constants/apiEndpoints.ts`. Không đọc Backend.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/monthly-costs` | MonthlyCostPage ("Chi phí tháng") | Accountant | Chọn tháng → Tải file mẫu → Chọn file Excel → Xem trước → Nhập chi phí tháng |
| `/kpi/targets` | KpiTargetListPage ("Chỉ tiêu KPI tổ may") | Admin, Manager | Lập chỉ tiêu → xem lịch KPI theo ngày / Lưu KPI theo ngày → Kết thúc đơn của tổ / Hủy chỉ tiêu |
| `/revenue` | AccountantRevenuePage ("Doanh thu") | Accountant | Lọc Tháng / Mã đơn hàng / Mã hàng → Xuất Excel |
| `/admin/revenue` | AdminRevenuePage ("Doanh thu") | Admin | Như trên |
| `/admin/break-even` | BreakEvenPage ("Hòa vốn & giá trị thặng dư") | Admin | Chọn tháng → "Tính hòa vốn" (xem dữ liệu đầu vào) → "Tính ngay" → xem chi tiết / lịch sử các lần tính → Xuất Excel |
| `/admin/cost-summary` | CostSummaryPage ("Tổng hợp chi phí") | Admin | Chọn tháng → xem tổng hợp (vật tư + lương) → Xuất Excel |
| `/accountant/accounting-reports` | AccountingReportsPage (tab ngày công/tăng ca, suất ăn, xe) | Accountant | Lọc → xem từng tab → Xuất Excel (`accounting-reports/export`) |

- Role sai → redirect `/`. KPI của tổ (`TeamKpiPanel`, endpoint `team-leader/kpi-targets`) nằm trên Trang chủ TL, không có route riêng.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Chọn tháng (mọi màn) | MonthPicker "Tháng" | `GetByLabel("Tháng")` / `GetByPlaceholder("Tháng")` |
| Tải file mẫu (Chi phí tháng) | Button tải template | `GetByRole(Button, "Tải file mẫu")` |
| Chọn file Excel / Xem trước / Nhập chi phí tháng | Upload `.xlsx,.xls`; sau xem trước hiện Hợp lệ / Lỗi / Trùng; Modal.confirm khi nhập | `Locator("input[type=file]")`, `GetByRole(Button, "Xem trước")`, `GetByRole(Button, "Nhập chi phí tháng")` |
| Bảng xem trước chi phí | Dòng, Loại chi phí, Kiểu (Cố định/Biến đổi), Mã hàng, Số tiền, Số lượng, Ghi chú, Trạng thái, Ghi chú lỗi | `GetByText("Hợp lệ")` |
| Tháng chưa có dữ liệu | Empty "Tháng này chưa có dữ liệu chi phí. Hãy tải file mẫu và nhập Excel." | `GetByText("Tháng này chưa có dữ liệu chi phí")` |
| KPI: Lập chỉ tiêu | Button primary | `GetByRole(Button, "Lập chỉ tiêu")` |
| KPI: Đơn hàng / Mã hàng (theo đơn) / Tổ may | Select required (Mã hàng khóa đến khi chọn đơn; Tổ may theo đơn–mã hàng) | `GetByLabel("Đơn hàng")`, `GetByLabel("Mã hàng (theo đơn)")`, `GetByLabel("Tổ may")` |
| KPI: Kỳ (từ - đến) | RangePicker required; tối đa 366 ngày ("Kỳ chỉ tiêu tối đa 366 ngày.") | `GetByLabel("Kỳ (từ - đến)")` |
| KPI: Chỉ tiêu cả kỳ | số > 0 ("Chỉ tiêu phải lớn hơn 0."), ≤ 1.000.000 ("Chỉ tiêu tối đa 1.000.000.") | `GetByLabel("Chỉ tiêu cả kỳ")` |
| KPI: Năng lực đơn | Descriptions (SL đơn, năng lực còn lại, …) chỉ đọc | `GetByText("Chỉ tiêu")` |
| KPI theo ngày | Lịch tháng, ô KPI từng ngày; nút "Lưu KPI theo ngày" | `GetByRole(Button, "Lưu KPI theo ngày")` |
| KPI: Hủy chỉ tiêu | Popconfirm "Hủy chỉ tiêu KPI này? Chỉ tiêu đã hủy không tính kết quả." | `GetByRole(Button, "Hủy chỉ tiêu")` |
| KPI: Kết thúc đơn của tổ | Modal "Kết thúc đơn của tổ": Chỉ tiêu / Thực tế / Kết quả / Thiếu hụt; nút Xác nhận | `GetByRole(Button, "Xác nhận")` |
| KPI: Tìm / Lọc trạng thái | "Tìm theo mã, tên..."; Select "Trạng thái" | `GetByPlaceholder("Trạng thái")` |
| Doanh thu: lọc | Tháng, Mã đơn hàng, Mã hàng (Select/placeholder) | `GetByPlaceholder("Mã đơn hàng")`, `GetByPlaceholder("Mã hàng")` |
| Xuất Excel | Button (doanh thu, hòa vốn, tổng hợp chi phí, báo cáo kế toán) | `GetByRole(Button, "Xuất Excel")` |
| Hòa vốn: Tính hòa vốn | Button primary (icon máy tính) → popup "Tính hòa vốn · Tháng {m}": "1. Dữ liệu đầu vào", nút "Tính ngay" | `GetByRole(Button, "Tính hòa vốn")`, `GetByRole(Button, "Tính ngay")` |
| Hòa vốn: lịch sử | Nút "Xem lịch sử các lần tính" ("Lần tính {…}") | `GetByRole(Button, "Xem lịch sử các lần tính")` |
| Tổng hợp chi phí | Thẻ "Tháng đã khóa"; bảng theo mã hàng; dữ liệu "mật" (GBR…) | `GetByText("Tháng đã khóa")` |
| Báo cáo kế toán | Tabs ngày công/tăng ca, "Đối soát suất ăn (MEALS)", xe; nút Xuất | `GetByRole(Tab, new{Name=<tab>})` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Xem chi phí tháng | `GET /api/v1/accountant/monthly-costs?periodMonth` | — | Tóm tắt + bảng |
| Tải file mẫu | `GET /api/v1/accountant/monthly-costs/template` | — | Tải `.xlsx` |
| Xem trước / nhập chi phí | `POST /api/v1/accountant/monthly-costs/import/preview`; `POST .../monthly-costs/import` | multipart (+ `periodMonth`) | Bảng xem trước; nhập xong reload |
| Danh sách KPI | `GET /api/v1/manager/kpi-targets`; `GET /api/v1/team-leader/kpi-targets` (TL) | `KpiTargetFilter` | Bảng chỉ tiêu |
| Tổ được chia | `GET .../kpi-targets/allotted-teams` (`getAllottedTeams(orderId, productId)`) | — | Select Tổ may |
| Năng lực đơn / dòng | `GET /api/v1/manager/kpi-targets/order-capacity`, `.../line-capacity` | `orderId, productId, exceptKpiTargetId?` | Khối "Năng lực" |
| Tạo / sửa chỉ tiêu | `POST /api/v1/manager/kpi-targets`; `PUT .../kpi-targets/{id}` | `{orderId, productId, sewingTeamId, from, to, targetFinishedQty}` | Toast "Đã lưu chỉ tiêu KPI." |
| Lịch / kế hoạch ngày | `GET .../kpi-targets/{id}/calendar` (Manager) hoặc `team-leader/.../calendar`; `PUT .../{id}/daily-plan` | `{days[{date, qty}]}` | Toast "Đã lưu KPI theo ngày." |
| Kết quả ngày | `GET .../kpi-targets/{id}/daily-results` | — | Lịch kết quả |
| Kết thúc đơn của tổ | `GET .../{id}/finish-preview`; `POST .../{id}/finish`; (TL xem) `.../finish-result` | — | Trạng thái chỉ tiêu đổi |
| Hủy chỉ tiêu | `POST .../kpi-targets/{id}/cancel` | — | Toast "Đã hủy chỉ tiêu KPI." |
| Doanh thu | `GET /api/v1/{accountant\|admin}/revenue` (+ `/filter-options`, `/export`) | `periodMonth, orderId?, productId?` | Bảng + tổng; tải `.xlsx` |
| Hòa vốn | `GET /api/v1/admin/break-even` (lịch sử); `GET .../break-even/inputs`; `POST .../break-even/calculate`; `GET .../break-even/export` | `{periodMonth, …}` | Thẻ tổng quan + bảng kết quả; tải `.xlsx` |
| Tổng hợp chi phí | `GET /api/v1/admin/cost-summary?periodMonth`; `GET .../cost-summary/export` | — | Tóm tắt + bảng; tải `.xlsx` |
| Báo cáo kế toán | `GET /api/v1/accountant/accounting-reports/workdays`, `/meals`, `/vehicles`; `GET .../accounting-reports/export` | bộ lọc kỳ | Từng tab; tải `.xlsx` |

## 4. Điểm nghi vấn lệch pha

- Hai route doanh thu (`/revenue` Kế toán, `/admin/revenue` Admin) dùng chung component nhưng khác API; nhãn lọc "Mã đơn hàng"/"Mã hàng" là placeholder của Select (không có label) → khó dùng `GetByLabel`.
- KPI: `periodMax` 366 ngày, chỉ tiêu ≤ 1.000.000 là hằng số FE; BE có thể giới hạn khác. "Năng lực còn lại" chỉ hiển thị, FE không chặn chỉ tiêu vượt năng lực.
- KPI: Mã hàng bị khóa cho tới khi chọn đơn (placeholder `noProduct`); đổi đơn không rõ có reset Tổ may.
- `KpiCalendarModal`/Home TL dùng `team-leader/kpi-targets` nhưng route `/kpi/targets` chỉ cho Admin/Manager: KPI của tổ không test được qua `/kpi/targets`.
- Nhập chi phí tháng: chỉ `.xlsx,.xls`, kết quả Hợp lệ/Lỗi/Trùng do BE; FE chưa thấy chặn nhập khi `errorCount > 0` (cần xác minh).
- "Tháng đã khóa" (cost summary) chỉ hiển thị; FE không chặn nhập chi phí tháng khi đã khóa — việc chặn ở BE.
- Tổng hợp chi phí có dữ liệu "mật" (GBR…): FE chỉ ẩn/hiện theo role Admin, route đã giới hạn Admin.
- Hòa vốn: popup "Tính hòa vốn" hiển thị dữ liệu đầu vào trước khi "Tính ngay" (hai bước); FE cho tính lại nhiều lần → "Xem lịch sử các lần tính".
- Báo cáo kế toán: tab "Đối soát suất ăn (MEALS)" dùng nhãn tiếng Anh pha tiếng Việt (hardcode `MEALS`).
- Nhiều nút Xuất Excel chỉ khác nhau theo ngữ cảnh trang → test cần giới hạn Locator trong từng trang.
