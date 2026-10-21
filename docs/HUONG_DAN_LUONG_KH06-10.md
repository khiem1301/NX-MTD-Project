# Hướng dẫn các luồng đã code (KH-06 → KH-10)

> Cập nhật 24/10/2026. Nhánh: `feature/KH-07-shared-foundation`. Tài liệu gốc: `NX-MTD_KH06-10_AI_Runbook.md` (Phase H → N).
> Mục đích: giúp thành viên nhóm (Thái, Kiều, Minh, Khiêm) biết **đã có gì**, **gọi thế nào**, **thêm luồng của mình ra sao**, và **chạy thử bằng cách nào**.
> Quy ước: DB lưu mã `UPPER_SNAKE` (`PENDING_APPROVAL`), API nhận và trả `PascalCase` (`PendingApproval`). Giờ trả dạng `2026-10-22T08:30:00+07:00`, ngày trả `2026-10-22`.

---

## 1. Tổng quan

| Phase | Nội dung | Task | BE | FE |
|---|---|---|---|---|
| H | Nền dùng chung: giờ VN, phân trang, thông báo, tham số hệ thống, lỗi SQL → 409 | KH-07 | ✔ | ✔ |
| I | Nhật ký thay đổi (`AuditLog`) + ảnh đính kèm (`Attachment`) | KH-07 | ✔ | ✔ |
| J, K | Đơn từ: khung 10 loại đơn, duyệt / từ chối / hoàn thành | KH-06 | ✔ | ✔ |
| L | Khách hàng, mã hàng, đơn hàng màu × size, tra cứu cho luồng khác | KH-08 | ✔ | ✔ |
| M | Đóng gói Review 1 (`docs/REVIEW-1.md`) | KH-09 | — | — |
| N | Nhân viên, tài khoản, tổ, hồ sơ, đổi mật khẩu | KH-10 | ✔ | ✔ |

Cấu trúc code (repo này là **1 project**, namespace `NX_MTD.*`, khác với runbook mô tả 4 project):

```
be/
  Domain/Constants/        hằng số (RequestConstants, HrConstants, AttachmentConstants, OrderConstants...)
  Application/             Interfaces, DTOs, Validators, Services/{Requests,Orders,Hr}
  Infrastructure/          Repositories, Audit, Notifications, Storage, Settings, Security
  API/Controllers/{Admin,Manager,Common}/   Modules/{Shared,Request,Order,Hr}Module.cs
fe/src/
  features/{requests,orders,users,teams,profile}/
  components/common/{attachments,AuditHistory,NoteModal}   hooks/{useAttachments,useOrderLookups,useHrLookups}
```

---

## 2. Nền dùng chung (Phase H, I)

### 2.1 Những thứ mọi luồng nên dùng

| Cần | Dùng | Ghi chú |
|---|---|---|
| Giờ hiện tại | `IClock.Now` / `IClock.Today` | Giờ VN, làm tròn giây. Không dùng `DateTime.Now` |
| Phân trang | `PageQuery` + `ToPagedAsync` | `?page&pageSize&keyword&sortBy&sortDir`; trả `{ items, page, pageSize, totalItems }` |
| Ghi nhật ký | `IAuditLogService.Add(new AuditEntry(...))` | **Không tự `SaveChanges`**, đi cùng transaction của bạn |
| Gửi thông báo | `INotificationService.NotifyEmployees / NotifyRoleAsync` | Chỉ thêm dòng `Notification` (`PushStatus = SKIPPED`), chuông + push là KH-12 |
| Tham số hệ thống | `ISystemSettingService.GetIntAsync` | Ví dụ `REQUEST_MAX_PHOTOS` |
| Lưu `RowVer` an toàn | `repo.SetOriginalRowVersion(entity, rowVer)` | Lệch → 409 `COMMON_CONCURRENCY_CONFLICT` |
| Transaction nhiều bước | `uow.InTransactionAsync(async () => ...)` | Có sẵn từ trước |
| Lỗi nghiệp vụ | `Result.Failure(code, message)` (409) | `Fail.NotFound()` cho 404 |

### 2.2 Lỗi SQL tự đổi thành 409 (middleware)
- Trùng khóa (2601, 2627) → `COMMON_DUPLICATE`.
- Vi phạm khóa ngoại / CHECK (547) → `COMMON_CONSTRAINT_VIOLATION`.
- Lỗi do trigger `THROW 5xxxx` → `COMMON_DB_RULE` kèm nguyên văn câu tiếng Việt của trigger.

