# So sánh màn hình với swimlane

Ghi ngày 25/10/2026. Đối chiếu code (backend và giao diện) với tài liệu SRS bản v25: mô tả luồng III.1–III.5 và các use case UC37–UC62. **Ảnh swimlane trong tài liệu không đọc được bằng máy**, nên bảng dưới đối chiếu theo phần chữ mô tả cùng luồng; cần Morgan hoặc bạn xem lại ảnh để chốt các điểm đánh dấu "chưa chắc".

Chưa thử trên giao diện trình duyệt. Các điểm "Đã sửa" đã chạy qua API trên database dựng từ script (kịch bản 21 bước) và có test.

## Kết luận ngắn

Các luồng khớp ở hầu hết bước. Có 6 điểm lệch đã sửa, 1 điểm lệch **chưa sửa được vì database chưa có chỗ lưu** (bước Quản đốc duyệt phiếu xuất), và vài điểm nhỏ chỉ ghi nhận.

## 1. Nhập kho NVL (III.1)

| Bước theo tài liệu | Màn hình trong app | Kết quả |
|---|---|---|
| Admin tạo đơn, chọn nguồn NVL | Admin: Đơn hàng | Lệch nhẹ: nguồn do hệ thống suy ra từ số khách cấp (cột tính `OrderMaterial.Source`), form đơn không có ô chọn. Chỉ ghi nhận. |
| Khách cấp: ghi NVL, báo Thủ kho lịch giao | Quản đốc / Admin: NVL của đơn; Thủ kho: NVL chờ nhận | Khớp. Lịch giao nay là **số lần giao dự kiến** (database mới). |
| NCC: Quản đốc lập đơn mua, báo Thủ kho | Quản đốc: Đơn mua NVL | Khớp. Đặt hàng có thêm "số lần NCC giao dự kiến". |
| Kế toán xác nhận thanh toán | Kế toán: Thanh toán đơn mua | Khớp. Thanh toán không chặn nhận hàng (tài liệu không nói rõ). |
| Thủ kho lập phiếu nhập: số đạt, thiếu, lỗi, ảnh chứng từ | Thủ kho: Phiếu nhập NVL | **Lệch có chủ đích:** Thủ kho chỉ nhập số đạt (script mới bỏ cột thiếu / lỗi, bạn đã chốt). Thiếu hàng do hệ thống tự tính. Cần Morgan sửa swimlane / UC37. |
| Ảnh chứng từ giao hàng | Chi tiết phiếu nhập | **Đã sửa:** Quản đốc không xác nhận được khi phiếu chưa có ảnh; nút xác nhận mờ và có cảnh báo. |
| Quản đốc xác nhận hoặc trả lại có lý do; Thủ kho sửa và gửi lại | Quản đốc / Thủ kho: Phiếu nhập NVL | Khớp. |
| Thiếu / lỗi: Quản đốc chọn bù hoặc không bù | Quản đốc: Thiếu NVL khi nhập | Khớp. Đơn thiếu NVL tự sinh khi phiếu xác nhận mà đã nhận đủ số lần giao dự kiến. |

## 2. Cắt (III.2)

| Bước theo tài liệu | Màn hình | Kết quả |
|---|---|---|
| Quản đốc lập lệnh cắt, hệ thống kiểm đủ vải, thiếu thì không lưu | Quản đốc: Lệnh cắt | Khớp. Vải tính từ định mức của mã hàng. |
| Tổ cắt xin vải, Thủ kho cấp hoặc từ chối có lý do | Tổ trưởng: Đơn từ của tôi; Thủ kho: Đơn cần xử lý, Phiếu cấp NVL / BTP | Khớp. |
| Tổ cắt xác nhận nhận vải, lệch phải có lý do | Tổ trưởng: Phiếu cấp NVL / BTP | Khớp. |
| Tổ cắt báo NVL lỗi, Quản đốc xử lý | Tổ trưởng: Đơn từ của tôi | **Đã sửa:** thêm form đơn "NVL lỗi" (trước đó có ở backend nhưng giao diện thiếu). |
| Ghi bàn cắt, cảnh báo / chặn vượt số | Tổ trưởng: Bàn cắt | Khớp (cảnh báo khi vượt kế hoạch trong % dư cho phép, chặn khi vượt hơn). |
| Bàn giao BTP, Thủ kho đếm / sửa / từ chối; tổ cắt hủy trước khi xác nhận | Tổ trưởng: Bàn giao BTP; Thủ kho: Bàn giao BTP | **Đã sửa:** thêm nút Rút phiếu (các bàn cắt đã khóa mở lại). |
| Trả vải thừa | Tổ trưởng / Thủ kho: Trả vải thừa | Khớp. |
| Quản đốc xem kết quả lệnh cắt | Quản đốc: Lệnh cắt | **Đã sửa:** chi tiết lệnh có dòng tổng kế hoạch / đã cắt / đã bàn giao và nút **Đóng lệnh** (nhập lý do khi BTP vào kho chưa đủ kế hoạch). Số vải thực dùng chưa có vì chưa có dữ liệu mét vải dùng. |

