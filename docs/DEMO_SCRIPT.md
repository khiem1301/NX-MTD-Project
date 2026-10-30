# Kịch bản demo (KH-17)

Dựng database: `sqlcmd -S localhost -E -C -I -f 65001 -b -i SQL/NX-MTD_Database.sql` (đã có dữ liệu demo mục 19).
Mật khẩu các tài khoản mẫu: `123456` (Admin đổi được sau khi đăng nhập).

| Vai trò | Tài khoản |
|---|---|
| Admin | `ADMIN` |
| Quản đốc | `QUANDOC` |
| Kế toán | `KETOAN` |
| Thủ kho | `THUKHO` |
| Tổ trưởng cắt / may | `TTCAT` / `TTMAY` |
| QA/QC | `QAQC1` |
| Công nhân may | `CNMAY1`…`CNMAY4` |

Đơn demo: **DEMO-DH001** (mã hàng DEMO-AO01, 5 dòng màu × size) và **DEMO-DH003** (2 dòng Đen M / L). Hai tổ may, một tổ cắt.

### Phiếu đang chờ sẵn để bấm thử (đơn DEMO-DH003 / DEMO-DH001, mục 19.3 của script)

Kho đã có tồn qua thẻ kho (tồn vải Đen 650 m, giữ chỗ 150 m; BTP và thành phẩm có tồn). Mỗi dòng dưới đây đang chờ đúng một người xác nhận:

| Ai xác nhận | Phiếu | Việc cần làm |
|---|---|---|
| Quản đốc | Phiếu nhập `PN-DEMO-003` (100 m vải) | Thủ kho chụp ảnh chứng từ trước, rồi Quản đốc xác nhận → tồn vải +100 |
| Quản đốc | Đơn mua `DM-DEMO-009` (khóa kéo, nhãn) | Chốt nhà cung cấp và đơn giá |
| Quản đốc | Kiểm kê `KK-DEMO-002` | Duyệt hoặc trả lại (vải lệch −8 m) |
| Tổ trưởng cắt (`TTCAT`) | Phiếu cấp vải `PC-DEMO-004` (150 m) | Nhận đủ, hoặc nhận thiếu kèm lý do |
| Thủ kho (`THUKHO`) | Trả vải `TV-DEMO-001` (8 m) | Đếm và xác nhận |
| Thủ kho | Bàn giao BTP `BG-DEMO-003` (30 M + 20 L) | Xác nhận → kho BTP tăng |
| Thủ kho | Bàn giao thành phẩm `TP-DEMO-004` (60 cái) | Xác nhận → kho thành phẩm tăng |
| Thủ kho | Phiếu xuất `XH-DEMO-001` / `XH-DEMO-003` | Chụp ảnh phiếu có Quản đốc ký (bắt buộc) rồi xuất kho thành phẩm; hoặc từ chối có lý do |
| Tổ trưởng may (`TTMAY`) | Phiếu cấp BTP `PC-DEMO-006` (30 M + 10 L) | Nhận BTP → tăng số bộ tổ đã nhận, mở thêm việc để giao |
| Công nhân (`CNMAY1`, `CNMAY3`) | Giao việc / thu hồi việc đang chờ | Tự xác nhận hoặc từ chối (nêu lý do); tổ trưởng xác nhận hộ khi công nhân vắng |
| Kế toán (`KETOAN`) | Phiếu xuất `XH-DEMO-002` (đang giao) | Xác nhận khách đã nhận |
| Thủ kho | Đơn xin `YC-DEMO-001` (vải) và `YC-DEMO-002` (BTP) | Lập phiếu cấp theo đơn xin |

Giao việc: vào **Giao việc** (`TTMAY`), chọn đơn DEMO-DH003 → mỗi công đoạn × màu × size hiện "Cần làm" = số bộ BTP tổ đã nhận (M: 50, L: 40 trước khi nhận PC-DEMO-006), "Đã giao", "Chờ xác nhận", "Chưa giao".

