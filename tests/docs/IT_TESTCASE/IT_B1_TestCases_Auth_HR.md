# Integration Test Cases — Batch 1: Auth & HR nền (`IT_B1_xxx`)

Nguồn: `IT_CONTRACT — Batch 1: Auth & HR nền`. Phân loại theo ISTQB: **Normal** (luồng hợp lệ), **Abnormal** (đầu vào/trạng thái không hợp lệ, phân quyền, xung đột), **Boundary** (giá trị biên: độ dài, số, ngày, đồng thời).

## 0. Quy ước chung

**Mã trạng thái HTTP kỳ vọng**
- `401`: thiếu / hết hạn / sai chữ ký token. `403`: đúng token nhưng sai role (chặn ở prefix hoặc `[Authorize(Roles)]`).
- `400`: dữ liệu đầu vào sai hoặc vi phạm quy tắc nghiệp vụ. `404`: không tìm thấy tài nguyên, hoặc route constraint `{id:int}` không khớp.
- `409`: trùng UNIQUE, sai `RowVer`, xung đột trạng thái/chức vụ.
- **Mọi lỗi phải xử lý êm. Bất kỳ `500` nào đều là FAIL**, kể cả khi DB vẫn đúng.
- "`403/404`" = hợp đồng chưa chốt cách chặn IDOR. Cả hai đều PASS, miễn là **không lộ dữ liệu**.
- `[?]` = hành vi nằm ở Service và chưa được xác nhận. Ghi lại hành vi thực tế làm kết quả, và đánh FAIL nếu thấy `500` hoặc dữ liệu DB không nhất quán.

**Quy ước cho vế [DB]**
- `Status` trong DB luôn **VIẾT HOA**: `ACTIVE`, `RESIGNED`. Các giá trị chưa xác nhận: `LOCKED` (Account), `DISCONTINUED` (Team), `INACTIVE` (GradeLevelCode). Mỗi khi dùng các giá trị này đều có đánh dấu `[?]`. Assert thêm rằng **không có giá trị PascalCase** (`Active`, `Resigned`) bị lưu nhầm.
- `RoleId` assert qua join `Role.RoleCode` (`WORKER`, `TEAM_LEADER`, `TEAM_DEPUTY`, …).
- "AuditLogs +1" = có đúng 1 dòng mới trong `AuditLogs` gắn với đối tượng bị tác động, và hành động khớp thao tác. Tên cột/giá trị action là `[?]`. Thao tác thất bại thì **không** được sinh dòng AuditLog.
- "DB không đổi" = snapshot các bảng liên quan (`Employee`, `Account`, `Team`, `GradeLevel`, `GradeLevelCode`, `AuditLogs`) trước và sau khi gọi trùng nhau (gồm cả `RowVer`).

**Dữ liệu nền S0** (reset trước mỗi test, ví dụ bằng Respawn hoặc transaction rollback). Tất cả `ACTIVE`, mật khẩu `P@ssw0rd!` (không tạm) trừ khi ghi khác.

| Alias | Username | Role | Tổ | AttendanceCode | Ghi chú |
|---|---|---|---|---|---|
| E_ADM | admin01 | Admin | — | NULL | Người gọi Admin |
| E_ADM2 | admin02 | Admin | — | NULL | Mục tiêu IDOR |
| E_MGR | mgr01 | Manager | — | NULL | Người gọi Manager |
| E_ACC | acc01 | Accountant | — | NULL | Mục tiêu IDOR |
| E_WH | wh01 | WarehouseKeeper | — | NULL | Đối tượng sửa/khóa |
| E_TL1 | tl01 | TeamLeader | T_SEW01 | 1101 | |
| E_TD1 | td01 | TeamDeputy | T_SEW01 | 1102 | |
| E_W1 | wk01 | Worker | T_SEW01 | 1001 | `CurrentGradeCode = B1` |
| E_W2 | wk02 | Worker | T_SEW02 | 1002 | |
| E_W5 | wk05 | Worker | T_SEW02 | 1005 | |
| E_W3 | wk03 | Worker | NULL | NULL | Chưa có tổ |
| E_TL2 | tl02 | TeamLeader | T_CUT01 | 1201 | |
| E_W4 | wk04 | Worker | T_CUT01 | 1004 | |
| E_RS | rs01 | Worker | NULL | 1009 | `Status = RESIGNED` |
| E_TMP | tmp01 | QaQc | — | NULL | `IsTemporaryPassword = 1` |
| E_LCK | lck01 | Electrician | — | NULL | Account `Status` = khóa `[?]` |

- **Team:** T_SEW01 (Sewing, có Leader E_TL1 và Deputy E_TD1); T_SEW02 (Sewing, chưa có Leader/Deputy, thành viên E_W2, E_W5); T_CUT01 (Cutting, có Leader E_TL2, chưa có Deputy, thành viên E_W4); T_EMPTY (Sewing, không có thành viên); T_OLD (Sewing, đã ngừng `[?]`).
- **GradeLevelCode:** B1 (`ACTIVE`, đang được E_W1 dùng); B2 (`ACTIVE`, chưa ai dùng); B3 (`ACTIVE`, chỉ có dòng GradeLevel); B9 (ngừng dùng `[?]`).
- **GradeLevel:** GL1 (B1, 2025-01-01, đang hiện hành); GL2 (B1, hôm nay + 30, chưa hiệu lực); GL3 (B3, 2025-01-01).
- **RjEmployeeSnapshots:** 2001 ("Nguyễn Văn An", chưa nhập); 2002 (chưa nhập); 1001 (đã nhập = E_W1).
- **Token:** `TK_<alias>` lấy bằng login. `TK_EXP` là token đã hết hạn. `TK_FORGED` là token bị sửa claim role nên sai chữ ký.

Viết tắt URL: `A/` = `/api/v1/admin/`, `M/` = `/api/v1/manager/`, `C/` = `/api/v1/common/`.

---

