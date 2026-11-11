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
| Công nhân may | `CNMAY1`…`CNMAY4` (tổ 1), `CNMAY5`…`CNMAY20` (tổ 2) |
| Tổ trưởng / tổ phó khác | `TTMAY2`, `TPMAY`, `TPMAY2`, `TPCAT` |
| Cơ điện | `CODIEN` |

Đơn demo: **DEMO-DH001** (mã hàng DEMO-AO01, 5 dòng màu × size), **DEMO-DH003** (2 dòng Đen M / L) và **DEMO-DH005…013** ở các trạng thái khác (mục dưới). Hai tổ may, một tổ cắt.

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

### Dữ liệu để test đủ chức năng (mục 19.7 của script)

Dữ liệu nằm ở mục 19.7 của `SQL/NX-MTD_Database.sql`: tạo DB mới từ script này là có, rồi khởi động lại backend để băm mật khẩu tài khoản mới. Các việc dưới đây đã có sẵn dữ liệu để bấm thử:

| Chức năng | Ai | Dữ liệu | Cách thử |
|---|---|---|---|
| Đăng nhập, tài khoản | Admin | `NVKHOA1` (khóa), `NVNGHI1` (đã nghỉ) | Đăng nhập bị chặn; Admin mở khóa `NVKHOA1` |
| Danh mục ngừng dùng | Admin | Công ty C, `DEMO-NGUNG01`, `DEMO-NGUNG04`, tổ `MAY3`, xe `51E-000.00`, bậc E | Lọc trạng thái, xem danh sách |
| Đơn hàng mọi trạng thái | Admin | `DEMO-DH005` Mới, `DH007` Đã hủy, `DH008` Đã dừng, `DH009` Đang giao, `DH010` Giao một phần, `DH011` Đã giao, `DH012` Hoàn tất | Lọc trạng thái; `DH010` có thẻ nguy cơ trễ |
| Chờ khai báo NVL, mã chưa có công đoạn | `QUANDOC`, `TTMAY2` | `DH005` (mã `DEMO-SM03`) | Khai NVL; màn Phân công báo mã hàng chưa khai công đoạn |
| Đơn đủ NVL | Admin, `QUANDOC` | `DH006` (khách cấp đủ) | Chuyển sang sản xuất, tạo lệnh cắt |
| Danh sách công nhân dài | `TTMAY2` | Tổ may 2 có 16 công nhân (`CNMAY5`…`CNMAY20`) | Phân công công đoạn → Theo công nhân |
| Đơn từ đủ 11 loại | `CODIEN`, `TTMAY`, `TTCAT`, `THUKHO` | `YC-DEMO-003`…`021`, đủ 6 trạng thái | Sửa, hủy, duyệt, từ chối, hoàn thành |
| NVL lỗi tự tạo đơn | `QUANDOC` | `YC-DEMO-011` (kho đủ), `YC-DEMO-012` (kho thiếu) | Duyệt → tự tạo đơn Xin NVL, và đơn Mua NVL cho phần thiếu |
| Cắt bù | `QUANDOC`, `TTMAY` | `YC-DEMO-014` chờ duyệt, `YC-DEMO-015` đã duyệt, phiếu `MAY1-261030-901` chưa có đơn | Duyệt; lập lệnh cắt bù; tổ may tạo đơn từ phiếu hàng hủy |
| Thiếu hàng khi nhập | `QUANDOC` | `YC-DEMO-016` (`PN-DEMO-013` thiếu 30 m) | Chọn giao bù hoặc không bù |
| Phiếu bị trả, từ chối, đã hủy | `THUKHO`, `QUANDOC` | `PN-DEMO-016`, `PC-DEMO-901`, `TV-DEMO-002`…`004`, `BG-DEMO-901/902`, `TP-DEMO-210/310`, `XH-DEMO-310/410`, `KK-DEMO-003` | Xem lý do, lọc trạng thái, lập lại |
| Phiếu xuất chờ xuất | `THUKHO` | `XH-DEMO-210` | Thiếu ảnh bị chặn; từ chối được |
| Xác nhận khách nhận | `KETOAN` | `XH-DEMO-009` | Xác nhận kèm ảnh biên bản |
| Kiểm kê kho BTP, thành phẩm | `QUANDOC` | `KK-DEMO-004`, `KK-DEMO-005` | Duyệt hoặc trả lại |
| Giao việc | `TTMAY` | Giao hộ và công nhân từ chối ở `DH003` (OP06, OP07) | Xem trạng thái, lý do |
| Hàng hỏng bị phản đối | `TTMAY` | `MAY1-261102-901` | Xem lý do phản đối của công nhân |
| Chấm công | `KETOAN`, tổ trưởng | Lượt quẹt 02/11: thiếu giờ ra (`CNMAY5`), tăng ca (`CNMAY3`), mã `999` chưa gán nhân viên; phiên đồng bộ lỗi; chấm hộ `CNMAY6`, `CNMAY7`, `CNMAY8` | Xem bảng công; gán mã chấm công |
| Bậc thợ | Admin, `QUANDOC` | Đề xuất của `CNMAY2` (chờ), `CNMAY4` (đã áp dụng), `CNMAY6` (không áp dụng) | Duyệt, xem lịch sử |
| Bảng lương | `KETOAN`, Admin | 05 đã duyệt + khóa tháng, 06 nháp, 07 bị trả lại, 09 chờ duyệt, 10 đã duyệt | Gửi duyệt, duyệt, từ chối, mở lại |
| Hòa vốn | Admin | Tháng **2026-10** có sẵn một lần tính: `DEMO-AO01` có lãi, `DEMO-QU02` nguy cơ lỗ | Xem kết quả, bấm tính lại |
| Thông báo | mọi vai trò | Gửi theo tổ, theo vai trò, toàn xưởng; hệ thống đủ 4 nguồn; có cái đã đọc | Hộp thư lọc Từ quản lý / Hệ thống |

