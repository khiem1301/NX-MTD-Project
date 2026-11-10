# Testcase validate – Lập lệnh cắt (UC19)

Test tay theo đơn mẫu **TEST-CAT-001** (patch `SQL/patches/2026-11-03_test_order_for_cutting.sql`).
Nguồn quy tắc: `WorkOrderDtoValidators.cs` (kiểm định dạng → HTTP 400), `WorkOrderService.cs` (kiểm nghiệp vụ → HTTP 409), `WorkOrderFormModal.tsx` (kiểm trên form).

## 0. Chuẩn bị

```
sqlcmd -S localhost -E -C -I -f 65001 -d NXMTD -i SQL/patches/2026-11-03_test_order_for_cutting.sql
```

Chạy backend + frontend (`run-dev.bat`), đăng nhập **QUANDOC** / `123456`, vào **Lệnh cắt** (`/cutting/work-orders`) → **Lập lệnh cắt**.

**Dữ liệu đơn TEST-CAT-001** (ngày nhận 01/11/2026, hạn đơn 20/12/2026, mã hàng DEMO-AO01):

| Dòng đơn | Số đặt |
|---|---|
| Đen – S | 50 |
| Đen – M | 100 |
| Đen – L | 80 |
| Xanh – M | 60 |
| Xanh – L | 40 |

Tổ cắt hợp lệ: **Tổ cắt (CAT)**. Các mã `DEMO-DH0xx` khác dùng cho mục đơn đã đóng.

**Bắt buộc đọc – dọn lệnh sau mỗi case lưu thành công.** Số đặt của mỗi dòng đơn là trần cộng dồn (mục 4). Ví dụ case 1.1 lưu Đen-M = 50 thì case 3.3 (Đen-M = 100) và 3.8 (cắt đủ toàn đơn) sẽ bị 409 vì 50 + 100 > 100. Sau mỗi case có kết quả "Lưu được", **hủy lệnh vừa tạo ngay** (nút Hủy lệnh, xem 9.6). Lệnh đã hủy không tính vào trần. Riêng mục 4 thì ngược lại: phải giữ lệnh A, B để thấy cộng dồn.

Quy ước cột **Tầng**: `FE` = form chặn ngay, không gọi API; `BE-400` = validator từ chối; `BE-409` = service từ chối (lỗi nghiệp vụ). Muốn test BE-400 / BE-409 phải gọi API trực tiếp (Postman, Swagger) vì form đã chặn trước:
`POST /api/v1/manager/WorkOrders` với body mẫu ở mục 9.

> **Quan trọng:** các case dạng "dán số vượt max vào ô" thực tế bị InputNumber ép về max. Case nào ghi rõ "API" thì gọi thẳng API.

---

## 1. Thông tin chung (đầu form)

| # | Bước | Dữ liệu | Kết quả mong đợi | Tầng |
|---|---|---|---|---|
| 1.1 | Lập lệnh hợp lệ (đường chuẩn) | Đơn TEST-CAT-001, mã DEMO-AO01, Tổ cắt, hạn 20/11/2026, Đen-M = 50 | Lưu được, sinh mã lệnh mới, trạng thái OPEN; tổ trưởng cắt và Thủ kho nhận thông báo | – |
| 1.2 | Bỏ trống đơn hàng, bấm Lưu | Không chọn đơn | "Vui lòng nhập thông tin này." dưới ô Đơn hàng; không gọi API | FE |
| 1.3 | Chưa chọn đơn thì ô Mã hàng | – | Ô Mã hàng bị khóa; bảng hiện gợi ý "Chọn đơn hàng và mã hàng để nhập số kế hoạch cắt." | FE |
| 1.4 | Bỏ trống mã hàng | Chọn đơn, không chọn mã | "Vui lòng nhập thông tin này." dưới ô Mã hàng | FE |
| 1.5 | Bỏ trống tổ cắt | – | "Vui lòng nhập thông tin này." dưới ô Tổ cắt | FE |
| 1.6 | Bỏ trống hạn cắt | – | "Vui lòng nhập thông tin này." dưới ô Hạn | FE |
| 1.7 | Đổi đơn hàng sau khi đã nhập số | Nhập Đen-M = 50, đổi sang đơn khác | Mã hàng và toàn bộ số kế hoạch, dòng cắt lẻ bị xóa | FE |
| 1.8 | Đổi mã hàng sau khi đã nhập số | Như trên, đổi mã hàng | Số kế hoạch và dòng cắt lẻ bị xóa | FE |
| 1.9 | API: `orderId` = 0 | `orderId: 0` | 400 "Vui lòng chọn đơn hàng." | BE-400 |
| 1.10 | API: `productId` = 0 | – | 400 "Vui lòng chọn mã hàng." | BE-400 |
| 1.11 | API: `cuttingTeamId` = 0 | – | 400 "Vui lòng chọn tổ cắt." | BE-400 |
| 1.12 | API: `orderId` không tồn tại | `orderId: 999999` | 404 không tìm thấy | BE-409/404 |

