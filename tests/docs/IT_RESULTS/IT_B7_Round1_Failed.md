# IT Batch 7 — Round 1: các ca Failed

Kết quả log chạy chốt: 133 ca, 97 Passed, 36 Failed. Các ca race condition (042, 057, 062, 109, 125, 133) Failed ổn định ở các lần chạy có seed hợp lệ; không có ca đồng thời nào chập chờn nên không khóa thêm. Log: `IT_B7_Round1.trx`.

| Ca | Nhóm | Spec | Thực tế |
|---|---|---|---|
| 027 | B | Lệnh mới có OL3 cả bộ 31 (tổng 201 > 200) → 400/409 | 200: tạo lệnh LC-… thành công, tổng kế hoạch cả bộ vượt số đặt (201/200) |
| 033 | B | DueDate 0001-01-01 / thiếu / quá khứ → 400; sau hạn đơn → 200/400; DB không có lệnh DueDate 0001 | Ca không phải lỗi HTTP: assertion `created.Should().OnlyContain(...)` thất bại vì danh sách lệnh mới rỗng (lệnh bị từ chối); cần xác nhận lại nếu có dòng nào của ca trả mã khác spec |
| 034 | A | CuttingTeamId là tổ SEWING / tổ ngừng → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Tổ nhận phải là tổ cắt đang hoạt động." |
| 035 | A | Dòng đơn của đơn khác (OL9) → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE "Mã hàng không có trong đơn." |
| 036 | A | ProductId khác sản phẩm của đơn → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Mã hàng không có trong đơn." |
| 037 | A | PCM của sản phẩm khác / 999999 → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE "Chi tiết cắt lẻ phải là chi tiết vải đang dùng trong định mức của đúng màu – size." |
| 038 | B | Đơn OD1 (NEW) / OD3 (đã hủy) lập lệnh → 400; đơn không tồn tại → 404 | 200: tạo lệnh cắt thành công cho đơn DH-0001 (trạng thái NEW) — app không chặn đơn chưa sản xuất |
| 039 | B | Note 1000 ký tự → 200; 1001 → 400 | 400 COMMON_VALIDATION "Ghi chú tối đa 600 ký tự." — giới hạn Note của DTO (600) nhỏ hơn cột DB nvarchar(1000) |
| 042 | C | 2 POST đồng thời OL3 cả bộ 30 (còn 30) → đúng 1 thành công | Cả 2 request trả 200: tổng kế hoạch cả bộ vượt trần số đặt (race, không khóa) |
| 043 | E | Dòng thứ 2 bị trigger chặn → 400/409, body không lộ lỗi DB | 409/400 COMMON_DB_RULE nhưng body chứa nguyên văn thông báo lỗi trigger ("it-b7 injected fault"); rollback đúng |
| 051 | B | close-short lệnh WO2 CUTTING → 200, Status COMPLETED | 409 WAREHOUSE_INVALID_STATUS "Còn phiếu bàn giao BTP chờ kho xác nhận, chưa đóng lệnh được." (S0 có phiếu CH1 PENDING) |
| 052 | A | close-short Reason rỗng / null / body null / không body → 400 | 415 Unsupported Media Type khi không gửi body (các trường hợp còn lại đúng 400) |
| 057 | C | Hủy lệnh đồng thời với tạo bàn cắt đầu tiên → đúng 1 thành công | Cả hai trả 200: lệnh bị hủy nhưng vẫn có bàn cắt RECORDED mới (race, thiếu khóa trạng thái) |
| 062 | C | 2 request replacement cùng RQ1 → 1×200, 1×409 | Cả 2 trả 200 (tạo 2 lệnh cắt bù cho cùng một đơn) |
| 063 | A | RequestId là đơn chưa duyệt / sai loại → 400/409 | 404 COMMON_NOT_FOUND "Không tìm thấy dữ liệu." (đơn chưa duyệt / sai loại bị coi như không tồn tại) |
| 066 | B | fabric-needs?color=Đen&sizeCode=M trả nhu cầu vải MA1 (> 0) | 200 nhưng không có dòng cho vải MA1 (mã vật tư không xuất hiện trong danh sách nhu cầu) |
| 070 | A | PageSize 0 / -1 / Page 0 → 200 (tự kẹp về mặc định) | 400 COMMON_VALIDATION "Số dòng mỗi trang phải từ 1 đến 100." — validator từ chối thay vì kẹp giá trị |
| 078 | B | Bàn cắt làm tổng cắt 51 > kế hoạch 50 → 400/409 | 200: tạo bàn cắt thành công, tổng cắt vượt kế hoạch lệnh |
| 081 | A | Dòng không thuộc lệnh (OL4 / OL9) → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE "Dòng đơn không thuộc lệnh cắt." |
| 083 | A | Fabrics rỗng / vải không thuộc định mức / 999999 → 400 (/404) | 409 WAREHOUSE_INVALID_REFERENCE "Bàn cắt phải nhập đủ đúng các vải của các dòng cắt. Thiếu: V-0001. Thừa: …" |
| 084 | A | WorkDate trước ngày lập lệnh / tương lai / 0001 / thiếu → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Ngày cắt không được trước ngày lập lệnh cắt (24/11/2026)." |
| 088 | E | INSERT CuttingLayFabric lỗi → lỗi có kiểm soát, body không lộ lỗi DB | Rollback đúng nhưng body lộ nguyên văn thông báo lỗi DB (COMMON_DB_RULE: "it-b7 injected fault") |
| 091 | B | Hạ ActualQty xuống 35 < đã bàn giao 40 → 400/409 | 200: bàn cắt được sửa xuống dưới số đã bàn giao |
| 103 | A | Dòng bàn giao không có bàn cắt / dòng đơn khác / PCM 999999 → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE "Dòng đơn không thuộc lệnh cắt." |
| 106 | E | Dòng thứ 2 của phiếu bàn giao bị chặn → lỗi có kiểm soát, body không lộ lỗi DB | Rollback đúng nhưng body lộ nguyên văn thông báo lỗi DB (COMMON_DB_RULE: "it-b7 injected fault") |
| 109 | C | Hủy (tổ trưởng) và xác nhận (thủ kho) đồng thời → 1×200, 1×409 | Cả 2 trả 200 ("Đã rút phiếu bàn giao BTP." và "Đã nhận BTP vào kho.") |
| 110 | E | confirm → 200, ConfirmedByName = tên thủ kho | 200 nhưng `confirmedByName` = null trong response của confirm (DB đã ghi ConfirmedBy) |
| 112 | A | Số xác nhận khác số giao mà thiếu lý do → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Số xác nhận khác số giao, vui lòng nhập lý do." |
| 113 | A | ConfirmedQty 31 / -1 / tràn → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Số xác nhận không được lớn hơn số tổ giao." |
| 115 | A | confirm thiếu dòng / Lines rỗng → 400 | 409 WAREHOUSE_INVALID_REFERENCE "Phiếu phải có ít nhất một dòng." |
| 116 | A | Dòng thuộc phiếu khác → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE "Phiếu phải có ít nhất một dòng." |
| 121 | E | Lỗi khi post tồn kho → lỗi có kiểm soát, body không lộ lỗi DB | Rollback đúng nhưng body lộ nguyên văn thông báo lỗi DB (COMMON_DB_RULE: "it-b7 injected fault") |
| 123 | A | reject Reason rỗng / null / không body → 400 | 415 Unsupported Media Type khi không gửi body |
| 125 | C | confirm và reject đồng thời → 1×200, 1×409 | Cả 2 trả 200 ("Đã nhận BTP vào kho." và "Đã từ chối phiếu bàn giao.") |
| 127 | E | View vw_WorkOrderLineProgress khớp API: CutQty=40, HandedQty=40 | View trả (CutQty=40, HandedToWarehouseQty=10): cột view chỉ tính phiếu đã nhận kho, không tính phiếu chờ nhận như API/spec |
| 133 | C | Lặp 10 vòng các race 042/073/119 → không vòng nào vượt trần | Race 042 vẫn trả 2×200 ở vòng lặp (cùng nguyên nhân ca 042) |
