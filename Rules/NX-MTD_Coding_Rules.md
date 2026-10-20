# NX-MTD – Quy tắc code thống nhất (Backend .NET + Frontend React)

> Áp dụng cho toàn bộ thành viên nhóm G40 (5 người), cho cả repo backend và repo frontend `nxmtd-web`. Mọi thành viên phải đọc trước khi code; mọi PR phải tuân thủ tài liệu này.
> Muốn thay đổi quy tắc: mở PR sửa file này, cả nhóm đồng ý rồi mới merge. Không tự ý làm khác.
> Code được phép đi trước tài liệu (Report, AI Context, DB design), nhưng **mọi chỗ code làm khác tài liệu phải ghi vào `docs/CODE_DEVIATIONS.md`** theo mục 8, để cuối dự án sửa lại Report nhanh.

**Stack:** ASP.NET Core 8 Web API · EF Core · SQL Server · React + TypeScript (Vite) · Ant Design · TanStack Query · Axios · Zustand · Jest + React Testing Library

**Nguồn:** gộp từ `CODING_RULES.md` (nguồn chính cho Backend, quy tắc chung, từ điển nghiệp vụ) và `NX-MTD-Frontend-Code-Standards.md` (nguồn chính cho Frontend khi hai file khác nhau). Các điểm đã chốt khi hai file mâu thuẫn được liệt kê ở **Phụ lục A**.

---

## Mục lục

1. Quy tắc chung
2. Hợp đồng API giữa BE và FE
3. Backend – ASP.NET Core
4. Frontend – React + TypeScript
5. Git & quy trình Pull Request
6. Checklist trước khi merge
7. Từ điển thuật ngữ nghiệp vụ
8. Ghi lại khác biệt giữa code và tài liệu (`CODE_DEVIATIONS.md`)
- Phụ lục A – Các điểm đã chốt khi hai file mâu thuẫn

---

## 1. Quy tắc chung