## 1. Auth — `C/Auth`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_001 | S0 | `POST C/Auth/login` `{Username:"admin01", Password:"P@ssw0rd!"}` | [API]: 200; có `AccessToken`, `RefreshToken`; `AccessExpiresAt` < `RefreshExpiresAt`, cả hai ở tương lai; `User.Role="Admin"`, `User.RoleName` khác rỗng, `IsTemporaryPassword=false`.<br>[DB]: không có bảng token/session nào thay đổi; `Account.PasswordHash`, `SecurityStamp`, `Status='ACTIVE'` không đổi. Có cột nào của Account được cập nhật hay không là `[?]`, cần ghi lại. | Normal |
| IT_B1_002 | S0 | login `{tmp01, P@ssw0rd!}` | [API]: 200; `User.IsTemporaryPassword=true`.<br>[DB]: `Account.IsTemporaryPassword=1` vẫn giữ nguyên. | Normal |
| IT_B1_003 | S0 | login `{tl01, …}` | [API]: 200; `User.TeamId`=T_SEW01, `TeamCode="T_SEW01"`, `TeamType="Sewing"`, `Role="TeamLeader"`.<br>[DB]: khớp `Employee.TeamId` join `Team`; `Role.RoleCode='TEAM_LEADER'`. | Normal |
| IT_B1_004 | S0 | login `{admin01, "SaiMatKhau"}` | [API]: 401; không có token trong body.<br>[DB]: không đổi. | Abnormal |
| IT_B1_005 | S0 | login `{"khongtontai", "P@ssw0rd!"}` | [API]: 401, message **giống hệt** IT_B1_004 (chống dò username).<br>[DB]: không đổi. | Abnormal |
| IT_B1_006 | S0 | login `{Username:"", Password:""}`; hoặc body `{}` | [API]: 400 (hoặc 401); không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B1_007 | E_LCK đang bị khóa | login `{lck01, P@ssw0rd!}` | [API]: 401/403; không cấp token.<br>[DB]: `Account.Status` vẫn là giá trị khóa `[?]`, `LockedAt` không đổi. | Abnormal |
| IT_B1_008 | E_RS đã `RESIGNED` | login `{rs01, P@ssw0rd!}` | [API]: 401/403; không cấp token.<br>[DB]: không đổi. | Abnormal |
| IT_B1_009 | S0 | login `{Username:"admin01' OR '1'='1' --", Password:"x"}` | [API]: 401; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B1_010 | S0 | login với Username dài 50 ký tự, và dài 51 ký tự (không tồn tại) | [API]: cả hai trả 401 (hoặc 400 với 51 ký tự); không 500.<br>[DB]: không đổi. | Boundary |
| IT_B1_011 | S0 | login `{"ADMIN01", "P@ssw0rd!"}` (khác hoa/thường) | [API]: 200 hoặc 401 `[?]`, tùy collation `CI_AS`; ghi lại hành vi; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B1_012 | Đã login admin01 | `POST C/Auth/refresh` `{RefreshToken:<hợp lệ>}` | [API]: 200; AccessToken mới gọi `C/Auth/me` thành công.<br>[DB]: không có INSERT nào (không có bảng RefreshToken). | Normal |
| IT_B1_013 | S0 | refresh `{RefreshToken:"abc.def.ghi"}` | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_014 | Đã login | refresh dùng **AccessToken** thay cho refresh token | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_015 | Đã login wk01 rồi đổi mật khẩu (SecurityStamp đổi `[?]`) | refresh bằng refresh token cũ | [API]: 401 `[?]` (token cũ bị vô hiệu theo SecurityStamp); ghi lại.<br>[DB]: không đổi thêm. | Abnormal |
| IT_B1_016 | Đã login wh01, sau đó Admin khóa wh01 | refresh bằng token của wh01 | [API]: 401/403.<br>[DB]: Account vẫn ở trạng thái khóa. | Abnormal |
| IT_B1_017 | RefreshToken vừa quá `RefreshExpiresAt` 1 giây (giả lập thời gian); và còn 1 giây trước hạn | refresh | [API]: quá hạn → 401; trước hạn → 200.<br>[DB]: không đổi. | Boundary |
| IT_B1_018 | TK_ADM | `POST C/Auth/logout` | [API]: 200, message `LoggedOut`.<br>[DB]: không đổi (phiên không lưu ở BE, theo D3). | Normal |
| IT_B1_019 | — | logout không gửi token | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_020 | TK_W1 | `GET C/Auth/me` | [API]: 200; `AccountId`/`EmployeeId` khớp wk01; `Role="Worker"`, `TeamCode="T_SEW01"`.<br>[DB]: dữ liệu trả về khớp `Account` join `Employee` join `Team`. | Normal |
| IT_B1_021 | TK_EXP | `GET C/Auth/me` | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_022 | TK_FORGED (claim role sửa từ Worker thành Admin) | `GET A/Employees` | [API]: 401 (sai chữ ký); không trả dữ liệu.<br>[DB]: không đổi. | Abnormal |
| IT_B1_023 | TK_W1 | `GET C/Auth/me?accountId=<AccountId của admin01>` (IDOR) | [API]: 200 nhưng trả dữ liệu **của wk01**; tham số query bị bỏ qua.<br>[DB]: không đổi. | Abnormal |

## 2. Profile — `C/Profile`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_024 | TK_W1 | `GET C/Profile` | [API]: 200; dữ liệu của wk01, có `IsTemporaryPassword`; **không** chứa PayType, AttendanceCode, trạng thái tài khoản (UC04).<br>[DB]: không đổi. | Normal |
| IT_B1_025 | TK_W1 | `PUT C/Profile` `{DateOfBirth:"1995-05-20", Phone:"0901234567"}` | [API]: 200.<br>[DB]: `Employee`(E_W1): `DateOfBirth=1995-05-20`, `Phone='0901234567'`, `UpdatedAt` khác NULL, `RowVer` đổi; các cột khác giữ nguyên; AuditLogs +1 `[?]`. | Normal |
| IT_B1_026 | E_W1 đang có Phone và DOB | `PUT C/Profile` `{DateOfBirth:null, Phone:""}` | [API]: 200.<br>[DB]: `Phone IS NULL`, `DateOfBirth IS NULL` (để trống = xóa, theo UC04). | Normal |
| IT_B1_027 | TK_W1 | Phone dài đúng 15 ký tự `"012345678901234"` | [API]: 200.<br>[DB]: Phone lưu đủ 15 ký tự. | Boundary |
| IT_B1_028 | TK_W1 | Phone dài 16 ký tự | [API]: 400 (lỗi validation, **không** để lỗi truncation của SQL biến thành 500).<br>[DB]: không đổi. | Boundary |
| IT_B1_029 | TK_W1 | DateOfBirth = ngày mai; và DateOfBirth = `"1900-01-01"` | [API]: ngày mai → 400 `[?]`; 1900 → 200 hoặc 400 `[?]`; không 500.<br>[DB]: ca bị từ chối thì không đổi. | Boundary |
| IT_B1_030 | TK_W1 | `PUT C/Profile` `{DateOfBirth, Phone, EmployeeId:<E_ADM>, FullName:"Hack", PayType:"Monthly", Status:"RESIGNED", RoleId:1}` (mass assignment + IDOR) | [API]: 200.<br>[DB]: chỉ DOB/Phone của **E_W1** đổi; FullName/PayType/Status/RoleId của E_W1 giữ nguyên; bản ghi E_ADM không đổi. | Abnormal |
| IT_B1_031 | — | `PUT C/Profile` không có token | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_032 | TK_TMP (mật khẩu tạm) | `POST C/Profile/change-password` `{CurrentPassword:null, NewPassword:"N3w@Pass!", ConfirmPassword:"N3w@Pass!"}` | [API]: 200 `[?]` (mật khẩu tạm được bỏ qua CurrentPassword).<br>[DB]: `Account.PasswordHash` đổi và khác chuỗi rõ "N3w@Pass!"; `IsTemporaryPassword=0`; `SecurityStamp` đổi `[?]`; AuditLogs +1 `[?]`. | Normal |
| IT_B1_033 | TK_W1 | change-password `{CurrentPassword:"P@ssw0rd!", NewPassword:"N3w@Pass!", ConfirmPassword:"N3w@Pass!"}` | [API]: 200; sau đó login bằng mật khẩu cũ → 401, bằng mật khẩu mới → 200.<br>[DB]: `PasswordHash` đổi; `IsTemporaryPassword=0`. | Normal |
| IT_B1_034 | TK_W1 (không phải mật khẩu tạm) | `{CurrentPassword:null, NewPassword:"N3w@Pass!", ConfirmPassword:"N3w@Pass!"}` | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_035 | TK_W1 | CurrentPassword sai | [API]: 400 (hoặc 401 `[?]`).<br>[DB]: không đổi. | Abnormal |
| IT_B1_036 | TK_W1 | `NewPassword:"N3w@Pass!"`, `ConfirmPassword:"N3w@Pass?"` | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_037 | TK_W1 | NewPassword có độ dài (min − 1) và đúng min theo chính sách `[?]` | [API]: min − 1 → 400; min → 200.<br>[DB]: chỉ ca hợp lệ làm đổi hash. | Boundary |
| IT_B1_038 | TK_W1 | NewPassword trùng CurrentPassword | [API]: 400 `[?]`; ghi lại.<br>[DB]: không đổi nếu bị từ chối. | Abnormal |

