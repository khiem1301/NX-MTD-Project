# ST_CONTRACT — Module 7: Cắt — Lệnh cắt, Bàn cắt & Bàn giao BTP

> Nguồn: `fe/src/features/cutting/*`, `features/warehouse/pages/PieceHoldingsPage`, `features/piece-movement/*`, `constants/apiEndpoints.ts`, `constants/messages.ts` (mục `cutting`). Không đọc Backend. `{role}` = `admin`/`manager`/`warehouse-keeper`/`team-leader` theo `RolePath`.

## 1. Routes & Phân quyền

| Route URL | Màn hình | Role hợp lệ | Luồng điều hướng trước/sau |
| :--- | :--- | :--- | :--- |
| `/cutting/work-orders` | WorkOrderListPage ("Lệnh cắt") + WorkOrderDetailModal | Admin, Manager, WarehouseKeeper, TeamLeader, TeamDeputy (tổ cắt) | Admin/Manager: Lập lệnh cắt / Lập lệnh cắt bù; mở chi tiết → Sửa / Đóng lệnh cắt thiếu / Hủy |
| `/cutting/lays` | CuttingLayPage ("Bàn cắt") | TeamLeader, TeamDeputy (tổ cắt) | Chọn lệnh cắt → Lập/Sửa bàn cắt |
| `/cutting/handovers` | CutHandoverPage (bàn giao BTP) + HandoverDetailModal | TeamLeader, TeamDeputy, WarehouseKeeper | Tổ cắt lập phiếu bàn giao → Thủ kho Xác nhận / Từ chối; Tổ cắt Rút phiếu |
| `/warehouse/piece-holdings` | PieceHoldingsPage (BTP tổ đang giữ) | TeamLeader, TeamDeputy | Xem tồn BTP theo tổ |
| `/warehouse/piece-movements` | LeaderPieceMovementsPage ("Giao sản lượng công đoạn theo ngày": tab "Giao theo ngày", "Phiếu giao / thu hồi") | TeamLeader, TeamDeputy (tổ may) | Chọn đơn → dòng đơn → giao việc cho công nhân theo ngày |
| `/my-piece-movements` | MyPieceMovementsPage ("Sản lượng công đoạn được giao") | Worker | Xác nhận / xác nhận loạt / từ chối phiếu giao |

- Role sai hoặc sai loại tổ (cắt/may) → redirect `/`.

## 2. Form, Bảng & Selector