## 2. Hạn cắt (ngày)

Hôm nay = ngày máy chủ. Ví dụ dưới giả định hôm nay là **04/11/2026**.

| # | Dữ liệu hạn | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 2.1 | Hôm nay (04/11/2026) | Lưu được (hạn không nhỏ hơn hôm nay và lớn hơn ngày nhận 01/11) | – |
| 2.2 | Hôm qua (03/11/2026) | Ô ngày bị vô hiệu; nếu gõ tay: "Không đặt hạn trong quá khứ." | FE |
| 2.3 | API: hạn 03/11/2026 | 409 "Không đặt lệnh cắt có hạn trong quá khứ." | BE-409 |
| 2.4 | Đúng bằng ngày nhận đơn (01/11) | Báo hạn quá khứ trước (vì 01/11 < hôm nay) | FE / BE-409 |
| 2.5 | Hạn ≤ ngày nhận đơn, trong khi ngày nhận ≥ hôm nay | "Hạn phải sau ngày bắt đầu của đơn (dd/MM/yyyy)." (FE); BE: "Hạn lệnh cắt phải sau ngày bắt đầu của đơn hàng (dd/MM/yyyy)." | FE + BE-409 |
| 2.6 | API: không gửi `dueDate` hoặc `0001-01-01` | 400 "Vui lòng nhập hạn cắt." | BE-400 |
| 2.7 | Sửa lệnh cũ có hạn đã qua, giữ nguyên hạn | Vẫn lưu được (BE không kiểm lại hạn không đổi) | – |
| 2.8 | Sửa lệnh cũ có hạn đã qua, đổi sang hạn quá khứ khác | Bị chặn như 2.2 / 2.3 | FE + BE-409 |

**Lưu ý case 2.5:** đơn TEST-CAT-001 nhận 01/11 đã qua, nên điều kiện "hạn ≤ ngày nhận" luôn bị báo "quá khứ" trước. Muốn cô lập, đẩy ngày nhận tạm sang tương lai rồi trả lại:

```sql
-- đẩy ngày nhận lên 10/11/2026 để test; hạn 05/11 sẽ báo "phải sau ngày bắt đầu (10/11/2026)"
UPDATE dbo.CustomerOrder SET ReceivedDate = '2026-11-10' WHERE OrderCode = 'TEST-CAT-001';
-- test xong trả lại
UPDATE dbo.CustomerOrder SET ReceivedDate = '2026-11-01' WHERE OrderCode = 'TEST-CAT-001';
```

## 3. Dòng cắt cả bộ (bảng kế hoạch)

Chọn đơn TEST-CAT-001, mã DEMO-AO01. Cột "Số kế hoạch" là InputNumber `min 0`, `max = số đặt`, không số lẻ.