### 2.3 Ảnh đính kèm
- API (`/api/v1/common/attachments`): `POST` (multipart: `ownerType`, `ownerId`, `purpose`, `file`), `GET ?ownerType&ownerId`, `GET {id}/content` (cần token), `DELETE {id}` (chỉ đặt `IsCurrent = 0`).
- Chỉ nhận JPG / PNG / WEBP, ≤ 5 MB, tối đa 5 ảnh / chứng từ. Loại ảnh kiểm bằng **byte đầu file**, không tin đuôi tên.
- File lưu `App_Data/uploads` (gitignore), `Attachment.FileUrl` là **khóa lưu trữ** (không phải URL công khai) – xem `DEV-KH-07-1`.
- **Quyền theo loại chứng từ:** mỗi luồng đăng ký một `IAttachmentOwnerAccess`. Loại chưa ai đăng ký → chỉ Admin.
- FE: `<AttachmentGallery ownerType="PurchaseOrder" ownerId={id} canEdit maxFiles={5} />` (từ `@/components/common/attachments`).

---

## 3. Đơn từ (Phase J, K – UC07, UC08)

### 3.1 Ai tạo, ai xử lý (bảng `RequestTypePolicies`)

| Loại đơn (PascalCase) | Ai tạo | Ai xử lý | Cần duyệt |
|---|---|---|---|
| SpareParts (Linh kiện) | Cơ điện | Admin | Có |
| NewMachine (Máy mới) | Cơ điện | Admin | Có |
| MachineSupplies (Vật tư máy) | Cơ điện | Kế toán | Không |
| AttendanceCorrection (Chấm công bù) | Tổ trưởng / tổ phó tổ cắt, may | Kế toán | Có |
| MaterialSupplement (Bổ sung NVL) | Tổ trưởng / tổ phó, Thủ kho | Quản đốc | Có |
| DefectiveMaterial (NVL lỗi) | Tổ trưởng / tổ phó tổ **cắt** | Quản đốc | Có |
| ReplacementCutting (Cắt bù) | Tổ trưởng / tổ phó tổ **may** | Quản đốc | Có |
| ShortDelivery (Thiếu hàng khi nhập) | Hệ thống (không ai tạo tay) | Quản đốc | Không |
| MaterialRequisition (Xin NVL) | Tổ trưởng / tổ phó tổ cắt **và may** (`DEV-KH-06-1`) | Thủ kho | Không |
| SemiFinishedRequisition (Xin BTP) | Tổ trưởng / tổ phó tổ may | Thủ kho | Không |

### 3.2 Trạng thái
```
tạo ──► PendingApproval ──duyệt──► Approved ──hoàn thành──► Completed
   └──► PendingProcessing (loại không cần duyệt) ──hoàn thành──► Completed
PendingApproval / PendingProcessing ──từ chối (bắt buộc lý do)──► Rejected
PendingApproval / PendingProcessing ──người tạo hủy──► Cancelled
```
- Quyết định (duyệt / từ chối) **không đổi được** nữa. Người tạo chỉ sửa / hủy khi còn `Pending*`.
- Ai thấy đơn nào: tổ trưởng / tổ phó thấy mọi đơn của **tổ mình**; vai trò khác thấy đơn **mình tạo**; người xử lý thấy mọi đơn thuộc loại mình xử lý. Không có quyền xem → 404.
- Tổ phó luôn có quyền như tổ trưởng (`DEV-KH-06-2`).
- Tạo đơn **không** ghi AuditLog `INSERT` (dòng "Tạo đơn" lấy từ chính đơn); sửa / hủy = `UPDATE`, duyệt = `APPROVE`, từ chối = `REJECT`, hoàn thành = `CONFIRM`.

### 3.3 API
| Method | Route | Ai |
|---|---|---|
| GET | `/api/v1/common/requests/types` | Loại đơn người đăng nhập được tạo |
| GET / POST | `/api/v1/common/requests` | Đơn của tôi / của tổ tôi; tạo đơn |
| GET / PUT | `/api/v1/common/requests/{id}` | Chi tiết (kèm `permissions`, `history`, `maxPhotos`); sửa |
| POST | `/api/v1/common/requests/{id}/cancel` | Người tạo hủy |
| GET | `/api/v1/common/request-inbox` | Người xử lý (Admin, Quản đốc, Kế toán, Thủ kho); mặc định lọc đơn đang mở |
| POST | `/api/v1/common/request-inbox/{id}/approve \| reject \| complete` | Người xử lý |

