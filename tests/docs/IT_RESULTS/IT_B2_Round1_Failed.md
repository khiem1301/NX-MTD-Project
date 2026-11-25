# IT Batch 2 — Round 1: các ca Failed (001–200)

Kết quả: **200 ca, 163 Passed, 37 Failed**. Ca đồng thời (13 ca: 032, 033, 039, 059, 091, 092, 118, 123, 153, 154, 176, 184, 200) chạy lặp **6 lần**: cả 6 lần đều 13/13 Passed, không ca nào không ổn định. Log: `IT_B2_Round1.trx`, `IT_B2_Round1.json`.
Nhóm: **A** = lệch mã HTTP so với spec · **B** = thiếu validation / quy tắc nghiệp vụ lệch spec · **C** = lỗi 500 / race · **D** = rò rỉ dữ liệu / bảo mật · **E** = lệch cấu trúc / audit / schema.
Không ca nào ở nhóm C, D: không có `500`, không lộ tên constraint / SQL trong body lỗi, không lộ file hệ thống khi path traversal.

Theo `CLAUDE.md` (Assert-Locking) mọi ca dưới đây giữ nguyên `Failed`, không sửa giá trị kỳ vọng. Phần sửa file test chỉ gồm dữ liệu mồi / hạ tầng (xem mục cuối).