| # | Dữ liệu | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 3.1 | Không nhập dòng nào, bấm Lưu | Cảnh báo "Nhập số kế hoạch hoặc số chi tiết cắt lẻ cho ít nhất một dòng."; không gọi API | FE |
| 3.2 | Mọi ô = 0 hoặc để trống | Như 3.1 (dòng 0 bị loại) | FE |
| 3.3 | Đen-M = 100 (đúng bằng số đặt) | Lưu được | – |
| 3.4 | Đen-M = 101 (nhập tay) | Ô tự ép về 100 | FE |
| 3.5 | Đen-S = 10.5 | Không nhận số lẻ, làm tròn / bỏ phần lẻ | FE |
| 3.6 | Nhập số âm (-5) | Không nhận, ô về 0 | FE |
| 3.7 | Nhập chữ "abc" | Ô không nhận ký tự | FE |
| 3.8 | Nhiều dòng cùng lúc: Đen S/M/L = 50/100/80, Xanh M/L = 60/40 (cắt đủ toàn đơn) | Lưu được; tổng kế hoạch 330 | – |
| 3.9 | API: `plannedQty: 0` | 400 "Số lượng phải lớn hơn 0." | BE-400 |
| 3.10 | API: `plannedQty: -1` | 400 "Số lượng phải lớn hơn 0." | BE-400 |
| 3.11 | API: `plannedQty: 1000001` | 400 "Số lượng cắt tối đa 1.000.000." | BE-400 |
| 3.12 | API: `plannedQty: 1000000` cho Đen-S (đặt 50) | Qua validator, bị service chặn: 409 `CUTTING_PLAN_EXCEEDS_ORDER` "Tổng số cắt theo kế hoạch vượt số lượng đặt: Đen-S: đặt 50, đã lên lệnh 0, lệnh này 1000000." | BE-409 |
| 3.13 | API: `plannedQty: 2147483648` (tràn int) | 400 lỗi định dạng JSON / model binding | BE-400 |
| 3.14 | API: `plannedQty: 5.5` | 400 lỗi định dạng (kiểu int) | BE-400 |
| 3.15 | API: `orderLineId: 0` | 400 "Dòng đơn không hợp lệ." | BE-400 |
| 3.16 | API: `lines: []` hoặc `null` | 400 "Phiếu phải có ít nhất một dòng." | BE-400 |
| 3.17 | API: 1001 dòng | 400 "Tối đa 1000 dòng." | BE-400 |
| 3.19 | API: hai dòng cùng `orderLineId`, cùng chi tiết (cùng `null`) | 400 "Một dòng bị khai báo nhiều lần." | BE-400 |
| 3.20 | API: `orderLineId` thuộc đơn khác | 409 "Mã hàng không có trong đơn." | BE-409 |
| 3.21 | API: `orderLineId` thuộc đơn này nhưng `productId` là mã hàng khác | 409 "Mã hàng không có trong đơn." | BE-409 |
| 3.22 | API: `orderLineId` không tồn tại | 409 "Mã hàng không có trong đơn." | BE-409 |

## 4. Cộng dồn số kế hoạch qua nhiều lệnh (trần theo số đặt)

Trần: Σ kế hoạch các lệnh cắt **thường** (không tính lệnh đã hủy, lệnh cắt bù, dòng cắt lẻ) ≤ số đặt của dòng đơn. Dùng dòng **Đen-M (đặt 100)**.

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 4.1 | Lệnh A: Đen-M = 60 | Lưu được |
| 4.2 | Lệnh B: Đen-M = 41 | 409 `CUTTING_PLAN_EXCEEDS_ORDER`: "Tổng số cắt theo kế hoạch vượt số lượng đặt: Đen-M: đặt 100, đã lên lệnh 60, lệnh này 41." (form vẫn cho nhập đến 100 vì max chỉ theo số đặt) |
| 4.3 | Lệnh B: Đen-M = 40 | Lưu được (60 + 40 = 100, đúng biên) |
| 4.4 | Lệnh C: Đen-M = 1 (khi A + B đã đủ 100) | 409 như 4.2, "đã lên lệnh 100, lệnh này 1" |
| 4.5 | Hủy lệnh B, sau đó lập lệnh C Đen-M = 40 | Lưu được (lệnh hủy không tính) |
| 4.6 | Sửa lệnh A từ 60 lên 61 khi B = 40 | 409 vượt (61 + 40 > 100) |
| 4.7 | Sửa lệnh A từ 60 xuống 50 | Lưu được; không tính lại chính nó (loại trừ lệnh đang sửa) |
| 4.8 | Một lệnh có nhiều dòng vượt trần cùng lúc (Đen-S và Đen-L cùng vượt) | Một thông báo, liệt kê cả hai dòng, cách nhau bằng `;` |
| 4.9 | Cắt lẻ: Đen-M cắt lẻ 500 chi tiết trong khi đã lên đủ 100 cả bộ | Lưu được (cắt lẻ không tính vào trần số đặt), miễn là qua mục 5 |

## 5. Dòng cắt lẻ thêm thành phần

Bấm **Cắt lẻ thêm thành phần**, mỗi dòng gồm: màu – size, thành phần, số chi tiết.