## 3. Lookups, HrLookups, GradeCodeOptions — `C/`

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_039 | TK_W1 (Worker) | `GET C/Lookups/teams` | [API]: 200; mọi vai trò đã đăng nhập đều gọi được.<br>[DB]: chỉ đọc, không đổi. | Normal |
| IT_B1_040 | TK_W1 | `GET C/Lookups/teams?teamType=Sewing` | [API]: 200; chỉ có tổ `TeamType='Sewing'`; T_OLD không xuất hiện `[?]`.<br>[DB]: kết quả khớp `SELECT … WHERE TeamType='Sewing'`. | Normal |
| IT_B1_041 | Có cả đơn mở và đơn đã đóng | `GET C/Lookups/orders?keyword=…` | [API]: 200; chỉ có đơn "mở".<br>[DB]: không đổi. | Normal |
| IT_B1_042 | Có NVL active và NVL inactive | `GET C/Lookups/materials` | [API]: 200; chỉ có NVL active.<br>[DB]: không đổi. | Normal |
| IT_B1_043 | Đơn X có NVL với tồn khả dụng = 0 và > 0 | `GET C/Lookups/requisition-materials?orderId=X` | [API]: 200; chỉ có NVL của đơn X; mọi `AvailableQty >= 0` (là trần số lượng xin, DEV-ALL-110).<br>[DB]: không đổi. | Boundary |
| IT_B1_044 | TK_W1 | `GET C/Lookups/orders/abc/lines` | [API]: 404 (route constraint `:int`).<br>[DB]: không đổi. | Abnormal |
| IT_B1_045 | TK_W1 | `GET C/Lookups/orders/999999/lines`; `GET C/Lookups/order-lines/999999/components` | [API]: 200 với danh sách rỗng hoặc 404; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B1_046 | — | `GET C/Lookups/materials` không có token | [API]: 401.<br>[DB]: không đổi. | Abnormal |
| IT_B1_047 | TK_TL1 (TeamLeader) | `GET C/HrLookups/teams` | [API]: 200 (thuộc `StaffExceptWorker`).<br>[DB]: không đổi. | Normal |
| IT_B1_048 | TK_W1 (Worker) | `GET C/HrLookups/teams`; `GET C/HrLookups/team-members?teamId=<T_SEW01>` | [API]: 403 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B1_049 | TK_ACC | `GET C/HrLookups/team-members` (thiếu `teamId`, vốn là int không nullable) | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_050 | TK_ACC | `GET C/HrLookups/team-members?teamId=<T_SEW01>` | [API]: 200; gồm tl01, td01, wk01; không có rs01.<br>[DB]: khớp `Employee WHERE TeamId=T_SEW01 AND Status='ACTIVE'`. | Normal |
| IT_B1_051 | TK_ACC | `teamId=<T_EMPTY>`; `teamId=0`; `teamId=2147483647` | [API]: T_EMPTY → 200, danh sách rỗng; 0 và max → 200 rỗng hoặc 404; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B1_052 | TK_ACC | `GET C/GradeCodeOptions` | [API]: 200; có B1, B2, B3; **không có B9** (`GetCodesAsync(true)`).<br>[DB]: khớp `GradeLevelCode WHERE Status='ACTIVE'`. | Normal |
| IT_B1_053 | TK_W1 | `GET C/GradeCodeOptions` | [API]: 403.<br>[DB]: không đổi. | Abnormal |

## 4. RBAC theo prefix (ma trận đại diện)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_054 | — | `GET A/Employees`, `GET M/Employees`, `GET C/Profile`, tất cả không có token | [API]: 401 với cả ba.<br>[DB]: không đổi. | Abnormal |
| IT_B1_055 | TK_MGR | `GET A/Employees`; `POST A/GradeLevels {B2, 1000, today+5}`; `POST A/GradeCodes {"B7"}` | [API]: 403 với tất cả (prefix admin chỉ cho `Roles="Admin"`).<br>[DB]: không có dòng GradeLevel/GradeLevelCode mới. | Abnormal |
| IT_B1_056 | TK_ADM | `GET M/Employees`; `GET M/Teams` | [API]: 200 (Admin gọi được mọi API Manager).<br>[DB]: không đổi. | Normal |
| IT_B1_057 | TK_ACC, TK_WH, TK_TL1, TK_W1 (lần lượt) | `GET M/Employees` | [API]: 403 với cả 4 role.<br>[DB]: không đổi. | Abnormal |
| IT_B1_058 | TK_TL1 | `POST M/Teams/<T_SEW01>/appoint {EmployeeId:<E_W1>, Position:"Deputy"}` | [API]: 403 (Tổ trưởng không có quyền bổ nhiệm).<br>[DB]: E_W1 vẫn là `WORKER`; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_059 | TK_W1 | `POST M/Employees/<E_W1>/account/reset-password` (tự reset cho chính mình) | [API]: 403.<br>[DB]: `PasswordHash`, `IsTemporaryPassword` của E_W1 không đổi. | Abnormal |