| Ca | Nhóm | Spec | Thực tế |
|---|---|---|---|
| 020 | E | Tạo khách hàng: body có `CustomerId`, `Status` | `data` chỉ là số (id), không phải object |
| 023 | A | Trùng SĐT khi tạo khách → 409 | 400 `PARTNER_PHONE_DUPLICATE` (kiểm tra trùng SĐT dùng chung giữa Khách hàng và NCC) |
| 024 | A | Trùng email khi tạo khách → 409 | 400 `PARTNER_EMAIL_DUPLICATE` |
| 025 | B | Khách mới `Phone:null, Email:null` → 201 | 400 `COMMON_VALIDATION`: bắt buộc nhập SĐT, email (trái với S0: CU2 có Phone / Email NULL) |
| 026 | B | `Phone:""`, `Email:""` → 201 và lưu NULL, hoặc 409 | 400: "Vui lòng nhập số điện thoại / email" |
| 028 | B | Phone dài 15 ký tự → 201, 16 ký tự → 400 | 400 ở cả ca 15 ký tự: `PhoneRule` chỉ nhận 10 số bắt đầu bằng 0, hoặc +84 + 9 số |
| 034 | B | PUT khách với `Email:null` → 200 | 400: bắt buộc nhập email |
| 035 | A | PUT khách trùng SĐT khách khác → 409 | 400 `PARTNER_PHONE_DUPLICATE` (trùng tên vẫn 409) |
| 041 | B | Ngừng khách thiếu `Reason` (null / rỗng / `{}`) → 400 (DTO ghi "bắt buộc") | 200, ngừng không cần lý do |
| 044 | B | Ngừng khách đã ngừng → 400/409 | 200 "Đã chuyển khách hàng sang ngừng hợp tác" |
| 046 | B | Reactivate khách đang ACTIVE → 400/409 | 200 "Đã lưu" |
| 051 | B | `SortDir=xyz` → giảm dần | Trả tăng dần (giống `SortDir=asc`) |
| 054 | A | Trùng tên / SĐT / email khi tạo NCC → 409 cả ba | Trùng tên → 409 đúng; trùng SĐT → 400 `PARTNER_PHONE_DUPLICATE` (ca con thứ ba, trùng email, không chạy tới vì ca thứ hai đã sai) |
| 055 | B | NCC mới `Phone:null, Email:null` → 201 | 400: bắt buộc nhập SĐT, email |
| 056 | B | NCC `Phone:""`, `Email:""` → 201 (chuẩn hóa NULL) hoặc 409 | 400: "Vui lòng nhập số điện thoại / email" |
| 067 | B | Reactivate NCC đang ACTIVE → 400/409 | 200 "Đã lưu" |
| 068 | B | Ngừng NCC đã ngừng → 400/409 | 200 "Đã ngừng hợp tác với nhà cung cấp" |
| 070 | E | Tạo NVL: AuditLogs +1 | AuditLogs +0 (Material không ghi nhật ký) |
| 083 | B | Khổ vải 99.99 → 201; 100.00 → 400 | 400 ngay ở 99.99: "Khổ vải tối đa 3,5 m" (DEV-ALL-102) |
| 093 | E | Sửa NVL: AuditLogs +1 | AuditLogs +0 |
| 100 | E | Đổi trạng thái NVL: AuditLogs +1 | AuditLogs +0 |
| 103 | E | Reopen NVL: AuditLogs +1 | AuditLogs +0 |
| 104 | B | Reopen NVL đang ACTIVE (`V-0001`) → 400/409 | 200 "Đã cập nhật trạng thái NVL" |
| 115 | E | Chèn trực tiếp `SortOrder = 0` / `6` thành công (chứng minh DB không có CHECK) | DB **có** CHECK `CK_ProductImage_Sort`: INSERT bị từ chối. `ProductImageConfiguration` và tài liệu hợp đồng không ghi ràng buộc này |
| 133 | B | Upload ảnh `6 291 456 − 2 KB` → 200 (`RequestSizeLimit` 6 MiB) | 400 `ATTACHMENT_FILE_TOO_LARGE`: "vượt quá 5 MB" (validate dung lượng 5 MB chặn trước giới hạn 6 MiB) |
| 142 | B | PUT spec đúng input spec (Norm Cổ→MA2, nhóm bìa 1 gồm Thân trước + Cổ) → 200 | 400 `PRODUCT_SPEC_INVALID`: nhóm bìa 1, màu Đen có hai vải khác nhau (một bìa chỉ trải một vải). Input mẫu của spec vi phạm quy tắc nghiệp vụ |
| 161 | E | Preview có `ParseError` + Norm trỏ thành phần không có → `CrossErrors` khác rỗng | `CrossErrors = []` (lỗi chỉ nằm ở dòng, `CanImport=false`) |
| 165 | B | Import file lẫn dòng lỗi → 400/409, rollback toàn bộ | 200 và **đã nhập** dòng hợp lệ ("Đã nhập: 1 thành phần…"); preview của cùng file báo `errorCount=1` nhưng `canImport=true`. Sheet "Hướng dẫn" ghi "còn lỗi thì không lưu gì" |
| 167 | B | Import `replace=true` bỏ "Túi" và "Thân trước": `Removed=2`, Thân trước chuyển ngừng dùng | `Removed=1`: chỉ "Túi" bị xóa; "Thân trước" (đang dùng) giữ ACTIVE, không bị ngừng dùng |
| 170 | E | Import có bìa của đơn nhưng không `orderId` → 200, `Notes` ghi bảng bị bỏ qua | 200, nhưng `SpecImportResultDto` không có `Notes`, message không nhắc bảng bị bỏ qua (Notes chỉ có ở preview) |
| 171 | B | Import bìa của đơn OD1 với `Status` để trống → `OrderMarkers > 0`, ACTIVE | `orderMarkers = 0`: `Status:""` không được mặc định ACTIVE khi gửi qua JSON (chỉ `ACTIVE`/`Active` được nhận) |
| 172 | A | Import với `orderId` của đơn thuộc mã hàng khác → 400/404 | 409 `ORDER_MARKER_INVALID` "Mã hàng không có trong đơn hàng" |
| 174 | B | Bìa của đơn `Status:"Active"` / `"PAUSED"`, `LengthM=-1`, `WidthM=100` → 400 | 200, nhập 1 bìa (`Status:"Active"` được chấp nhận; các ca còn lại không chạy tới do ca đầu đã sai) |
| 180 | B | `PlateNumber` dài 20 ký tự → 201 | 400: "Biển số không đúng định dạng (ví dụ 51C-123.45 hoặc 30F-1234)" |
| 182 | B | `FuelNormPer100Km = 9999.99` → 201 | 400: `must be less than or equal to '9999'` (validator cắt ở 9999, lệch `decimal(6,2)`) |
| 189 | B | Ngừng xe đã ngừng (VE3) → 400/409 | 200 "Đã lưu xe" |
| 197 | B | Thao tác thất bại không ghi AuditLogs (gồm import lỗi IT_B2_165 phải 4xx) | Thành phần import lỗi trả 200 (cùng nguyên nhân IT_B2_165). Các thao tác 021, 076, 108, 144 trả 4xx đúng |

## Hành vi thực tế của các ca đánh dấu `[?]` (Passed, ghi lại để QA)

