# Luồng đơn hàng: từ Admin tạo đơn đến khi giao xong

Ghi ngày 24/10/2026, **cập nhật 26/10/2026 theo database bản 25/10** (định mức theo mã hàng, số lần giao dự kiến, thiếu hàng tự tính; bỏ lịch KPI theo ngày). Đã chạy thử toàn bộ luồng qua API trên database dựng mới từ `SQL/NX-MTD_Database.sql`, tất cả các điểm kiểm đều khớp. **Chưa thử trên giao diện trình duyệt.** Đối chiếu với swimlane: xem `docs/SO_SANH_SWIMLANE.md`.

Cột "Màn hình" là tên mục trong menu bên trái của vai trò đó.

## Các bước

| # | Ai | Màn hình | Làm gì | Hệ thống tự làm / chặn | Trạng thái đơn |
|---|---|---|---|---|---|
| 1 | Admin | Đơn hàng | Tạo đơn: khách, ngày nhận / giao, các dòng mã hàng × màu × size × số lượng × đơn giá CMT | Gửi thông báo "Có đơn hàng mới" cho Quản đốc, bấm vào mở thẳng đơn ở màn NVL của đơn | NEW, NVL chờ (Awaiting) |
| 2 | Quản đốc | NVL của đơn | Thấy đơn trong bảng "Đơn hàng chờ khai báo / mua NVL". Khai báo từng NVL: cần bao nhiêu, khách cấp bao nhiêu (kèm số lần khách giao dự kiến). Định mức nằm ở màn Mã hàng (Admin khai một lần), màn này chỉ hiện "cần theo định mức" để đối chiếu | Số phải mua = cần − khách cấp. Nếu khách cấp thì báo Thủ kho | NEW |
| 3 | Quản đốc | NVL của đơn → Tạo (khi chưa chọn đơn) | Lập đơn mua NVL cho đơn (các dòng phải mua điền sẵn) | Chỉ cho mua NVL đã khai báo ở bước 2 | NEW |
| 4 | Quản đốc | Đơn mua NVL | Đặt hàng với nhà cung cấp (chọn NCC, đơn giá, ngày hẹn giao) | Báo Kế toán chờ thanh toán, báo Thủ kho có đơn mua chờ nhận | NEW |
| 5 | Kế toán | Thanh toán đơn mua | Xác nhận thanh toán (kèm ảnh chứng từ) hoặc trả lại Quản đốc | Không chặn việc nhận hàng (xem mục "Lưu ý") | NEW |
| 6 | Thủ kho | NVL chờ nhận | Hàng về (khách cấp hoặc NCC giao) thì bấm Lập phiếu nhập, số còn phải nhận điền sẵn. Chỉ nhập **số đạt** và đính kèm **ảnh chứng từ giao hàng** | Không nhận vượt số còn phải nhận (cả hàng NCC và hàng khách cấp). Một NVL chỉ có **một phiếu chờ xác nhận** tại một thời điểm: gửi lại cùng NVL khi phiếu trước chưa được xác nhận hoặc trả lại sẽ bị chặn; Quản đốc xác nhận cũng kiểm lại trần nên tồn kho không thể tăng gấp. Cột "lần giao" hiện đã nhận / dự kiến | NEW |
| 7 | Quản đốc | Phiếu nhập NVL | Xác nhận phiếu nhập (cần có ảnh chứng từ) hoặc trả lại có lý do | Tăng tồn kho NVL. Tính lại trạng thái NVL của đơn (đủ thì Complete). Khi đã nhận đủ số lần giao dự kiến mà còn thiếu thì **tự sinh** đơn "Thiếu NVL khi nhập" cho Quản đốc chọn giao bù hoặc không bù | NEW, NVL Complete |
| 8 | Admin | Đơn hàng (chi tiết) | Chuyển đơn sang sản xuất | Chỉ cho khi NVL không còn "chờ" (Awaiting) | IN_PRODUCTION |
| 9 | Quản đốc | Lệnh cắt | Lập lệnh cắt cho tổ cắt (dòng đơn và số lượng) | Kiểm tra đủ vải trong kho theo định mức, không cắt vượt số đặt | IN_PRODUCTION |
| 10 | Thủ kho → Tổ trưởng cắt | Phiếu cấp NVL / BTP | Thủ kho cấp vải theo lệnh cắt, tổ trưởng cắt xác nhận nhận | Giữ chỗ vải khi lập phiếu, trừ kho khi tổ nhận | IN_PRODUCTION |
| 11 | Tổ trưởng cắt | Bàn cắt, Bàn giao BTP | Ghi bàn cắt (số lớp, tỉ lệ), rồi bàn giao BTP cho kho | Số cắt không vượt vải đã nhận; số bàn giao không vượt số đã cắt | IN_PRODUCTION |
| 12 | Thủ kho | Bàn giao BTP | Đếm và nhận BTP vào kho (lệch phải ghi lý do) | Tăng tồn kho BTP | IN_PRODUCTION |
| 13 | Quản đốc | Chỉ tiêu KPI tổ may | Lập KPI cho tổ may (nhập tổng cả kỳ). Xem kết quả từng ngày ở bảng kết quả của KPI | Chặn khi tổng KPI còn hiệu lực của đơn + mã hàng vượt BTP (bộ đủ) đã vào kho; báo số còn đặt được | IN_PRODUCTION |
| 14 | Tổ trưởng may | Đơn từ của tôi | Gửi đơn "Xin BTP" cho Thủ kho | Chỉ tổ may được tạo | IN_PRODUCTION |
| 15 | Thủ kho → Tổ trưởng may | Phiếu cấp NVL / BTP | Thủ kho cấp BTP theo đơn xin, tổ trưởng may xác nhận nhận | Trừ kho BTP; đơn xin tự hoàn thành sau khi cấp | IN_PRODUCTION (đơn NEW tự sang IN_PRODUCTION khi tổ may xác nhận nhận BTP) |
| 16 | Tổ trưởng may → Công nhân | Giao việc cho công nhân; Việc được giao cho tôi | Tổ trưởng chia việc theo công đoạn (số lần làm), công nhân xác nhận (hoặc tổ trưởng xác nhận hộ có lý do) | Tổng số lần giao của công đoạn không vượt số bộ BTP tổ đã nhận; sản lượng không vượt số lần được giao | IN_PRODUCTION |
| 17 | Tổ trưởng may | Phân công công đoạn, Sản lượng cuối ca | Phân công công đoạn cho công nhân, nhập sản lượng cuối ca | Sản lượng không vượt BTP công nhân đang giữ (MSG16); đơn giá khóa theo ngày nhập | IN_PRODUCTION |
| 18 | QA/QC | Hàng hoàn thiện (QC) | Nhập số hàng đạt ra chuyền | Là số "thực tế" của KPI | IN_PRODUCTION |
| 19 | QA/QC → Thủ kho | Bàn giao thành phẩm | Lập phiếu bàn giao, Thủ kho đếm và nhận vào kho thành phẩm | Tổng bàn giao không vượt tổng QC đạt. Đây là nguồn doanh thu tháng | IN_PRODUCTION |
| 20 | Quản đốc | Phiếu xuất hàng | Lập phiếu xuất (giữ chỗ thành phẩm) | Không xuất vượt tồn khả dụng | IN_PRODUCTION |
| 21 | Thủ kho | Phiếu xuất hàng | Xác nhận xuất kho (hoặc từ chối có lý do) | Trừ kho thành phẩm, báo Kế toán | IN_DELIVERY |
| 22 | Kế toán | Phiếu xuất hàng | Xác nhận khách đã nhận (ngày nhận, số nhận từng dòng, ảnh biên bản) | Cộng dồn số khách nhận | PARTIALLY_DELIVERED, hoặc DELIVERED khi tổng nhận ≥ tổng đặt |