| # | Dữ liệu | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 5.1 | Thêm dòng cắt lẻ, bỏ trống màu – size | "Vui lòng nhập thông tin này." | FE |
| 5.2 | Chọn màu – size, bỏ trống thành phần | "Vui lòng nhập thông tin này." | FE |
| 5.3 | Chưa chọn màu – size thì ô thành phần | Bị khóa | FE |
| 5.4 | Đổi màu – size sau khi đã chọn thành phần | Thành phần bị xóa, chọn lại | FE |
| 5.5 | Dòng cắt lẻ để trống số chi tiết | "Vui lòng nhập thông tin này."; không lưu được (dòng cắt lẻ đã thêm thì phải điền đủ hoặc xóa dòng) | FE |
| 5.5b | Nhập số chi tiết = 0 hoặc số âm | Ô tự ép về 1 (`min 1`) | FE |
| 5.6 | Chỉ có dòng cắt lẻ hợp lệ (không có dòng cả bộ) | Lưu được | – |
| 5.7 | Số chi tiết cắt lẻ = 1.000.000 | Lưu được | FE |
| 5.8 | Số chi tiết cắt lẻ > 1.000.000 | Bị chặn trên form (`MAX_PIECES`) | FE |
| 5.9 | Hai dòng cắt lẻ cùng màu – size, cùng thành phần | Cảnh báo "Mỗi chi tiết cắt lẻ chỉ khai một lần cho một màu - size."; không gọi API | FE |
| 5.10 | API: hai dòng cắt lẻ trùng (`orderLineId`, `productComponentMaterialId`) | 400 "Một dòng bị khai báo nhiều lần." | BE-400 |
| 5.11 | API: `productComponentMaterialId: 0` | 400 "Vui lòng chọn chi tiết." | BE-400 |
| 5.12 | API: `productComponentMaterialId` là chi tiết của màu – size **khác** (ví dụ chi tiết của Xanh-M gắn vào dòng Đen-M) | 409 "Chi tiết cắt lẻ phải là chi tiết vải đang dùng trong định mức của đúng màu – size." | BE-409 |
| 5.13 | API: `productComponentMaterialId` là phụ liệu (không phải PANEL) | 409 như 5.12 | BE-409 |
| 5.14 | API: `productComponentMaterialId` của chi tiết đã ngừng dùng | 409 như 5.12 | BE-409 |
| 5.15 | API: `productComponentMaterialId` không tồn tại | 409 như 5.12 | BE-409 |
| 5.16 | Một dòng cả bộ + một dòng cắt lẻ cùng `orderLineId` | Lưu được (khóa trùng gồm cả `productComponentMaterialId`, nên khác nhau là hợp lệ) | – |

## 6. Ghi chú và tổ cắt

| # | Dữ liệu | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 6.1 | Ghi chú 600 ký tự | Lưu được | – |
| 6.2 | Gõ ghi chú 601 ký tự | Ô chặn ở 600, hiện bộ đếm | FE |
| 6.3 | API: ghi chú 601 ký tự | 400 "Ghi chú tối đa 600 ký tự." | BE-400 |
| 6.4 | API: ghi chú 600 ký tự + 5 khoảng trắng đầu cuối | Qua (độ dài tính sau khi cắt khoảng trắng) | BE |
| 6.5 | API: ghi chú toàn khoảng trắng | Lưu được, ghi chú lưu null | BE |
| 6.6 | API: `cuttingTeamId` là tổ may | 409 "Tổ nhận phải là tổ cắt đang hoạt động." | BE-409 |
| 6.7 | API: `cuttingTeamId` là tổ cắt đã ngừng hoạt động | 409 như 6.6 | BE-409 |
| 6.8 | API: `cuttingTeamId` không tồn tại | 409 như 6.6 | BE-409 |
| 6.9 | Mở danh sách Tổ cắt trên form | Chỉ hiện tổ loại CUTTING | FE |

## 7. Đơn hàng đã đóng

