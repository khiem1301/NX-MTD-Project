# Tiến độ chức năng (cập nhật 23/10/2026)

Nhánh: `feature/ALL-missing-features` (chưa gộp vào `main`, cần mở PR).
Kiểm tra: 960 test backend, 78 test frontend, `tsc` và `eslint` sạch. Các luồng mới đã chạy qua API trên DB thử.
**Chưa có chức năng nào của đợt này được thử trên trình duyệt thật.**

## Đã làm được

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
- `docs/CODE_DEVIATIONS.md`: DEV-ALL-1…6 ghi các chỗ code khác tài liệu gốc.

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