Hủy / dừng đơn: Admin bấm Hủy (CANCELLED) hoặc Dừng (STOPPED, có lý do) ở chi tiết đơn.

## Kết quả đối chiếu

**Khớp.** Mỗi bước nhận đúng kết quả của bước trước:
- Đơn mới hiện ở danh sách chờ khai báo của Quản đốc, thông báo dẫn thẳng tới đơn.
- Đơn mua đã đặt hàng và NVL khách cấp hiện ở màn NVL chờ nhận của Thủ kho; phiếu nhập xác nhận xong thì số còn phải nhận giảm.
- Nhập đủ NVL thì đơn chuyển từ "chờ NVL" sang đủ, Admin mới chuyển sản xuất được.
- BTP đi đúng chuỗi cắt → kho → tổ may → công nhân → QC → kho thành phẩm → giao hàng. Mỗi bước trừ / cộng đúng kho và chặn vượt số.
- KPI: tổng chỉ tiêu của đơn + mã hàng không được vượt BTP (bộ đủ) đã vào kho (đã thử: kho có 60 BTP thì KPI 80 bị chặn, KPI 60 đặt được).
- Doanh thu tháng có đơn này sau khi bàn giao thành phẩm.

## Điểm chưa khớp, cần bạn quyết định

1. **Đơn không bao giờ sang trạng thái COMPLETED.** Hệ thống đặt DELIVERED khi khách nhận đủ, sau đó không có thao tác hay tự động nào chuyển sang COMPLETED. Trạng thái này chỉ có trong danh mục và dữ liệu demo. Nếu "hoàn thành đơn" là bước riêng (ví dụ Admin chốt sau khi đối soát), cần thêm nút; nếu DELIVERED đã là cuối thì nên bỏ COMPLETED khỏi tài liệu.
2. **IN_PRODUCTION: đã sửa một phần (25/10).** Tổ may xác nhận nhận BTP thì đơn NEW (NVL không còn chờ) tự sang IN_PRODUCTION theo swimlane; nút của Admin vẫn dùng được. Cắt, may, QC, bàn giao, xuất hàng vẫn chỉ chặn đơn đã đóng, không đòi đơn phải IN_PRODUCTION, nên cảnh báo nguy cơ trễ (JOB-04) vẫn bỏ sót đơn chạy khi còn NEW (ví dụ cắt xong nhưng chưa cấp BTP cho tổ may).
3. **Thanh toán đơn mua không chặn nhận hàng.** Kế toán chưa xác nhận thanh toán hoặc đã trả lại thì Thủ kho vẫn nhận được hàng. Phù hợp với tài liệu hiện tại, nhưng cần xác nhận đây là ý muốn. Bước này tôi chưa chạy thử trong luồng kiểm tra.
4. **Số KPI không tự điều chỉnh.** Lịch KPI theo ngày đã gỡ (database mới không có bảng `KpiDailyTarget`, xem DEV-ALL-11) nên không còn "số còn lại để đặt KPI" trên lịch; thay vào đó đặt KPI bị chặn khi tổng KPI vượt BTP trong kho.
5. **Trạng thái NVL của đơn chỉ tính lại khi xác nhận phiếu nhập.** Sửa số khách cấp hoặc số cần ở "NVL của đơn" sau đó thì trạng thái không đổi cho tới phiếu nhập hoặc lần xử lý thiếu NVL kế tiếp.