| Hạng mục | Quy tắc |
|---|---|
| Ngôn ngữ code | Tên biến, hàm, class, bảng, cột, type: **tiếng Anh**. Comment và chữ hiển thị cho người dùng: tiếng Việt. |
| Thuật ngữ nghiệp vụ | Dùng đúng bảng từ điển ở mục 7, không tự đặt tên khác cho cùng một khái niệm. Thuật ngữ mới phải thêm vào bảng trước khi code. |
| Encoding | UTF-8, xuống dòng LF, có dòng trống cuối file. Indent **4 space (C#)** / **2 space (TS/TSX, JSON, CSS)**. |
| Format tự động | `dotnet format` (BE), Prettier + ESLint (FE). Code chưa format không được merge. |
| Secret | Không commit connection string, JWT key, mật khẩu, token, file `.env`. BE dùng `appsettings.Development.json` (gitignore) hoặc User Secrets; FE dùng `.env` (gitignore), chỉ commit `.env.example`. |
| Ngày giờ | Lưu DB theo **UTC** (`DateTime.UtcNow`). API trả ISO 8601 UTC. FE hiển thị theo `Asia/Ho_Chi_Minh`, định dạng `dd/MM/yyyy HH:mm`. |
| Tiền | BE dùng `decimal(18,2)`, không dùng `float/double`. FE hiển thị dạng `1.250.000 ₫`. |
| Magic number/string | Cấm. BE đưa vào `const`, `enum` hoặc config; FE đưa vào `constants/`. |
| Code chết | Không để code comment-out, biến không dùng, `console.log`. `TODO` phải kèm tên người và mã issue: `// TODO(khiem, G40-31): ...`. |
| Khác tài liệu | Code làm khác Report-3 SRS, `NX-MTD_AI_Context.md`, `NX-MTD_Database.sql` hoặc tổng hợp Use Case thì **không cần sửa tài liệu ngay**, nhưng phải thêm một mục vào `docs/CODE_DEVIATIONS.md` **trong cùng PR** (mục 8). Không được làm khác mà không ghi lại. |

---

## 2. Hợp đồng API giữa BE và FE

Phần này là “luật chung” cả hai bên phải giữ giống nhau. BE là nguồn sự thật; FE không tự đổi tên field hay tự đoán contract.

### 2.1 URL & method

- RESTful, URL **kebab-case, danh từ số nhiều**, có **tiền tố role**: `/api/v1/<role>/<resource>`.
  Ví dụ: `/api/v1/storekeeper/material-issues`, `/api/v1/admin/production-orders/{id}`. Chức năng chung mọi role: `/api/v1/common/...` (auth, profile).
- Method: `GET` đọc · `POST` tạo · `PUT` cập nhật toàn bộ · `PATCH` cập nhật một phần/đổi trạng thái · `DELETE` xoá.
- Hành động nghiệp vụ không phải CRUD: `POST /api/v1/admin/payrolls/{id}/approve`.
- Phân trang/lọc: `?page=1&pageSize=20&sortBy=createdAt&sortDir=desc&keyword=...`; `pageSize` tối đa 100.

### 2.2 JSON

- Tên field **camelCase**. BE serialize camelCase; FE khai báo type đúng y tên field BE trả về, không tự đổi.
- Enum (trạng thái, role) trả về dạng **chuỗi PascalCase** (`"Pending"`, `"Approved"`) bằng `JsonStringEnumConverter`, để FE khai báo bằng object `as const` khớp giá trị (xem 4.5).
- Ngày giờ: chuỗi ISO 8601 UTC. Tiền: số (`number` ở FE), không gửi dạng chuỗi đã format.

### 2.3 Response chuẩn (bắt buộc)

Serialize từ `Application/Common/Result.cs` (BE) và khai báo tương ứng ở `src/types/` (FE):

```json
// Thành công
{ "success": true, "data": { ... }, "message": null }

// Danh sách phân trang
{ "success": true, "data": { "items": [], "page": 1, "pageSize": 20, "totalItems": 135 } }

// Lỗi
{ "success": false, "errorCode": "WAREHOUSE_INSUFFICIENT_STOCK",
  "message": "Tồn kho không đủ", "errors": { "quantity": ["Phải lớn hơn 0"] } }
```

Type FE tương ứng (`src/types/api.ts`):

```ts
export type ApiResponse<T> = { success: true; data: T; message: string | null };

export type Paginated<T> = { items: T[]; page: number; pageSize: number; totalItems: number };

export type ApiError = {
  success: false;
  errorCode: string;
  message: string;
  errors?: Record<string, string[]>;
};
```

- `errorCode`: chuỗi UPPER_SNAKE_CASE, tiền tố theo module: `WAREHOUSE_INSUFFICIENT_STOCK`, `PAYROLL_ALREADY_APPROVED`, `COMMON_CONCURRENCY_CONFLICT`. Danh sách mã lỗi khai báo một nơi ở BE (`Domain/Constants/ErrorCodes.cs`) và đồng bộ sang FE (`src/constants/errorCodes.ts`).

### 2.4 HTTP status – BE trả gì, FE xử lý thế nào

| Status | BE trả khi | FE xử lý (trong `apiClient` / hook) |
|---|---|---|
| 200 / 201 / 204 | OK / Tạo mới / Không có nội dung | Hiển thị dữ liệu / thông báo thành công |
| 400 | Sai dữ liệu đầu vào (FluentValidation), có `errors` theo field | Hiện lỗi dưới từng ô nhập theo `errors` |
| 401 | Chưa đăng nhập / token hết hạn | Refresh token **một lần**, thất bại thì đăng xuất |
| 403 | Không đủ quyền | Báo không có quyền |
| 404 | Không tìm thấy | Báo không tìm thấy dữ liệu |
| 409 | Xung đột nghiệp vụ: sai trạng thái, trùng mã, không đủ tồn kho, dữ liệu đã bị người khác sửa | Hiện `message` theo `errorCode`. Riêng `COMMON_CONCURRENCY_CONFLICT` thì báo dữ liệu đã thay đổi và tải lại |
| 500 / mất mạng | Lỗi hệ thống, chỉ do **middleware xử lý lỗi toàn cục** trả ra, không lộ stack trace | Thông báo chung, **giữ nguyên dữ liệu đang nhập** |

> Không dùng 422. Mọi lỗi nghiệp vụ đi qua 409 + `errorCode` (xem Phụ lục A, mục 5).

### 2.5 Khi BE chưa xong API

- Thống nhất contract (URL, DTO, `errorCode`) trên Swagger hoặc trong issue trước, rồi FE mock đúng contract đó. Không tự đoán field.
- Khuyến khích sinh type FE từ Swagger bằng `openapi-typescript`.

---

## 3. Backend – ASP.NET Core

### 3.1 Cấu trúc project

```
Project
├── API
│   └── Controllers
│       ├── Common/        # Auth, Profile – mọi role
│       ├── Admin/         # Users, Teams, ProductionOrders, Payrolls (duyệt)
│       ├── Accountant/    # Payrolls (lập), Attendances
│       ├── Storekeeper/   # GoodsReceipts, MaterialIssues, MaterialRequests, Inventories
│       ├── TeamLeader/    # Attendances, WorkerOutputs, MaterialRequests, MealRegistrations
│       └── QaQc/          # QualityInspections, LineOutputs
├── Application
│   ├── Common
│   │   └── Result.cs
│   ├── DTOs
│   │   └── ...Dto.cs
│   ├── Interfaces
│   │   ├── I...Service.cs
│   │   ├── IGenericRepository.cs
│   │   └── IUnitOfWork.cs
│   ├── Mappings
│   │   └── ...Profile.cs
│   ├── Validators
│   │   └── ...DtoValidator.cs
│   └── Services
│       └── ...Service.cs
├── Domain
│   ├── Constants
│   │   ├── Roles.cs
│   │   └── ErrorCodes.cs
│   └── Entities
├── Infrastructure
│   ├── Data
│   │   └── DbContext.cs
│   ├── Repositories
│   │   ├── GenericRepository.cs
│   │   └── ...Repository.cs
│   └── UnitOfWork
│       └── UnitOfWork.cs
├── SQL
│   ├── CreateAndSeed.sql          # bản đầy đủ, luôn khớp schema mới nhất
│   └── Migrations
│       └── 20261021_01_KH-05_add-order-note.sql   # mỗi thay đổi schema một file
├── Program.cs
└── appsettings.json
```

- Phụ thuộc một chiều: `API → Application → Domain`, `Infrastructure → Application/Domain`.
- **Domain** không reference EF Core, ASP.NET hay package hạ tầng nào.

### 3.2 Controller chia theo role

- **Folder = role** (trùng tên trong `Roles.cs`); chức năng chung → `Common/`.
- **Route** có tiền tố role: `api/v1/storekeeper/material-issues`.
- **Phân quyền** đặt ở base controller của role, mọi controller trong folder kế thừa.
- **Hai role cùng tài nguyên** (VD: Payrolls) → mỗi folder một controller, chỉ chứa action của role đó, dùng chung Service.
- Services, DTOs, Repositories **không** chia theo role.

```csharp
[ApiController]
[Route("api/v1/storekeeper/[controller]")]
[Authorize(Roles = Roles.Storekeeper)]
public abstract class StorekeeperBaseController : ControllerBase { }

public class MaterialIssuesController : StorekeeperBaseController { ... }
```

> `[controller]` sinh ra `MaterialIssues`; cấu hình route token transformer (slugify) để URL thành `material-issues`.

### 3.3 Đặt tên

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Class, method, property, enum | PascalCase | `MaterialIssueService`, `GetByIdAsync` |
| Biến local, tham số | camelCase | `orderId`, `totalQuantity` |
| Field private | `_camelCase` | `_dbContext`, `_logger` |
| Interface | Tiền tố `I` | `IMaterialIssueService` |
| Hàm async | Hậu tố `Async` | `CreateAsync` |
| Hằng số | PascalCase | `MaxPageSize` |
| DTO | `<Action><Entity>Dto` (đầu vào) / `<Entity>Dto` (đầu ra) | `CreateOrderDto`, `OrderDto` |
| Validator | `<Dto>Validator` | `CreateOrderDtoValidator` |
| AutoMapper | `<Entity>Profile` | `OrderProfile` |
| Repository | `<Entity>Repository` kế thừa `GenericRepository<T>` | `InventoryRepository` |
| Controller | Số nhiều + `Controller` | `OrdersController` |
| Bool | Tiền tố `Is/Has/Can` | `IsApproved`, `HasDefect` |

### 3.4 Controller

- Controller **mỏng**: nhận request → gọi Service → trả `Result`. Không viết logic nghiệp vụ, không gọi `DbContext`/Repository trực tiếp.
- Kế thừa base controller của role (mục 3.2); chỉ `Common/AuthController` được `[AllowAnonymous]` ở action login/refresh.
- Không trả Entity ra ngoài, **chỉ trả DTO** (map bằng AutoMapper Profile trong `Application/Mappings`).

### 3.5 Service & nghiệp vụ

- Mọi logic nghiệp vụ nằm ở `Application/Services`.
- Một method = một use case. Method > 40 dòng → tách.
- Service truy cập dữ liệu qua `IUnitOfWork` / Repository, không inject `DbContext` trực tiếp.
- Nghiệp vụ nhiều bước (VD: xuất kho + trừ tồn + ghi lịch sử) bắt buộc chạy trong **một transaction** của `UnitOfWork` và chỉ gọi `SaveChangesAsync()` một lần ở cuối.
- Lỗi nghiệp vụ → `return Result.Failure(ErrorCodes.WarehouseInsufficientStock, "Tồn kho không đủ")`. Không trả `null` để báo lỗi; chỉ `throw` cho lỗi hệ thống thật sự.
- Service **không biết role của controller gọi nó**; nếu cần kiểm tra quyền sở hữu dữ liệu (tổ trưởng chỉ xem tổ mình) thì nhận `userId`/`teamId` làm tham số.
- Truyền `CancellationToken` xuyên suốt các hàm async.

### 3.6 Validation

- Dùng **FluentValidation** cho mọi DTO đầu vào, đặt ở `Application/Validators`.
- Validate định dạng ở Validator (→ 400); validate nghiệp vụ (tồn kho, trạng thái đơn…) ở Service (→ 409).

### 3.7 Database & EF Core

- Bảng: **PascalCase số nhiều** (`ProductionOrders`), cột PascalCase, khoá chính `Id`, khoá ngoại `<Entity>Id`.
- Mọi entity kế thừa `BaseEntity`: `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted` (soft delete + global query filter).
- Cấu hình bằng **Fluent API** (`IEntityTypeConfiguration<T>`), không dùng Data Annotation trên entity.
- Thay đổi schema (bảng, cột, ràng buộc, view, trigger, proc, dữ liệu seed): trong **cùng một PR** phải có đủ 3 thứ: (1) **file migrate mới** trong `SQL/Migrations/`, (2) `SQL/CreateAndSeed.sql` được cập nhật cho khớp (ai tạo DB mới chạy một file là đủ), (3) Entity + `DbContext` sửa theo. Chi tiết ở mục 3.12.
- Truy vấn chỉ đọc dùng `AsNoTracking()`; chỉ `Select` cột cần dùng; cấm N+1 (dùng `Include`/projection).
- Trạng thái lưu bằng `enum` (lưu int hoặc string), không dùng chuỗi tự do. Khi trả API luôn là chuỗi (mục 2.2).
- Dữ liệu tồn kho, công, lương: **không xoá cứng**, mọi thay đổi phải truy vết được (audit log).

### 3.8 Bảo mật & phân quyền

- Xác thực JWT (access token ngắn hạn + refresh token). Mật khẩu hash bằng BCrypt/ASP.NET Identity.
- Role khai báo trong `Domain/Constants/Roles.cs`: `Admin`, `Accountant` (Kế toán), `Storekeeper` (Thủ kho), `TeamLeader` (Tổ trưởng), `QaQc`, `Worker`. FE dùng đúng các chuỗi này.
- Kiểm tra quyền ở **BE**; FE ẩn nút chỉ để trải nghiệm, không coi là bảo mật.
- Kiểm tra quyền sở hữu dữ liệu (tổ trưởng chỉ xem tổ mình).

### 3.9 Logging & lỗi

- Dùng `ILogger<T>` (Serilog). Không log mật khẩu, token, thông tin lương chi tiết.
- Mức log: `Information` cho nghiệp vụ quan trọng (duyệt lương, xuất kho), `Warning` cho lỗi nghiệp vụ, `Error` cho exception.
- Không `try/catch` nuốt lỗi. Chỉ bắt lỗi khi xử lý được, còn lại để middleware toàn cục xử lý.

### 3.10 Test

- Service nghiệp vụ quan trọng (tính lương, tính công, tồn kho) bắt buộc có unit test (xUnit + Moq/NSubstitute + FluentAssertions).
- Tên test: `MethodName_Condition_ExpectedResult` → `CreateAsync_StockNotEnough_ReturnsFailure`.
- Sửa bug thì thêm test tái hiện bug đó nếu có thể.

### 3.11 `.editorconfig` cho repo BE (tối thiểu)

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 2
trim_trailing_whitespace = true

[*.cs]
indent_size = 4

[*.md]
trim_trailing_whitespace = false
```

### 3.12 File migrate SQL (thay đổi database)

Mục đích: thành viên khác chỉ cần chạy các file migrate mới, **không phải xoá và tạo lại DB từ đầu** (mất dữ liệu test).

**Đặt tên và vị trí**
- Thư mục `SQL/Migrations/`, tên file `<yyyyMMdd>_<NN>_<mã task>_<mo-ta-ngan>.sql`; `NN` là số thứ tự trong ngày. VD `20261021_01_KH-05_add-order-note.sql`.
- Thứ tự chạy = thứ tự tên file. Một file một thay đổi có ý nghĩa; không gộp nhiều task vào một file.
- **Không sửa file migrate đã merge vào `develop`.** Sai thì thêm file migrate mới để sửa.

**Nội dung file**
- Có thể chạy lại mà không lỗi (idempotent), vì không có bảng theo dõi migrate: kiểm tra trước khi tạo/sửa bằng `IF COL_LENGTH('dbo.CustomerOrder', 'Note') IS NULL`, `IF OBJECT_ID(...) IS NULL`, `CREATE OR ALTER` cho view/proc/trigger/hàm.
- Dòng đầu là comment: mã task, người viết, ngày, mô tả, mã `DEV-...` nếu khác tài liệu (mục 8).
- Dùng tên bảng/cột đúng schema, `USE <db>` không hard-code. Bọc `BEGIN TRANSACTION` / `COMMIT` khi có nhiều lệnh; thêm cột `NOT NULL` vào bảng đã có dữ liệu phải kèm `DEFAULT` hoặc điền dữ liệu trước.
- Hạn chế xoá cột/bảng. Nếu bắt buộc: ghi trong mô tả PR và báo cả nhóm trước khi merge.
- Thay dữ liệu seed cũng là migrate (`IF NOT EXISTS ... INSERT`, hoặc `UPDATE` có điều kiện).

**Quy trình**
1. Viết migrate, chạy thử trên DB local đang có dữ liệu. Sau đó chạy `CreateAndSeed.sql` trên DB trống để chắc hai đường cho cùng một schema.
2. Mở PR gồm: file migrate + `CreateAndSeed.sql` + Entity/`DbContext`. Mô tả PR ghi tên file migrate và tóm tắt bảng/cột đổi; tag người có luồng dùng bảng đó.
3. Sau khi PR được merge, mọi thành viên `git pull` rồi chạy các file migrate mới theo thứ tự tên file.
4. Bản demo/deploy: chạy các migrate chưa chạy theo thứ tự, trước khi chạy bản BE mới.

---

## 4. Frontend – React + TypeScript (repo `nxmtd-web`)

### 4.1 Yêu cầu môi trường

| Công cụ | Phiên bản |
|---|---|
| Node.js | **≥ 24** (dùng bản LTS mới nhất của dòng 24) |
| Package manager | **npm** (không dùng yarn/pnpm) |
| Ngôn ngữ | **TypeScript** (`.ts`, `.tsx`) |
| Build tool | **Vite** |
| Editor | VS Code + extension **ESLint**, **Prettier**, **EditorConfig** |

`package.json`:

```json
{
  "engines": { "node": ">=24" }
}
```

File `.nvmrc` ở thư mục gốc:

```text
24
```

### 4.2 Cách chạy project

```bash
# 1. Kiểm tra phiên bản Node (phải >= 24)
node -v

# 2. Clone repo và vào thư mục
git clone <url-repo> nxmtd-web
cd nxmtd-web

# 3. Cài dependency đúng theo lockfile
npm ci

# 4. Tạo file môi trường từ file mẫu rồi sửa giá trị
cp .env.example .env

# 5. Chạy môi trường dev
npm run dev
```

| Lệnh | Tác dụng |
|---|---|
| `npm run dev` | Chạy dev server (mặc định http://localhost:5173) |
| `npm run typecheck` | Kiểm tra lỗi kiểu TypeScript |
| `npm run lint` | Kiểm tra lỗi ESLint |
| `npm run format` | Format code bằng Prettier |
| `npm run test` | Chạy test (Jest + React Testing Library) |
| `npm run build` | Kiểm tra kiểu rồi build production vào `dist/` |
| `npm run preview` | Chạy thử bản build |

```json
{
  "type": "module",
  "scripts": {
    "dev": "vite",
    "typecheck": "tsc --noEmit",
    "build": "tsc --noEmit && vite build",
    "preview": "vite preview",
    "lint": "eslint .",
    "format": "prettier --write .",
    "test": "jest"
  }
}
```

- Thêm package mới: `npm install <package>` (thư viện dev thêm `-D`), commit cả `package.json` và `package-lock.json`. Không sửa lockfile bằng tay.
- Tạo project từ đầu (chỉ người phụ trách setup repo, làm một lần): `npm create vite@latest nxmtd-web -- --template react-ts`.

Package chính và package dev cần cho cấu hình ở 4.4:

```bash
npm install antd @tanstack/react-query axios react-router-dom zustand zod

npm install -D typescript @types/react @types/react-dom @vitejs/plugin-react \
  eslint @eslint/js globals typescript-eslint eslint-plugin-react-hooks eslint-plugin-react-refresh \
  prettier jest ts-jest @types/jest jest-environment-jsdom identity-obj-proxy \
  @testing-library/react @testing-library/jest-dom @testing-library/user-event
```

### 4.3 Cấu trúc thư mục (feature-based)

```text
nxmtd-web/
├── public/                  # favicon, robots.txt
├── src/
│   ├── app/
│   │   ├── App.tsx
│   │   ├── router.tsx       # khai báo route + phân quyền theo role
│   │   └── providers.tsx    # QueryClientProvider, AuthProvider, ConfigProvider (antd)
│   ├── assets/              # ảnh, font, icon
│   ├── components/
│   │   ├── common/          # Button, Modal, DataTable, ConfirmModal… (không chứa nghiệp vụ)
│   │   ├── layout/          # Header, Sidebar (menu theo role), MainLayout
│   │   └── guards/          # ProtectedRoute, Can (ẩn/hiện theo role)
│   ├── features/
│   │   └── <feature-name>/  # auth, users, warehouse, cutting, sewing, qaqc, meal, payroll
│   │       ├── api/         # warehouseApi.ts – gọi API qua services/apiClient
│   │       ├── components/
│   │       ├── hooks/       # useMaterialIssues.ts (TanStack Query)
│   │       ├── pages/       # MaterialIssueListPage.tsx
│   │       ├── utils/
│   │       ├── constants.ts
│   │       ├── types.ts     # type request/response của feature (khớp DTO BE)
│   │       └── index.ts     # export công khai của feature
│   ├── hooks/               # useAuth, useDebounce, usePermission
│   ├── services/            # apiClient.ts, env.ts, queryClient.ts, tokenStorage.ts
│   ├── store/               # chỉ state client toàn cục (auth, UI) – Zustand
│   ├── constants/           # roles.ts, routes.ts, apiEndpoints.ts, messages.ts, errorCodes.ts
│   ├── utils/               # hàm thuần dùng chung: formatDate, formatCurrency
│   ├── styles/              # CSS toàn cục, theme antd
│   ├── types/               # type dùng chung: ApiResponse<T>, Paginated<T>, ApiError, Role
│   ├── test/                # setupTests.ts, mock dữ liệu
│   ├── vite-env.d.ts
│   └── main.tsx
├── .editorconfig  .env.example  .gitignore  .nvmrc  .prettierrc.json  .prettierignore
├── eslint.config.js  jest.config.js  tsconfig.json  vite.config.ts
├── index.html  package.json  package-lock.json  README.md
```

- **Feature chia theo nghiệp vụ, không theo role** (giống Services ở BE). Role chỉ quyết định route, menu và nút được hiển thị.
- Chỉ một feature dùng → đặt trong feature đó. ≥ 2 feature dùng → chuyển lên `components/`, `hooks/`, `utils/`, `types/`.
- Bên ngoài chỉ import feature qua `index.ts`: `import { MaterialIssueListPage } from '@/features/warehouse'`. Cấm import sâu vào file bên trong feature khác; feature không import chéo feature khác.
- `services/` chỉ chứa cấu hình HTTP và hạ tầng dùng chung. Hàm gọi từng endpoint nằm ở `features/<x>/api/`.
- `store/` **không** chứa dữ liệu lấy từ API (để TanStack Query quản lý).
- Đường dẫn API (có tiền tố role khớp BE) khai báo tập trung ở `constants/apiEndpoints.ts`.
- File test đặt cạnh file nguồn: `Button.tsx` / `Button.test.tsx`; `test/` chỉ dành cho setup và mock.
- Import trong `src` dùng alias `@/` thay cho đường dẫn tương đối dài (`../../../`).

### 4.4 File cấu hình (copy vào thư mục gốc repo)

#### `tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "noUncheckedIndexedAccess": true,
    "isolatedModules": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "noEmit": true,
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] },
    "types": ["vite/client", "jest", "@testing-library/jest-dom"]
  },
  "include": ["src", "vite.config.ts"]
}
```

#### `vite.config.ts`

```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: { port: 5173 },
});
```

#### `src/vite-env.d.ts`

```ts
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
```

#### `src/services/env.ts`

Mọi chỗ đọc biến môi trường đi qua file này (Jest không chạy được `import.meta.env`, khi test thì mock file này):

```ts
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL,
} as const;
```

#### `.editorconfig`

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 2
trim_trailing_whitespace = true

[*.md]
trim_trailing_whitespace = false
```

