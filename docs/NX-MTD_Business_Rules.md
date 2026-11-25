# NX-MTD – Tổng hợp Business Rule

> Bản 25/11/2026. Tổng hợp từ `Rules/NX-MTD_AI_Context.md`, `docs/CODE_DEVIATIONS.md` (tới DEV-ALL-173), code backend hiện tại và các quyết định chốt trong buổi trao đổi ngày 25/11/2026.
> File này chỉ ghi chú, chưa sửa code hay tài liệu nào.
>
> Ký hiệu cột **Trạng thái**:
> - **Code ✅**: code đang làm đúng như vậy.
> - **Mới 25/11 ⚠️**: quyết định mới, code chưa làm hoặc đang làm khác (ghi chỗ cần sửa).
> - **Mở ❓**: chưa chốt.

---

## 1. Vai trò và phân quyền

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-ROLE-01 | Hệ thống có các vai trò: Admin (Giám đốc), Quản đốc, Kế toán, Thủ kho, Tổ trưởng (cắt hoặc may, theo loại tổ), QA/QC, Cơ điện, Công nhân. | AI_Context §3 | Code ✅ (còn `TEAM_DEPUTY`, xem BR-ROLE-06) |
| BR-ROLE-02 | Phạm vi dữ liệu kiểm tra ở server cho mọi request: công nhân chỉ thấy dữ liệu của mình, tổ trưởng chỉ thấy tổ mình. | GBR-01, R1 | Code ✅ |
| BR-ROLE-03 | Admin có toàn bộ chức năng của Quản đốc (lệnh cắt, phiếu nhập NVL, đơn mua, phiếu xuất, KPI, sản lượng, phiếu lỗi, đợt QC, sửa quá hạn, xử lý đơn từ của Quản đốc). Quản đốc không có quyền riêng của Admin. | DEV-ALL-34 | Code ✅ |
| BR-ROLE-04 | Admin quản lý toàn bộ tài khoản: cấp tài khoản + mật khẩu tạm, reset mật khẩu, khóa/mở tài khoản, đổi vai trò. | R2, 25/11 | Code ✅ |
| BR-ROLE-05 | Quản đốc quản lý nhân viên trừ Admin, Quản đốc khác và Kế toán: **chỉ đổi vai trò và chuyển tổ** (không gán được vai trò Admin / Quản đốc / Kế toán). Quản đốc **không** tạo tài khoản, **không** reset mật khẩu, **không** khóa tài khoản, **không** thấy Quản đốc khác. | 25/11 | Mới 25/11 ⚠️ – code đang ẩn Admin + Kế toán, Quản đốc vẫn thấy/sửa Quản đốc khác; cần thêm `MANAGER` vào `HiddenFromManager` |
| BR-ROLE-06 | **Không có role tổ phó.** Mỗi tổ cắt/may có đúng 1 tổ trưởng. | 25/11 | Mới 25/11 ⚠️ – code còn `TEAM_DEPUTY`, trigger `trg_Employee_TeamLeadership`, `vw_TeamActingLeader` (DEV-KH-06-2) |
| BR-ROLE-07 | Khi tổ trưởng nghỉ, Quản đốc bổ nhiệm một người trong tổ làm tổ trưởng **cho ngày hôm đó**. Khi tổ trưởng cũ đi làm lại, Quản đốc bổ nhiệm lại bằng tay. Hệ thống không tự trả quyền, không nhắc; nếu Quản đốc chưa đổi lại thì người tạm vẫn giữ quyền (xử lý ngoài hệ thống, chấp nhận được vì tổ trưởng có thể nghỉ liền 3–4 ngày). | 25/11 | Mới 25/11 ⚠️ – code đang hoán đổi tổ trưởng/tổ phó vĩnh viễn (DEV-KH-10-2) |
| BR-ROLE-08 | Ngày nào một công nhân lương sản phẩm đang giữ quyền tổ trưởng tạm thì ngày đó **tính công theo ngày**, không tính sản phẩm (tránh tự nhập sản lượng cho mình); giữ quyền nhiều ngày liền thì mọi ngày đó đều tính theo ngày. | 25/11 | Mới 25/11 ⚠️ |
| BR-ROLE-09 | Người bị đổi vai trò hoặc đổi tổ phải đăng nhập lại (token cũ hết hiệu lực); đổi tổ ghi nhật ký "Chuyển tổ". | DEV-ALL-7, DEV-KH-10-2 | Code ✅ |
| BR-ROLE-10 | Thủ kho không xem lương ngày. QA/QC không xem số lỗi / tỉ lệ lỗi. | R10 | Code ✅ |
| BR-ROLE-11 | Dữ liệu mật: đơn giá CMT, hòa vốn, giá trị thặng dư, tổng hợp chi phí chỉ Admin; doanh thu Admin + Kế toán. | GBR-02 | Code ✅ |