| Field/Nút | Loại & Rule FE | Playwright Locator ưu tiên |
| :--- | :--- | :--- |
| Lập lệnh cắt | Button primary (chỉ Admin/Manager, `<Can>`) | `GetByRole(Button, "Lập lệnh cắt")` |
| Lập lệnh cắt bù | Button; modal chọn "Đơn cắt bù đã duyệt" (required), Tổ cắt (required), Hạn cắt (required), Ghi chú max `noteMax` | `GetByRole(Button, "Lập lệnh cắt bù")`, `GetByLabel("Đơn cắt bù đã duyệt")`, `GetByLabel("Tổ cắt")`, `GetByLabel("Hạn cắt")` |
| Đơn hàng / Mã hàng | Select required ("Chọn đơn hàng", "Chọn mã hàng") | `GetByLabel("Đơn hàng")`, `GetByLabel("Mã hàng")` |
| Hạn cắt | DatePicker required + validator (xem `WorkOrderFormModal`) | `GetByLabel("Hạn cắt")` |
| Kế hoạch từng màu - size | Số nguyên; vượt "Còn lên lệnh được" → "Vượt số còn lên lệnh được ({n})."; phải nhập ≥ 1 dòng ("Nhập số kế hoạch hoặc số chi tiết cắt lẻ cho ít nhất một dòng"); chi tiết cắt lẻ không khai trùng | `GetByRole(Spinbutton)` theo dòng |
| Cảnh báo vải | Alert "Không đủ vải cho lệnh cắt" / "Vải không đủ rộng để cắt" | `GetByText("Không đủ vải cho lệnh cắt")` |
| Đóng lệnh cắt thiếu | Modal: "Lý do cắt thiếu" required, max `closeReasonMax` | `GetByRole(Button, "Đóng lệnh cắt thiếu")`, `GetByLabel("Lý do cắt thiếu …")` |
| Hủy lệnh cắt | Popconfirm "Hủy lệnh cắt này? Thao tác không hoàn tác được." | `GetByRole(Button, "Hủy lệnh cắt")` rồi OK |
| Bàn cắt: Lập bàn cắt | Ngày cắt required, Ca required (SHIFT_OPTIONS), SL sau cắt ≥ 1 màu - size ("Nhập số lượng sau cắt cho ít nhất một màu - size."), vải đã dùng (m) ≤ vải còn lại; Ghi chú max 500 | `GetByLabel("Ngày cắt")`, `GetByLabel("Ca")`, `GetByLabel("Số mét đã dùng")` |
| Lập phiếu bàn giao BTP | Modal: SL bàn giao mỗi dòng 0–`remainingQty` (`maxQtyRule`); xác nhận (confirm) | `GetByRole(Button, "Xác nhận")` |
| Rút phiếu | Popconfirm "Rút phiếu bàn giao này? …" | `GetByRole(Button, "Rút phiếu")` |
| Thủ kho xác nhận | SL thực nhận required (≥0, ≤ bàn giao); Lý do chênh lệch bắt buộc khi lệch (max `REASON_MAX_LENGTH`) | `GetByRole(Button, "Xác nhận")`, `GetByRole(Textbox)` theo dòng |
| Thủ kho từ chối | Button danger → lý do | `GetByRole(Button, "Từ chối")` |
| Giao việc theo ngày | Ngày làm việc, "Chọn đơn hàng để giao việc", "Chọn mã hàng và màu - size để giao việc", "Tìm mã, tên công nhân", lọc "Tình trạng việc" | `GetByLabel("Ngày làm việc")`, `GetByPlaceholder("Tìm mã, tên công nhân")` |
| Lưu giao việc | Nút Lưu kèm số thay đổi `Lưu (n)`; Lý do (max `REASON_MAX_LENGTH`); chọn công đoạn thêm | `GetByRole(Button, new{Name=new Regex("^Lưu")})` |
| Công nhân xác nhận | "Số lần xác nhận" required ≥ 1 (≤ số lần giao); "Lý do chênh lệch" bắt buộc khi hạ số; xác nhận loạt (input aria-label "Số lần xác nhận {công đoạn}") | `GetByLabel("Số lần xác nhận")`, `GetByRole(Button, "Xác nhận")` |
| Xác nhận thay công nhân (TL) | Modal cảnh báo `onBehalfWarn`, OK | `GetByRole(Button, "Xác nhận")` |

## 3. API Mapping