#### `.prettierrc.json`

```json
{
  "singleQuote": true,
  "semi": true,
  "trailingComma": "all",
  "printWidth": 100,
  "tabWidth": 2,
  "useTabs": false,
  "endOfLine": "lf"
}
```

#### `.prettierignore`

```text
node_modules
dist
coverage
package-lock.json
```

#### `eslint.config.js`

```js
import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 'latest',
      globals: { ...globals.browser },
    },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': [
        'warn',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/consistent-type-imports': 'warn',
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      eqeqeq: ['error', 'always'],
    },
  },
  {
    files: ['**/*.test.{ts,tsx}'],
    languageOptions: { globals: { ...globals.jest } },
  },
);
```

#### `jest.config.js`

```js
export default {
  testEnvironment: 'jsdom',
  transform: { '^.+\\.tsx?$': ['ts-jest', { tsconfig: 'tsconfig.json' }] },
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
    '\\.(css|scss)$': 'identity-obj-proxy',
  },
  setupFilesAfterEnv: ['<rootDir>/src/test/setupTests.ts'],
};
```

`src/test/setupTests.ts`:

```ts
import '@testing-library/jest-dom';
```

#### `.gitignore`

```text
node_modules/
dist/
coverage/
.env
.env.*
!.env.example
*.log
*.tsbuildinfo
```