## 9 bước (UC09 → 25 → 37 → 51 → 55 → 57 → 45 → 61 → 59 → 21)

| # | Ai | Làm gì | Màn hình | Kết quả cần thấy |
|---|---|---|---|---|
| 1 | Admin | Vào **Mã hàng → Định mức** khai thành phần và định mức NVL; tạo đơn mới (màu × size mới tự thành biến thể); xem đơn DEMO-DH001 | Mã hàng, Đơn hàng → chi tiết | Nút "Chuyển sang sản xuất" bị khóa khi đơn còn "Chờ NVL"; sau này tổ may nhận BTP thì đơn tự sang sản xuất |
| 2 | Kế toán / Admin | Duyệt đơn giá công đoạn của DEMO-AO01 | Công đoạn và đơn giá / Duyệt đơn giá | Đơn giá ở trạng thái Đã duyệt thì mới nhập sản lượng được |
| 3 | Quản đốc / Thủ kho | Đặt hàng đơn mua với **số lần NCC giao dự kiến = 2**; Thủ kho lập phiếu nhập lần 1 và đính kèm ảnh chứng từ; Quản đốc xác nhận. Lập tiếp lần 2 còn thiếu | Đơn mua NVL, NVL chờ nhận, Phiếu nhập NVL | NVL chờ nhận hiện "đã nhận / dự kiến"; Quản đốc không xác nhận được khi chưa có ảnh; sau lần giao cuối còn thiếu thì tự sinh "Thiếu NVL khi nhập" |
| 4 | Quản đốc | Chọn giao bù cho đơn thiếu; Thủ kho lập phiếu bổ sung | Thiếu NVL khi nhập | Đủ hàng bù thì đơn thiếu tự hoàn thành, đơn mua thành "Đã nhận đủ" |
| 5 | Tổ trưởng cắt | Ghi bàn cắt, bàn giao BTP vào kho (có thể rút phiếu khi Thủ kho chưa nhận) | Bàn cắt, Bàn giao BTP | Thủ kho nhận → tồn kho BTP tăng; Quản đốc đặt KPI không vượt BTP trong kho, và đóng lệnh cắt ở chi tiết lệnh |
| 6 | Tổ trưởng may | Chọn đơn ở bảng chia việc, bấm Giao ở từng công đoạn (số lần làm, tối đa số bộ BTP tổ đã nhận); công nhân xác nhận | Giao việc cho công nhân, Việc được giao cho tôi | Công nhân tự xác nhận hoặc tổ trưởng xác nhận hộ (có lý do); chưa được giao việc thì không báo sản lượng được |
| 7 | Tổ trưởng may | Nhập sản lượng cuối ca | Sản lượng cuối ca | Vượt số lần được giao ở công đoạn đó thì bị chặn (MSG16) |
| 8 | QA/QC | Nhập đợt QC, rồi lập phiếu bàn giao thành phẩm; Thủ kho nhận | Kiểm hoàn thiện, Bàn giao thành phẩm | Kho thành phẩm tăng đúng số Thủ kho đếm |
| 9 | Quản đốc → Thủ kho → Kế toán | Lập phiếu xuất, xuất hàng, xác nhận khách nhận | Phiếu xuất hàng | Đơn chuyển Đang giao → Giao một phần / Đã giao |

Kết thúc bằng Admin / Quản đốc mở **Trang chủ**: bảng *Tiến độ đơn hàng* hiện cắt, vào kho BTP, cấp tổ may, hoàn thiện, nhập kho, đã giao và tỉ lệ lỗi; đơn nguy cơ trễ có thẻ đỏ.

## Cuối tháng (lương, chi phí, hòa vốn)

