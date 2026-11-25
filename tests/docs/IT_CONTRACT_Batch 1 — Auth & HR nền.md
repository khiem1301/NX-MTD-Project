# IT_CONTRACT — Batch 1: Auth & HR nền

Nguồn: 21 file Controller / DTO / Entity của Batch 1, cộng `AdminBaseController`, `ManagerBaseController`, `CommonBaseController`, `Domain/Constants/Roles.cs`, và `NxMtdDbContext.cs` (chỉ danh sách DbSet + `OnModelCreating`). **Không đọc Service/Repository** (quy tắc tách biệt ngữ cảnh), 6 file `Data/Configurations` (Account, Employee, Role, Team, GradeLevel, GradeLevelCode); không đọc entity `AuditLog` hay cấu hình các bảng khác.

**Route prefix và policy mặc định (đã xác nhận từ BaseController)**
| Nhóm | Prefix | Policy cấp class |
|---|---|---|
| Common | `/api/v1/common/[controller]` | Không có (từng controller tự khai báo `[Authorize]`) |
| Admin | `/api/v1/admin/[controller]` | `[Authorize(Roles = "Admin")]` |
| Manager | `/api/v1/manager/[controller]` | `[Authorize(Roles = "Admin,Manager")]` — Admin cũng gọi được mọi API Manager |

**Role (`Roles.cs`)**: Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, QaQc, Electrician, Worker. Role claim dùng giá trị PascalCase này; cột `Role.RoleCode` trong DB dùng dạng `ADMIN`, `MANAGER`, `ACCOUNTANT`, `WAREHOUSE_KEEPER`, `TEAM_LEADER`, `TEAM_DEPUTY`, `QAQC`, `ELECTRICIAN`, `WORKER`.
- `Roles.StaffExceptWorker` = `Admin,Manager,Accountant,WarehouseKeeper,TeamLeader,TeamDeputy,QaQc,Electrician` (mọi vai trò trừ Worker). Áp dụng cho `HrLookups` và `GradeCodeOptions`.
- `Roles.AdminOrManager` = `Admin,Manager`.
- `[X]` = suy ra trực tiếp từ controller/DTO/entity/comment. `[?]` = suy đoán từ cấu trúc entity, **cần xác nhận khi chạy IT** vì logic nằm ở Service.
- Mọi endpoint trả qua `this.ToResponse(Result…)` (envelope chưa đọc).

---

## 1. Bảng Endpoints

### 1.1 Auth — `/api/v1/common/Auth`
| Method | URL | Role/Policy | Tham số |
|---|---|---|---|
| POST | `/api/v1/common/Auth/login` | `[AllowAnonymous]` | Body `LoginDto{Username, Password}` |
| POST | `/api/v1/common/Auth/refresh` | `[AllowAnonymous]` | Body `RefreshTokenDto{RefreshToken}` |
| POST | `/api/v1/common/Auth/logout` | `[Authorize]` (mọi vai trò) | — |
| GET | `/api/v1/common/Auth/me` | `[Authorize]` | — (AccountId lấy từ `ICurrentUser`) |

### 1.2 Profile — `/api/v1/common/Profile` (`[Authorize]` cấp class, mọi vai trò)
| Method | URL | Role/Policy | Tham số |
|---|---|---|---|
| GET | `/api/v1/common/Profile` | Authorize | — |
| PUT | `/api/v1/common/Profile` | Authorize | Body `UpdateProfileDto{DateOfBirth, Phone}` |
| POST | `/api/v1/common/Profile/change-password` | Authorize | Body `ChangePasswordDto{CurrentPassword?, NewPassword, ConfirmPassword}` |