## 5. Employees — tạo, danh sách, chi tiết, sửa, nghỉ việc

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_060 | TK_ADM | `POST A/Employees` `{EmployeeCode:"NV900", FullName:"Phạm Hồng Thái", Gender:"M", DateOfBirth:"1990-01-01", Phone:"0912345678", Role:"WarehouseKeeper", TeamId:null, PayType:<hợp lệ>, StartDate:today, AttendanceCode:null}` | [API]: 201/200; body có `{EmployeeId, Username, TemporaryPassword}`; Username dạng `ThaiPH`; response header `Cache-Control: no-store`.<br>[DB]: có 1 dòng `Employee` (`Status='ACTIVE'`, RoleCode `WAREHOUSE_KEEPER`, `FullName` giữ nguyên dấu); có 1 dòng `Account` (`EmployeeId` khớp, `IsTemporaryPassword=1`, `Status='ACTIVE'`, `PasswordHash` ≠ TemporaryPassword dạng rõ, `SecurityStamp` khác NULL); AuditLogs +1 `[?]`. | Normal |
| IT_B1_061 | TK_MGR | Như IT_B1_060 nhưng `Role:"QaQc"`, `EmployeeCode:"NV901"` | [API]: 201.<br>[DB]: Employee có RoleCode `QAQC`; có Account đi kèm. | Normal |
| IT_B1_062 | TK_ADM | `Role:"Worker"` | [API]: 400 (công nhân phải tạo qua `from-rj`).<br>[DB]: không có Employee/Account mới. | Abnormal |
| IT_B1_063 | TK_ADM | `Role:"TeamLeader"`; `Role:"TeamDeputy"` | [API]: 400 (chức vụ chỉ được bổ nhiệm sau).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_064 | TK_ADM | `Role:"Superuser"`; `Role:"WAREHOUSE_KEEPER"` (dạng RoleCode trong DB) | [API]: 400 (chỉ nhận PascalCase hợp lệ) `[?]`; không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_065 | TK_MGR | `Role:"Admin"`; `Role:"Accountant"`; `Role:"Manager"` | [API]: 400/403 (Manager không được gán các vai trò này, UC16).<br>[DB]: không có Employee/Account mới. | Abnormal |
| IT_B1_066 | NV900 đã tồn tại | `POST A/Employees` với `EmployeeCode:"NV900"` | [API]: 409.<br>[DB]: `COUNT(Employee WHERE EmployeeCode='NV900') = 1`; không có Account mồ côi. | Abnormal |
| IT_B1_067 | NV900 đã tồn tại | `EmployeeCode:"nv900"` | [API]: 409 (collation `CI` nên `UQ_Employee_Code` coi là trùng); không 500.<br>[DB]: không có dòng mới. | Boundary |
| IT_B1_068 | TK_ADM | EmployeeCode dài 20 ký tự; và dài 21 ký tự | [API]: 20 → 201; 21 → 400 (không 500).<br>[DB]: chỉ ca 20 ký tự được lưu. | Boundary |
| IT_B1_069 | TK_ADM | FullName gồm 100 ký tự tiếng Việt có dấu; và 101 ký tự | [API]: 100 → 201; 101 → 400.<br>[DB]: ca 100 ký tự lưu đúng dấu (nvarchar), không bị `?`. | Boundary |
| IT_B1_070 | TK_ADM | `Gender:"M"`; `Gender:"MA"` | [API]: "M" → 201; "MA" → 400 (cột `char(1)`), không 500.<br>[DB]: ca hợp lệ lưu `Gender='M'`. | Boundary |
| IT_B1_071 | TK_ADM | `TeamId:999999`; `TeamId:<T_OLD>` | [API]: 999999 → 400/404 (FK); T_OLD → 400 `[?]`; không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_072 | TK_ADM | Thiếu `FullName`; thiếu `StartDate`; `PayType:"Xyz"` | [API]: 400 với từng ca.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_073 | TK_ADM | Tạo 2 nhân viên cùng FullName "Phạm Hồng Thái" (NV910, NV911) | [API]: cả hai 201; hai `Username` khác nhau.<br>[DB]: không vi phạm `UQ_Account_Username`; có 2 Account. | Normal |
| IT_B1_074 | Chèn sẵn một Account có `Username='AnNV'`, đúng chuỗi mà bộ sinh sẽ tạo cho "Nguyễn Văn An" (hoặc dùng fault-injection làm INSERT Account lỗi) | `POST A/Employees` `{EmployeeCode:"NV920", FullName:"Nguyễn Văn An", Role:"Electrician", …}` | [API]: 201 với username khác (nếu bộ sinh tự tránh trùng), hoặc 409; **không 500**.<br>[DB]: **Transaction rollback**: nếu thất bại thì `Employee WHERE EmployeeCode='NV920'` không tồn tại (không có Employee mồ côi thiếu Account). | Abnormal |
| IT_B1_075 | TK_MGR | `POST M/Employees/from-rj` `{AttendanceCode:2001, TeamId:<T_SEW02>, PayType:<hợp lệ>, StartDate:today}` | [API]: 201; `{EmployeeId, Username, TemporaryPassword}`; header `no-store`.<br>[DB]: Employee có `FullName='Nguyễn Văn An'` (lấy từ snapshot), RoleCode `WORKER`, `AttendanceCode=2001`, `TeamId=T_SEW02`, `Status='ACTIVE'`; Account có `IsTemporaryPassword=1`; `RjEmployeeSnapshots` được đánh dấu đã nhập `[?]`; AuditLogs +1 `[?]`. | Normal |
| IT_B1_076 | TK_ADM | from-rj `{AttendanceCode:2002}` (các trường khác để null) | [API]: 201.<br>[DB]: `TeamId IS NULL`; PayType/StartDate lấy giá trị mặc định `[?]`, ghi lại. | Normal |
| IT_B1_077 | TK_ADM | from-rj `{AttendanceCode:999999}` (không có trong snapshot) | [API]: 404/400.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_078 | 1001 đã nhập (E_W1) | from-rj `{AttendanceCode:1001}` | [API]: 409.<br>[DB]: `COUNT(Employee WHERE AttendanceCode=1001) = 1`. | Abnormal |
| IT_B1_079 | TK_ADM | from-rj `{AttendanceCode:0}`; `{AttendanceCode:-1}` | [API]: 400/404; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B1_080 | TK_ADM | from-rj `{AttendanceCode:2001, TeamId:<T_OLD>}` | [API]: 400 `[?]`.<br>[DB]: không có Employee mới; snapshot 2001 vẫn ở trạng thái chưa nhập. | Abnormal |
| IT_B1_081 | TK_ADM | `GET A/Employees` (không truyền `Status`) | [API]: 200; chỉ có nhân viên đang làm; không có rs01.<br>[DB]: tổng số bản ghi khớp `COUNT(*) WHERE Status='ACTIVE'`. | Normal |
| IT_B1_082 | TK_ADM | `?Status=Resigned`; `?Status=All` | [API]: Resigned → chỉ có rs01; All → có cả ACTIVE và RESIGNED.<br>[DB]: khớp `WHERE Status='RESIGNED'` (chữ HOA) và tổng toàn bảng. | Normal |
| IT_B1_083 | TK_ADM | `?Status=RESIGNED`; `?Status=Xyz` | [API]: 400, hoặc map không phân biệt hoa/thường `[?]`; không 500; ghi lại.<br>[DB]: không đổi. | Abnormal |
| IT_B1_084 | TK_ADM | `?TeamId=<T_SEW01>&Role=Worker` | [API]: 200; chỉ có wk01.<br>[DB]: không đổi. | Normal |
| IT_B1_085 | TK_MGR | `GET M/Employees?Status=All` | [API]: 200; **không** có admin01, admin02, acc01 (UC16); manager có xuất hiện hay không là `[?]`, ghi lại.<br>[DB]: không đổi. | Normal |
| IT_B1_086 | TK_MGR | `GET M/Employees?Role=Admin`; `?Role=Accountant` | [API]: 200 với danh sách rỗng (hoặc 403); không lộ bản ghi nào.<br>[DB]: không đổi. | Abnormal |
| IT_B1_087 | TK_ADM | PageQuery: trang vượt tổng số trang; pageSize = 0 / âm / rất lớn (tên field `[?]`) | [API]: trang vượt → 200 rỗng; pageSize không hợp lệ → 400 hoặc bị kẹp về giới hạn `[?]`; không 500.<br>[DB]: không đổi. | Boundary |
| IT_B1_088 | TK_ADM | `GET A/Employees/<E_W1>` | [API]: 200; `Account` khác null (Username, Status); có `RowVer`; có `History[]`.<br>[DB]: dữ liệu khớp; `History` khớp `AuditLogs` của E_W1. | Normal |
| IT_B1_089 | TK_MGR | `GET M/Employees/<E_W1>` | [API]: 200; `Account = null`.<br>[DB]: không đổi. | Normal |
| IT_B1_090 | TK_MGR | `GET M/Employees/<E_ADM2>`; `GET M/Employees/<E_ACC>` (IDOR) | [API]: 403/404; body không chứa FullName/Phone của mục tiêu.<br>[DB]: không đổi. | Abnormal |
| IT_B1_091 | TK_ADM | `GET A/Employees/999999`; `GET A/Employees/abc` | [API]: 404 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B1_092 | TK_ADM; đã lấy `RowVer` hiện tại của E_WH | `PUT A/Employees/<E_WH>` `{FullName:"Kho Mới", Phone:"0987654321", Role:"WarehouseKeeper", PayType, StartDate, AttendanceCode:null, RowVer}` | [API]: 200 (trả RowVer mới nếu có).<br>[DB]: FullName/Phone đã cập nhật; `RowVer` khác giá trị cũ; `UpdatedAt` khác NULL; AuditLogs +1 `[?]`. | Normal |
| IT_B1_093 | E_WH đã được sửa một lần (RowVer cũ đã lỗi thời) | PUT với **RowVer cũ** | [API]: 409 (khóa lạc quan).<br>[DB]: không đổi so với sau lần sửa đầu; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_094 | TK_ADM | PUT thiếu `RowVer`; `RowVer:"không-phải-base64"` | [API]: 400; không 500.<br>[DB]: không đổi. | Abnormal |
| IT_B1_095 | 2 client cùng đọc một RowVer của E_WH | Gửi đồng thời 2 PUT với cùng RowVer | [API]: đúng 1 request 200, request còn lại 409.<br>[DB]: chỉ dữ liệu của request thắng được lưu; AuditLogs +1. | Boundary |
| IT_B1_096 | TK_MGR | `PUT M/Employees/<E_ACC>` với RowVer hợp lệ (IDOR) | [API]: 403/404.<br>[DB]: E_ACC không đổi. | Abnormal |
| IT_B1_097 | TK_MGR | `PUT M/Employees/<E_WH>` với `Role:"Admin"` / `"Accountant"` / `"Manager"` | [API]: 400/403.<br>[DB]: RoleCode của E_WH vẫn là `WAREHOUSE_KEEPER`. | Abnormal |
| IT_B1_098 | TK_ADM | PUT E_WH với Phone dài 16 ký tự | [API]: 400 (không 500).<br>[DB]: không đổi. | Boundary |
| IT_B1_099 | TK_ADM; E_WH có `StartDate=2024-01-01` | `POST A/Employees/<E_WH>/resign` `{ResignDate:today, Reason:"Nghỉ theo nguyện vọng", RowVer}` | [API]: 200.<br>[DB]: `Employee.Status='RESIGNED'` (chữ HOA), `ResignDate=today`, `RowVer` đổi; `Account.Status` đổi sang khóa/vô hiệu `[?]`; AuditLogs +1 `[?]`. | Normal |
| IT_B1_100 | Sau IT_B1_099 | login `{wh01, P@ssw0rd!}`; `GET A/Employees?Status=Resigned` | [API]: login → 401/403; danh sách Resigned có wh01.<br>[DB]: `Status='RESIGNED'`. | Normal |
| IT_B1_101 | E_RS đã RESIGNED | resign E_RS lần nữa | [API]: 400/409.<br>[DB]: `ResignDate` không đổi; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_102 | E_WH có `StartDate=2024-01-01` | `ResignDate=2024-01-01`; và `ResignDate=2023-12-31` | [API]: bằng StartDate → 200 `[?]`; trước StartDate → 400.<br>[DB]: ca bị từ chối thì không đổi. | Boundary |
| IT_B1_103 | TK_ADM | resign với `Reason:""`; thiếu `ResignDate`; RowVer cũ | [API]: 400 / 400 / 409 tương ứng.<br>[DB]: không đổi. | Abnormal |
| IT_B1_104 | TK_MGR | `POST M/Employees/<E_ADM2>/resign` (IDOR) | [API]: 403/404.<br>[DB]: E_ADM2 vẫn `ACTIVE`. | Abnormal |
| IT_B1_105 | TK_ADM | admin01 tự resign chính mình | [API]: 400 `[?]` (không để mất Admin cuối cùng/tự khóa mình); ghi lại; không 500.<br>[DB]: ghi lại trạng thái thực tế. | Abnormal |