1. Kế toán nhập chấm công (hoặc dùng dữ liệu đồng bộ máy RJ), xem **Lương tạm tính** của công nhân.
2. Kế toán nhập bảng lương tháng, gửi duyệt; Admin duyệt rồi khóa tháng.
3. Kế toán nhập chi phí tháng; Admin vào **Hòa vốn**, chọn tháng **2026-08** (đã có sẵn doanh thu, chi phí và bảng lương đã duyệt) rồi "Tính toán".

## Việc phụ có sẵn dữ liệu demo

- **Thông báo**: Quản đốc đã gửi một thông báo khẩn toàn xưởng; chuông trên thanh trên cùng hiện số chưa đọc.
- **Bậc thợ**: có một đề xuất bậc B cho CNMAY2 đang chờ Admin / Quản đốc quyết định; Admin xem và thêm mức tiền bậc.
- **Kiểm kê**: phiếu `KK-DEMO-001` (kho NVL, đếm thiếu 3m) chờ duyệt.
- **Sổ xe**: xe `29C-12345` có chuyến lệch km và chuyến xăng vượt định mức để xem cảnh báo.
- **Nhật ký thay đổi** (Admin): xem ai đã đổi gì; **Nhân viên → Nhập từ Excel** để nhập nhiều nhân viên một lần.

## Thử các thay đổi 28/10 (theo sheet ghi chú)

| Việc | Ai | Cách thử |
|---|---|---|
| Menu theo loại tổ | `TTMAY` và `TTCAT` | `TTCAT` thấy Lệnh cắt, Bàn cắt, Bàn giao BTP; `TTMAY` không thấy, gõ thẳng đường dẫn cũng bị đưa về trang chủ |
| Định mức | `KETOAN`, `ADMIN` | Mã hàng → Định mức: Thành phần + Định mức NVL (kèm dài × rộng chi tiết); tab Màu – size chỉ đọc, nhập kích thước thành phẩm; Kế toán có mục "Mã hàng" riêng (chỉ danh sách + định mức) |
| Lập KPI | `QUANDOC` | Chỉ tiêu KPI → Lập: ô "BTP khả dụng" hiện kho / tổ đang giữ / KPI khác và chặn khi vượt |
| Kết thúc đơn của tổ | `QUANDOC` | Chỉ tiêu KPI → Kết thúc đơn: xem lệch theo công nhân, KPI đạt / không, tăng ca; xác nhận thì phần lệch thành phiếu hàng hỏng "Lệch kiểm cuối đơn" (cần công đoạn cuối và đơn giá đã duyệt) |
| Bậc thợ | `ADMIN` | Bậc thợ → đặt bậc chọn Tổ rồi Công nhân (hiện bậc cũ); bậc cố định A–D |
| Quản lý tài khoản | `QUANDOC` | Nhân viên: thêm, sửa, khóa, đặt lại mật khẩu mọi người trừ Admin và Kế toán |
| Xuất hàng | `QUANDOC`, `THUKHO` | `XH-DEMO-001`: Thủ kho chụp ảnh phiếu có ký rồi xuất (thiếu ảnh bị chặn); Kế toán xác nhận khách nhận kèm ảnh biên bản giao |
| Cắt xong | `TTCAT`, `QUANDOC` | `LC-DEMO-002`: Tổ cắt bấm "Cắt xong" (nêu lý do nếu thiếu kế hoạch) → Quản đốc nhận thông báo kèm vải nhận / trả / thực dùng |
| Thông báo | mọi vai trò | Trang chủ có khối "Thông báo mới" (2 thông báo mẫu nghỉ lễ / sự kiện của mục 19.5) |
| Doanh thu | `ADMIN` | Trang chủ có khối Doanh thu tháng này (theo SL hoàn thiện đã nhập kho TP) |

## Lưu ý khi diễn tập

- Dữ liệu demo không có họ tên thật hay lương thật.
- Chưa thử trên trình duyệt thật; các luồng trên đã chạy đủ qua API và test tự động.
- Chưa có push FCM: thông báo chỉ hiện trong ứng dụng.