## 2. Tài khoản và đăng nhập

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-AUTH-01 | Không tự đăng ký, không tự reset mật khẩu. Username = mã nhân viên. | R2 | Code ✅ |
| BR-AUTH-02 | Không ép đổi mật khẩu, không có luật độ mạnh mật khẩu (chỉ cần nhập lại khớp). Đổi mật khẩu thì đăng xuất mọi phiên khác. | GBR-04 | Code ✅ |
| BR-AUTH-03 | Sai mật khẩu nhiều lần **không** khóa tài khoản. Thông báo lỗi không tiết lộ sai tên hay sai mật khẩu. | GBR-03, R3 | Code ✅ |
| BR-AUTH-04 | Nhân viên tự sửa được ngày sinh và số điện thoại ở "Hồ sơ của tôi". | DEV-ALL-114 | Code ✅ |

## 3. Danh mục (master data)

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-MD-01 | Khách hàng duy nhất là công ty mẹ. Khách ngừng hợp tác thì không tạo được đơn mới. | AI_Context §1 | Code ✅ |
| BR-MD-02 | **Chỉ Admin** liên hệ bên thứ ba và thêm / sửa / ngừng hợp tác nhà cung cấp. Quản đốc chỉ chọn NCC đã có trong hệ thống khi mua NVL. | 25/11 | Code ✅ |
| BR-MD-03 | Số điện thoại, email không trùng giữa các khách hàng và nhà cung cấp. Ngừng hợp tác không bắt buộc lý do. | DEV-ALL-133, DEV-ALL-76 | Code ✅ |
| BR-MD-04 | **Admin** thêm / sửa / ngừng dùng / xóa bậc thợ (A, B, C, D, …). Bậc đã dùng thì chỉ ngừng dùng, không xóa. Mức tiền bậc đổi bằng dòng mới có ngày hiệu lực; chỉ sửa/xóa mức chưa hiệu lực. | DEV-ALL-17, 25/11 | Code ✅ |
| BR-MD-05 | Bậc thợ chỉ áp cho công nhân may và tổ trưởng. | AI_Context §3 | Code ✅ |
| BR-MD-06 | Không xóa cứng master data đã dùng, chỉ chuyển trạng thái (ngừng / nghỉ việc / khóa). Không xóa dữ liệu kho, công, lương. | GBR-14, R5 | Code ✅ |
| BR-MD-07 | NVL có loại do người dùng chọn (bỏ nhóm NVL); có lại Mã NVL. Quản đốc, Admin quản lý danh mục NVL. | DEV-ALL-134, 149, 65 | Code ✅ |
| BR-MD-08 | Vải cố định đơn vị **mét**, khổ vải tính bằng mét. | DEV-ALL-46 | Code ✅ |
| BR-MD-09 | **Không fix cứng** chiều dài / chiều rộng bàn cắt, khổ vải, khổ bìa: người dùng nhập, trần lấy từ `SystemSetting`. | 25/11 | Mới 25/11 ⚠️ – `WidthLimits.MaxM = 3.5m` đang hard-code (DEV-ALL-102) |