Đơn đã đóng gồm 4 trạng thái: Đã hủy, Đã dừng, Đã giao, Hoàn tất (`OrderStatuses.Closed`). Đơn demo: `DEMO-DH007` (Đã hủy, mã hàng DEMO-AO01), `DEMO-DH008` (Đã dừng), `DEMO-DH011` (Đã giao, mã DEMO-QU02), `DEMO-DH012` (Hoàn tất).
Lấy `OrderLineId` của đơn đó bằng câu SQL ở cuối file, rồi gọi API.

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 7.1 | API: lập lệnh cắt cho `DEMO-DH007` (đã hủy) | 409 `WAREHOUSE_ORDER_CLOSED` "Đơn hàng đã đóng, không thể lập thêm phiếu." |
| 7.2 | API: như trên cho `DEMO-DH008` (đã dừng), `DEMO-DH011` (đã giao), `DEMO-DH012` (hoàn tất) | 409 như 7.1 cho cả ba |
| 7.3 | Mở danh sách đơn trên form | Ghi lại đơn đã đóng có xuất hiện trong danh sách chọn hay không; nếu có, chọn rồi Lưu phải ra lỗi 7.1 |
| 7.4 | Đơn chưa đóng (TEST-CAT-001, trạng thái Mới) | Không bị chặn bởi trạng thái đơn |

## 8. Kiểm vải (chỉ cảnh báo, không chặn lưu)

Khi đã chọn đơn + tổ cắt và có ít nhất một dòng, form gọi `POST …/WorkOrders/fabric-check` và hiện cảnh báo dưới bảng. Kết quả phụ thuộc tồn kho thực tế; đối chiếu tồn ở màn Kho trước khi kết luận.

| # | Dữ liệu | Kết quả mong đợi |
|---|---|---|
| 8.1 | Đen S/M/L cắt đủ, kho còn vải Đen | Không cảnh báo, hoặc trạng thái Enough |
| 8.2 | Tăng số cắt đến khi số mét ước tính vượt kho + vải tổ đang giữ | Cảnh báo "{vải}: thiếu khoảng X m (cần khoảng Y m, có Z m: kho K, tổ đang giữ G)."; **vẫn lưu được** |
| 8.3 | Xanh-M / Xanh-L | Vải thân Xanh và phối tay vải Trắng được tính riêng theo bìa 1 và bìa 2 (DEMO-VAI02, DEMO-VAI03) |
| 8.4 | Mã hàng / màu chưa có bìa (ví dụ DEMO-QU02 màu khác Be) | "Chưa tính được vải cho … vì chưa có định mức vải." |
| 8.5 | Bìa rộng hơn khổ vải | "{vải}: rộng hơn khổ vải … m: …" |
| 8.6 | Vải chưa khai khổ | "{vải}: chưa tính được vì NVL chưa có khổ vải." |
| 8.7 | Vải không tính theo mét | "{vải}: chưa tính được vì NVL không tính theo mét." |
| 8.8 | Xóa hết số kế hoạch | Cảnh báo biến mất, không gọi fabric-check |
| 8.9 | API: `fabric-check` với `orderId` = 0 | 400 "Vui lòng chọn đơn hàng." |
| 8.10 | API: `fabric-check` với `lines` rỗng | 400 "Phiếu phải có ít nhất một dòng." |
| 8.11 | API: `fabric-check` có `orderLineId` không thuộc đơn | Dòng đó bị bỏ qua, không lỗi |
| 8.12 | Lệnh cắt thường có thông báo thiếu vải ở 8.2, bấm Lưu | Vẫn tạo lệnh. Kiểm vải chỉ chặn khi **lệnh cắt bù** (mục 10) |

## 9. Sửa và hủy lệnh cắt

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 9.1 | Sửa lệnh OPEN chưa có bàn cắt / phiếu cấp | Lưu được; tổ trưởng cắt nhận thông báo "Lệnh cắt được sửa" |
| 9.2 | Sửa lệnh đã có phiếu cấp vải (không bị từ chối) | 409 "Lệnh cắt đã có bàn cắt hoặc phiếu cấp, không thể sửa / hủy." |
| 9.3 | Sửa lệnh đã có bàn cắt | 409 như 9.2 |
| 9.4 | Sửa lệnh có phiếu cấp **bị từ chối** | Vẫn sửa được (phiếu từ chối không tính) |
| 9.5 | Sửa lệnh đã Cutting / Completed / Cancelled | 409 trạng thái không hợp lệ |
| 9.6 | Hủy lệnh OPEN | Thành công, trạng thái Cancelled, tổ cắt nhận thông báo |
| 9.7 | Hủy lệnh đã có phiếu cấp / bàn cắt | 409 như 9.2 |
| 9.8 | Hủy lệnh đã hủy | 409 trạng thái không hợp lệ |
| 9.9 | Sửa: đổi đơn / mã hàng sang cái khác | Áp dụng đủ các kiểm tra mục 1 – 6 như lập mới |
| 9.10 | API: `PUT` / `cancel` với id không tồn tại | 404 |

