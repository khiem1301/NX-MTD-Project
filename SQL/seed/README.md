# Dữ liệu test NX-MTD

Xóa sạch dữ liệu rồi tạo bộ dữ liệu test mới phủ mọi luồng của hệ thống. Chạy lại bao nhiêu lần cũng được, ngày tháng tính theo ngày chạy (hôm nay, 3 tháng trước...).

```powershell
# tại thư mục gốc dự án
sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\seed\Reseed.sql
```

- Giữ nguyên cấu trúc và danh mục cấu hình (`Role`, `GradeLevelCode`, `GradeLevel`, `SystemSetting`, `AccountingPeriod`).
- Mọi dòng seed đi qua đúng CHECK / FK / trigger của DB (trần giao việc, trần sản lượng, thẻ kho chỉ ghi qua `usp_Stock_Post`...). Cuối file 99 có kiểm tra toàn vẹn, thấy `CẢNH BÁO` là có lỗi.
- Ảnh đính kèm dùng một ảnh mẫu: copy `SQL/seed/placeholder.png` thành `be/App_Data/uploads/seed/placeholder.png` (thư mục `App_Data` bị git bỏ qua). Thiếu file thì chỉ ảnh bị trống, dữ liệu vẫn đúng.
- Muốn quay lại dữ liệu cũ: khôi phục bản sao lưu `D:\Learning\NX_MTD_backup_before_reseed.bak` (tạo trước lần seed đầu tiên).

## Tài khoản

Tên đăng nhập = mã nhân viên. Tài khoản đã có từ trước giữ mật khẩu đang dùng; tài khoản mới tạo (QAQC2, CNCAT3-6, TTMAY3, CNMAY21, NVNGHI2) dùng mật khẩu mặc định `123456`. Không đăng nhập được thì đặt lại mật khẩu ở màn Nhân sự.

| Vai trò | Tài khoản |
|---|---|
| Giám đốc | `ADMIN` |
| Quản đốc | `QUANDOC` |
| Kế toán | `KETOAN` |
| Thủ kho | `THUKHO` |
| QA/QC | `QAQC1`, `QAQC2` |
| Cơ điện | `CODIEN` |
| Tổ cắt | tổ trưởng `TTCAT`, tổ phó `TPCAT`, thợ `CNCAT1`..`CNCAT6` |
| Tổ may 1 | tổ trưởng `TTMAY`, tổ phó `TPMAY`, công nhân `CNMAY1`..`CNMAY8` |
| Tổ may 2 | tổ trưởng `TTMAY2` (vắng hôm nay), tổ phó `TPMAY2` (đang thay), công nhân `CNMAY9`..`CNMAY16` |
| Tổ may 3 | tổ trưởng `TTMAY3` (chưa có tổ phó), công nhân `CNMAY17`..`CNMAY21` |
| Trường hợp riêng | `CNMAY20`, `CNMAY21` chưa map mã chấm công; `CNMOI1` chưa vào tổ; `NVNGHI1`, `NVNGHI2` đã nghỉ việc, tài khoản khóa |

## Đơn hàng và tình huống

| Đơn | Trạng thái | Mã hàng | Có gì để test |
|---|---|---|---|
| DH-2509-001 | COMPLETED | AS-0412 áo sơ mi | Chuỗi đầy đủ từ mua NVL đến giao hết; chốt giá trị, thanh toán 3 đợt (có đợt đã sửa); NVL khách cấp (nhãn) |
| DH-2510-002 | DELIVERED | QK-0308 quần kaki | NCC (đã ngừng hợp tác) giao thiếu → đơn mua Chốt không bù; giá trị chốt có trừ; thanh toán có khoản hoàn (số âm); giao xe công ty + khách tự lấy |
| DH-2510-003 | PARTIALLY_DELIVERED | AK-0522 áo khoác | Nguy cơ trễ (còn 2 ngày); giao đợt 1, còn phiếu xuất PENDING / REJECTED; lệnh cắt hủy; BTP cấp bị từ chối; thu hồi việc; công nhân từ chối nhận |
| DH-2510-004 | IN_DELIVERY | AS-0412 | Phiếu xuất IN_DELIVERY (chuyến xe đang chạy) + DELIVERED + PENDING + CANCELLED; đơn mua phụ liệu bị Kế toán trả lại |
| DH-2511-005 | IN_PRODUCTION | AP-0715 áo polo | Đang cắt / may dở; vải về một phần; lệnh cắt OPEN + CUTTING; bàn giao BTP PENDING; phiếu cấp chờ nhận; giao việc PENDING |
| DH-2511-006 | IN_PRODUCTION | AS-0412 | Mới cắt lô đầu; phiếu nhập bị trả lại; bàn giao BTP bị hủy; đơn mua đang giao dở |
| DH-2511-007 | NEW (chờ NVL) | QK-0308 | Đơn mua PENDING_FINALISATION (Quản đốc chưa chốt NCC); khách cấp một phần, lịch giao theo ngày |
| DH-2511-008 | NEW (đủ NVL) | AK-0522 | Khách cấp toàn bộ NVL (CUSTOMER_SUPPLIED); phiếu nhập NVL khách cấp đang chờ xác nhận |
| DH-2510-009 | STOPPED | AK-0522 | Đơn bị dừng giữa chừng có lý do; thu hồi toàn bộ việc đã giao |
| DH-2510-010 | CANCELLED | AP-0715 | Đơn hủy của khách đã ngừng hợp tác |