## 4. Mã hàng, định mức, đơn hàng

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-ORD-01 | Admin tạo đơn hàng (đơn → dòng đơn = mã hàng × màu × size, số đặt, đơn giá CMT). Size là chữ tự do. | AI_Context §4.1 | Code ✅ |
| BR-ORD-02 | Thêm mã hàng bắt buộc có dữ liệu Excel; mã hàng tạo lúc lập đơn chỉ ghi khi lưu đơn. | DEV-ALL-124, 151 | Code ✅ |
| BR-ORD-03 | Quản đốc nhập định mức qua Excel; Kế toán chỉ xem định mức; Admin chỉ xem công đoạn. | DEV-ALL-117 | Code ✅ |
| BR-ORD-04 | Định mức chi tiết lưu theo chi tiết × vải, mỗi chi tiết **1 dòng** kèm **số chi tiết / bộ** (`PiecesPerGarment`, 1–99); tay áo lưu 1 dòng với số chi tiết/bộ = 2, không phân biệt tay trái / tay phải. Số chi tiết/bộ chỉ dùng khi cắt lẻ / cắt bù một chi tiết (1 lớp cắt được số sản phẩm của size × số chi tiết/bộ miếng); cắt cả bộ tính theo bìa (bìa đã gồm đủ chi tiết). Công đoạn và sản lượng tính theo **sản phẩm**, không theo số chi tiết ("tra tay" ghi 1 lần / áo). | Code, 25/11 | Code ✅ |
| BR-ORD-05 | Mỗi chi tiết cắt một vải; công đoạn chỉ gắn chi tiết vải. | DEV-ALL-86 | Code ✅ |
| BR-ORD-06 | NVL của đơn: mỗi (đơn, NVL, màu) một dòng, số cần nhập tay theo số khách báo. Thiếu NVL thì xin khách trước, khách không cấp mới mua. | AI_Context §4.1 | Code ✅ |
| BR-ORD-07 | Đơn chỉ chuyển Mới → Đang sản xuất khi tình trạng NVL khác "Chờ NVL". | AI_Context §4.1 | Code ✅ |
| BR-ORD-08 | Đơn có địa chỉ giao hàng; số lần giao = số ngày giao, dùng chung cho cả đơn. | DEV-ALL-147, 95, 113 | Code ✅ |
| BR-ORD-09 | Dừng đơn bị chặn khi hàng đã xuất hết; hủy / dừng đơn tự hủy phiếu xuất, lệnh cắt, KPI đang chờ. Dừng đơn bắt buộc lý do. | DEV-ALL-81 | Code ✅ |
| BR-ORD-10 | Đơn hàng có giá trị chốt (có thể âm) và thanh toán nhiều lần. | DEV-ALL-137 | Code ✅ |
| BR-ORD-11 | Tiến độ đơn tách "Trễ hạn" và "Nguy cơ trễ". JOB 17:00 đặt cờ nguy cơ trễ (SL còn lại ÷ trung bình hoàn thiện 7 ngày > số ngày còn lại), báo Admin + Quản đốc. | DEV-ALL-143, 27 | Code ✅ |

## 5. Mua NVL và nhập kho

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-PUR-01 | Quản đốc liên hệ NCC khi cần mua thêm NVL và tạo đơn mua; NCC phải có sẵn và đang hợp tác. Tạo đơn mua là đặt hàng luôn. | DEV-ALL-40, 25/11 | Code ✅ |
| BR-PUR-02 | Sau khi đặt, số lượng và NCC bị khóa. Kế toán xác nhận thanh toán (nhiều lần, ảnh chứng từ không bắt buộc), không sửa NCC / số lượng. | GBR-18, DEV-MI-07-1, DEV-ALL-136 | Code ✅ |
| BR-PUR-03 | Đơn mua chốt "không bù": Kế toán nhập tổng giá trị thực tế, trạng thái thanh toán xét theo giá trị này. | DEV-ALL-129 | Code ✅ |
| BR-PUR-04 | Thủ kho lập phiếu nhập (nhận / thiếu / lỗi từng dòng) → Quản đốc xác nhận → kho tăng theo số nhận. Có thiếu/lỗi → hệ thống tạo yêu cầu thiếu NVL → Quản đốc chọn giao bù hoặc không bù. | GBR-19, AI_Context §4.1 | Code ✅ |

## 6. Kho

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-WH-01 | 3 kho: NVL (theo NVL + màu), bán thành phẩm, thành phẩm (theo dòng đơn). | AI_Context §4.5 | Code ✅ |
| BR-WH-02 | Thẻ kho chỉ ghi thêm, không sửa/xóa, tồn sau giao dịch không âm. Không có bảng số dư, tồn tính từ view. | AI_Context §4.5 | Code ✅ |
| BR-WH-03 | Kho chỉ đổi khi chứng từ được xác nhận, ghi trong cùng transaction. | GBR-07 | Code ✅ |
| BR-WH-04 | Phiếu cấp chỉ **giữ chỗ**; tổ xác nhận nhận mới trừ kho. Nhận lệch bắt buộc lý do và báo Quản đốc. Từ chối thì nhả giữ chỗ. | GBR-06, AI_Context §4.2 | Code ✅ |
| BR-WH-05 | **Phiếu cấp không được vượt tồn khả dụng.** | GBR-07 | Code ✅ |
| BR-WH-06 | Đơn xin NVL / BTP **không bị chặn vì đơn khác đang chờ** (vd. kho 40 bộ, 2 tổ cùng xin 30 thì cả 2 đơn đều tạo được). Thủ kho thấy cảnh báo "đang có đơn khác chờ cùng hàng", tự quyết và **từ chối kèm lý do** gửi lên hệ thống. | 25/11 | Mới 25/11 ⚠️ – thiếu cảnh báo ở màn cấp; code vẫn chặn một đơn xin vượt tồn khi tạo |
| BR-WH-07 | Kiểm kê: Thủ kho lập, Quản đốc/Admin duyệt (điều chỉnh kho) hoặc trả lại có lý do. | AI_Context §4.5 | Code ✅ |
| BR-WH-08 | Ngày chứng từ kho, ngày dự kiến giao có ràng buộc; kiểm số trả vải thừa. | DEV-ALL-82 | Code ✅ |

