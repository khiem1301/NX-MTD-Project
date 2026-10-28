# Tiến độ chức năng (cập nhật 28/10/2026)

Nhánh: `feature/ALL-missing-features` (chưa gộp vào `main`, cần mở PR).
Kiểm tra: 1045 test backend, 92 test frontend, `tsc` và `eslint` sạch. Các luồng mới đã chạy qua API (45 bước e2e) trên database dựng từ script 26/10 kèm dữ liệu demo.
**Chưa có chức năng nào của đợt này được thử trên trình duyệt thật.**

## Đã làm được

**Bổ sung 28/10 (sửa theo sheet ghi chú):** menu và route theo loại tổ (tổ cắt / tổ may), Admin bỏ "Ngày công của tôi"; tìm kiếm tức thì và bộ lọc mặc định "Tất cả"; Khách hàng / Nhà cung cấp bắt buộc SĐT, email, địa chỉ; duyệt đơn giá có tab "Tất cả" và Admin không thấy Nháp; cột STT + toast ở Nhân viên; "Thêm công đoạn"; đặt bậc chọn tổ trước; bậc thợ động (Admin thêm / sửa / ngừng dùng / xóa); Quản đốc quản lý tài khoản trừ Admin và Kế toán; lệnh cắt tự chuyển Hoàn thành khi kho nhận đủ BTP (không đóng tay), kiểm tra vải có giữ chỗ + xem trước vải + tab "Vải theo đơn"; KPI theo BTP khả dụng, chỉ đánh giá cuối đơn ("Kết thúc đơn": phiếu lỗi lệch kiểm cuối đơn cho từng công nhân, chốt KPI, đơn tăng ca); thông báo hệ thống cho luồng cắt, kho, KPI (chuông 30 giây + toast); hàng hỏng tìm theo công nhân / mã hàng / tổ; trang chủ có thông báo mới và doanh thu (Admin). Xem `DEV-ALL-15`.

**Bổ sung 27/10 (chuyển sang database bản 26/10):** màu NVL nằm trong NVL (danh mục NVL có Màu; các form bỏ ô màu); giao việc cho công nhân **không còn BTP giao kèm** (chỉ số lần làm công đoạn, trần = số bộ BTP tổ đã nhận); trang "BTP của tổ" hiện đã nhận / QC đạt / hàng hủy / còn trên chuyền; thêm validate (NVL trùng theo màu, NVL của đơn 0/0, dòng phiếu cấp sai loại kho, chi tiết ngoài định mức, giao vượt số chưa giao, xác nhận ngoài 1..số trên phiếu); dữ liệu demo 19.3 (đơn DEMO-DH003 và phiếu đang chờ ở từng bước xác nhận). Xem `DEV-ALL-14`.

**Bổ sung 26/10 (chuyển sang database bản 25/10):** ghi kho qua thủ tục `usp_Stock_Post` (kiểm kê tạo dòng tồn khi cần); cắt bù theo chi tiết × vải; **Giao việc cho công nhân theo công đoạn** (bảng chia việc, giao kèm BTP, trần sản lượng theo công đoạn, tìm theo công đoạn to); màn Công đoạn có công đoạn to, chi tiết và công tắc giao kèm BTP; sửa định mức đang được dùng không còn lỗi (cập nhật tại chỗ). Giao diện mới: thanh bên gom nhóm, header có tìm nhanh và menu người dùng.

**Sửa 25/10 (lần 2):** phiếu nhập NVL không gửi trùng được (mỗi NVL một phiếu chờ xác nhận; kiểm trần cả hàng khách cấp và lúc Quản đốc xác nhận); phân công công đoạn chọn được nhiều công đoạn cho một công nhân (`POST /team-leader/operation-assignments/bulk`).

**Bổ sung 25/10 (chuyển sang database mới của Morgan, bản 24/10):** định mức theo mã hàng (màn Mã hàng → Định mức: thành phần, màu – size, NVL; dòng đơn tự tạo biến thể), số lần giao dự kiến cho NVL khách cấp và đơn mua, thiếu hàng tự tính (Thủ kho không nhập thiếu / lỗi), cắt bù chọn chi tiết, tồn kho BTP chi tiết lẻ, đăng nhập bằng mã nhân viên; **bỏ lịch KPI theo ngày và dữ liệu demo 19.3–19.6** (xem DEV-ALL-11). Theo swimlane (xem `docs/SO_SANH_SWIMLANE.md`): đơn tự sang sản xuất khi tổ may nhận BTP, rút phiếu bàn giao BTP, ảnh chứng từ khi xác nhận phiếu nhập, mở form cắt bù sau khi ghi hàng hủy, form đơn NVL lỗi, KPI không vượt BTP trong kho, đóng lệnh cắt. **Chưa làm:** bước Quản đốc duyệt phiếu xuất (database chưa có trạng thái, DEV-ALL-12).

**Bổ sung 24/10 (lần 6):** Quản đốc có danh sách đơn chờ khai báo NVL (bấm thông báo mở đúng đơn); Thủ kho có màn "NVL chờ nhận" và được báo khi khách cấp NVL hoặc đơn mua được đặt (xem DEV-ALL-10).

**Bổ sung 24/10 (lần 5, phần lịch KPI đã gỡ ngày 25/10):** lập KPI chỉ nhập tổng; nút "Xem theo ngày" mở lịch tháng để đặt KPI từng ngày (gõ tay hoặc chia tăng dần), có Tổng bán thành phẩm và số còn lại để đặt KPI (xem DEV-ALL-8, DEV-ALL-9). Màn "So sánh BTP – KPI" riêng đã gộp vào lịch.