#### `.env.example`

```ini
# Chỉ ghi tên biến và giá trị mẫu. Biến VITE_* sẽ lộ ra trình duyệt, không đặt secret.
VITE_API_BASE_URL=https://localhost:5001
```

#### `.vscode/settings.json` (khuyến nghị)

```json
{
  "editor.formatOnSave": true,
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "editor.codeActionsOnSave": { "source.fixAll.eslint": "explicit" },
  "typescript.tsdk": "node_modules/typescript/lib"
}
```

### 4.5 Đặt tên

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Component, file component | PascalCase, đuôi `.tsx`, tên file = tên component | `MaterialIssueForm.tsx` |
| Page | Hậu tố `Page` | `PayrollApprovalPage.tsx` |
| Hook | Tiền tố `use`, đuôi `.ts` | `useCreateMaterialIssue.ts` |
| Biến, hàm, file không phải component | camelCase, đuôi `.ts` | `warehouseApi.ts`, `formatCurrency` |
| Thư mục feature | kebab-case, số ít | `warehouse`, `meal` |
| Hằng số | UPPER_SNAKE_CASE | `DEFAULT_PAGE_SIZE`, `MAX_FILE_SIZE` |
| Type / Interface | PascalCase, không tiền tố `I` | `MaterialIssue`, `CreateMaterialIssuePayload` |
| Props type | `<Component>Props` | `MaterialIssueFormProps` |
| Handler / prop callback | `handle<Event>` / `on<Event>` | `handleSubmit`, `onApprove` |
| Bool | `is/has/can` | `isLoading`, `canApprove` |
| Route URL | kebab-case | `/warehouse/material-issues/:id` |