## 7. Cắt

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-CUT-01 | Quản lý vải theo **mét**. | DEV-ALL-46, 25/11 | Code ✅ |
| BR-CUT-02 | Lệnh cắt thường chỉ **cảnh báo** thiếu vải, vẫn lưu được; lệnh cắt bù thiếu vải thì **chặn**. | DEV-ALL-108, quyết định đã chốt | Code ✅ |
| BR-CUT-03 | Ước số mét vải = số lớp × chiều dài bìa; có tính trải nhiều bìa song song khi vải rộng gấp nhiều lần bìa; cảnh báo bìa rộng hơn khổ vải. | DEV-ALL-85, 103–105 | Code ✅ |
| BR-CUT-04 | Bàn cắt nhập số lượng sau cắt và số mét từng vải đã dùng; số thực tế khác số tính phải có lý do. Tổng cắt vượt số đặt trong mức % dư thì cảnh báo, vượt mức thì chặn. | DEV-ALL-90, 93, GBR-08 | Code ✅ |
| BR-CUT-05 | Vải còn lại chuyển thành gợi ý ở phiếu trả vải thừa. Chỉ tổ cắt trả vải thừa, theo phiếu cấp gốc. | DEV-ALL-53, AI_Context §4.2 | Code ✅ |
| BR-CUT-06 | Tổ cắt bàn giao BTP vào kho; Thủ kho xác nhận (lệch có lý do) hoặc từ chối. | AI_Context §4.2 | Code ✅ |
| BR-CUT-07 | Tổ cắt tính công theo ngày, không theo sản lượng. Tổ cắt không có KPI (lệnh cắt là chỉ tiêu). | Quyết định đã chốt | Code ✅ |
| BR-CUT-08 | **Vải hỏng thành phế phẩm:** vải cắt nham nhở, trên sổ còn đủ mét nhưng thực tế không cắt được nữa thì coi là **phế phẩm**, không trả về kho, không có phiếu hao hụt riêng. Tổ trưởng cắt trả vải thừa theo số dùng lại được (sửa xuống từ số gợi ý). Cần vải mới thì gửi đơn **NVL lỗi** → Quản đốc duyệt → đủ tồn hệ thống tự tạo đơn Xin NVL cho Thủ kho cấp, thiếu thì tự tạo đơn Mua NVL cho Quản đốc. Vải hỏng không vào phiếu lỗi công nhân, không ảnh hưởng xét bậc. | 25/11, DEV-ALL-40, DEV-ALL-53 | Code ✅ |

## 8. May, giao việc, sản lượng

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-SEW-01 | **Mỗi tổ may chỉ chạy 1 đơn tại một thời điểm.** Tổ đang có KPI đang chạy của đơn A thì không giao KPI / không chia dòng đơn của đơn B cho tổ đó cho tới khi đơn A kết thúc. | 25/11 | Mới 25/11 ⚠️ – code chỉ chặn trùng (đơn + mã hàng + tổ) và trùng kỳ |
| BR-SEW-02 | Quản đốc, Admin chia dòng đơn cho tổ may. | DEV-ALL-49 | Code ✅ |
| BR-SEW-03 | Tổ trưởng may xin BTP → Thủ kho cấp BTP + phụ liệu → tổ xác nhận như BR-WH-04. Tổ may xin thêm chỉ khi làm hỏng (cắt bù) hoặc hết vải. | AI_Context §4.3 | Code ✅ |
| BR-SEW-04 | Kế toán định nghĩa công đoạn + đơn giá, Admin duyệt. Chưa duyệt đơn giá thì không giao việc và không nhập sản lượng. Mỗi mã hàng đúng 1 công đoạn may cuối. | GBR-11, DEV-ALL-73 | Code ✅ |
| BR-SEW-05 | Tổ trưởng giao sản lượng công đoạn theo ngày cho công nhân, công nhân **xác nhận** (xác nhận nhiều phiếu một lần được). Quá 60 phút không xác nhận thì hệ thống tự xác nhận đủ số. Hạ số cũng phải chờ xác nhận. Chỉ sửa trong ngày. | DEV-ALL-100, 101, 126, 157, 78 | Code ✅ |
| BR-SEW-06 | Sản lượng cuối ca: đơn giá khóa lúc nhập; trần ≤ BTP công nhân đã nhận; công nhân chưa chấm công thì cảnh báo. Phần vượt chuyển sang hôm sau. | GBR-08, DEV-ALL-64 | Code ✅ |
| BR-SEW-07 | Hai luồng sản lượng tách biệt: sản lượng công đoạn (tổ trưởng nhập, dùng tính lương) và hàng ra chuyền (QA/QC đếm sau kiểm tra, dùng tính tiến độ, bàn giao, doanh thu). | AI_Context §1 | Code ✅ |
| BR-SEW-08 | QA/QC nhập số đạt theo đợt, ≤ hàng trên chuyền; sửa không được giảm dưới số đã bàn giao. Bàn giao thành phẩm cho kho được giao một phần, tổng ≤ số QC đạt. | DEV-ALL-38 | Code ✅ |
| BR-SEW-09 | Cửa sổ sửa: sản lượng + phiếu lỗi đến hết ngày làm việc kế tiếp (bỏ Chủ nhật); đợt QC đến hết ca. Sau hạn chỉ Quản đốc sửa, bắt buộc lý do, ghi nhật ký. | GBR-09 | Code ✅ |