- Trạng thái ngừng trong DB là `DISCONTINUED` (HOA) cho cả 5 bảng; không có `Active` / `Discontinued` PascalCase trong DB (IT_B2_195). Response API trả `Status` dạng PascalCase (`Active`, `Discontinued`) nhất quán giữa list và options (IT_B2_196, 047).
- Ngừng khi còn tham chiếu đều trả 200 và **không** cascade / SET NULL: khách có đơn (043), NCC có PO (065), NVL có tồn kho (102), mã hàng đang có đơn (125), xe đang có chuyến STARTED (188), cả 5 trong 199.
- Chữ hoa / thường: tên khách "CÔNG TY MAY VIỆT" và thừa khoảng trắng → 409 (022); mã mã hàng `sp001` → 409 (119); `v-0001` → 409 (074); màu `đen` → 409 (079); biển số `51c-123.45` → 409 (179).
- `Keyword='%'` trả 0 dòng (050); `MaterialType='fabric'` → 400 (085); tạo `SCRAP` qua API → 200 (086); `PUT` NVL kèm `MaterialCode` → 200 và bỏ qua trường (097).
- Spec: `PiecesPerGarment = 32767` → 400 (giới hạn nghiệp vụ, 150); PUT spec toàn mảng rỗng → 200 (152); PUT spec cho mã hàng đã ngừng → 200 (155); `ComponentType='panel'` → 200 (149); biến thể mới (Xanh, XL) → 400 (147).
- Ảnh phụ: key 500 ký tự không có trong storage → 400 (116); `[K1, K1]` → 400 (117).
- Excel: `parts=abc` → 400 (163); workbook rỗng → 400 (175); IT_B2_153 (INSERT nhóm bìa lỗi giữa chừng) → 409 và rollback sạch, `ProductComponent` / `ProductComponentMaterial` giữ nguyên.
- Customer không có `RowVer`: hai PUT đồng thời đều 200, last-write-wins (039).
- AuditLogs ghi cho Customer, Supplier, Product, Vehicle (mỗi thao tác 1 dòng; IT_B2_198 đúng 3 dòng cho SU2); **không** ghi cho Material (070, 093, 100, 103).

## Sửa file test / hạ tầng trước khi chốt Round 1 (chỉ dữ liệu mồi và hạ tầng, không đổi giá trị kỳ vọng)

- Test DB `NXMTD` có sẵn dữ liệu DEMO commit (vd. xe `51C-123.45` trùng VE1 của S0, SĐT `0901000001` của NCC trùng CU1). `TestDataSeeder.B2` đổi tạm các khóa UNIQUE trùng S0 trong ambient transaction (Rollback tự khôi phục). Chế độ đồng thời commit thật nên `B2ConcurrentTestBase` khôi phục các khóa này khi dọn.
- Body request các ca không nêu trường: `CustomerBody` / `SupplierBody` tự điền SĐT, email, địa chỉ hợp lệ và duy nhất để request vượt qua validate các trường không liên quan tới ca test.
- `UQ_VT_OneOpen` (index duy nhất 1 chuyến STARTED / xe) chặn dữ liệu bất thường của VE4. S0 chỉ có 1 chuyến STARTED cho VE4; IT_B2_191 tạm vô hiệu hóa index trong ambient transaction (`ALTER INDEX … DISABLE`, được Rollback) để dựng chuyến thứ hai.
- OD2 (thuộc PR2) tạo theo yêu cầu trong IT_B2_172 vì S0 quy định PR2 không có đơn / biến thể (IT_B2_128).
- File mẫu Excel: `spec_ok.xlsx` = file mẫu do chính hệ thống sinh cho PR1 (`/common/import-templates/spec`), bỏ các dòng ví dụ ở "Nhóm bìa" / "Bìa của đơn" và đổi tên "Túi" thành "Túi mới" để có đúng một dòng mới hợp lệ. `spec_err.xlsx` = bản đó có thêm dòng "Thân trước" sai loại.
- Tồn kho (`StockBalance` qua `usp_Stock_Post`) chỉ seed ở chế độ ambient; chế độ đồng thời không seed vì `StockTransaction` là sổ chỉ-thêm (trigger chặn xóa).
- `BaseIntegrationTest.InitializeAsync`: Rollback ambient transaction nếu seed lỗi (trước đó transaction treo làm các test sau chờ khóa 30 giây).

## Cần xử lý ngoài code test

Các lần chạy thử đầu tiên (trước khi sửa cơ chế dọn của `B2ConcurrentTestBase`) đã để lại **dữ liệu S0 Batch 2 đã commit** trong Test DB `NXMTD`: khách `Công ty May Việt`, `Garment ABC`, `Khách Cũ`, `KH ĐT R1`; NVL `V-0001…V-0005`, `PL-0001`; mã hàng `SP001–SP003`; đơn `OD1-B2`; tồn kho / `StockTransaction` của 2 dòng tồn; PO `PO-B2-01/02`. Test hiện tại đã né các dòng này (đổi tạm khóa trong transaction) nên kết quả Round 1 không bị ảnh hưởng, nhưng chúng vẫn nằm trong DB và cần được xóa thủ công (xóa `StockTransaction` cần tắt tạm trigger `trg_StockTransaction_AppendOnly`).