## Phạm vi dữ liệu theo màn hình

- **Nhân sự / tài khoản / bậc thợ**: 44 nhân viên, đủ 9 vai trò, tổ giải thể, đề xuất bậc đủ 3 trạng thái.
- **Danh mục**: 3 khách hàng (1 ngừng), 5 NCC (1 ngừng), 28 NVL (có phế liệu, 1 ngừng), 5 mã hàng (1 ngừng) kèm định mức vải / phụ liệu theo màu, nhóm bìa, size đã ngừng.
- **Công đoạn và đơn giá**: 32 công đoạn; đơn giá đủ DRAFT, PENDING_APPROVAL, APPROVED (giá cũ, mới, tương lai), REJECTED.
- **Mua hàng**: 22 đơn mua đủ trạng thái hàng và thanh toán; phiếu nhập CONFIRMED / PENDING / RETURNED.
- **Kho**: tồn 3 kho qua thẻ kho; phiếu cấp, trả vải / phế liệu, bàn giao BTP, bàn giao thành phẩm, phiếu xuất; kiểm kê APPROVED / PENDING_APPROVAL / RETURNED.
- **Sản xuất**: lệnh cắt, bàn cắt, phân công công đoạn (có lịch sử), giao / thu hồi việc (đủ cách xác nhận), ~4.900 dòng sản lượng, hàng lỗi (Rework / Scrap / Output_Err, có phản hồi của công nhân; rải đều các đơn, đơn DH-2510-003 có thêm 4 phiếu Scrap làm nguồn cho đơn cắt bù), đợt kiểm QA/QC, KPI (ACTIVE / CLOSED đạt - không đạt / CANCELLED).
- **Chấm công**: từ 03/08 đến hôm nay: đi muộn, tăng ca, quên quẹt ra, quẹt lặp / thừa, nghỉ, làm Chủ nhật, mã chưa map (201-203), chấm bù đủ 4 loại; các lần đồng bộ RJ SUCCEEDED / FAILED / RUNNING và import Excel.
- **Suất ăn**: 60 ngày, đủ OPEN / CLOSED / AUTO_CLOSED / KITCHEN_NOTIFIED, suất khách, tổ xác nhận và hệ thống tự lấy.
- **Đơn từ**: đủ 12 loại với các trạng thái PENDING_APPROVAL, PENDING_PROCESSING, APPROVED, REJECTED, COMPLETED, NOT_COMPLETED, CANCELLED.
  - Đơn cắt bù (4 đơn: chờ duyệt, từ chối, đã duyệt chờ lập lệnh, đã duyệt đã có lệnh cắt bù) đều gắn phiếu hàng hỏng Scrap và dòng chi tiết cần cắt bù (định mức PANEL đúng màu áo); lệnh cắt bù có dòng cắt lẻ theo chi tiết.
  - Đơn giao thiếu gắn phiếu nhập gốc và dòng phiếu nhập; đơn bù (SD1) có phiếu nhập bổ sung đang chờ xác nhận.
  - Đơn mua Chờ chốt và Đã từ chối đều có nhà cung cấp dự kiến (hộp thoại Đặt hàng tự chọn sẵn, Quản đốc đổi được); chỉ đơn Khách cấp NVL không có nhà cung cấp vì không mua của ai.
- **Tài chính**: bảng lương tháng 8 (REJECTED), 9 (APPROVED), 10 (PENDING_APPROVAL), 11 (DRAFT) với phiếu lương theo bảng công và sản lượng; chi phí tháng; hòa vốn; thanh toán đơn hàng; tháng 1-9 đã khóa (thử sửa sản lượng tháng cũ sẽ bị chặn).
- **Khác**: xe và chuyến xe (có cảnh báo km, xăng), thông báo nhanh, thông báo người dùng, nhật ký thay đổi, ảnh đính kèm.

Một số khoản cố ý để lệch nhỏ để test chênh lệch có lý do (nhận thiếu vài sản phẩm, đếm thiếu mét vải).