## 9. KPI, đánh giá, tăng ca

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-KPI-01 | Quản đốc tạo KPI tổng cho tổ may rồi giao KPI theo ngày (chia theo màu × size). Không có tỉ lệ lỗi tối đa, không thưởng KPI. | DEV-ALL-8, 62, quyết định đã chốt | Code ✅ |
| BR-KPI-02 | KPI ngày **chỉ dùng để theo dõi** và quyết định tăng ca giữa chừng. KPI ngày khóa sau 08:00. | 25/11, DEV-ALL-24 | Code ✅ |
| BR-KPI-03 | **Đánh giá sản lượng và sản phẩm chính thức chỉ làm khi kết thúc đơn**, không đánh giá theo ngày hay theo tháng. | 25/11 | Code ✅ (cần ghi rõ trong tài liệu) |
| BR-KPI-04 | Thiếu KPI: thiếu ít (≤ ngưỡng `KPI_OT_AUTO_THRESHOLD_PCT`) thì cho tăng ca; thiếu nhiều thì tổ trưởng đánh giá có tăng ca hay không. | 25/11 | Code ✅ – chốt 25/11: chỉ thông báo, không lưu quyết định tăng ca; giờ tăng ca lấy từ máy chấm công |

## 10. Phiếu lỗi

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-DEF-01 | Phiếu lỗi có các loại: Rework (trả lại sửa, không trừ sản lượng, chỉ tính khi xét bậc), Scrap (trừ hàng trên chuyền và tiền của người gây lỗi theo đơn giá khóa, sinh yêu cầu cắt bù), Nhập sai sản lượng `OUTPUT_ERR` (trừ ngoài, không sửa bản ghi cũ). | GBR-21, DEV-ALL-79 | Code ✅ |
| BR-DEF-02 | Phiếu lỗi bắt buộc công đoạn, công nhân gây lỗi, lý do (Khác phải ghi chú); báo cho công nhân. Tổ trưởng sửa/hủy trong 24 giờ, sau đó Quản đốc sửa/hủy theo ràng buộc. | DEV-ALL-106 | Code ✅ |
| BR-DEF-03 | **Lệch sản lượng khi kết thúc đơn cũng là một loại lỗi.** Với mỗi công đoạn × dòng đơn có sản lượng tổ báo > QC đạt + hàng hủy, hệ thống tạo **phiếu lỗi nháp** (loại nhập sai sản lượng) **chưa gán công nhân**. Tổ trưởng **bắt buộc gán đúng người** mới đóng được đơn; chỉ khi đã gán mới trừ sản lượng và tiền. | 25/11 | Mới 25/11 ⚠️ – code chỉ cảnh báo, không tạo phiếu (DEV-ALL-107); cần trạng thái nháp cho phiếu lỗi |
| BR-DEF-04 | Tổ báo **thiếu** (sản lượng công đoạn < QC đạt) khi kết thúc đơn thì **tổ tự chịu**: hệ thống chỉ biết BTP đã giao đủ và công nhân đã xác nhận số lượng. Không cảnh báo, không nhập bù. | 25/11 | Code ✅ (code đang bỏ qua báo thiếu, DEV-ALL-107) |