`rowVer` là chuỗi base64 trong JSON: lấy từ chi tiết đơn và **gửi nguyên** khi sửa / hủy / duyệt / từ chối / hoàn thành.

### 3.4 Màn hình
- `/requests` "Đơn từ của tôi" (Cơ điện, tổ trưởng, tổ phó, Thủ kho): bảng, lọc, "Tạo đơn" (3 loại của Cơ điện, kèm ảnh).
- `/requests/inbox` "Đơn cần xử lý" (Admin, Quản đốc, Kế toán, Thủ kho).
- Chi tiết mở bằng Drawer theo `?id=` (chia sẻ được link). Nút Sửa / Hủy / Duyệt / Từ chối / Hoàn thành **chỉ hiện theo `permissions` server trả về**.

### 3.5 Thêm loại đơn của luồng mình (không sửa code của Khiêm)
```csharp
public sealed class MaterialRequisitionHandler : IRequestTypeHandler
{
    public string RequestType => RequestTypes.MaterialRequisition;
    public bool AllowManualComplete => false;   // đơn tự hoàn thành khi xong bước sau
    public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct) { /* kiểm dòng NVL... */ }
    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) => ...;
}
// Đăng ký trong module của bạn:
services.AddScoped<IRequestTypeHandler, MaterialRequisitionHandler>();
```
- Hoàn thành đơn từ nghiệp vụ của bạn: trong transaction của bạn gọi `IRequestService.MarkCompletedAsync(requestId, note, ct)`.
- Hệ thống tự tạo đơn (thiếu hàng khi nhập): `var req = await _requestService.StageSystemRequestAsync(draft, ct);` rồi gán `req.MaterialReceipt = receipt` và lưu cùng phiếu nhập.
- Muốn đổi ai tạo / ai xử lý một loại đơn: **nhắn Khiêm** (sửa bảng `RequestTypePolicies` + ghi DEV).
- Ảnh cho loại chứng từ khác: tạo `IAttachmentOwnerAccess` cho `OwnerType` của bạn (mẫu: `RequestAttachmentAccess`).

---

## 4. Khách hàng, mã hàng, đơn hàng (Phase L – UC09, UC10)

Chỉ **Admin** thấy và sửa (có đơn giá CMT). Các endpoint `Admin/`:

| Route | Chức năng |
|---|---|
| `/api/v1/admin/customers` GET, POST, PUT `{id}` | Khách hàng (không xóa) |
| `/api/v1/admin/products` GET, GET `{id}`, POST, PUT `{id}`, POST `{id}/discontinue \| reactivate` | Mã hàng (không xóa, chỉ ngừng dùng) |
| `/api/v1/admin/customer-orders` GET, GET `next-code`, GET `{id}`, POST, PUT `{id}`, POST `{id}/cancel \| stop` | Đơn hàng + dòng màu × size |

Quy tắc:
- Mã hàng / mã đơn tự `Trim` + chữ hoa. Mã hàng đã dùng trong đơn thì **không đổi mã** (đổi tên được). Mã hàng đã ngừng dùng không thêm vào đơn mới.
- Ảnh mẫu mã hàng là **đường dẫn** (chưa upload) – `DEV-KH-08-1`.
- Mã đơn gợi ý `DH<yyMM>-<NNN>` (`GET next-code`), sửa được khi đơn còn `New`.
- Đơn mới tạo: `Status = New`, `MaterialStatus = Awaiting`, gửi thông báo cho Quản đốc.
- **Sửa đơn:**
  - `New`: sửa tự do (thêm / sửa / xóa dòng; gửi **đủ** mọi dòng muốn giữ).
  - `InProduction` / `InDelivery` / `PartiallyDelivered`: chỉ đổi số lượng, đơn giá, **thêm** dòng. Không xóa dòng (`ORDER_LINE_REMOVE_NOT_ALLOWED`), không đổi mã hàng / màu / size dòng cũ (`ORDER_LINE_KEY_LOCKED`), không đổi mã đơn / khách / ngày nhận.
  - Trạng thái khác: không sửa (`ORDER_NOT_EDITABLE`).
- Hủy chỉ khi `New`. Dừng (`Stopped`, bắt buộc lý do) khi đang sản xuất / giao.
- Chuyển `New → InProduction`, giao hàng: là KH-15 (Phase 2), chưa làm.

