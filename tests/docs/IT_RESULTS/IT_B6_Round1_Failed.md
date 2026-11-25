# IT Batch 6 — Round 1: các ca Failed

Kết quả log chạy chốt: 125 ca, 84 Passed, 41 Failed. Ca 047 (race condition) chập chờn giữa các lần chạy: Failed ở 2 lần chạy trước, Passed ở lần chốt, nên khóa **Failed** trong báo cáo. Kết quả báo cáo: **125 ca, 83 Passed, 42 Failed** (41 Failed của log chốt + 047). Log: `IT_B6_Round1.trx`.

| Ca | Nhóm | Spec | Thực tế |
|---|---|---|---|
| 010 | E | TK_ACC / TK_TL / TK_WRK gọi GET Attachments của MR1 → 200 | 404 COMMON_NOT_FOUND ("Không tìm thấy dữ liệu hoặc bạn không có quyền xem"): app giới hạn xem ảnh theo quyền, không mở cho mọi role đã đăng nhập |
| 013 | B | Thủ kho lập phiếu CUSTOMER cho OD2 khi MR1 đang PENDING → 200 | 409 RECEIPT_DUPLICATE_PENDING: NVL đã có phiếu chờ xác nhận. Quy tắc này không có trong Spec |
| 014 | B | Thủ kho lập phiếu SUPPLIER cho PO2 khi MR2 đang PENDING → 200 | 409 RECEIPT_DUPLICATE_PENDING (chặn trùng phiếu chờ) |
| 015 | A | Source=CUSTOMER kèm PurchaseOrderId → 400 | 409 RECEIPT_DUPLICATE_PENDING: kiểm trùng phiếu chờ chạy trước kiểm Source/PO |
| 018 | A | Dòng thuộc NVL của đơn khác → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 019 | A | Dòng thuộc đơn mua khác → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 020 | A | Dòng sai khóa (PO line cho phiếu CUSTOMER, cả hai, không có) → 400 | 409 RECEIPT_DUPLICATE_PENDING: kiểm trùng chạy trước kiểm dạng dòng nên không xác nhận được 400 |
| 023 | B | AcceptedQty 0 / âm / 1e15 → 400 | 409 RECEIPT_DUPLICATE_PENDING: các biên số lượng không được kiểm tới |
| 026 | B | Note 1000 ký tự → 200 | 409 RECEIPT_DUPLICATE_PENDING |
| 028 | A | Đơn / đơn mua không tồn tại hoặc đã hủy → 400/404 | 409 WAREHOUSE_INVALID_STATUS (đơn hoặc PO đã hủy) |
| 029 | A | Khóa dòng không tồn tại → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 030 | B | Mass-assignment: field thừa bị bỏ qua → 200 | 409 RECEIPT_DUPLICATE_PENDING nên không kiểm được việc bỏ qua field thừa |
| 037 | B | Sửa phiếu đã trả lại (nộp lại) → 200 | 409 RECEIPT_DUPLICATE_PENDING |
| 038 | B | Đổi nguồn phiếu → 200 hoặc 400 | 409 RECEIPT_DUPLICATE_PENDING |
| 043 | C | PUT và confirm đồng thời: không 500, thẻ kho khớp dòng phiếu | Có response 5xx ở ít nhất một vòng khi PUT và confirm chạy đồng thời |
| 045 | E | Confirm MR2 nhận thiếu 50 → Data.ShortDeliveryRequestId khác null | Response confirm không có shortDeliveryRequestId (null) |
| 047 | C | 2 confirm đồng thời: đúng 1 request 200, request còn lại 409 | Chập chờn: Failed ở 2/3 lần chạy trước (cả hai request 409 RECEIPT_DELIVERY_COUNT_REACHED), Passed ở lần chốt. Khóa Failed theo quy ước race |
| 048 | C | 2 confirm đồng thời phiếu NCC thiếu: 1×200, 1×409, đúng 1 SHORT_DELIVERY | Không request nào thắng: cả hai 409 RECEIPT_DELIVERY_COUNT_REACHED (đã nhận đủ số lần giao dự kiến của PO2) |
| 049 | D | Lỗi ghi thẻ kho: rollback toàn bộ, body không lộ thông tin DB | 4xx COMMON_DB_RULE trả nguyên message của trigger ("it-b5 injected fault") trong body |
| 050 | D | Lỗi ghi StockBalance: rollback toàn bộ, body không lộ thông tin DB | 4xx COMMON_DB_RULE trả nguyên message của trigger trong body |
| 051 | B | Phiếu SUPPLIER thứ hai cùng PO2 sau khi MR2 xác nhận → 200 | 409 RECEIPT_DELIVERY_COUNT_REACHED (đã nhận đủ 1 lần giao dự kiến) |
| 057 | B | Reason 501 ký tự → 400 | 200: chấp nhận lý do dài hơn giới hạn 500 |
| 065 | A | Dòng thuộc yêu cầu khác (RL9) → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 066 | A | AcceptedQty 41 vượt số giao bù 40 → 200 hoặc 400 | 409 SHORTAGE_EXCEEDED |
| 073 | E | Resolve REPLENISH lưu Note vào ResultNote | ResultNote NULL sau khi resolve (Note không được lưu) |
| 076 | C | ExpectedDeliveryCount hợp lệ (1 / 99) → 200 | 500 COMMON_INTERNAL_ERROR ở giá trị 1 hoặc 99 |
| 077 | B | Ngày giao bù thiếu / trùng / 0001-01-01 / quá khứ → 400 | 200: ít nhất một trường hợp ngày sai vẫn được ghi nhận |
| 078 | A | ReplenishQty 101 / 0 / âm / tràn → 400 | 409 SHORTAGE_EXCEEDED |
| 079 | A | Dòng thuộc yêu cầu khác khi resolve → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 080 | A | REPLENISH thiếu dòng số lượng (null / rỗng / sai dòng) → 400/404 | 409 WAREHOUSE_INVALID_REFERENCE |
| 081 | B | ResultNote 1000 ký tự → 200 | 400 COMMON_VALIDATION: giới hạn Note nhỏ hơn 1000 |
| 086 | D | Lỗi chèn lịch giao: rollback, body không lộ thông tin DB | 4xx COMMON_DB_RULE trả nguyên message của trigger trong body |
| 087 | E | INSERT SHORT_DELIVERY thứ hai cho MR3 bị UX_Request_Receipt chặn | Không có ngoại lệ: DB cho phép chèn bản ghi trùng (index lọc không có hiệu lực) |
| 088 | E | IncomingMaterials có đủ Customer / Supplier / ClosedShort khớp dữ liệu nền | Thiếu ít nhất một phần tử mong đợi trong JSON (Sequence contains no matching element) |
| 107 | A | ownerId 0 / -1 / abc → 400 | 404 COMMON_NOT_FOUND |
| 109 | B | Upload ảnh mới gỡ ảnh cũ (IsCurrent=0, RemovedAt) | Ảnh cũ vẫn IsCurrent=1 |
| 115 | A | Công nhân đính ảnh vào phiếu đã xác nhận → 400/403 | 404 COMMON_NOT_FOUND |
| 119 | C | 3 upload đồng thời: đúng 1 ảnh IsCurrent | 3 ảnh cùng IsCurrent=1 |
| 120 | D | Lỗi chèn Attachment: ảnh cũ còn hiện hành, body không lộ thông tin DB | 4xx COMMON_DB_RULE trả nguyên message của trigger trong body |
| 123 | B | Tạo phiếu CUSTOMER mới → 200 | 409 RECEIPT_DUPLICATE_PENDING |
| 124 | A | ReplenishQty 101 → 400 | 409 SHORTAGE_EXCEEDED |
| 125 | C | Lặp 10 vòng: sau 3 upload song song đúng 1 ảnh IsCurrent | 3 ảnh cùng IsCurrent=1 |