### 1.3 Lookups chung — `/api/v1/common/Lookups` (`[Authorize]`, mọi vai trò)
| Method | URL | Tham số |
|---|---|---|
| GET | `/api/v1/common/Lookups/orders` | Query `keyword?`, `sewingTeamId?` |
| GET | `/api/v1/common/Lookups/orders/{orderId:int}/lines` | Route `orderId` |
| GET | `/api/v1/common/Lookups/order-lines/{orderLineId:int}/components` | Route `orderLineId` |
| GET | `/api/v1/common/Lookups/teams` | Query `teamType?` |
| GET | `/api/v1/common/Lookups/materials` | Query `keyword?` |
| GET | `/api/v1/common/Lookups/scrap-materials` | — |
| GET | `/api/v1/common/Lookups/requisition-materials` | Query `orderId?` |
| GET | `/api/v1/common/Lookups/orders/{orderId:int}/requisition-pieces` | Route `orderId` |

### 1.4 HR Lookups — `/api/v1/common/HrLookups` (`[Authorize(Roles = Roles.StaffExceptWorker)]` = mọi role trừ Worker)
| Method | URL | Tham số |
|---|---|---|
| GET | `/api/v1/common/HrLookups/teams` | — |
| GET | `/api/v1/common/HrLookups/team-members` | Query `teamId` (int, không nullable) |

### 1.5 Grade code options — `/api/v1/common/GradeCodeOptions` (`StaffExceptWorker`)
| Method | URL | Tham số |
|---|---|---|
| GET | `/api/v1/common/GradeCodeOptions` | — (chỉ bậc đang dùng: `GetCodesAsync(true)`) |

### 1.6 Employees — `/api/v1/admin/Employees` (role: Admin) và `/api/v1/manager/Employees` (role: Admin, Manager)
Hai controller cùng cấu trúc; khác nhau **chỉ ở** body của PUT (và quyền dữ liệu, xem mục 4).

| Method | URL (sau prefix) | Tham số | Khác biệt Admin / Manager |
|---|---|---|---|
| GET | `/Employees` | Query `EmployeeQuery{TeamId?, Role?, Status?}` + `PageQuery` (field chưa đọc) | — |
| GET | `/Employees/{id:int}` | Route `id` | Admin: `Account` có dữ liệu; Manager: `Account = null` |
| POST | `/Employees` | Body `CreateEmployeeDto` · `ResponseCache(NoStore)` | — |
| POST | `/Employees/from-rj` | Body `ImportEmployeeFromRjDto` · NoStore | — |
| PUT | `/Employees/{id:int}` | Route `id`; Body `AdminUpdateEmployeeDto` (Admin) / `ManagerUpdateEmployeeDto` (Manager) | Cùng field, khác kiểu |
| POST | `/Employees/{id:int}/resign` | Route `id`; Body `ResignEmployeeDto{ResignDate, Reason, RowVer}` | — |
| POST | `/Employees/{id:int}/account/lock` | Route `id` (= EmployeeId); Body `LockAccountDto{Reason}` | — |
| POST | `/Employees/{id:int}/account/unlock` | Route `id` | — |
| POST | `/Employees/{id:int}/account/reset-password` | Route `id` · NoStore | — |

### 1.7 Teams — `/api/v1/admin/Teams` (role: Admin) và `/api/v1/manager/Teams` (role: Admin, Manager); hai controller giống hệt, cùng `ITeamService`
| Method | URL (sau prefix) | Tham số |
|---|---|---|
| GET | `/Teams` | Query `includeDiscontinued` (bool, mặc định false), `type?` |
| GET | `/Teams/{id:int}` | Route `id` |
| POST | `/Teams` | Body `SaveTeamDto{TeamCode, TeamType}` |
| PUT | `/Teams/{id:int}` | Route `id`; Body `SaveTeamDto` |
| POST | `/Teams/{id:int}/discontinue` | Route `id` |
| POST | `/Teams/{id:int}/reactivate` | Route `id` |
| POST | `/Teams/{id:int}/transfer-in` | Route `id`; Body `TransferMembersDto{EmployeeIds[], EffectiveDate, Reason?}` |
| GET | `/Teams/candidates` | Query `teamId?` (có → `CandidatesAsync(id)`, không → `CandidatesAsync()`) |
| POST | `/Teams/{id:int}/add-members` | Route `id`; Body `TeamMembersDto{EmployeeIds[], Reason?}` |
| POST | `/Teams/{id:int}/remove-members` | Route `id`; Body `TeamMembersDto` |
| POST | `/Teams/{id:int}/dismiss` | Route `id`; Body `DismissDto{EmployeeId}` |
| POST | `/Teams/{id:int}/appoint` | Route `id`; Body `AppointDto{EmployeeId, Position}` |