## 3. May (III.3)

| Bước theo tài liệu | Màn hình | Kết quả |
|---|---|---|
| Quản đốc đặt KPI, hệ thống kiểm tồn BTP | Quản đốc: Chỉ tiêu KPI tổ may | **Đã sửa:** tổng KPI còn hiệu lực của đơn + mã hàng không được vượt BTP (bộ đủ) đã vào kho; báo "còn đặt được". |
| Tổ may xin BTP, Thủ kho cấp, tổ may xác nhận nhận → đơn sang In Production | Tổ trưởng may: Đơn từ của tôi, Phiếu cấp NVL / BTP | **Đã sửa:** xác nhận nhận BTP thì đơn NEW tự sang IN_PRODUCTION (nút của Admin vẫn dùng). Số BTP tổ may nhận là trần sản lượng, đã có. |
| Giao BTP cho công nhân, công nhân xác nhận hoặc tổ trưởng xác nhận hộ có lý do; thu hồi | Tổ trưởng may: Giao / thu hồi BTP; Công nhân: BTP của tôi | Khớp. |
| Sản lượng cuối ca, cảnh báo chưa chấm công, chặn vượt trần, hạn sửa, Quản đốc sửa sau hạn | Tổ trưởng: Sản lượng cuối ca; Quản đốc: Sửa sản lượng sau hạn | Khớp. |
| Hàng hỏng Rework / Scrap, công nhân được báo và phản đối | Tổ trưởng: Hàng hỏng; Công nhân: Hàng hỏng của tôi | Khớp. |
| Scrap mở yêu cầu cắt bù cho Quản đốc | Tổ trưởng: Hàng hỏng | **Đã sửa:** ghi hàng hủy xong thì form đơn cắt bù tự mở (tổ trưởng chọn chi tiết cần cắt bù vì hệ thống không biết chi tiết nào hỏng). Vẫn gửi từ phiếu cũ được. |
| QA/QC nhập hàng đạt, bàn giao thành phẩm, Thủ kho nhận | QA/QC: Hàng hoàn thiện, Bàn giao thành phẩm | Khớp. |
| Quản đốc so kết quả với KPI | Quản đốc: Chỉ tiêu KPI tổ may | Khớp (bảng kết quả từng ngày). Lịch KPI theo ngày đã gỡ (xem `DEV-ALL-11`). |

## 4. Xuất hàng (III.4)

| Bước theo tài liệu | Màn hình | Kết quả |
|---|---|---|
| Quản đốc lập phiếu xuất | Quản đốc: Phiếu xuất hàng | Khớp. |
| Thủ kho chuẩn bị, Quản đốc duyệt hoặc trả lại có lý do | — | **Chưa làm được.** `DispatchNote.Status` không có trạng thái "đã duyệt" và bảng không có cột người duyệt. Cần Morgan thêm (đề xuất trong `DEV-ALL-12`); sau đó code đổi theo. App hiện: Quản đốc lập → Thủ kho xuất hoặc từ chối có lý do. |
| Thủ kho in phiếu PDF, tải ảnh phiếu ký, xác nhận xuất | Thủ kho: Phiếu xuất hàng | Lệch nhẹ: in ra **Excel**, ảnh phiếu ký chưa bắt buộc. Chỉ ghi nhận. |
| Kế toán ghi sổ xe và nhiên liệu | Kế toán: Sổ xe và nhiên liệu | Khớp (màn riêng, không gắn trạng thái phiếu). |
| Kế toán xác nhận khách nhận (ngày, ảnh, ghi chú) | Kế toán: Phiếu xuất hàng | Lệch nhẹ: chưa có ô ghi chú. Đơn sang Delivered khi đủ số đặt, đã đúng. |

## 5. Suất ăn (III.5)

Khớp: tổ trưởng xác nhận trước 08:30 (đánh dấu sẵn theo chấm công), Kế toán nhập suất khách, job nhắc 08:15 và tự chốt 08:30, Kế toán nhập đơn giá (mặc định giá hôm trước) và đánh dấu đã báo bếp, chặn sửa sau giờ chốt. Lệch nhẹ: màn của Kế toán chỉ làm việc với ngày hôm nay.

## Việc cần bạn hoặc Morgan quyết

1. Thêm trạng thái duyệt cho phiếu xuất (đoạn đề xuất ở `DEV-ALL-12`), hoặc bỏ bước duyệt khỏi swimlane.
2. Cập nhật swimlane / UC37: Thủ kho không nhập thiếu / lỗi, thiếu hàng tự tính theo số lần giao dự kiến.
3. Xem lại ảnh swimlane để xác nhận các điểm chỉ ghi nhận ở trên.