## 10. Lệnh cắt bù (`POST …/WorkOrders/replacement`)

Dữ liệu: `YC-DEMO-015` (đơn cắt bù đã duyệt, chưa có lệnh), `YC-DEMO-014` (chờ duyệt).

| # | Dữ liệu | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 10.1 | Hợp lệ: `YC-DEMO-015`, tổ cắt, hạn từ hôm nay trở đi | Tạo lệnh cắt bù; ghi chú mặc định "Cắt bù theo {mã đơn}" nếu để trống | – |
| 10.2 | `requestId: 0` | 400 "Vui lòng chọn đơn cắt bù." | BE-400 |
| 10.3 | `cuttingTeamId: 0` | 400 "Vui lòng chọn tổ cắt." | BE-400 |
| 10.4 | Thiếu hạn | 400 "Vui lòng nhập hạn cắt." | BE-400 |
| 10.5 | Ghi chú 601 ký tự | 400 "Ghi chú tối đa 600 ký tự." | BE-400 |
| 10.6 | Đơn `YC-DEMO-014` chưa duyệt | 409 "Chỉ lập lệnh cắt bù cho đơn cắt bù đã được duyệt." | BE-409 |
| 10.7 | Lập lần hai cho cùng đơn đã có lệnh | 409 "Đơn cắt bù này đã có lệnh cắt bù." | BE-409 |
| 10.8 | Lập sau khi hủy lệnh cắt bù cũ | Lập lại được (lệnh hủy không tính) | – |
| 10.9 | Hạn trong quá khứ | 409 "Không đặt lệnh cắt có hạn trong quá khứ." | BE-409 |
| 10.10 | Tổ không phải tổ cắt / đã ngừng | 409 "Tổ nhận phải là tổ cắt đang hoạt động." | BE-409 |
| 10.11 | Đơn hàng của đơn cắt bù đã đóng | 409 "Đơn hàng đã đóng, không thể lập thêm phiếu." | BE-409 |
| 10.12 | Đơn cắt bù không có dòng hoặc dòng thiếu chi tiết | 409 "Đơn cắt bù chưa có dòng chi tiết cần cắt bù hợp lệ." | BE-409 |
| 10.13 | Dòng cắt bù thuộc nhiều mã hàng | 409 "Mã hàng không có trong đơn." | BE-409 |
| 10.14 | Kho + vải tổ đang giữ không đủ cho phần cắt bù | 409 `CUTTING_FABRIC_SHORTAGE` "Không đủ vải để cắt bù: …"; **chặn lưu** (khác lệnh thường) | BE-409 |
| 10.15 | Chi tiết chưa có kích thước | Cho qua (không ước tính được) | – |
| 10.16 | Lệnh cắt bù **không** tính vào trần số đặt | Lập lệnh cắt bù cho dòng đã cắt đủ 100%, không báo vượt | – |

## 11. Đóng lệnh cắt thiếu (`POST …/WorkOrders/{id}/close-short`)

| # | Thao tác | Kết quả mong đợi | Tầng |
|---|---|---|---|
| 11.1 | Lý do rỗng hoặc toàn khoảng trắng | 400 "Vui lòng nhập lý do cắt thiếu." | BE-400 |
| 11.2 | Lý do 300 ký tự | Đóng được (nếu qua các điều kiện dưới) | – |
| 11.3 | Lý do 301 ký tự | 400 "Lý do cắt thiếu tối đa 300 ký tự." | BE-400 |
| 11.4 | Lệnh đang OPEN (chưa có bàn cắt) | 409 "Chỉ đóng được lệnh đang cắt (đã có bàn cắt)." | BE-409 |
| 11.5 | Còn phiếu bàn giao BTP chờ kho xác nhận | 409 "Còn phiếu bàn giao BTP chờ kho xác nhận, chưa đóng lệnh được." | BE-409 |
| 11.6 | Còn phiếu trả vải thừa chờ kho xác nhận | 409 "Còn phiếu trả vải thừa chờ kho xác nhận, chưa đóng lệnh được." | BE-409 |
| 11.7 | Còn BTP đã cắt chưa bàn giao / chưa được kho xác nhận | 409 "Còn BTP đã cắt chưa bàn giao và được kho xác nhận: N. Tổ cắt bàn giao hết rồi mới đóng lệnh." | BE-409 |
| 11.8 | Ghi chú lệnh 600 ký tự + lý do 300 ký tự | Đóng được: 600 + 1 + 11 ("Cắt thiếu: ") + 300 = 912 ký tự, vừa cột Note (1000). Thông báo "Ghi chú của lệnh cắt quá dài…" chỉ hiện khi dữ liệu cũ có ghi chú dài hơn 688 ký tự (không tái hiện được qua form / API hiện tại) | – |
| 11.9 | Đóng thành công | Trạng thái Completed; ghi chú thêm dòng "Cắt thiếu: {lý do}"; tổ cắt nhận thông báo "Lệnh cắt đã đóng" | – |
| 11.10 | `id` không tồn tại | 404 | – |