### 1.8 Grade levels — `/api/v1/admin/GradeLevels` (role: Admin)
| Method | URL | Tham số |
|---|---|---|
| GET | `/api/v1/admin/GradeLevels` | — |
| POST | `/api/v1/admin/GradeLevels` | Body `SaveGradeLevelDto{GradeCode, MonthlyAmount, EffectiveDate}` |
| PUT | `/api/v1/admin/GradeLevels/{id:int}` | Route `id`; Body `UpdateGradeLevelDto{MonthlyAmount, EffectiveDate}` |
| DELETE | `/api/v1/admin/GradeLevels/{id:int}` | Route `id` |

### 1.9 Grade codes — `/api/v1/admin/GradeCodes` (role: Admin)
| Method | URL | Tham số |
|---|---|---|
| GET | `/api/v1/admin/GradeCodes` | — (`GetCodesAsync(false)` = gồm cả bậc ngừng dùng) |
| POST | `/api/v1/admin/GradeCodes` | Body `SaveGradeCodeDto{GradeCode}` |
| PUT | `/api/v1/admin/GradeCodes/{code}` | Route `code` (string); Body `UpdateGradeCodeDto{Status}` |
| DELETE | `/api/v1/admin/GradeCodes/{code}` | Route `code` (string) |

---

## 2. Ràng buộc dữ liệu (Input & DB Schema)

**Trạng thái cập nhật:** `NxMtdDbContext.OnModelCreating` chỉ đặt collation `Vietnamese_100_CI_AS` (so sánh chuỗi không phân biệt hoa/thường, nhạy dấu — ảnh hưởng đến UNIQUE trên cột string) rồi gọi `ApplyConfigurationsFromAssembly`. Độ dài, UNIQUE, `decimal(p,s)`, FK dưới đây lấy từ 6 file `Infrastructure/Data/Configurations/{Account,Employee,Role,Team,GradeLevel,GradeLevelCode}Configuration.cs`. Quy ước chung: chuỗi `varchar` (IsUnicode=false) trừ khi ghi `nvarchar`; `DateTime` là `datetime2(0)`; các cột `CreatedAt` có default `dbo.fn_VnNow()`.

**Lưu ý quan trọng cho QA:** giá trị lưu trong DB của `Status` là **chữ HOA** (`ACTIVE` là default ở Account, Employee, Team, GradeLevelCode) trong khi DTO/query dùng PascalCase (`Active`/`Resigned`/`All`). Cần kiểm tra mapping khi assert DB.

Hiện trạng nguồn (còn áp dụng cho validation DTO):
- "NOT NULL" = suy từ nullable reference type (`string` không `?` ⇒ NOT NULL; `?` ⇒ NULL).
- "Độ dài tối đa", "UNIQUE", `decimal` đã điền từ các file Configuration (cột "Max length", "Unique", "Default").
- Các DTO không có DataAnnotation → không có validation khai báo ở tầng DTO; mọi kiểm tra (nếu có) nằm ở Service/validator, chưa đọc.

### 2.1 Bảng `Role`
| Cột | Kiểu DB | NOT NULL | Max length | Unique | FK |
|---|---|---|---|---|---|
| RoleId | int | ✔ | — | PK | — |
| RoleCode | varchar | ✔ | 30 | `UQ_Role_Code` | — |
| RoleName | nvarchar | ✔ | 100 | — | — |
| IsProduction | bit | ✔ | — | — | — |