## 11. Xuất hàng và giao hàng

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-DLV-01 | Quản đốc lập phiếu xuất (giữ chỗ thành phẩm) → Thủ kho xác nhận xuất (trừ kho) hoặc từ chối có lý do. | AI_Context §4.4 | Code ✅ |
| BR-DLV-02 | Thủ kho chọn cách giao khi xuất: khách tự lấy hoặc công ty giao; xe công ty thì hệ thống tạo luôn chuyến xe. Kế toán chỉ kết thúc chuyến. | DEV-ALL-127 | Code ✅ |
| BR-DLV-03 | Khi giao, lái xe mang phiếu giao hàng về cho Kế toán; Kế toán chụp lại phiếu giao thành công thì đơn chuyển trạng thái **Đã giao**. | 25/11 | Code ✅ |
| BR-DLV-04 | Giao hàng luôn thành công; không có giao thất bại, trả hàng, giao một phần hay nhập số khách nhận. Đơn hàng Đã giao khi tổng đã giao ≥ số đặt. | GBR-23, 24, DEV-ALL-66, 70 | Code ✅ |

## 12. Đơn từ (Request)

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-REQ-01 | Mỗi loại đơn có người tạo và người xử lý cố định (bảng loại đơn ở AI_Context §4.8); có thêm "Đơn Khác" do người tạo chọn vai trò xử lý; đơn Sửa máy do tổ cắt/may gửi, Cơ điện xử lý. | AI_Context §4.8, DEV-ALL-91, 169 | Code ✅ |
| BR-REQ-02 | Từ chối / trả lại luôn bắt buộc lý do. Quyết định đã đưa ra không đổi được. | GBR-05, R4 | Code ✅ |
| BR-REQ-03 | Tối đa 5 ảnh, tính theo từng nhóm ảnh; vai trò nào tải nhóm ảnh của mình. | DEV-ALL-128 | Code ✅ |

## 13. Chấm công và suất ăn

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-ATT-01 | Agent đọc máy chấm công Ronald Jack Pro mỗi 10 phút, chỉ lấy ID, mã chấm công, giờ chấm; trùng thì bỏ qua; mã chưa map vẫn lưu. Quá 30 phút không đồng bộ được thì báo Kế toán; dự phòng nhập Excel. | AI_Context §4.6 | Code ✅ |
| BR-ATT-02 | Ca hành chính 07:30–16:30, nghỉ trưa 11:30–12:30, 1 công = 8 giờ; giờ ra = lượt quẹt kế tiếp sau giờ vào; tăng ca = giờ ra − 16:30; Chủ nhật x2. Các mốc để trong `SystemSetting`. | AI_Context §4.6 | Code ✅ |
| BR-ATT-03 | Chấm bù chỉ Kế toán làm, bắt buộc lý do. | GBR-25 | Code ✅ |
| BR-ATT-04 | **Máy tính của công ty cài Ronald Jack Pro phải luôn bật** (kèm mạng) để agent đồng bộ chấm công chạy liên tục (10 phút/lần). Máy tắt thì không có dữ liệu chấm công mới: quá 30 phút hệ thống báo Kế toán (MSG23); trước 08:30 chưa đồng bộ được thì suất ăn chốt theo số quẹt đã có; Kế toán dùng nhập Excel dự phòng. Bật lại máy thì agent đồng bộ tiếp từ lượt quẹt cuối, không mất dữ liệu. | 25/11, AI_Context §4.6 | Code ✅ (ràng buộc vận hành) |
| BR-MEAL-01 | Tổ trưởng xác nhận số suất ăn tổ; 08:15 nhắc tổ chưa xác nhận; 08:30 chốt (tổ chưa xác nhận lấy số người đã quẹt) và khóa, không sửa được. Kế toán nhập suất khách. | AI_Context §4.6 | Code ✅ |

## 14. Lương, chi phí, tài chính

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-PAY-01 | Kiểu lương: công nhân may theo sản phẩm; thợ cắt + QA/QC theo ngày công; vai trò khác theo tháng. Ngày làm tổ trưởng tạm tính theo ngày (BR-ROLE-08). | R8, 25/11 | Code ✅ (phần tổ trưởng tạm chưa có) |
| BR-PAY-02 | Kế toán xuất dữ liệu → tính ngoài → nhập bảng lương → Admin duyệt (hoặc từ chối/mở lại có lý do) → công nhân xem phiếu lương → khóa tháng. Thuế làm ở MISA. | GBR-12, R7, 25/11 | Code ✅ – chốt 25/11: hệ thống **không** tự tính lương chính thức |
| BR-PAY-03 | Tháng đã khóa không sửa/nhập/tính lại sản lượng, lỗi, QC, chấm bù, chi phí. | GBR-10 | Code ✅ |
| BR-PAY-04 | Lương tạm tính chỉ cho công nhân may: Σ(SL × đơn giá khóa) − Σ(Scrap × đơn giá Scrap) − phiếu nhập sai; Chủ nhật tiền sản phẩm x2, khoản trừ không nhân đôi; luôn ghi "tạm tính". | AI_Context §4.7, DEV-ALL-79 | Code ✅ |
| BR-FIN-01 | Chi phí tháng: Kế toán nhập Excel 10 khoản, cố định/biến đổi (biến đổi phải gắn mã hàng); nhập lại thì thay số tháng đó. | AI_Context §4.7 | Code ✅ |
| BR-FIN-02 | Doanh thu = SL thành phẩm nhập kho trong tháng × đơn giá CMT. | AI_Context §4.7 | Code ✅ |
| BR-FIN-03 | Hòa vốn chỉ tính khi bảng lương tháng đã duyệt và chi phí tháng đã nhập; tính cả hai cách chia định phí; tháng đã khóa sổ chỉ cảnh báo, không chặn. Chỉ Admin. | GBR-17, DEV-ALL-171, 172 | Code ✅ |