## 12. Lọc danh sách (`GET …/WorkOrders`)

| # | Tham số | Kết quả mong đợi |
|---|---|---|
| 12.1 | `status=OPEN` | Lọc đúng |
| 12.2 | `status=ABC` | 400 "Trạng thái lệnh cắt không hợp lệ." |
| 12.3 | `orderId=0` hoặc `-1` | 400 "Đơn hàng không hợp lệ." |
| 12.4 | `page=0` | 400 "Số trang phải từ 1 trở lên." |
| 12.5 | `pageSize=0` hoặc `pageSize=101` | 400 "Số dòng mỗi trang phải từ 1 đến 100." |
| 12.6 | `keyword` 101 ký tự | 400 "Từ khóa tối đa 100 ký tự." |
| 12.7 | `keyword` theo mã lệnh / mã đơn / mã hàng | Lọc đúng |

## 13. Phân quyền

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 13.1 | Tổ trưởng cắt (`TTCAT`) gọi `GET …/teamleader/WorkOrders/{id}` của lệnh thuộc tổ cắt khác | 403 (`WAREHOUSE_TEAM_MISMATCH`) |
| 13.2 | `TTCAT`, `THUKHO`, `KETOAN`, công nhân gọi `POST …/manager/WorkOrders` (chỉ Admin / Quản đốc được lập) | 403 |
| 13.3 | Gọi API không có token | 401 |

---

## Body mẫu để gọi API

```jsonc
// POST /api/v1/manager/WorkOrders  — thay các id theo DB thật (OrderId, ProductId, TeamId, OrderLineId)
{
  "orderId": 0,
  "productId": 0,
  "cuttingTeamId": 0,
  "dueDate": "2026-11-20",
  "note": null,
  "lines": [
    { "orderLineId": 0, "plannedQty": 50 },
    { "orderLineId": 0, "plannedQty": 20, "productComponentMaterialId": 0 }
  ]
}
```

Lấy id nhanh:

```sql
SELECT o.OrderId, o.OrderCode, o.ReceivedDate, ol.OrderLineId, ol.ProductId, ol.OrderedQty, pv.Color, pv.SizeCode
FROM dbo.CustomerOrder o
JOIN dbo.OrderLine ol ON ol.OrderId = o.OrderId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
WHERE o.OrderCode = 'TEST-CAT-001';

SELECT TeamId, TeamCode, TeamType, Status FROM dbo.Team;
```

## Dọn dữ liệu sau khi test

Lệnh cắt tạo trong lúc test nằm cùng đơn TEST-CAT-001 và cộng dồn vào trần số đặt. Hủy hết lệnh đã tạo trước khi test lại mục 4, hoặc xóa đơn rồi chạy lại patch.

---

## 14. Dữ liệu mẫu để nhập vào các trường