Comment: 9 vai trò (gồm Tổ trưởng, Tổ phó). Được tham chiếu bởi `Employee.RoleId`, `AnnouncementTarget`.

### 2.2 Bảng `Account` (1–1 Employee)
| Cột | Kiểu DB | NOT NULL | Max length | Unique | Default | FK |
|---|---|---|---|---|---|---|
| AccountId | int | ✔ | — | PK | — | — |
| EmployeeId | int | ✔ | — | `UQ_Account_Employee` (1–1) | — | `FK_Account_Employee` → Employee.EmployeeId (ClientSetNull) |
| Username | varchar | ✔ | 50 | `UQ_Account_Username` | — | — |
| PasswordHash | nvarchar | ✔ | 500 | — | — | — |
| IsTemporaryPassword | bit | ✔ | — | — | `true` | — |
| SecurityStamp | uniqueidentifier | ✔ | — | — | `newid()` | — |
| Status | varchar | ✔ | 20 | — | `'ACTIVE'` | — |
| LockedAt | datetime2(0) | NULL | — | — | — | — |
| LockReason | nvarchar | NULL | 300 | — | — | — |
| CreatedAt | datetime2(0) | ✔ | — | — | `fn_VnNow()` | — |

Username comment: sinh từ họ tên (vd `ThaiPH`), không đổi sau khi tạo.

### 2.3 Bảng `Employee` (có trigger `trg_Employee_TeamLeadership`)
| Cột | Kiểu DB | NOT NULL | Max length | Unique | Default | FK |
|---|---|---|---|---|---|---|
| EmployeeId | int | ✔ | — | PK | — | — |
| EmployeeCode | varchar | ✔ | 20 | `UQ_Employee_Code` | — | — |
| FullName | nvarchar | ✔ | 100 | — | — | — |
| Gender | char (fixed) | NULL | 1 | — | — | — |
| DateOfBirth | date | NULL | — | — | — | — |
| Phone | varchar | NULL | 15 | — | — | — |
| AvatarUrl | nvarchar | NULL | 500 | — | — | — |
| RoleId | int | ✔ | — | — | — | `FK_Employee_Role` → Role.RoleId (ClientSetNull) |
| TeamId | int | NULL | — | — (index `IX_Employee_Team`) | — | `FK_Employee_Team` → Team.TeamId |
| PayType | varchar | ✔ | 20 | — | — | — |
| CurrentGradeCode | varchar | NULL | 5 | — | — | `FK_Employee_Grade` → GradeLevelCode.GradeCode |
| AttendanceCode | int | NULL | — | `UX_Employee_AttendanceCode` (unique có filter `AttendanceCode IS NOT NULL`) | — | — |
| StartDate | date | ✔ | — | — | — | — |
| ResignDate | date | NULL | — | — | — | — |
| Status | varchar | ✔ | 20 | — | `'ACTIVE'` | — |
| CreatedAt | datetime2(0) | ✔ | — | — | `fn_VnNow()` | — |
| UpdatedAt | datetime2(0) | NULL | — | — | — | — |
| RowVer | rowversion | ✔ | — | — | — (concurrency token) | — |

Hệ quả test: `Phone` tối đa 15 ký tự và `Gender` đúng 1 ký tự (độ dài vượt ngưỡng sẽ lỗi ở DB nếu không bị chặn trước); nhiều nhân viên có `AttendanceCode = NULL` là hợp lệ, trùng giá trị khác NULL thì vi phạm unique. Trigger `trg_Employee_TeamLeadership` có thể từ chối thay đổi RoleId/TeamId liên quan tổ trưởng/phó (nội dung trigger chưa đọc).

Lưu ý: `CreateEmployeeDto.Email` **không có cột tương ứng** trong `Employee`/`Account` → nơi lưu chưa xác định.

