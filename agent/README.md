# NX-MTD — Agent chấm công Ronald Jack Pro

Console .NET chạy trên máy Windows cài phần mềm Ronald Jack Pro. Mỗi chu kỳ agent:

1. Gọi `GET /api/v1/agent/attendance-sync/cursor` để lấy mốc `CheckInOut.ID` đã đồng bộ và chu kỳ chạy (`ATTENDANCE_SYNC_MINUTES`).
2. Đọc bảng `CheckInOut` từ `RJData.mdb` (Access) bằng OleDb, chỉ 3 cột `ID`, `MaChamCong`, `GioCham`, điều kiện `ID > mốc`.
3. Gửi lô qua `POST /api/v1/agent/attendance-sync/punches` với header `X-Agent-Key`.
4. Bỏ trùng, bỏ tháng khóa, tính lại bảng công do thủ tục `usp_Attendance_IngestBatch` phía DB lo (GBR-16) — gửi lại cùng lô không sinh bản ghi trùng.

## Yêu cầu

- .NET 9 SDK.
- **Microsoft Access Database Engine 2010 Redistributable (ACE 12.0)** đã cài trên máy chạy agent (để đọc `.mdb`).
- Khóa `X-Agent-Key` của agent phải trùng với `Attendance:AgentApiKey` đã cấu hình phía backend.

## Cấu hình

Mặc định trong `appsettings.json`; ghi đè bằng biến môi trường (không commit secret vào repo).

| Khóa (`Agent`) | Biến môi trường | Ý nghĩa | Mặc định |
|---|---|---|---|
| `ApiBaseUrl` | `Agent__ApiBaseUrl` | Địa chỉ API backend | `http://localhost:5080` |
| `ApiKey` | `Agent__ApiKey` | Khóa xác thực agent (trùng backend) | rỗng — bắt buộc đặt |
| `DbPath` | `Agent__DbPath` | Đường dẫn đầy đủ tới `RJData.mdb` | rỗng — bắt buộc đặt |
| `Provider` | `Agent__Provider` | OleDb provider | `Microsoft.ACE.OLEDB.12.0` |
| `BatchSize` | `Agent__BatchSize` | Số lượt/lô (trần API là 5000) | `5000` |
| `PollIntervalMinutes` | `Agent__PollIntervalMinutes` | Chu kỳ dự phòng khi chưa lấy được mốc từ API | null → 10 phút |

Ví dụ đặt biến môi trường (máy chạy agent):

```bat
setx Agent__ApiKey "khoa-giong-backend"
setx Agent__DbPath "C:\Ronald Jack Pro\Data\RJData.mdb"
```

## Build & chạy

```bat
cd agent
dotnet build
dotnet run
```

Chạy nền 24/7: dùng **Task Scheduler** của Windows chạy lúc khởi động, hoặc cài làm Windows Service (dùng `sc.exe` + `dotnet publish -r win-x64`).

## Lưu ý

- **32/64-bit:** nếu driver ACE chỉ là bản 32-bit, build agent với `dotnet publish -r win-x86` để khớp kiến trúc driver, nếu không OleDb báo lỗi "provider not registered".
- `RJData.mdb` có thể đang bị phần mềm RJ mở; OleDb mở ở chế độ đọc chung thường ổn. Nếu gặp lỗi khóa file, agent tự thử lại chu kỳ sau.
- Agent **không** đọc vân tay, mật khẩu, ảnh hay bảng nhân viên/máy của RJ (Rules mục 4.6).
- `appsettings.Development.json` đã nằm trong `.gitignore` — có thể tạo bản local chứa `ApiKey`/`DbPath` thật để không phải đặt biến môi trường.