## 15. Bậc thợ và AI gợi ý

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-GRD-01 | Tổ trưởng đề xuất bậc cho tổ mình; Admin / Quản đốc quyết. Admin / Quản đốc đặt bậc trực tiếp được (lý do bắt buộc, tối đa 1 lần / công nhân / ngày). | GBR-22, DEV-ALL-7 | Code ✅ |
| BR-GRD-02 | AI (Gemini) nhận chỉ số ẩn danh của công nhân (hiệu suất, chất lượng, ngày công) + danh sách bậc, trả về bậc gợi ý kèm lý do, cho cả tổ. Chỉ là gợi ý. | DEV-ALL-148 | Code ✅ |

## 16. Sổ xe

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-VEH-01 | Một xe chỉ có một chuyến đang chạy. Km cuối phải lớn hơn km đầu; cảnh báo km không liền chuyến trước. | DEV-ALL-158, 130 | Code ✅ |
| BR-VEH-02 | Tiền xăng bắt buộc có ảnh hóa đơn; bỏ ô số lít khi kết thúc chuyến. | DEV-ALL-173, 167 | Code ✅ |

## 17. Quy tắc chung

| # | Business rule | Nguồn | Trạng thái |
|---|---|---|---|
| BR-GEN-01 | Mọi thay đổi chứng từ kho, sản lượng, chấm công, đơn giá, bảng lương, tài khoản ghi nhật ký (người, lúc, giá trị cũ/mới, lý do) trong cùng transaction. | GBR-15, R6 | Code ✅ |
| BR-GEN-02 | Nhận lệch số luôn bắt buộc lý do và báo Quản đốc. | GBR-06 | Code ✅ |
| BR-GEN-03 | Lỗi ghi đồng thời dùng optimistic concurrency, báo xung đột. | R12 | Code ✅ |
| BR-GEN-04 | Ngoài phạm vi: thuế/MISA, là/gấp, nghỉ phép, hàng trả về, GPS/IoT, AI nhận diện lỗi, đa nhà máy, offline, khóa tài khoản tự động, QA/QC phân loại lỗi, thưởng KPI, giao hàng thất bại. | AI_Context §9 | – |

---

## 18. Ràng buộc vận hành

> Những điều kiện ngoài code phải đúng thì hệ thống mới chạy đúng. Chốt ngày 25/11/2026.