### 2.4 Bảng `Team`
| Cột | Kiểu DB | NOT NULL | Max length | Unique | Default | FK |
|---|---|---|---|---|---|---|
| TeamId | int | ✔ | — | PK | — | — |
| TeamCode | varchar | ✔ | 20 | `UQ_Team_Code` | — | — |
| TeamType | varchar | ✔ | 10 | — | — | — (DTO: Cutting / Sewing / Office) |
| Status | varchar | ✔ | 20 | — | `'ACTIVE'` | — |
| CreatedAt | datetime2(0) | ✔ | — | — | `fn_VnNow()` | — |

Team được tham chiếu bởi Employee, GradeEvaluation, WorkOrder, QcBatch, Request, … (xem navigation).

### 2.5 Bảng `GradeLevelCode` (danh mục bậc)
| Cột | Kiểu DB | NOT NULL | Max length | Unique | Default | FK |
|---|---|---|---|---|---|---|
| GradeCode | varchar | ✔ | 5 | PK | — | — |
| Status | varchar | ✔ | 10 | — | `'ACTIVE'` | — |
| CreatedAt | datetime2(0) | ✔ | — | — | `fn_VnNow()` | — |

Entity **không có** cột tên/thứ tự, dù comment ở `GradeCodesController` nói "sửa tên / thứ tự"; `UpdateGradeCodeDto` chỉ có `Status`. (Mâu thuẫn comment ↔ code — ghi nhận.)

### 2.6 Bảng `GradeLevel` (tiền bậc theo ngày hiệu lực)
| Cột | Kiểu DB | NOT NULL | Max length | Unique | Default | FK |
|---|---|---|---|---|---|---|
| GradeLevelId | int | ✔ | — | PK | — | — |
| GradeCode | varchar | ✔ | 5 | `UQ_GradeLevel` (GradeCode + EffectiveDate) | — | `FK_GradeLevel_Code` → GradeLevelCode.GradeCode (ClientSetNull) |
| MonthlyAmount | **decimal(18,2)** | ✔ | — | — | — | — |
| EffectiveDate | date | ✔ | — | (thuộc `UQ_GradeLevel`) | — | — |
| CreatedAt | datetime2(0) | ✔ | — | — | `fn_VnNow()` | — |

Hệ quả test: thêm hai dòng cùng `GradeCode` + `EffectiveDate` vi phạm unique; `GradeCode` dài tối đa 5 ký tự.

### 2.7 Input DTO (kiểu & bắt buộc theo khai báo C#)
| DTO | Field → kiểu (`?` = tùy chọn) |
|---|---|
| LoginDto | Username string, Password string |
| RefreshTokenDto | RefreshToken string |
| UpdateProfileDto | DateOfBirth DateOnly?, Phone string? (để trống = xóa giá trị) |
| ChangePasswordDto | CurrentPassword string?, NewPassword string, ConfirmPassword string |
| EmployeeQuery | TeamId int?, Role string? (PascalCase), Status string? (Active/Resigned/All; null = Active), + PageQuery |
| CreateEmployeeDto | EmployeeCode string, FullName string, Gender string?, DateOfBirth DateOnly?, Phone string?, Role string, TeamId int?, PayType string, StartDate DateOnly, AttendanceCode int?, Email string? (=null) |
| ImportEmployeeFromRjDto | AttendanceCode int, TeamId int?, PayType string?, StartDate DateOnly? |
| AdminUpdateEmployeeDto / ManagerUpdateEmployeeDto | FullName string, Gender string?, DateOfBirth DateOnly?, Phone string?, Role string, TeamId int?, PayType string, StartDate DateOnly, AttendanceCode int?, RowVer byte[] |
| ResignEmployeeDto | ResignDate DateOnly, Reason string, RowVer byte[] |
| LockAccountDto | Reason string |
| SaveTeamDto | TeamCode string, TeamType string |
| TransferMembersDto | EmployeeIds IReadOnlyList<int>, EffectiveDate DateOnly, Reason string? |
| TeamMembersDto | EmployeeIds IReadOnlyList<int>, Reason string? |
| DismissDto | EmployeeId int |
| AppointDto | EmployeeId int, Position string (Leader / Deputy) |
| SaveGradeLevelDto | GradeCode string, MonthlyAmount decimal, EffectiveDate DateOnly |
| UpdateGradeLevelDto | MonthlyAmount decimal, EffectiveDate DateOnly |
| SaveGradeCodeDto | GradeCode string |
| UpdateGradeCodeDto | Status string |