### 4.6 TypeScript

- Bật `strict` (cấu hình ở 4.4). **Không dùng `any`** (ESLint báo lỗi). Dữ liệu chưa rõ kiểu dùng `unknown` rồi kiểm tra trước khi dùng.
- Không dùng `// @ts-ignore`. Nếu bắt buộc thì dùng `// @ts-expect-error` kèm lý do.
- Hạn chế ép kiểu `as`, không dùng `as` để “cho qua lỗi”; không dùng non-null `!` khi chưa chắc giá trị tồn tại.
- Props khai báo bằng `type <Component>Props = {...}` và destructure ngay ở tham số.
- Type request/response khai báo trong `features/<tên>/types.ts`, tên field đúng như BE (camelCase).
- Trạng thái, role dùng object `as const` + union type, giá trị khớp chuỗi BE trả về (mục 2.2):

```ts
export const ORDER_STATUS = {
  PENDING: 'Pending',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
} as const;

export type OrderStatus = (typeof ORDER_STATUS)[keyof typeof ORDER_STATUS];
```

- Import kiểu dùng `import type { ... }`.

### 4.7 Component & viết code

- Chỉ dùng **function component + hooks**. Mỗi file một component chính.
- Component ≤ 200 dòng; vượt quá → tách component con hoặc custom hook.
- Component chỉ lo giao diện; logic xử lý dữ liệu đưa vào hook hoặc `utils/`.
- Gọi hooks ở top-level, khai báo đủ dependency cho `useEffect`, không tắt rule `react-hooks`.
- Không mutate props/state trực tiếp; không dùng index làm `key` khi có `id`.
- Không khai báo component bên trong component khác.
- Không để `console.log`, code chết, biến không dùng khi merge.
- Không viết magic number/string; đưa vào `constants/`.
- Không dùng `dangerouslySetInnerHTML` với dữ liệu chưa kiểm tra.

### 4.8 Gọi API & state

- **Không gọi `fetch`/axios trực tiếp trong component.** Luồng bắt buộc: component → hook (`features/<x>/hooks`) → hàm API (`features/<x>/api`) → `services/apiClient.ts`.
- Một `apiClient` duy nhất (`services/apiClient.ts`, tạo bằng Axios):
  - `baseURL` lấy từ `env.apiBaseUrl` (`services/env.ts`).
  - Tự gắn access token; 401 thì refresh token **một lần**, thất bại thì đăng xuất.
  - Chuẩn hoá lỗi về `ApiError` (`errorCode`, `message`, `errors`) và xử lý theo bảng ở mục 2.4.