## 6. Employees — tài khoản (`{id}` = EmployeeId)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_106 | TK_ADM | `POST A/Employees/<E_WH>/account/lock` `{Reason:"Vi phạm nội quy"}` | [API]: 200.<br>[DB]: `Account.Status` = giá trị khóa viết HOA (`LOCKED` `[?]`); `LockedAt` ≈ giờ hiện tại (giờ VN, theo `fn_VnNow`); `LockReason='Vi phạm nội quy'`; `Employee.Status` vẫn `ACTIVE`; AuditLogs +1. | Normal |
| IT_B1_107 | Sau IT_B1_106, wh01 còn giữ token cũ | login wh01; refresh bằng token cũ; `GET C/Auth/me` bằng access token cũ | [API]: login → 401/403; refresh → 401; me → 401 hoặc 200 cho tới khi token hết hạn `[?]`; ghi lại.<br>[DB]: không đổi. | Normal |
| IT_B1_108 | TK_ADM | lock với `{Reason:""}`; và body `{}` | [API]: 400.<br>[DB]: Account không đổi. | Abnormal |
| IT_B1_109 | TK_ADM | `Reason` dài 300 ký tự có dấu; và 301 ký tự | [API]: 300 → 200; 301 → 400 (không 500).<br>[DB]: ca 300 ký tự lưu đủ (nvarchar). | Boundary |
| IT_B1_110 | E_LCK đang bị khóa | lock E_LCK lần nữa | [API]: 400/409.<br>[DB]: `LockedAt` và `LockReason` cũ giữ nguyên. | Abnormal |
| IT_B1_111 | Chọn id = `AccountId` của wh01, trong khi id này lại là `EmployeeId` của nhân viên khác (EX) | `POST A/Employees/<id>/account/lock` | [API]: 200; tác động lên Account của **EX** (`WHERE EmployeeId=id`).<br>[DB]: Account của EX bị khóa; Account của wh01 không đổi. | Abnormal |
| IT_B1_112 | TK_MGR | `POST M/Employees/<E_ADM2>/account/lock`; reset-password cho E_ACC (IDOR) | [API]: 403/404.<br>[DB]: Account của E_ADM2 và E_ACC không đổi. | Abnormal |
| IT_B1_113 | TK_ADM | admin01 tự khóa tài khoản của mình | [API]: 400 `[?]`; không 500.<br>[DB]: ghi lại. | Abnormal |
| IT_B1_114 | E_LCK đang bị khóa | `POST A/Employees/<E_LCK>/account/unlock` | [API]: 200; sau đó login lck01 → 200.<br>[DB]: `Account.Status='ACTIVE'`; `LockedAt`/`LockReason` về NULL `[?]`; AuditLogs +1. | Normal |
| IT_B1_115 | E_WH đang ACTIVE | unlock E_WH | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_116 | TK_MGR | `POST M/Employees/<E_W1>/account/reset-password` | [API]: 200; `{Username:"wk01", TemporaryPassword}`; header `no-store`; login bằng mật khẩu cũ → 401, bằng mật khẩu tạm → 200 với `IsTemporaryPassword=true`.<br>[DB]: `PasswordHash` đổi (≠ chuỗi rõ); `IsTemporaryPassword=1`; `SecurityStamp` đổi `[?]`; AuditLogs +1. | Normal |
| IT_B1_117 | TK_ADM | reset-password cho E_RS (đã nghỉ); và id 999999 | [API]: E_RS → 400 `[?]`; 999999 → 404.<br>[DB]: không đổi. | Abnormal |

## 7. Teams — vòng đời và thành viên

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_118 | TK_MGR | `GET M/Teams` | [API]: 200; không có T_OLD.<br>[DB]: không đổi. | Normal |
| IT_B1_119 | TK_MGR | `?includeDiscontinued=true`; `?type=Cutting` | [API]: lần lượt có T_OLD; chỉ có T_CUT01.<br>[DB]: không đổi. | Normal |
| IT_B1_120 | TK_MGR | `POST M/Teams` `{TeamCode:"T_SEW03", TeamType:"Sewing"}` | [API]: 201.<br>[DB]: 1 dòng `Team` với `Status='ACTIVE'`, `CreatedAt` khác NULL; AuditLogs +1 `[?]`. | Normal |
| IT_B1_121 | TK_MGR | `{TeamCode:"t_sew01", TeamType:"Sewing"}` | [API]: 409 (`UQ_Team_Code`, collation CI).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_122 | TK_MGR | `TeamType:"Packing"`; `TeamType:"sewing"` | [API]: 400 (chỉ nhận Cutting/Sewing/Office); `"sewing"` ghi lại `[?]`; không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_123 | TK_MGR | TeamCode dài 20 ký tự; 21 ký tự; `""` | [API]: 20 → 201; 21 → 400; rỗng → 400.<br>[DB]: chỉ ca 20 ký tự được lưu. | Boundary |
| IT_B1_124 | TK_ADM | `PUT A/Teams/<T_EMPTY>` `{TeamCode:"T_EMPTY", TeamType:"Office"}` | [API]: 200.<br>[DB]: `TeamType='Office'`. | Normal |
| IT_B1_125 | TK_ADM | `PUT A/Teams/<T_EMPTY>` `{TeamCode:"T_SEW01", …}` | [API]: 409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_126 | TK_MGR | `POST M/Teams/<T_EMPTY>/discontinue` | [API]: 200; T_EMPTY không còn xuất hiện ở `GET M/Teams`.<br>[DB]: `Team.Status` = giá trị ngừng viết HOA (`DISCONTINUED` `[?]`). | Normal |
| IT_B1_127 | T_SEW02 có thành viên | discontinue T_SEW02 | [API]: 400/409, hoặc 200 kèm gỡ thành viên `[?]`; không 500.<br>[DB]: nhất quán: nếu 200 thì không còn nhân viên ACTIVE nào trỏ vào tổ đã ngừng `[?]`; nếu bị từ chối thì không đổi. | Abnormal |
| IT_B1_128 | T_OLD đã ngừng | discontinue T_OLD | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_129 | T_OLD đã ngừng | `POST M/Teams/<T_OLD>/reactivate` | [API]: 200.<br>[DB]: `Team.Status='ACTIVE'`. | Normal |
| IT_B1_130 | T_SEW01 đang ACTIVE | reactivate T_SEW01 | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_131 | TK_MGR | `GET M/Teams/candidates`; `GET M/Teams/candidates?teamId=<T_SEW02>` | [API]: 200; không truyền teamId → có wk03 (chưa có tổ), không có rs01; có teamId → danh sách theo `CandidatesAsync(id)` `[?]`; ghi lại.<br>[DB]: không đổi. | Normal |
| IT_B1_132 | TK_MGR | `POST M/Teams/<T_EMPTY>/add-members` `{EmployeeIds:[<E_W3>], Reason:"Bổ sung"}` | [API]: 200.<br>[DB]: `E_W3.TeamId=T_EMPTY`; AuditLogs +1 `[?]`. | Normal |
| IT_B1_133 | TK_MGR | add-members `{EmployeeIds:[<E_W3>, <E_W1>]}` (E_W1 đã có tổ) | [API]: 400/409.<br>[DB]: **Rollback**: `E_W3.TeamId` vẫn NULL; `E_W1.TeamId` vẫn T_SEW01; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_134 | TK_MGR | `EmployeeIds:[]`; `EmployeeIds:[<E_W3>, <E_W3>]` | [API]: rỗng → 400; trùng → 200 (thêm 1 lần) hoặc 400 `[?]`; không 500.<br>[DB]: không có trạng thái trung gian. | Boundary |
| IT_B1_135 | TK_MGR | add-members E_RS (đã nghỉ) vào T_SEW02; add-members E_W3 vào T_OLD (đã ngừng) | [API]: 400 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B1_136 | TK_MGR | `POST M/Teams/<T_SEW02>/remove-members` `{EmployeeIds:[<E_W2>]}` | [API]: 200.<br>[DB]: `E_W2.TeamId IS NULL`; AuditLogs +1 `[?]`. | Normal |
| IT_B1_137 | TK_MGR | remove-members T_SEW02 `{EmployeeIds:[<E_W1>]}` (E_W1 không thuộc T_SEW02) | [API]: 400.<br>[DB]: `E_W1.TeamId` vẫn T_SEW01. | Abnormal |
| IT_B1_138 | TK_MGR | `POST M/Teams/<T_SEW02>/transfer-in` `{EmployeeIds:[<E_W1>], EffectiveDate:today, Reason:"Điều chuyển"}` | [API]: 200.<br>[DB]: `E_W1.TeamId=T_SEW02`; AuditLogs +1 ghi nhận tổ cũ/mới và EffectiveDate `[?]`. | Normal |
| IT_B1_139 | TK_MGR | transfer-in `{EmployeeIds:[<E_W1>, 999999]}` | [API]: 400/404.<br>[DB]: **Rollback**: `E_W1.TeamId` vẫn T_SEW01. | Abnormal |
| IT_B1_140 | TK_MGR | transfer-in E_W1 vào T_OLD; transfer-in E_W2 vào chính T_SEW02 (tổ hiện tại) | [API]: 400 với cả hai.<br>[DB]: không đổi. | Abnormal |
| IT_B1_141 | TK_MGR | transfer-in với `EffectiveDate` = hôm qua / hôm nay + 1 / `0001-01-01` | [API]: chấp nhận hoặc từ chối theo quy tắc `[?]`; ngày vô lý → 400; không 500.<br>[DB]: ghi lại; không có trạng thái trung gian. | Boundary |