### Tra cứu cho luồng khác (không có đơn giá)
`/api/v1/common/order-lookups` – mọi vai trò trừ Công nhân:
- `GET orders` (mặc định: đơn đang chạy), `GET orders/{id}/lines`, `GET products` (chỉ mã đang dùng).
- FE: `useOrderLookup`, `useOrderLineLookup`, `useProductLookup` (`@/hooks/useOrderLookups`).
- Song song vẫn còn `/common/lookups/orders` của luồng kho (Thái) – chưa gộp.

### FE
`/admin/orders` (danh sách), `/admin/orders/new`, `/admin/orders/:id` (chi tiết: lưới, lịch sử), `/admin/orders/:id/edit`, `/admin/products`, `/admin/customers`.
Lưới màu × size: mỗi khối là một mã hàng + đơn giá; hàng = màu, cột = size. Đơn đang sản xuất thì ô dòng cũ khóa, vẫn thêm màu / size mới.
**Tab "NVL của đơn"**: chỗ gắn ở `fe/src/app/orderDetailTabs.ts` (`ORDER_DETAIL_TABS`, hiện rỗng) – Thái thêm component của mình vào.

---

## 5. Nhân sự (Phase N – UC03, UC04, UC16, UC17, UC18)

### 5.1 Ai làm được gì
| Ai | Được làm |
|---|---|
| Mọi vai trò | Xem hồ sơ của mình (`/profile`), đổi mật khẩu |
| Admin | Mọi nhân viên: thêm (kèm tài khoản), sửa kể cả vai trò / tổ / kiểu lương / mã chấm công, cho nghỉ; khóa / mở khóa / đặt lại mật khẩu; quản lý tổ |
| Quản đốc | Nhân viên **trừ** Admin / Quản đốc / Kế toán (không thấy – 404): xem, sửa thông tin cá nhân + mã chấm công. Không thêm, không cho nghỉ, không đụng tài khoản. Quản lý tổ, chuyển tổ, bổ nhiệm tổ trưởng / tổ phó |

### 5.2 API
| Route | Ghi chú |
|---|---|
| `GET /common/profile`, `POST /common/profile/change-password` | Đổi mật khẩu trả **cặp token mới** cho phiên hiện tại; phiên khác bị đẩy ra |
| `/admin/employees` GET, GET `{id}`, POST, PUT `{id}` | POST trả **mật khẩu tạm 6 số, chỉ một lần** |
| `/admin/employees/{id}/resign`, `/account/lock`, `/account/unlock`, `/account/reset-password` | Khóa bắt buộc lý do; không tự khóa mình |
| `/manager/employees` GET, GET `{id}`, PUT `{id}` | Chỉ trường cho phép; trường thừa (vd. `role`) bị bỏ qua |
| `/admin/teams` và `/manager/teams`: GET, GET `{id}`, POST, PUT, `discontinue`, `reactivate`, `transfer-in`, `appoint` | Cùng một service cho 2 vai trò |
| `GET /common/hr-lookups/teams`, `/team-members?teamId=` | Tổ trưởng / tổ phó chỉ tra tổ mình |

### 5.3 Quy tắc cần nhớ
- Thêm nhân viên **luôn** tạo tài khoản: tên đăng nhập = mã NV, mật khẩu tạm ngẫu nhiên. Đăng nhập lần đầu bị chuyển tới `/profile?changePassword=1` cho đến khi đổi mật khẩu.
- Đổi vai trò, đổi tổ, khóa, đặt lại mật khẩu, cho nghỉ, bổ nhiệm → đổi `SecurityStamp` → người đó **phải đăng nhập lại** (token cũ mang quyền cũ).
- Mã chấm công đặt qua thủ tục `usp_Attendance_MapCode` (gán lại lượt quẹt cũ, tính lại công). Mã đã có người giữ → `EMPLOYEE_ATTENDANCE_CODE_TAKEN`.
- Mỗi tổ cắt / may chỉ có **1 tổ trưởng + 1 tổ phó**: service báo lỗi có tên người đang giữ; trigger `trg_Employee_TeamLeadership` là lưới an toàn.
- **Bổ nhiệm** (`appoint`): người được chọn phải là Công nhân / tổ trưởng / tổ phó của tổ đó; người đang giữ chức tự chuyển về Công nhân, giữ kiểu lương và bậc (`DEV-KH-10-2`, **chờ Morgan xác nhận**).
- **Chuyển tổ:** ngày hiệu lực ≤ hôm nay, `TeamId` đổi ngay, ngày ghi trong lịch sử (`DEV-KH-10-1`). Không chuyển được tổ trưởng / tổ phó (phải bổ nhiệm người khác trước).
- Không đổi bậc thợ ở đây (KH-14).