Ánh xạ DTO → cột: `Role` (string PascalCase) ↔ `Employee.RoleId` (int, qua `Roles.ToRoleCode` → `Role.RoleCode` unique); `Position` Leader/Deputy ↔ `RoleId`.

### 2.8 Output DTO đáng lưu ý cho assert
- `LoginResultDto{AccessToken, AccessExpiresAt, RefreshToken, RefreshExpiresAt, User: UserInfoDto}`; `UserInfoDto{AccountId, EmployeeId, Username, FullName, Role, RoleName, TeamId?, TeamCode?, TeamType?, IsTemporaryPassword}`.
- `CreatedEmployeeDto{EmployeeId, Username, TemporaryPassword}`; `TemporaryPasswordDto{Username, TemporaryPassword}` — trả **một lần**.
- `EmployeeDetailDto` có `RowVer`, `Account?` (chỉ Admin), `History[]`.
- `GradeLevelDto.IsCurrent`; `GradeCodeDto.InUse`.

---

## 3. Bảng DB chịu tác động khi gọi thành công

**Xác nhận từ danh sách DbSet của `NxMtdDbContext`:**
- Bảng `AuditLogs` **có tồn tại** (cùng `Employee.AuditLogs`, và `EmployeeDetailDto.History` / `TeamDetailDto.History` dùng `AuditLogDto`). Việc endpoint nào ghi vào bảng này vẫn là `[?]` vì quyết định nằm ở Service.
- **Không có** bảng `RefreshToken` (cũng không có `Session`/`Token`), và `Account` không có cột lưu refresh token ⇒ `login` / `refresh` **không có chỗ lưu token ở DB**. Việc login có cập nhật cột nào của `Account` hay không vẫn `[?]`.
- **Có** bảng `RjEmployeeSnapshots` ⇒ nguồn dữ liệu của `POST Employees/from-rj` (đọc theo `AttendanceCode`). Có thể bảng này cũng bị UPDATE (đánh dấu đã nhập) — `[?]`.
- Không có bảng nào riêng cho email ⇒ `CreateEmployeeDto.Email` không có chỗ lưu trong schema (xác nhận bất thường số 1 bên dưới).

Mọi dòng `AuditLog` trong bảng dưới là `[?]`.