## 8. Trigger `trg_Employee_TeamLeadership` — va chạm chức vụ (cấm 500)

> Mọi ca ở mục này: nếu thấy `500`, hoặc lỗi SQL lộ ra trong body (`RAISERROR` / `THROW` / "trigger"), là **FAIL**. Vế [DB] luôn phải kiểm tra thêm bất biến: **mỗi tổ có tối đa 1 `TEAM_LEADER` và 1 `TEAM_DEPUTY`**.

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_142 | T_SEW01 đã có Leader E_TL1 | `POST M/Teams/<T_SEW01>/appoint` `{EmployeeId:<E_W1>, Position:"Leader"}` | [API]: 400/409, message nghiệp vụ rõ ràng.<br>[DB]: E_W1 vẫn `WORKER`, E_TL1 vẫn `TEAM_LEADER`; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_143 | T_SEW01 đã có Deputy E_TD1 | appoint `{EmployeeId:<E_W1>, Position:"Deputy"}` vào T_SEW01 | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_144 | T_SEW02 chưa có Leader | appoint `{EmployeeId:<E_W2>, Position:"Leader"}` vào T_SEW02 | [API]: 200.<br>[DB]: E_W2 có RoleCode `TEAM_LEADER`, `TeamId` vẫn T_SEW02; AuditLogs +1; `TeamDetailDto.History` có mục bổ nhiệm. | Normal |
| IT_B1_145 | T_CUT01 chưa có Deputy | appoint `{EmployeeId:<E_W4>, Position:"Deputy"}` vào T_CUT01 | [API]: 200.<br>[DB]: E_W4 có RoleCode `TEAM_DEPUTY`; AuditLogs +1. | Normal |
| IT_B1_146 | E_W1 thuộc T_SEW01 | appoint `{EmployeeId:<E_W1>, Position:"Leader"}` vào **T_SEW02** (không phải thành viên) | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_147 | E_TL1 đang là Leader của T_SEW01 | appoint `{EmployeeId:<E_TL1>, Position:"Deputy"}` vào T_SEW01 | [API]: 400/409 (đang giữ chức khác trong cùng tổ).<br>[DB]: E_TL1 vẫn `TEAM_LEADER`; T_SEW01 vẫn có đúng 1 Leader và 1 Deputy. | Abnormal |
| IT_B1_148 | E_TL2 là Leader của T_CUT01 | appoint `{EmployeeId:<E_TL2>, Position:"Leader"}` vào T_SEW02 | [API]: 400/409.<br>[DB]: không đổi. | Abnormal |
| IT_B1_149 | TK_MGR | `Position:"Manager"`; `Position:"leader"`; `Position:""` | [API]: 400 (chỉ nhận Leader/Deputy; chữ thường ghi lại `[?]`).<br>[DB]: không đổi. | Abnormal |
| IT_B1_150 | TK_MGR | appoint E_RS (đã nghỉ); appoint vào T_OLD (đã ngừng); `EmployeeId:999999` | [API]: 400 / 400 / 404 (hoặc 400).<br>[DB]: không đổi. | Abnormal |
| IT_B1_151 | T_SEW02 chưa có Leader; có 2 thành viên E_W2, E_W5 | Gửi **đồng thời** 2 request appoint Leader cho E_W2 và E_W5 | [API]: đúng 1 request 200, request còn lại 409/400; không 500 (trigger hoặc race condition phải được map thành lỗi êm).<br>[DB]: `COUNT(TEAM_LEADER trong T_SEW02) = 1`. | Boundary |
| IT_B1_152 | E_TD1 là Deputy của T_SEW01 | `POST M/Teams/<T_SEW01>/dismiss` `{EmployeeId:<E_TD1>}` | [API]: 200.<br>[DB]: E_TD1 có RoleCode `WORKER`, `TeamId` vẫn T_SEW01; AuditLogs +1. | Normal |
| IT_B1_153 | E_W1 là Worker | dismiss `{EmployeeId:<E_W1>}` ở T_SEW01 (người không giữ chức) | [API]: 400/409.<br>[DB]: không đổi; AuditLogs không thêm dòng. | Abnormal |
| IT_B1_154 | E_TL2 là Leader của T_CUT01 | dismiss `{EmployeeId:<E_TL2>}` qua route **T_SEW01** | [API]: 400.<br>[DB]: E_TL2 vẫn `TEAM_LEADER`. | Abnormal |
| IT_B1_155 | TK_MGR | dismiss `{EmployeeId:999999}`; body `{}` | [API]: 404/400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_156 | E_TL1 là Leader của T_SEW01 | `POST M/Teams/<T_SEW02>/transfer-in` `{EmployeeIds:[<E_TL1>], EffectiveDate:today}` | [API]: 400/409 (phải miễn nhiệm trước), hoặc 200 kèm tự hạ về Worker `[?]`; **không 500**.<br>[DB]: nếu bị từ chối thì không đổi; nếu 200 thì E_TL1 là `WORKER` ở T_SEW02 và T_SEW01 không còn Leader. Không được có Leader nằm sai tổ. | Abnormal |
| IT_B1_157 | E_TD1 là Deputy của T_SEW01; T_CUT01 chưa có Deputy | transfer-in E_TD1 vào T_CUT01 | [API]: 400/409 (hoặc `[?]` như IT_B1_156); không 500.<br>[DB]: không có Deputy nào được mang chức sang tổ mới ngoài luồng `appoint`. | Abnormal |
| IT_B1_158 | E_W1 (thường) và E_TL1 (Leader) cùng ở T_SEW01 | transfer-in `{EmployeeIds:[<E_W1>, <E_TL1>]}` vào T_SEW02 | [API]: 400/409.<br>[DB]: **Rollback**: cả E_W1 và E_TL1 vẫn ở T_SEW01. | Abnormal |
| IT_B1_159 | E_TL1 là Leader | `POST M/Teams/<T_SEW01>/remove-members` `{EmployeeIds:[<E_TL1>]}` | [API]: 400/409; không 500.<br>[DB]: E_TL1 vẫn `TeamId=T_SEW01`, vẫn `TEAM_LEADER`. | Abnormal |
| IT_B1_160 | TK_ADM; RowVer hợp lệ | `PUT A/Employees/<E_TL1>` với `TeamId:<T_SEW02>` (đổi tổ của Leader qua luồng sửa nhân viên) | [API]: 400/409; không 500.<br>[DB]: E_TL1 không đổi; RowVer không đổi. | Abnormal |
| IT_B1_161 | T_SEW01 đã có Leader | `PUT A/Employees/<E_W1>` với `Role:"TeamLeader"` (đi vòng qua `appoint`) | [API]: 400/409; không 500.<br>[DB]: E_W1 vẫn `WORKER`. | Abnormal |
| IT_B1_162 | E_TL1 là Leader | `POST A/Employees/<E_TL1>/resign` `{ResignDate:today, Reason, RowVer}` | [API]: 400/409 (yêu cầu miễn nhiệm trước), hoặc 200 kèm tự gỡ chức `[?]`; không 500.<br>[DB]: không có nhân viên `RESIGNED` nào còn giữ `TEAM_LEADER` của tổ. | Abnormal |
| IT_B1_163 | T_SEW01 có Leader và Deputy | `POST M/Teams/<T_SEW01>/discontinue` | [API]: 400/409, hoặc 200 kèm reset chức vụ `[?]`; không 500.<br>[DB]: nhất quán theo nhánh thực tế; không có Leader của tổ đã ngừng còn ACTIVE `[?]`. | Abnormal |
| IT_B1_164 | T_SEW01 có Leader E_TL1 | Gọi tuần tự: dismiss E_TL1, rồi appoint `{E_W1, "Leader"}` vào T_SEW01 | [API]: cả hai 200.<br>[DB]: E_TL1 là `WORKER`, E_W1 là `TEAM_LEADER`; `COUNT(TEAM_LEADER trong T_SEW01) = 1`; AuditLogs +2. | Normal |

