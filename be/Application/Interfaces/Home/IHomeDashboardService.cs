using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>DEV-ALL-170: số liệu trang chủ theo vai trò (chỉ đọc).</summary>
public interface IHomeDashboardService
{
    Task<Result<AdminHomeDto>> GetAdminAsync(CancellationToken ct);
    Task<Result<ManagerHomeDto>> GetManagerAsync(CancellationToken ct);
    Task<Result<AccountantHomeDto>> GetAccountantAsync(CancellationToken ct);
    Task<Result<WarehouseHomeDto>> GetWarehouseAsync(CancellationToken ct);

    /// <summary>Tổ trưởng / tổ phó của tổ đang đăng nhập; rẽ nhánh theo loại tổ (cắt / may).</summary>
    Task<Result<TeamLeaderHomeDto>> GetTeamLeaderAsync(CancellationToken ct);
    Task<Result<QaqcHomeDto>> GetQaqcAsync(CancellationToken ct);
    Task<Result<WorkerHomeDto>> GetWorkerAsync(CancellationToken ct);
    Task<Result<ElectricianHomeDto>> GetElectricianAsync(CancellationToken ct);
}