| Endpoint | INSERT | UPDATE | DELETE |
|---|---|---|---|
| Auth login | — (không có bảng RefreshToken/Session) `[X]` | Account `[?]` (cột nào nếu có: không xác định) | — |
| Auth refresh | — `[X]` | — `[?]` | — |
| Auth logout | — (không lưu phiên ở BE, comment D3) `[X]` | — | — |
| Auth me / mọi GET | — | — | — |
| PUT Profile | — | Employee (DateOfBirth, Phone, UpdatedAt) `[X]`; AuditLog `[?]` | — |
| POST Profile/change-password | — | Account (PasswordHash, IsTemporaryPassword, SecurityStamp `[?]`) | — |
| POST Employees | Employee, Account `[X]`; AuditLog `[?]` | — | — |
| POST Employees/from-rj | Employee (Role = Worker), Account `[X]`; AuditLog `[?]` (đọc từ `RjEmployeeSnapshots`) | RjEmployeeSnapshots `[?]` | — |
| PUT Employees/{id} | — | Employee (gồm RowVer) `[X]`; AuditLog `[?]` | — |
| POST Employees/{id}/resign | — | Employee (ResignDate, Status) `[X]`; Account `[?]` (khả năng khóa/vô hiệu); AuditLog `[?]` | — |
| POST …/account/lock | AuditLog `[?]` | Account (Status, LockedAt, LockReason) `[X]` | — |
| POST …/account/unlock | AuditLog `[?]` | Account (Status; LockedAt/LockReason `[?]`) | — |
| POST …/account/reset-password | AuditLog `[?]` | Account (PasswordHash, IsTemporaryPassword, SecurityStamp `[?]`) | — |
| POST Teams | Team `[X]`; AuditLog `[?]` | — | — |
| PUT Teams/{id} | — | Team (TeamCode, TeamType) `[X]` | — |
| POST Teams/{id}/discontinue | — | Team.Status `[X]`; Employee.TeamId của thành viên `[?]` | — |
| POST Teams/{id}/reactivate | — | Team.Status `[X]` | — |
| POST Teams/{id}/transfer-in | AuditLog `[?]` | Employee.TeamId `[X]` | — |
| POST Teams/{id}/add-members | AuditLog `[?]` | Employee.TeamId `[X]` | — |
| POST Teams/{id}/remove-members | AuditLog `[?]` | Employee.TeamId (→ NULL) `[X]` | — |
| POST Teams/{id}/dismiss | AuditLog `[?]` | Employee.RoleId (→ Worker, comment DTO) `[X]` | — |
| POST Teams/{id}/appoint | AuditLog `[?]` | Employee.RoleId (→ Leader/Deputy); người đang giữ chức `[?]` | — |
| POST GradeLevels | GradeLevel `[X]` | — | — |
| PUT GradeLevels/{id} | — | GradeLevel (MonthlyAmount, EffectiveDate) `[X]` | — |
| DELETE GradeLevels/{id} | — | — | GradeLevel `[X]` |
| POST GradeCodes | GradeLevelCode `[X]` | — | — |
| PUT GradeCodes/{code} | — | GradeLevelCode.Status `[X]` | — |
| DELETE GradeCodes/{code} | — | — | GradeLevelCode `[X]` |

---

## 4. Quy tắc nghiệp vụ cơ bản (từ routing / attribute / DTO / comment)

**Xác thực & quyền sở hữu**
1. `login`, `refresh` ẩn danh; `logout`, `me` yêu cầu đăng nhập. Phiên **không lưu ở BE** (D3): logout chỉ trả message `LoggedOut`, FE tự xóa token.
2. `Me` và `Profile` không nhận id trong route: dữ liệu luôn của người dùng hiện tại (AccountId / Employee từ token). Profile mở cho **mọi vai trò**.
3. UC04: người dùng chỉ tự sửa được **DateOfBirth** và **Phone**; để trống = xóa giá trị. Không lộ kiểu lương, mã chấm công, trạng thái tài khoản.
4. `ChangePasswordDto.CurrentPassword` là nullable (lý do không nêu trong code đã đọc; liên quan `IsTemporaryPassword` là `[?]`). `ProfileDto`/`UserInfoDto` có `IsTemporaryPassword`.

**Phân quyền vùng HR**
5. `HrLookups` và `GradeCodeOptions` yêu cầu `Roles.StaffExceptWorker` (không dành cho Worker). `Lookups` mở cho mọi vai trò đăng nhập, chỉ đọc.
6. Admin: quản lý nhân viên + tài khoản (UC17), `Account` trong chi tiết chỉ Admin thấy. Manager (UC16): quản lý mọi người **trừ Admin và Kế toán**; không gán được vai trò Admin / Quản đốc / Kế toán, không thấy nhân viên thuộc các vai trò đó; `EmployeeDetailDto.Account = null`.
7. Tổ (UC18): Admin và Manager dùng chung `ITeamService`, bổ nhiệm tổ trưởng/phó theo D33.
8. Bậc thợ (UC15): chỉ Admin quản lý `GradeLevels` và `GradeCodes`.