## 9. Filtered Unique Index `UX_Employee_AttendanceCode` (`WHERE AttendanceCode IS NOT NULL`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_165 | S0 đã có nhiều dòng `AttendanceCode IS NULL` (E_ADM, E_WH, E_W3…) | `POST A/Employees` tạo NV930 rồi NV931, cả hai `AttendanceCode:null` (hoặc bỏ field) | [API]: cả hai 201.<br>[DB]: `COUNT(*) WHERE AttendanceCode IS NULL` tăng đúng 2; không vi phạm index. | Normal |
| IT_B1_166 | 1001 thuộc E_W1 | `POST A/Employees` `{EmployeeCode:"NV932", AttendanceCode:1001, Role:"Electrician", …}` | [API]: **409 Conflict**; không 500.<br>[DB]: `COUNT(AttendanceCode=1001) = 1`; không có Employee NV932; không có Account mồ côi (rollback). | Abnormal |
| IT_B1_167 | S0 | Tạo NV933 với `AttendanceCode:5001`, rồi NV934 với `AttendanceCode:5001` | [API]: lần đầu 201, lần sau **409**.<br>[DB]: chỉ có NV933 mang 5001. | Abnormal |
| IT_B1_168 | 1002 thuộc E_W2 | `PUT A/Employees/<E_WH>` với `AttendanceCode:1002` | [API]: **409**.<br>[DB]: E_WH vẫn `AttendanceCode IS NULL`; RowVer không đổi. | Abnormal |
| IT_B1_169 | E_W3 đã có `AttendanceCode=NULL` | `PUT A/Employees/<E_W1>` với `AttendanceCode:null` | [API]: 200.<br>[DB]: E_W1 và E_W3 cùng NULL, tồn tại song song hợp lệ. | Normal |
| IT_B1_170 | 1009 thuộc E_RS (đã RESIGNED) | Tạo nhân viên mới với `AttendanceCode:1009` | [API]: **409** (index lọc theo NULL, không lọc theo Status).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_171 | S0 | `AttendanceCode`: `2147483647`; `0`; `-1`; `2147483648` | [API]: max int → 201; 0 / −1 → 201 hoặc 400 `[?]`; 2147483648 → 400 (tràn int khi deserialize JSON), **không 500**.<br>[DB]: chỉ ca được chấp nhận mới lưu. | Boundary |
| IT_B1_172 | S0 | Gửi **đồng thời** 2 POST tạo nhân viên cùng `AttendanceCode:5002` | [API]: đúng 1 request 201, request còn lại **409** (lỗi unique từ DB phải được map thành 409, không 500).<br>[DB]: `COUNT(AttendanceCode=5002) = 1`; không có Account mồ côi. | Boundary |
| IT_B1_173 | Có nhân viên tạo tay mang `AttendanceCode=2002` | `POST A/Employees/from-rj` `{AttendanceCode:2002}` | [API]: **409**.<br>[DB]: không có Employee/Account mới; snapshot 2002 không bị đánh dấu đã nhập. | Abnormal |

## 10. Bất thường hợp đồng

### 10.1 `CreateEmployeeDto.Email` (không có cột lưu)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_174 | TK_ADM | Body hợp lệ như IT_B1_060 (`EmployeeCode:"NV940"`) kèm `Email:"thai.ph@nxmtd.vn"` | [API]: 201 (Email được chấp nhận rồi bỏ qua); ghi lại response có trả lại Email hay không.<br>[DB]: Employee/Account được tạo bình thường, không có lỗi DB; xác nhận qua `INFORMATION_SCHEMA.COLUMNS` rằng không có cột Email nào nhận giá trị. **Ghi nhận finding**: dữ liệu Email bị mất. | Normal |
| IT_B1_175 | TK_ADM | `Email:"abc"` (sai định dạng) | [API]: 400 (nếu có validator) hoặc 201 (bỏ qua); ghi lại; **không 500**.<br>[DB]: nhất quán với API (201 → có dòng mới; 400 → không có). | Abnormal |
| IT_B1_176 | TK_ADM | `Email:""`; Email dài 1.000 ký tự; `Email:123` (sai kiểu) | [API]: `""` / dài → 201 hoặc 400; sai kiểu → 400; không 500.<br>[DB]: không có lỗi truncation. | Boundary |
| IT_B1_177 | TK_ADM | Body không có Email (mặc định null) | [API]: 201; cấu trúc response giống IT_B1_174.<br>[DB]: các cột giống hệt ca có gửi Email (chứng minh Email không ảnh hưởng gì). | Normal |

### 10.2 `PUT A/GradeCodes/{code}`: DTO chỉ có `Status`, trong khi comment ghi "sửa tên / thứ tự"

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_178 | B2 đang ACTIVE | `PUT A/GradeCodes/B2` `{Status:"Inactive"}` (giá trị DTO `[?]`) | [API]: 200.<br>[DB]: `GradeLevelCode.Status` của B2 = `INACTIVE` viết HOA `[?]` (không lưu PascalCase); `GET C/GradeCodeOptions` không còn B2; `GET A/GradeCodes` vẫn có B2. | Normal |
| IT_B1_179 | B2 đang ACTIVE | `PUT A/GradeCodes/B2` `{Status:"Active", Name:"Bậc 2 mới", SortOrder:5}` | [API]: 200 (field thừa bị bỏ qua) hoặc 400 nếu bật kiểm tra JSON chặt; không 500.<br>[DB]: chỉ có cột Status (không có cột tên/thứ tự). **Ghi nhận finding**: không có cách nào đổi tên/thứ tự, trái với comment. | Abnormal |
| IT_B1_180 | B2 tồn tại | `PUT A/GradeCodes/B2` `{Name:"X"}` (không có Status) | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_181 | B2 tồn tại | `PUT A/GradeCodes/B2` `{GradeCode:"B5", Status:"Active"}` (thử đổi mã qua body) | [API]: 200 (field bị bỏ qua) hoặc 400.<br>[DB]: không tồn tại `GradeCode='B5'`; B2 giữ nguyên mã (PK không đổi). | Abnormal |
| IT_B1_182 | TK_ADM | `{Status:"Deleted"}`; `{Status:""}` | [API]: 400.<br>[DB]: không đổi. | Abnormal |
| IT_B1_183 | TK_ADM | `PUT A/GradeCodes/ZZ` `{Status:"Inactive"}` | [API]: 404.<br>[DB]: không có dòng mới (không upsert). | Abnormal |
| IT_B1_184 | B1 đang được E_W1 dùng | `PUT A/GradeCodes/B1` `{Status:"Inactive"}` | [API]: 200 hoặc 409 `[?]`; ghi lại.<br>[DB]: `E_W1.CurrentGradeCode` vẫn là `'B1'` (không bị xóa/thay). | Abnormal |
| IT_B1_185 | B2 tồn tại | `PUT A/GradeCodes/b2` (route viết thường) | [API]: 200 (khớp B2 nhờ collation CI) hoặc 404 `[?]`; không 500.<br>[DB]: không có dòng `'b2'` mới. | Boundary |