| # | Ràng buộc | Nếu không đảm bảo | Trạng thái |
|---|---|---|---|
| OPS-01 | Máy tính công ty cài Ronald Jack Pro luôn bật và có mạng (xem BR-ATT-04). | Không có lượt quẹt mới; quá 30 phút báo Kế toán (MSG23); dùng nhập Excel dự phòng. | Chốt |
| OPS-02 | Phần mềm Ronald Jack Pro luôn mở và tự tải lượt quẹt từ máy vân tay về `RJData.mdb` (agent chỉ đọc file này, không đọc máy vân tay). **Kế toán kiểm mỗi sáng trước 08:30** (máy tính + phần mềm RJ), nhất là khi nhận cảnh báo mất đồng bộ. | Máy bật nhưng không có dữ liệu mới; hệ thống không phân biệt được lỗi ở máy tính, phần mềm RJ hay máy vân tay. | Chốt |
| OPS-03 | Agent chạy nền khi máy khởi động (Task Scheduler / Windows Service), có driver Access (ACE) đúng bản 32/64-bit, tắt chế độ ngủ, nên có UPS; Windows Update khởi động lại máy thì agent tự chạy lại. | Agent dừng âm thầm, giống OPS-01. | Chốt |
| OPS-04 | Nhân viên mới phải được lấy vân tay **và** Kế toán gắn mã chấm công trước ngày đi làm đầu tiên. | Lượt quẹt vẫn lưu nhưng bảng công trống, tổ trưởng thấy cảnh báo chưa chấm công, không được tính suất ăn; gắn mã xong hệ thống tính lại ngày cũ. | Chốt |
| OPS-05 | Công nhân quẹt vào trước 08:30 để được tính suất ăn; quên quẹt thì tổ trưởng gửi đơn chấm bù. | Thiếu suất ăn, thiếu công. | Chốt |
| OPS-06 | Server chạy 24/7, **chỉ 1 bản**, không để tự ngủ (IIS phải tắt idle timeout). Các job chạy trong web app: nhắc suất ăn 08:15, chốt suất ăn 08:30, cảnh báo trễ đơn 17:00, dọn thông báo 01:00, tự xác nhận phiếu giao việc (dò 5 phút/lần), cảnh báo mất đồng bộ. | Job không chạy (không ai vào web buổi sáng); chạy 2 bản thì job chạy đôi. Lưu ý code: ngày chạy gần nhất chỉ giữ trong bộ nhớ, server khởi động lại sau giờ chạy thì job hôm đó chạy lại lần nữa (vd. nhắc suất ăn 2 lần). | Chốt |
| OPS-07 | Giờ server và database là giờ Việt Nam. | Sai mọi mốc 08:00, 08:30, hạn sửa, khóa suất ăn. | Chốt |
| OPS-08 | Ảnh chứng từ lưu trên ổ đĩa server (`LocalFileStorage`): server phải dùng **ổ đĩa cố định**; **sao lưu database và thư mục ảnh hằng ngày** (ngoài phạm vi code). | Deploy lại trên ổ tạm là mất hết ảnh (phiếu giao, biên bản, hóa đơn xăng, chứng từ thanh toán); mất DB là mất thẻ kho, công, lương (thẻ kho chỉ ghi thêm, không dựng lại được). | Chốt |
| OPS-09 | Có internet và khóa API Gemini để dùng AI gợi ý bậc. | Chỉ mất phần gợi ý, không chặn việc khác. | Chốt |
| OPS-10 | Tổ trưởng, QA/QC, Thủ kho có điện thoại có mạng ở xưởng. Công nhân **không bắt buộc** có điện thoại: phiếu giao việc không xác nhận trong 60 phút thì hệ thống tự xác nhận và **coi như công nhân đồng ý** (mất quyền khiếu nại số đã giao). | Đi kèm BR-DEF-04: tổ báo thiếu thì tổ tự chịu. | Chốt |
| OPS-11 | Người dùng tuân theo các mốc giờ: KPI ngày khóa 08:00; tổ trưởng xác nhận suất ăn trước 08:30; sản lượng và phiếu lỗi sửa đến hết ngày làm việc kế tiếp; đợt QC sửa đến hết ca; sau hạn chỉ Quản đốc sửa. | Phải nhờ Quản đốc sửa quá hạn, có lý do và nhật ký. | Chốt |
| OPS-12 | **Giới hạn hệ thống:** chỉ bỏ qua Chủ nhật, không có danh sách ngày lễ. Ngày lễ (Tết, 30/4…) thì hạn sửa, số ngày làm việc KPI, cờ nguy cơ trễ đơn vẫn tính ngày lễ; Quản đốc sửa quá hạn / chỉnh KPI bằng tay. | Hạn sửa hết trong kỳ nghỉ, cờ trễ đơn sai. | Chốt (giới hạn) |
| OPS-13 | Giấy tờ chạy song song: phiếu xuất có chữ ký Quản đốc (Thủ kho chụp lên); lái xe mang phiếu giao hàng về cho Kế toán chụp lên. | Lái xe không mang phiếu về thì đơn không chuyển "Đã giao". | Chốt |
| OPS-14 | Kế toán khóa tháng sau khi Admin duyệt bảng lương; nhập chi phí tháng để tính hòa vốn. | Không khóa thì dữ liệu tháng cũ (sản lượng, lỗi, chấm bù, chi phí) vẫn sửa được; thiếu chi phí thì không tính được hòa vốn. | Chốt |
| OPS-15 | Khóa API của agent (`X-Agent-Key`) giống nhau ở máy agent và server, giữ bí mật. | Ai có khóa đều đẩy được lượt quẹt giả lên hệ thống. | Chốt |
| OPS-16 | Tổ trưởng tạm (BR-ROLE-07): Quản đốc tự bổ nhiệm lại tổ trưởng cũ khi người đó đi làm, hệ thống không nhắc. | Người tạm giữ quyền đến khi Quản đốc đổi lại (chấp nhận). | Chốt |

## 19. Câu hỏi còn mở

| # | Câu hỏi | Ghi chú |
|---|---|---|
| – | Không còn câu hỏi mở (chốt hết ngày 25/11/2026). | |