**Trạng thái & dữ liệu**
9. Mật khẩu tạm (create, from-rj, reset-password) trả **một lần**, `ResponseCache(NoStore)`, không log, không lưu bản rõ (D29).
10. `CreateEmployeeDto.Role` chỉ nhận role văn phòng (Admin, Manager, Accountant, WarehouseKeeper, QaQc, Electrician); công nhân tạo qua `from-rj` với vai trò mặc định Worker, tổ trưởng/phó bổ nhiệm sau.
11. `Update` và `Resign` bắt buộc `RowVer` ⇒ khóa lạc quan; sai phiên bản là điều kiện lỗi `[?]` (mã lỗi chưa đọc).
12. `EmployeeQuery.Status`: `Active` / `Resigned` / `All`; null = Active. `Role` và `PayType` dùng PascalCase.
13. `lock/unlock/reset-password` dùng `{id}` = **EmployeeId** (không phải AccountId); lock bắt buộc `Reason`; resign bắt buộc `Reason` và `ResignDate`.
14. Tổ: `TeamType` ∈ Cutting / Sewing / Office; có vòng đời discontinue ⇄ reactivate (`includeDiscontinued=false` mặc định ẩn tổ đã ngừng); `add-members` dành cho nhân viên **chưa có tổ**; `dismiss` đưa người giữ chức về Công nhân của tổ; `Position` ∈ Leader / Deputy; `candidates` có hai chế độ theo `teamId`.
15. `transfer-in` có `EffectiveDate` (chuyển tổ theo ngày hiệu lực).
16. Tiền bậc: đổi tiền = **thêm dòng mới** theo `EffectiveDate`, không sửa dòng cũ; **sửa/xóa chỉ với mức chưa hiệu lực** (dòng đã hiệu lực là lịch sử lương). `GradeLevelDto.IsCurrent` đánh dấu dòng hiện hành.
17. Danh mục bậc: thêm, đổi trạng thái (ngừng dùng), **xóa chỉ khi `InUse = false`** (chưa có nhân viên, đánh giá hoặc tiền bậc dùng). `GradeCodeOptions` chỉ trả bậc đang dùng; `GradeCodes` trả tất cả.
18. Lookups: `orders` chỉ đơn "mở"; `materials` chỉ NVL active; `scrap-materials` dùng cho trả kho; `requisition-materials` / `requisition-pieces` kèm `AvailableQty` làm trần số lượng xin (DEV-ALL-110); `requisition-materials?orderId` giới hạn NVL của đơn.

**Bất thường cần QA thiết kế test case (giữ nguyên từ bản trước)**
1. `CreateEmployeeDto.Email` không có cột tương ứng trong `Employee`/`Account` (và không có bảng nào khác ở DbContext chứa email) — test: gửi Email hợp lệ/rỗng/sai định dạng, kiểm tra response và dữ liệu thực sự lưu.
2. Comment của `GradeCodesController` nói "sửa tên / thứ tự", nhưng entity `GradeLevelCode` không có cột đó và `UpdateGradeCodeDto` chỉ có `Status` — test: PUT chỉ đổi được Status; không có cách đổi tên/thứ tự.

**Còn chưa xác định (cần Configurations hoặc Service)**
- Độ dài tối đa, UNIQUE index, precision `MonthlyAmount`, giá trị hợp lệ của `Account.Status` / `Team.Status` / `GradeLevelCode.Status`.
- `login` có cập nhật `Account` hay không; mã lỗi/HTTP status của các vi phạm (RowVer, bậc đang dùng, mức đã hiệu lực).
- Manager có bị chặn truy cập nhân viên Admin/Accountant bằng 403 hay 404 (logic ở Service).