- Mỗi hàm API khai báo kiểu trả về: `getMaterialIssues(params: MaterialIssueQuery): Promise<Paginated<MaterialIssue>>`.
- **Server state** (dữ liệu từ API): TanStack Query. Query key dạng mảng: `['material-issues', { page, keyword }]`. Sau mutation phải `invalidateQueries` đúng key.
- **Client state** (UI, user đăng nhập): `useState` / Context / Zustand. Không đưa dữ liệu API vào Zustand.
- Mọi màn hình gọi API phải xử lý đủ: **loading – có dữ liệu – rỗng – lỗi**.

```ts
// features/warehouse/hooks/useMaterialIssues.ts
export const useMaterialIssues = (params: MaterialIssueQuery) =>
  useQuery({
    queryKey: ['material-issues', params],
    queryFn: () => warehouseApi.getMaterialIssues(params),
  });
```

### 4.9 Form, thông báo & UI/UX

- Form dùng Ant Design `Form` (hoặc React Hook Form) + schema **Zod**. Input phải có `label`.
- Validate ở FE chỉ để báo sớm; **BE là nguồn sự thật**. Hiển thị lỗi field theo `errors` BE trả về (400).
- Đang gửi form thì disable nút submit để tránh gửi trùng.
- Thao tác không hoàn tác được (xoá, duyệt, từ chối, xác nhận, duyệt lương, xuất kho) phải có **modal xác nhận**.
- Chuỗi thông báo khai báo một lần trong `constants/messages.ts`, không viết cứng trong JSX. Thông báo kết quả bằng `message`/`notification` của antd, tiếng Việt, ngắn gọn.
- Dùng component của Ant Design; không tự viết lại bảng, modal, date picker.
- Mọi màn hình danh sách có: tìm kiếm, phân trang server-side, loading, trạng thái rỗng, thông báo lỗi.
- Ẩn/disable chức năng theo role bằng `usePermission()` / `<Can role="Admin">`.
- Giao diện dùng được trên màn hình ≥ 1280px; các màn hình thao tác tại xưởng (nhập sản lượng, đăng ký suất ăn) phải dùng được trên tablet.

### 4.10 Routing

- Khai báo tập trung ở `app/router.tsx`, lazy load theo page.
- Route bảo vệ bằng `<ProtectedRoute roles={[...]}>`; URL kebab-case: `/warehouse/material-issues`.

### 4.11 Bảo mật

- Không commit `.env`, token, mật khẩu. Không đặt secret trong biến `VITE_*`.
- Token không đưa vào URL hoặc log.
- Ẩn nút theo quyền chỉ để hiển thị; phân quyền thật nằm ở BE.

### 4.12 Kiểm thử

- Dùng **Jest + React Testing Library** (cấu hình ở 4.4).
- Bắt buộc test: `apiClient`, các hàm trong `utils/` (`formatDate`, `formatCurrency`…), form có validation, phân quyền route.
- Sửa bug thì thêm test tái hiện bug đó nếu có thể.

---

## 5. Git & quy trình Pull Request

Áp dụng chung cho cả repo BE và FE.

### 5.1 Branch

| Branch | Dùng cho |
|---|---|
| `main` | Bản ổn định, demo. Không push trực tiếp |
| `develop` | Tích hợp. Chỉ merge qua PR |
| `feature/<task-id>-<ten-ngan>` | Tính năng mới, tạo từ `develop` |
| `bugfix/<task-id>-<ten-ngan>` | Sửa lỗi, tạo từ `develop` |
| `hotfix/<ten-ngan>` | Sửa gấp trên `main`, merge lại vào cả `main` và `develop` |

- `<task-id>` là mã task trên bảng quản lý công việc của nhóm (VD `G40-23`, `UC53`, `BUG12`); `<ten-ngan>` kebab-case tiếng Anh không dấu.
- Ví dụ: `feature/G40-23-material-issue-api`, `feature/UC53-shift-output`, `bugfix/BUG12-date-format`.

### 5.2 Commit (Conventional Commits)

```text
<type>(<scope>): <mô tả ngắn>
```

