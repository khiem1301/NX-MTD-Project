# Bản Review 1 – 23/10/2026

> Bản nháp do phần KH-06 → KH-10 soạn. Các dòng của Kiều, Thái, Minh để người sở hữu điền PR / tình trạng.
> Hướng dẫn chi tiết các luồng của Khiêm: `docs/HUONG_DAN_LUONG_KH06-10.md`.

## Có trong bản này
| Chức năng | Task | Người | UC | PR |
|---|---|---|---|---|
| Đăng nhập, đăng xuất, phân quyền menu theo 9 vai trò | KH-01…KH-05 | Khiêm | UC01, UC02 | (đã merge) |
| Nhân viên, tài khoản, tổ, hồ sơ, đổi mật khẩu | KH-10 | Khiêm | UC03, UC04, UC16, UC17, UC18 | #… |
| Đơn từ + ảnh + nhật ký | KH-06, KH-07 | Khiêm | UC07, UC08 | #… |
| Mã hàng + khách hàng + đơn hàng màu × size | KH-08 | Khiêm | UC09, UC10 | #… |
| Kho, cắt (NVL của đơn, phiếu nhập, cấp NVL, lệnh cắt...) | TH-01…TH-12 | Thái | UC19, UC37… | #… |
| Công đoạn + đơn giá, duyệt đơn giá | KI-01, KI-02 | Kiều | UC25, UC26 | #… |
| Đồng bộ chấm công, map mã chấm công, bảng công | MI-01, MI-02 | Minh | UC61, UC28 | #… |

## Chưa có trong bản này
| Chức năng | Task | Dự kiến |
|---|---|---|
| Chuông thông báo + push | KH-12 | Phase 2 |
| Đổi bậc thợ | KH-14 | Phase 2 |
| Chuyển trạng thái đơn hàng sang sản xuất / giao | KH-15 | Phase 2 |
| Tổ phó thay tổ trưởng theo giờ vào | KH-24 | Phase 2 |
| Lưu ảnh S3 | KH-25 | Phase 2 |

## Cách chạy
1. SQL Server 2019+. DB `NXMTD` đã có 66 bảng + 10 trigger (tạo từ file portable của Morgan). Nếu DB thiếu trigger: chạy `CREATE OR ALTER TRIGGER` theo phần 15 của `NX-MTD_Database.sql`.
2. BE: `dotnet user-secrets set "ConnectionStrings:Default" "<chuỗi kết nối>" --project be`, `dotnet user-secrets set "Jwt:Key" "<≥ 32 ký tự>" --project be`, rồi `dotnet run --project be --launch-profile http` (cổng 5080).
3. FE: `fe/.env` có `VITE_API_BASE_URL=http://localhost:5080`; `cd fe && npm ci && npm run dev` (cổng 5173). Hoặc chạy `run-dev.bat`.

## Tài khoản demo
| Tài khoản | Vai trò |
|---|---|
| ADMIN | Admin |
| TEST-QD | Quản đốc |
| TEST-TK | Thủ kho |
| TEST-TTC | Tổ trưởng tổ cắt |
| TEST-TTM | Tổ trưởng tổ may |
Mật khẩu: hỏi Khiêm (không ghi vào repo). Thêm tài khoản Cơ điện / Kế toán / Công nhân bằng màn "Nhân viên" của Admin (mật khẩu tạm hiện một lần).

## Kịch bản demo (10 phút)
| Phút | Người | Nội dung |
|---|---|---|
| 0–1 | Khiêm | Kiến trúc BE / FE / DB; nói rõ bản này có gì, chưa có gì |
| 1–3 | Khiêm | Admin thêm nhân viên Cơ điện (mật khẩu tạm) → đăng nhập bị buộc đổi mật khẩu → menu theo vai trò. Admin khóa tài khoản → phiên đó bị đẩy ra |
| 3–5 | Khiêm | Cơ điện gửi đơn Linh kiện kèm 2 ảnh → Admin xem "Đơn cần xử lý" → từ chối **không** lý do (báo lỗi), có lý do → Cơ điện thấy trạng thái + lịch sử |
| 5–7 | Khiêm | Admin tạo mã hàng + đơn hàng 2 màu × 3 size; sửa đơn, xem lịch sử thay đổi |
| 7–9 | Thái | Nhập NVL của đơn, phiếu nhập, cấp NVL, lệnh cắt |
| 9–10 | Kiều / Minh | Công đoạn + đơn giá; đồng bộ chấm công |

## Lỗi đã biết
| # | Mô tả | Ảnh hưởng demo | Người sửa | Hạn |
|---|---|---|---|---|
| 1 | Giao diện chưa được bấm thử trên trình duyệt với backend thật | Có thể còn lỗi giao diện nhỏ | Khiêm / Thái | trước demo |
| 2 | Tab "NVL của đơn" trong chi tiết đơn hàng chưa gắn (màn NVL của đơn đang ở `/warehouse/order-materials`) | Không | Thái | — |
| 3 | `/common/lookups/orders` (kho) và `/common/order-lookups` (KH-08) song song | Không | Khiêm + Thái | Phase 2 |

## Khác biệt so với tài liệu
Xem `docs/CODE_DEVIATIONS.md` (DEV-KH-06-1, DEV-KH-06-2, DEV-KH-07-1, DEV-KH-08-1, DEV-KH-10-1, DEV-KH-10-2).

## Checklist đóng gói (Phase M)
- [ ] `develop` của 2 repo build + test xanh trên CI
- [ ] DB demo tạo lại được từ đầu; 10 trigger có mặt
- [ ] Bảng bấm tay (smoke test) dòng 1–6 đều ✓
- [ ] Tag `review-1` đã push (chỉ tag khi cả nhóm đã merge xong)