### 5.4 FE
`/profile`, `/admin/employees`, `/manager/employees` (cùng trang, API theo vai trò), `/admin|manager/employees/:id`, `/teams` (Drawer chi tiết `?id=`: thành viên, bổ nhiệm, chuyển tổ, lịch sử).

---

## 6. Chạy thử

### 6.1 DB test
DB dev `NXMTD` (đã có 66 bảng, 10 trigger). Để không làm bẩn dữ liệu, test trên bản sao:
1. Sao lưu `NXMTD` (copy-only) rồi khôi phục thành `NXMTD_Test`.
2. Thêm vài tài khoản test (Cơ điện, Kế toán, Công nhân) copy `PasswordHash` của `ADMIN`.
3. Chạy API trỏ DB test mà không sửa user-secrets:
```bash
export ConnectionStrings__Default="Server=localhost;Database=NXMTD_Test;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5081
cd be && dotnet run --no-launch-profile
```
Dùng `sqlcmd -I` khi chèn dữ liệu (DB có index lọc, cần `QUOTED_IDENTIFIER ON`).

### 6.2 Lệnh kiểm tra
```bash
dotnet test tests/Application.Tests          # BE: 116 test
cd fe && npm run typecheck && npm run lint && npx jest --maxWorkers=2 && npm run build
```
Jest chậm khi máy tải nặng (antd trong jsdom): nếu test báo quá 20 giây, chạy lại với `--maxWorkers=2`. Trong test dùng `getByText` / class CSS thay `getByRole` cho DOM antd lớn.

---

## 7. Kết quả kiểm thử (24/10/2026)

| Hạng mục | Kết quả |
|---|---|
| BE xUnit | 116 pass |
| FE jest | 61 pass (19 suite); typecheck, lint, build sạch |
| API thật (DB `NXMTD_Test`) – đơn từ, ảnh | Đúng: 403 Công nhân, Kế toán không duyệt đơn Linh kiện, từ chối bắt buộc lý do, 409 lệch `rowVer`, thông báo vào `Notification`, ảnh sai loại bị từ chối, Công nhân không xem được ảnh đơn người khác |
| API thật – mã hàng, đơn hàng | Đúng: khóa dòng khi đang sản xuất, không đổi mã hàng đã dùng, mã hàng ngừng dùng bị chặn, tra cứu không có giá |
| API thật – nhân sự | Đúng: mật khẩu tạm, đổi mật khẩu đẩy phiên cũ ra, Quản đốc không thấy Admin / Kế toán, bổ nhiệm hạ người cũ, chuyển tổ, khóa / reset, nghỉ việc, audit không chứa mật khẩu |
| Bấm tay trên giao diện (trình duyệt) | **Chưa kiểm** |

## 8. Việc còn lại / cần quyết
- **Chưa làm:** H6 (file migrate trigger – thiếu `NX-MTD_Database.sql`; DB `NXMTD` đã có đủ trigger nên chưa chặn), tag `review-1` (cần cả nhóm merge xong).
- **Chờ Morgan:** D33 / `DEV-KH-10-2` (bổ nhiệm hạ người cũ; Quản đốc đổi vai trò qua bổ nhiệm).
- **Để sau:** chuông + push thông báo (KH-12), tổ phó thay tổ trưởng theo giờ vào (KH-24), đổi bậc thợ (KH-14), chuyển trạng thái đơn hàng `New → InProduction` (KH-15), S3 cho ảnh (KH-25), upload ảnh mẫu mã hàng.
- **Tài liệu cần sửa theo `CODE_DEVIATIONS.md`:** Report-3 UC07, UC10, UC16, UC18; `NX-MTD_AI_Context.md`; comment cột `Attachment.FileUrl`.
- **Sửa quy tắc:** `NX-MTD_Coding_Rules.md` mục 3.2 – thêm ngoại lệ "chức năng dùng chung nhiều vai trò, quyền theo dữ liệu (đơn từ, ảnh, hồ sơ, tra cứu) đặt ở `Controllers/Common`".