## 11. GradeCodes và GradeLevels (UC15 — chỉ Admin)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_186 | TK_ADM | `GET A/GradeCodes` | [API]: 200; có cả B9 (`GetCodesAsync(false)`); `InUse=true` với B1, B3; `InUse=false` với B2.<br>[DB]: `InUse` khớp việc có tham chiếu từ `Employee.CurrentGradeCode` / GradeEvaluation / `GradeLevel`. | Normal |
| IT_B1_187 | TK_ADM | `POST A/GradeCodes` `{GradeCode:"B4"}` | [API]: 201.<br>[DB]: dòng `GradeLevelCode('B4', Status='ACTIVE')`, `CreatedAt` khác NULL. | Normal |
| IT_B1_188 | B1 tồn tại | `POST A/GradeCodes` `{GradeCode:"b1"}` | [API]: 409 (trùng PK theo collation CI); không 500.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_189 | TK_ADM | `GradeCode`: `"B1234"` (5 ký tự); `"B12345"` (6); `""`; `"  "` | [API]: 5 ký tự → 201; 6 ký tự → 400; rỗng/khoảng trắng → 400; không 500.<br>[DB]: chỉ có `'B1234'` được lưu. | Boundary |
| IT_B1_190 | B2 không được dùng | `DELETE A/GradeCodes/B2` | [API]: 200/204.<br>[DB]: dòng B2 bị xóa. | Normal |
| IT_B1_191 | B1 đang được Employee dùng; B3 chỉ có dòng GradeLevel | `DELETE A/GradeCodes/B1`; `DELETE A/GradeCodes/B3` | [API]: 409 với cả hai (`InUse=true`); không 500 do lỗi FK.<br>[DB]: B1, B3 vẫn còn; GL1, GL3 vẫn còn. | Abnormal |
| IT_B1_192 | TK_ADM | `DELETE A/GradeCodes/ZZ` | [API]: 404.<br>[DB]: không đổi. | Abnormal |
| IT_B1_193 | TK_ADM | `GET A/GradeLevels` | [API]: 200; GL1 `IsCurrent=true`, GL2 `IsCurrent=false` (tương lai), GL3 `IsCurrent=true`.<br>[DB]: `IsCurrent` khớp dòng có `EffectiveDate` lớn nhất nhưng ≤ hôm nay, theo từng GradeCode. | Normal |
| IT_B1_194 | TK_ADM | `POST A/GradeLevels` `{GradeCode:"B2", MonthlyAmount:5000000, EffectiveDate:today+10}` | [API]: 201.<br>[DB]: `MonthlyAmount=5000000.00`, `EffectiveDate` đúng như gửi. | Normal |
| IT_B1_195 | GL2 = (B1, today+30) | POST `{B1, 6000000, today+30}` | [API]: 409 (`UQ_GradeLevel`).<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_196 | TK_ADM | POST với `GradeCode:"ZZ"`; với `GradeCode:"B9"` (ngừng dùng) | [API]: ZZ → 400/404 (không 500 do FK); B9 → 400 `[?]`.<br>[DB]: không có dòng mới. | Abnormal |
| IT_B1_197 | TK_ADM | `MonthlyAmount`: `-1`; `0`; `0.01` | [API]: −1 → 400; 0 → 400 `[?]`; 0.01 → 201.<br>[DB]: chỉ ca hợp lệ được lưu (`0.01`). | Boundary |
| IT_B1_198 | TK_ADM | `MonthlyAmount`: `9999999999999999.99` (tối đa của `decimal(18,2)`); `10000000000000000.00` | [API]: giá trị tối đa → 201; vượt → 400, **không 500** (arithmetic overflow).<br>[DB]: giá trị tối đa lưu chính xác. | Boundary |
| IT_B1_199 | TK_ADM | `MonthlyAmount: 1000.555` (3 chữ số thập phân) | [API]: 400, hoặc 201 kèm làm tròn `[?]`; ghi lại.<br>[DB]: nếu được lưu thì giá trị là `1000.56` hoặc `1000.55`; ghi lại cách làm tròn. | Boundary |
| IT_B1_200 | TK_ADM | POST `{B2, 5000000, EffectiveDate:yesterday}` | [API]: 400 `[?]` (không được tạo mức lùi ngày làm thay đổi lịch sử lương); ghi lại.<br>[DB]: không có dòng mới nếu bị từ chối. | Boundary |
| IT_B1_201 | GL2 chưa hiệu lực | `PUT A/GradeLevels/<GL2>` `{MonthlyAmount:6500000, EffectiveDate:today+45}` | [API]: 200.<br>[DB]: GL2 có `MonthlyAmount=6500000.00`, `EffectiveDate=today+45`; GL1 không đổi. | Normal |
| IT_B1_202 | GL1 đã hiệu lực | `PUT A/GradeLevels/<GL1>` `{MonthlyAmount:9999999, EffectiveDate:"2025-01-01"}` | [API]: 400/409 (là lịch sử lương).<br>[DB]: GL1 không đổi. | Abnormal |
| IT_B1_203 | Thêm sẵn GL4 = (B2, EffectiveDate=today) | PUT GL4; DELETE GL4 | [API]: 400/409 nếu "hôm nay" được coi là đã hiệu lực `[?]`; ghi lại điểm biên.<br>[DB]: không đổi nếu bị từ chối. | Boundary |
| IT_B1_204 | GL2 (B1, today+30); thêm sẵn GL5 (B1, today+60) | PUT GL2 với `EffectiveDate:today+60` | [API]: 409 (trùng `UQ_GradeLevel`).<br>[DB]: GL2 không đổi. | Abnormal |
| IT_B1_205 | GL2 chưa hiệu lực | PUT GL2 với `EffectiveDate:yesterday` | [API]: 400.<br>[DB]: GL2 không đổi. | Abnormal |
| IT_B1_206 | GL2 chưa hiệu lực | `DELETE A/GradeLevels/<GL2>` | [API]: 200/204.<br>[DB]: GL2 bị xóa; GL1 vẫn còn và vẫn `IsCurrent`. | Normal |
| IT_B1_207 | TK_ADM | `DELETE A/GradeLevels/<GL1>`; `DELETE A/GradeLevels/999999` | [API]: GL1 → 400/409; 999999 → 404.<br>[DB]: GL1 vẫn còn. | Abnormal |

## 12. Đồng bộ trạng thái và AuditLog (kiểm tra chéo)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type |
|---|---|---|---|---|
| IT_B1_208 | TK_ADM; ghi `MAX(AuditLogs.Id)` trước khi chạy | Chuỗi thao tác trên E_WH: lock → unlock → reset-password → PUT (đổi Phone) | [API]: cả 4 trả 200; `GET A/Employees/<E_WH>` có `History[]` gồm 4 mục mới, theo đúng thứ tự thời gian.<br>[DB]: AuditLogs +4, gắn với E_WH, hành động tương ứng; **không** dòng nào chứa mật khẩu tạm hoặc PasswordHash (D29). | Normal |
| IT_B1_209 | TK_MGR | Chuỗi: appoint `{E_W2, Leader}` vào T_SEW02 → dismiss E_W2 → transfer-in E_W3 | [API]: cả 3 trả 200; `GET M/Teams/<T_SEW02>` có `History` gồm đủ 3 sự kiện.<br>[DB]: AuditLogs +3; RoleCode của E_W2 cuối cùng là `WORKER`. | Normal |
| IT_B1_210 | Ghi `MAX(AuditLogs.Id)` trước khi chạy | Các thao tác thất bại: PUT với RowVer cũ (IT_B1_093), appoint vào tổ đã có Leader (IT_B1_142), add-members một phần không hợp lệ (IT_B1_133) | [API]: 409 / 400-409 / 400-409.<br>[DB]: `MAX(AuditLogs.Id)` **không đổi** (log ghi cùng transaction với thao tác chính và được rollback theo). | Abnormal |
| IT_B1_211 | Sau các ca resign/lock/discontinue/GradeCode ở trên | Truy vấn `SELECT DISTINCT Status` trên `Employee`, `Account`, `Team`, `GradeLevelCode` | [API]: —<br>[DB]: mọi giá trị đều VIẾT HOA (`ACTIVE`, `RESIGNED`, cùng các giá trị `LOCKED` / `DISCONTINUED` / `INACTIVE` `[?]`); không tồn tại `Active`, `Resigned`, `Inactive`. | Normal |
| IT_B1_212 | E_WH vừa được resign (IT_B1_099) | `GET A/Employees?Status=Resigned` và `GET A/Employees/<E_WH>` | [API]: E_WH có trong danh sách Resigned; chi tiết trả trạng thái dạng PascalCase hoặc nhãn hiển thị (khớp quy ước của DTO).<br>[DB]: `Status='RESIGNED'`; dữ liệu chuyển đổi DB ↔ DTO nhất quán ở cả 2 chiều. | Normal |