| Hành động UI | Method + Endpoint | Payload | Phản hồi UI kỳ vọng |
| :--- | :--- | :--- | :--- |
| Danh sách / chi tiết lệnh cắt | `GET /api/v1/{role}/work-orders` (+ `/{id}`) | filter, `pageSize` | Bảng + modal |
| Kiểm vải | `POST .../work-orders/fabric-check` | dòng kế hoạch | Cảnh báo vải trong form |
| Lập / sửa lệnh cắt | `POST .../work-orders`; `PUT .../work-orders/{id}` | `{orderId, productId, cuttingTeamId, dueDate, lines, note}` | Toast "Đã lưu lệnh cắt." |
| Lệnh cắt bù | `GET .../work-orders/replacement-requests`; `POST .../work-orders/replacement` | `{requestId, cuttingTeamId, dueDate, note}` | Toast "Đã lập lệnh cắt bù." |
| Đóng thiếu / Hủy | `POST .../work-orders/{id}/close-short` `{reason}`; `.../cancel` | — | Toast "Đã đóng lệnh cắt thiếu, lệnh cắt hoàn thành." / "Đã hủy lệnh cắt." |
| Bàn cắt | `GET/POST /api/v1/{role}/cutting-lays`; `PUT .../cutting-lays/{id}` | `{workOrderId, workDate, shiftCode, lines, fabrics, note}` | Toast "Đã lưu bàn cắt."; lỗi vải: "Số mét vải đã dùng vượt vải còn lại…" |
| Phiếu bàn giao BTP | `GET /api/v1/{role}/cut-handovers` (+ `/{id}`, `/handoverable?workOrderId`); `POST` tạo | `{workOrderId, lines[{…, handedQty}]}` | Toast "Đã lập phiếu bàn giao BTP. Các bàn cắt của lệnh đã khóa." |
| Xác nhận / từ chối / rút phiếu | `POST .../cut-handovers/{id}/confirm` (lines thực nhận + lý do); `.../reject` `{reason}`; `.../cancel` | — | "Đã rút phiếu bàn giao BTP. Các bàn cắt đã mở lại để sửa." (rút) |
| BTP tổ đang giữ | `GET /api/v1/{role}/piece-holdings` | — | Bảng tồn |
| Giao việc theo ngày | `GET /api/v1/team-leader/work-allocations/board`, `.../daily`; `PUT .../work-allocations/daily` | `{workDate, orderId, orderLineId, items}` | Toast lưu; bảng cập nhật |
| Phiếu giao / thu hồi | `GET /api/v1/team-leader/work-allocations`; `GET /api/v1/worker/work-allocations` | filter | Tab Phiếu giao / thu hồi |
| Xác nhận thay | `POST /api/v1/team-leader/work-allocations/{id}/confirm-on-behalf` | `{confirmedQty, reason?}` | Trạng thái "CONFIRMED" |
| CN xác nhận / loạt / từ chối | `POST /api/v1/worker/work-allocations/{id}/confirm`, `.../confirm-batch`, `.../{id}/reject` | `{confirmedQty, reason?}` / `{reason}` | Trạng thái đổi; phiếu rời danh sách chờ |

## 4. Điểm nghi vấn lệch pha

- `/cutting/work-orders` mở cho cả TL/TD và Thủ kho nhưng chỉ Admin/Manager thấy nút Lập lệnh cắt (`<Can>`): FE ẩn nút nhưng route vẫn vào được; cần kiểm ca "Role không có quyền".
- Lập lệnh cắt dùng `message.warning` cho `noLines`/`duplicateExtra` (không phải lỗi form) → test phải bắt toast, không phải thông báo dưới ô.
- Vải: cảnh báo "Không đủ vải" chỉ là Alert, FE không chặn nút Lưu → hành vi lưu khi thiếu vải phụ thuộc BE.
- Số lượng vượt `plannableQty` hiện thông báo FE, nhưng đây là số tải trước; thay đổi đồng thời chỉ biết khi API lỗi.
- Bàn giao BTP: FE khóa bàn cắt sau khi lập phiếu (theo toast) nhưng không tự đổi trạng thái nút Sửa bàn cắt cho tới khi reload.
- Lý do chênh lệch bàn giao/giao việc dùng `validator` riêng theo dòng (`reasonRule`) → thông báo lỗi nằm trong bảng, khó định vị bằng Semantic Locator.
- Giao việc theo ngày: `workDate` không chặn ngày tương lai/quá khứ; chưa thấy kiểm trùng cùng công đoạn–công nhân–ngày.
- `MyPieceMovementsPage` có xác nhận loạt, `aria-label` của ô là chuỗi ghép `"Số lần xác nhận {công đoạn}"` (nên dùng regex).
- `/warehouse/piece-holdings` và `/warehouse/piece-movements` nằm dưới tiền tố `/warehouse` nhưng chỉ TL/TD truy cập → dễ nhầm với quyền Thủ kho.
- Tên nhãn trạng thái dùng chung khóa `PENDING/CONFIRMED/REJECTED/CANCELLED` cho nhiều loại phiếu (bàn giao, giao việc) nhưng nhãn hiển thị khác nhau.