**Bổ sung 23/10 (lần 3):** Quản đốc sửa được tổ và vai trò nhân viên (trừ Admin / Quản đốc / Kế toán), Admin và Quản đốc đặt bậc thợ trực tiếp cho công nhân (xem DEV-ALL-7).

**Bổ sung 23/10 (lần 2):** TH-16 (in / xuất Excel từng phiếu nhập, cấp, xuất hàng) và test phạm vi dữ liệu 3 tầng KH-24 đã xong.

### Kho, nhập, xuất (Thái)
| Mã | Chức năng | Ghi chú |
|---|---|---|
| TH-12 | Phiếu nhập NVL từ nhà cung cấp theo đơn mua | Số nhập ≤ số còn phải nhận; cập nhật trạng thái đơn mua |
| TH-13 | Nhận thiếu | Tự sinh đơn "Thiếu NVL khi nhập"; Quản đốc chọn giao bù hoặc không bù; phiếu bổ sung; cập nhật trạng thái NVL của đơn hàng |
| TH-11 | Bàn giao thành phẩm QA/QC → Thủ kho | Giao ≤ số QC đạt; Thủ kho nhận hoặc từ chối; tăng kho thành phẩm |
| TH-14 | Phiếu xuất hàng | Quản đốc lập (giữ chỗ) → Thủ kho xuất → Kế toán xác nhận khách nhận; đơn tự đổi trạng thái giao |
| TH-10 | Lệnh cắt bù và tổ cắt trả vải thừa | Lệnh cắt bù từ đơn cắt bù đã duyệt; Thủ kho xác nhận trả vải |
| TH-19 | Giao / thu hồi bán thành phẩm với công nhân | Công nhân tự xác nhận hoặc tổ trưởng xác nhận hộ (có lý do); trần sản lượng bám số BTP thật |
| TH-16 | In / xuất Excel phiếu nhập, phiếu cấp, phiếu xuất hàng | Nút "In / xuất Excel" trong ngăn chi tiết; một phiếu một file A4 có chỗ ký tên |

### Kế toán, kiểm kê (Thảo)
| Mã | Chức năng | Ghi chú |
|---|---|---|
| TA-02 | Kiểm kê kho | Chụp số sổ, nhập số đếm, duyệt thì điều chỉnh kho; trả lại có lý do |
| TA-03 | Sổ xe và nhiên liệu | Cảnh báo lệch km và xăng vượt định mức |

### Quản trị, thông báo, báo cáo (Khiêm)
| Mã | Chức năng | Ghi chú |
|---|---|---|
| KH-12 | Thông báo | Chuông trên thanh trên, danh sách, đánh dấu đã đọc, thông báo nhanh (thường / khẩn) gửi toàn xưởng, theo tổ hoặc vai trò |
| KH-14 | Bậc thợ | Mức tiền bậc A–D, tổ trưởng đề xuất, Admin / Quản đốc quyết định |
| KH-15 | Chuyển đơn sang sản xuất | Chặn khi đơn còn chờ NVL |
| KH-16 | Dashboard tiến độ đơn | Trên Trang chủ của Admin / Quản đốc |
| KH-22 | Nhật ký thay đổi (Admin) và nhập nhân viên từ Excel | Có bước xem trước, báo lỗi và trùng |
| KH-23 | Job nền | 17:00 gắn cờ đơn nguy cơ trễ; 01:00 xóa thông báo quá 90 ngày |
| KH-17 | Kịch bản demo 9 bước | `docs/DEMO_SCRIPT.md` |
| KH-24 | Test phân quyền và phạm vi dữ liệu 3 tầng | Mọi controller khóa đúng vai trò (`RbacControllerTests`); dữ liệu tầng Mật chỉ tới đúng vai trò, công nhân / tổ trưởng không tự chọn người / tổ (`DataScopeTests`) |


### Dữ liệu và tài liệu
- Dữ liệu demo mục 19.4–19.5 trong `SQL/NX-MTD_Database.sql` (thẻ kho, bậc thợ, sổ xe, kiểm kê, bàn giao chờ nhận, thông báo).
- `docs/CODE_DEVIATIONS.md`: DEV-ALL-1…10 ghi các chỗ code khác tài liệu gốc.

## Chưa làm

### Cố ý bỏ qua (đã thống nhất)
- Xuất file MISA (MI-14): ngoài phạm vi.
- Push FCM của KH-12: thông báo chỉ hiện trong ứng dụng.

### Chưa làm vì cần code thêm
- Giao diện điện thoại (để sau, theo yêu cầu).
- Thử toàn bộ màn hình mới trên trình duyệt thật (kể cả giao diện điện thoại).

### Không phải code (cần người làm)
- MI-06 đồng bộ máy chấm công RJ thật; KH-09, KH-11, KH-18…21, KH-25, KH-26; TA-01, TA-04, TA-05; TH-15; KI-15, KI-16, KI-19; MI-11, MI-15.
- Vẽ sơ đồ, diễn tập demo, đặt tag, deploy AWS, gom báo cáo, test case Report 5.

## Đang chạy để thử
- Backend `http://localhost:5080`, frontend `http://localhost:5173`, database `NXMTD_demo2` (dựng mới từ script, mật khẩu mọi tài khoản mẫu `123456`).
- Database cũ `NXMTD_demo` giữ nguyên, `NXMTD` không đụng tới.