Chưa có sẵn: ảnh và file đính kèm (`Attachment`, `ProductImage`) vì cần file thật, hãy tải lên khi thử. Xác nhận suất ăn chỉ làm được trước giờ chốt 08:30 của đúng ngày (`2026-11-03` đang mở, tổ may 1 đã xác nhận).

Giao việc: vào **Giao việc** (`TTMAY`), chọn đơn DEMO-DH003 → mỗi công đoạn × màu × size hiện "Cần làm" = số bộ BTP tổ đã nhận (M: 50, L: 40 trước khi nhận PC-DEMO-006), "Đã giao", "Chờ xác nhận", "Chưa giao".

## 9 bước (UC09 → 25 → 37 → 51 → 55 → 57 → 45 → 61 → 59 → 21)

| # | Ai | Làm gì | Màn hình | Kết quả cần thấy |
|---|---|---|---|---|
| 1 | Admin | Vào **Mã hàng → Định mức** khai thành phần và định mức NVL; tạo đơn mới (màu × size mới tự thành biến thể); xem đơn DEMO-DH001 | Mã hàng, Đơn hàng → chi tiết | Nút "Chuyển sang sản xuất" bị khóa khi đơn còn "Chờ NVL"; sau này tổ may nhận BTP thì đơn tự sang sản xuất |
| 2 | Kế toán / Admin | Duyệt đơn giá công đoạn của DEMO-AO01 | Công đoạn và đơn giá / Duyệt đơn giá | Đơn giá ở trạng thái Đã duyệt thì mới nhập sản lượng được |
| 3 | Quản đốc / Thủ kho | Đặt hàng đơn mua với **số lần NCC giao dự kiến = 2**; Thủ kho lập phiếu nhập lần 1 và đính kèm ảnh chứng từ; Quản đốc xác nhận. Lập tiếp lần 2 còn thiếu | Đơn mua NVL, NVL chờ nhận, Phiếu nhập NVL | NVL chờ nhận hiện "đã nhận / dự kiến"; Quản đốc không xác nhận được khi chưa có ảnh; sau lần giao cuối còn thiếu thì tự sinh "Thiếu NVL khi nhập" |
| 4 | Quản đốc | Chọn giao bù cho đơn thiếu; Thủ kho lập phiếu bổ sung | Thiếu NVL khi nhập | Đủ hàng bù thì đơn thiếu tự hoàn thành, đơn mua thành "Đã nhận đủ" |
| 5 | Tổ trưởng cắt | Ghi bàn cắt, bàn giao BTP vào kho (có thể rút phiếu khi Thủ kho chưa nhận) | Bàn cắt, Bàn giao BTP | Thủ kho nhận → tồn kho BTP tăng; Quản đốc đặt KPI không vượt BTP trong kho, và đóng lệnh cắt ở chi tiết lệnh |
| 6 | Tổ trưởng may | Chọn ngày, đơn, mở từng công nhân và nhập số lần giao của ngày (công đoạn đã phân công lấy sẵn, thêm được công đoạn ngoài phân công; tối đa số bộ BTP tổ đã nhận); cuối ngày bấm Thu hồi cuối ngày kèm lý do | Giao sản lượng theo ngày, Sản lượng công đoạn được giao | Phiếu của tổ trưởng có hiệu lực ngay; chưa được giao việc thì không báo sản lượng được; phần chưa làm hết bị thu hồi (hệ thống tự thu sau hạn sửa sản lượng) |
| 7 | Tổ trưởng may | Nhập sản lượng cuối ca | Sản lượng cuối ca | Vượt số lần được giao ở công đoạn đó thì bị chặn (MSG16) |
| 8 | QA/QC | Nhập đợt QC, rồi lập phiếu bàn giao thành phẩm; Thủ kho nhận | Kiểm hoàn thiện, Bàn giao thành phẩm | Kho thành phẩm tăng đúng số Thủ kho đếm |
| 9 | Quản đốc → Thủ kho → Kế toán | Lập phiếu xuất, xuất hàng, xác nhận khách nhận | Phiếu xuất hàng | Đơn chuyển Đang giao → Giao một phần / Đã giao |

Kết thúc bằng Admin / Quản đốc mở **Trang chủ**: bảng *Tiến độ đơn hàng* hiện cắt, vào kho BTP, cấp tổ may, hoàn thiện, nhập kho, đã giao và tỉ lệ lỗi; đơn nguy cơ trễ có thẻ đỏ.

## Cuối tháng (lương, chi phí, hòa vốn)

1. Kế toán nhập chấm công (hoặc dùng dữ liệu đồng bộ máy RJ), xem **Lương tạm tính** của công nhân.
2. Kế toán nhập bảng lương tháng, gửi duyệt; Admin duyệt rồi khóa tháng.
3. Kế toán nhập chi phí tháng; Admin vào **Hòa vốn**, chọn tháng **2026-10** (đã có sẵn doanh thu, chi phí và bảng lương đã duyệt) rồi "Tính toán".

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
| Định mức | `KETOAN`, `ADMIN` | Mã hàng → Định mức: Thành phần + Định mức NVL (kèm dài × rộng chi tiết); tab Màu – size chỉ đọc; Kế toán có mục "Mã hàng" riêng (chỉ danh sách + định mức) |
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