Mỗi đoạn dưới đây có **đúng số ký tự** ghi ở tiêu đề (đã đếm bằng script, đếm theo UTF-16 giống `string.Length` của .NET). Cuối đoạn có nhãn `[số]` để bạn nhìn là biết đoạn nào. Khi copy, **chỉ copy phần trong khung**, đừng copy dấu ```; thêm khoảng trắng đầu/cuối sẽ làm sai kết quả (trừ case chủ ý ở 14.3).

> **Lưu ý khi dán vào form:** ô Ghi chú của form có `maxLength=600` nên trình duyệt tự cắt phần dư khi dán. Dán đoạn 601 vào form chỉ thấy 600 ký tự, **không** ra lỗi. Muốn thấy lỗi "tối đa 600 ký tự" phải dán đoạn 601 vào body API (Postman / Swagger). Đoạn 600 dán vào form hay API đều lưu được.

### 14.1 Ghi chú lệnh cắt (giới hạn 600) – case 6.1, 6.2, 6.3, 10.5

| Đoạn | Dán vào | Kết quả mong đợi |
|---|---|---|
| 600 ký tự | Form hoặc API | Lưu được |
| 601 ký tự | Form | Bị cắt còn 600, vẫn lưu được; bộ đếm ghi 600/600 |
| 601 ký tự | API (`note`) | 400 "Ghi chú tối đa 600 ký tự." |

### Ghi chú – 600 ký tự

Biên trên hợp lệ. Kết quả mong đợi: lưu được.

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. [600]
```

### Ghi chú – 601 ký tự

Vượt biên 1 ký tự (chỉ thấy lỗi khi gọi API). Kết quả mong đợi: 400 "Ghi chú tối đa 600 ký tự."

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt.. [601]
```

### 14.2 Lý do cắt thiếu (giới hạn 300) – case 11.1 – 11.3

### Lý do – 300 ký tự

Biên trên hợp lệ. Kết quả mong đợi: đóng được lệnh (nếu qua các điều kiện 11.4 – 11.7).

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trướ [300]
```

### Lý do – 301 ký tự

Vượt biên 1 ký tự. Kết quả mong đợi: 400 "Lý do cắt thiếu tối đa 300 ký tự."

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước [301]
```

### 14.3 Chuỗi đặc biệt (dán vào Ghi chú, lý do, từ khóa)

| Mã | Chuỗi | Kết quả mong đợi |
|---|---|---|
| S1 | `   Ghi chú có khoảng trắng đầu và cuối   ` (3 dấu cách mỗi đầu) | Lưu được; sau khi lưu mở lại thấy đã bị cắt khoảng trắng đầu cuối |
| S2 | (chỉ gõ 5 dấu cách) | Ghi chú: lưu được và để trống (null). Lý do cắt thiếu: 400 "Vui lòng nhập lý do cắt thiếu." |
| S3 | `<script>alert('x')</script> & "nháy kép" 'nháy đơn'` | Lưu và hiển thị nguyên văn, **không** chạy script, không lỗi 500 |
| S4 | `'; DROP TABLE WorkOrder; --` | Lưu và hiển thị nguyên văn, bảng không bị ảnh hưởng |
| S5 | `Cắt gấp 🔥 hạn sớm` (có emoji) | Lưu được, hiển thị đúng emoji (emoji chiếm 2 ký tự khi đếm độ dài) |
| S6 | Hai dòng: `Dòng 1` Enter `Dòng 2` | Lưu được, hiển thị đúng 2 dòng (ô Ghi chú là textarea) |
| S7 | `Đ ư ơ â ê ô ă – “ngoặc cong” …` (tiếng Việt có dấu, gạch dài) | Lưu và hiển thị đúng, không bị lỗi font hay dấu ? |

### 14.4 Từ khóa tìm kiếm (giới hạn 100) – case 12.6

### Từ khóa – 100 ký tự

Dán vào ô tìm kiếm của danh sách lệnh cắt hoặc `keyword` của API. Kết quả mong đợi: hợp lệ, trả danh sách (có thể rỗng).

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo. [100]
```

### Từ khóa – 101 ký tự

Gọi API `GET …/WorkOrders?keyword=…`. Kết quả mong đợi: 400 "Từ khóa tối đa 100 ký tự."

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo b [101]
```

### 14.5 Ghi chú 500 ký tự (bàn cắt, các phiếu khác) – tham khảo

Ghi chú bàn cắt và lý do sửa số của bàn cắt giới hạn 500 (`SaveCuttingLayDtoValidator`).

### Ghi chú bàn cắt – 500 ký tự

Biên trên hợp lệ. Kết quả mong đợi: lưu được.

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và c [500]
```

### Ghi chú bàn cắt – 501 ký tự

Vượt biên 1 ký tự (gọi API). Kết quả mong đợi: 400 "Ghi chú tối đa 500 ký tự."

```text
Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và chừa 2% hao hụt. Cắt theo bìa số 1, ưu tiên màu Đen trước, kiểm tra khổ vải 1,5 m và ch [501]
```