- `type`: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`.
- `scope`: tên module/feature theo từ điển mục 7 (`warehouse`, `payroll`, `sewing`, `auth`…).
- Ví dụ:
  ```
  feat(warehouse): add material issue API
  fix(payroll): correct overtime calculation
  refactor(sewing): extract output validation
  docs: update coding rules
  ```
- Commit nhỏ, một mục đích; push lên remote ít nhất mỗi ngày làm việc.

### 5.3 Quy trình PR

1. Kéo `develop` mới nhất (rebase hoặc merge), tự giải quyết conflict.
2. Chạy kiểm tra, tất cả phải pass:
   - BE: `dotnet build` (không warning mới), `dotnet format --verify-no-changes`, `dotnet test`.
   - FE: `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`.
3. Nếu PR làm khác tài liệu gốc: thêm mục vào `docs/CODE_DEVIATIONS.md` theo mẫu ở mục 8 (commit chung trong PR).
4. Mở PR vào `develop`. PR nhỏ (**≤ 400 dòng thay đổi**, không tính file SQL sinh tự động). Mô tả ghi: mã task, làm gì, test thế nào, ảnh chụp màn hình nếu đổi UI, nếu đổi schema thì tên file migrate trong `SQL/Migrations/` và bảng/cột đổi, **mã DEV đã thêm** (VD `DEV-KH-05-1`) hoặc ghi "Không khác tài liệu". Khác biệt ảnh hưởng luồng người khác thì tag người đó vào PR.
5. Ít nhất **1 người review approve**, **Project Manager duyệt merge**, GitHub Actions phải xanh. Reviewer thấy code khác tài liệu mà chưa có mục DEV thì yêu cầu bổ sung trước khi approve.
6. Merge xong xoá branch.

### 5.4 Phân công review (nhóm 5 người)

| Vai trò | Trách nhiệm |
|---|---|
| Project Manager | Duyệt merge vào `develop` và `main`, tạo release |
| Back-end developer | Review mọi PR của BE |
| Front-end developer | Review mọi PR của web |
| Thành viên khác | Review chéo khi được giao, test chức năng trên `develop` |

---

## 6. Checklist trước khi merge

**Chung**
- [ ] Đúng tên branch, commit theo Conventional Commits, PR ≤ 400 dòng
- [ ] Không có secret, file `.env`, `appsettings.Development.json`
- [ ] Không có code comment-out, `TODO` thiếu tên + issue
- [ ] Dùng đúng thuật ngữ ở mục 7
- [ ] Code làm khác Report-3 / AI Context / DB design / Use Case → đã thêm mục vào `docs/CODE_DEVIATIONS.md` (mục 8) và ghi mã DEV trong mô tả PR
- [ ] CI (GitHub Actions) xanh, ≥ 1 approve

**Backend**
- [ ] `dotnet build` không warning mới, `dotnet format` sạch, `dotnet test` pass
- [ ] Controller nằm đúng folder role, kế thừa base controller của role đó
- [ ] Controller không chứa logic nghiệp vụ, chỉ trả DTO qua `Result`
- [ ] Có validator cho request mới; phân quyền đúng role
- [ ] Lỗi nghiệp vụ trả đúng status (400/409) và `errorCode` đã khai báo trong `ErrorCodes`
- [ ] Thao tác nhiều bước có transaction
- [ ] Đổi schema: có file migrate mới trong `SQL/Migrations/` (idempotent, đúng tên, đã chạy thử trên DB có dữ liệu), `CreateAndSeed.sql` đã cập nhật, Entity + `DbContext` đã sửa, mô tả PR ghi tên file migrate
- [ ] Unit test cho logic tính toán (lương, công, tồn kho)
- [ ] Swagger hiển thị đúng endpoint mới

**Frontend**
- [ ] `npm run typecheck`, `lint`, `test`, `build` đều pass
- [ ] Không có `any`, `@ts-ignore`, `console.log`
- [ ] Code đặt đúng feature, chỉ import feature khác qua `index.ts`, dùng alias `@/`
- [ ] Gọi API qua hook → `features/<x>/api` → `apiClient`; invalidate query sau mutation
- [ ] Có xử lý loading / empty / error
- [ ] Hành động không hoàn tác được có xác nhận; chức năng ẩn theo role
- [ ] Text hiển thị tiếng Việt lấy từ `constants/messages.ts`, định dạng ngày/tiền đúng chuẩn
- [ ] Có ảnh chụp màn hình nếu thay đổi giao diện

---

## 7. Từ điển thuật ngữ nghiệp vụ

Dùng thống nhất ở tên bảng, entity, API, type FE, scope commit.

| Tiếng Việt | Code (Entity / module) | API path |
|---|---|---|
| Đơn hàng (từ công ty mẹ) | `ProductionOrder` | `production-orders` |
| Nhập kho | `GoodsReceipt` | `goods-receipts` |
| Xuất kho | `MaterialIssue` | `material-issues` |
| Yêu cầu cấp vải (cắt bù / thiếu vải) | `MaterialRequest` | `material-requests` |
| Tồn kho | `Inventory` | `inventories` |
| Tổ cắt / Phiếu cắt | `CuttingRecord` | `cutting-records` |
| Chấm công theo ngày | `Attendance` | `attendances` |
| Tổ may / Công đoạn | `SewingOperation` | `sewing-operations` |
| Sản lượng công nhân theo công đoạn | `WorkerOutput` | `worker-outputs` |
| Hàng ra chuyền (sau QA/QC) | `LineOutput` | `line-outputs` |
| Kiểm hàng QA/QC | `QualityInspection` | `quality-inspections` |
| Suất ăn | `MealRegistration` | `meal-registrations` |
| Bảng lương | `Payroll` | `payrolls` |
| Tổ / Chuyền | `Team` | `teams` |

> Bổ sung thuật ngữ mới vào bảng này trước khi code.

---

## 8. Ghi lại khác biệt giữa code và tài liệu (`CODE_DEVIATIONS.md`)

Nhóm không đủ thời gian vừa code vừa sửa tài liệu. Vì vậy: **code và sửa trực tiếp, ghi lại khác biệt ngay lúc code, cuối dự án dựa vào file này để sửa Report** thay vì phải dò lại toàn bộ code.

### 8.1 File ở đâu

- Mỗi repo có **một** file sống: `docs/CODE_DEVIATIONS.md` (repo BE và repo `nxmtd-web` mỗi bên một file). Người dựng repo (Khiêm) tạo file từ mẫu ở 8.4.
- Chỉ **thêm mục mới vào cuối file**, không xoá mục cũ. Khi conflict lúc merge thì giữ cả hai mục.

### 8.2 Khi nào phải ghi

Phải ghi khi code làm **khác** một trong các tài liệu gốc sau:

| Tài liệu gốc | Ví dụ khác biệt |
|---|---|
| Report-3 SRS (UC, FR, quy tắc nghiệp vụ, MSG, mockup màn hình) | Bỏ/thêm bước trong luồng, đổi role được làm, đổi điều kiện kiểm tra, đổi câu thông báo, màn hình gộp/tách |
| `NX-MTD_AI_Context.md` (quy tắc đã chốt, phân công, ngoài phạm vi) | Làm một việc đang ghi là ngoài phạm vi, đổi trạng thái, đổi người sở hữu luồng |
| `NX-MTD_Database.sql` / `NX-MTD_Database_Giai_thich.md` | Thêm/xoá/đổi tên bảng, cột, ràng buộc, giá trị `Status`, view, trigger (mục DEV ghi thêm tên file migrate tương ứng, xem mục 3.12) |
| `NX-MTD_Tong_hop_Use_Case.md` | UC chưa làm, làm khác, hoặc phát sinh UC mới |
| Phụ lục A / quy tắc trong file này | Không ghi ở đây: đổi quy tắc code thì mở PR sửa file này (xem đầu file) |

**Không cần ghi:** chi tiết cài đặt mà tài liệu không nói tới (tên hàm, cách tách component), sửa bug để code khớp lại tài liệu, đổi giao diện nhỏ không đổi luồng.

Không chắc có phải khác biệt hay không thì **cứ ghi**; mục thừa dễ bỏ hơn mục thiếu.

### 8.3 Cách ghi

1. Mã mục: `DEV-<mã task>-<số thứ tự>`, VD `DEV-KH-05-1`, `DEV-TH-14-2`. Dùng mã task để hai người không trùng mã.
2. Ghi **trong cùng PR** với code tạo ra khác biệt, không để "ghi sau".
3. Ghi ngắn, đủ để người sửa Report hiểu mà không cần đọc code: tài liệu ghi gì, code làm gì, vì sao, sửa mục nào.
4. Nếu khác biệt ảnh hưởng luồng người khác hoặc schema chung: tag người đó vào PR và báo trong nhóm.
5. Sau này code quay lại đúng tài liệu thì **không xoá mục**, đổi trạng thái thành `Bỏ` và ghi lý do.
6. Cuối Phase 3 (trước khi nộp Report bản cuối), mỗi người tự sửa phần Report thuộc luồng mình theo các mục `Mở`, rồi đổi trạng thái sang `Đã sửa tài liệu`. PM kiểm tra không còn mục `Mở`.

### 8.4 Mẫu file `docs/CODE_DEVIATIONS.md`

````markdown
# NX-MTD – Khác biệt giữa code và tài liệu

> Ghi theo Coding Rules mục 8. Chỉ thêm mục mới vào cuối file. Trạng thái: `Mở` · `Đã sửa tài liệu` · `Bỏ`.
> Tài liệu gốc: Report-3 SRS (16/10), NX-MTD_AI_Context.md (19/10), NX-MTD_Database.sql (65 bảng), NX-MTD_Tong_hop_Use_Case.md (20/10).

## Bảng tổng hợp

| Mã | Ngày | Người | UC / bảng | Tóm tắt | Trạng thái |
|---|---|---|---|---|---|
| DEV-KH-05-1 | 21/10/2026 | Khiêm | UC09, `CustomerOrder` | Bỏ bước Admin duyệt đơn hàng | Mở |

---

### DEV-KH-05-1 · Bỏ bước Admin duyệt đơn hàng

- **Ngày / Người / Task / PR:** 21/10/2026 · Khiêm · KH-05 · #12
- **Ảnh hưởng tới:** UC09, FR-xx, bảng `CustomerOrder` (cột `Status`), MSG10
- **Tài liệu gốc ghi:** (trích ngắn, ghi rõ nguồn và mục) Report-3 UC09: Quản đốc tạo đơn → Admin duyệt → ...
- **Code đang làm:** Quản đốc tạo đơn là có hiệu lực ngay, không có trạng thái `PendingApproval`.
- **Lý do:** (phát hiện gì khi code, ai quyết định) ...
- **Ảnh hưởng người khác:** Thái (lệnh cắt không còn chờ duyệt) – đã báo trong PR.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: mục UC09 (luồng chính), sơ đồ trạng thái đơn hàng
  - [ ] `NX-MTD_AI_Context.md` mục 6.3
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: CHECK của `CustomerOrder.Status`
- **Trạng thái:** Mở
````

> Mỗi khác biệt: 1 dòng trong **Bảng tổng hợp** + 1 mục chi tiết ở cuối file.

### 8.5 Với AI hỗ trợ code (Claude, Copilot…)

- `docs/CODE_DEVIATIONS.md` là một phần của context: AI phải đọc file này cùng `NX-MTD_AI_Context.md` (code hiện tại có thể đã khác tài liệu).
- Khi AI viết code làm khác tài liệu gốc (theo 8.2), dù do người yêu cầu hay do AI đề xuất, AI phải **tự thêm mục vào `docs/CODE_DEVIATIONS.md` trong cùng lần sửa** và nói rõ với người dùng mục đã thêm.
- AI không được tự ý làm khác tài liệu chỉ vì "thấy hợp lý hơn": đề xuất trước, người code đồng ý thì mới làm và ghi lại.

---

## Phụ lục A – Các điểm đã chốt khi hai file mâu thuẫn

Nguyên tắc: BE theo `CODING_RULES.md`; FE theo `NX-MTD-Frontend-Code-Standards.md`; phần hợp đồng API do BE quyết định vì FE phải theo BE.

| # | Điểm lệch | File 1 (CODING_RULES) | File 2 (FE Standards) | Đã chốt |
|---|---|---|---|---|
| 1 | Tên HTTP client | `axiosClient.ts` | `apiClient.ts` | `services/apiClient.ts` (FE theo file 2) |
| 2 | Biến môi trường API | `VITE_API_URL` | `VITE_API_BASE_URL` | `VITE_API_BASE_URL`, đọc qua `services/env.ts` |
| 3 | Branch sửa lỗi | `fix/<mã-task>-...` | `bugfix/...` + `hotfix/...` | `bugfix/` + `hotfix/` cho cả BE và FE (file 2 đầy đủ hơn; commit type vẫn là `fix`) |
| 4 | Test FE | Không nêu framework | Jest + RTL | Jest + React Testing Library |
| 5 | Lỗi nghiệp vụ | 400 validation, 409 xung đột nghiệp vụ | 409 = dữ liệu đã đổi, 422 = lỗi nghiệp vụ theo mã MSG | Giữ BE: **400 + 409, không dùng 422**. FE xử lý 409 bằng `message` theo `errorCode`; xung đột đồng thời dùng mã riêng `COMMON_CONCURRENCY_CONFLICT` |
| 6 | Mã lỗi | `errorCode` dạng chuỗi | “mã MSG” | `errorCode` dạng UPPER_SNAKE_CASE |
| 7 | Endpoint | `/api/v1/<role>/<resource>` | `/api/v1/<resource>` | Có tiền tố role (theo BE) |
| 8 | Tên field API | JSON camelCase | Đặt đúng như BE | Không mâu thuẫn: BE trả camelCase, FE dùng y nguyên |
| 9 | Kiểu phân trang FE | `PagedResult<T>` | `Paginated<T>` | `Paginated<T>` (FE theo file 2), shape giữ theo BE: `items, page, pageSize, totalItems` |
| 10 | `@ts-ignore` / `as` | Cấm cả hai | Cho `@ts-expect-error` kèm lý do; hạn chế `as` | Theo file 2 |
| 11 | Trạng thái/role ở FE | Không nêu | Object `as const` | `as const`; BE thêm quy tắc trả enum dạng chuỗi để khớp |
| 12 | Vị trí page trong feature | `pages/` + `index.ts` | File page ở gốc feature | Giữ `pages/` + `index.ts` (chi tiết hơn, file 2 chỉ là ví dụ) |
| 13 | `.env` | `.env.local` | `.env` + `.env.example` | `.env` (gitignore) + commit `.env.example` |
| 14 | Git, commit, checklist trùng lặp | Mục 4–5 | Mục 6 | Gộp thành mục 5–6; giữ PR ≤ 400 dòng (file 1) và PM duyệt merge, CI xanh, xoá branch (file 2) |

**Cần Morgan xác nhận (đang tạm theo lựa chọn ở bảng trên):**
- Mục 5: nếu nhóm muốn tách “lỗi nghiệp vụ” sang 422 như file 2 thì BE phải đổi theo; hiện chọn 409 để giữ nguyên BE.
- Mục 3: tên branch `bugfix/` áp dụng cho cả repo BE.
- Mục 5.4: thêm vai trò “Back-end developer” review PR BE (file 2 chỉ nói về web).
