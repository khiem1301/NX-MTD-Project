using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResultDto>> LoginAsync(LoginDto dto, CancellationToken ct);
    Task<Result<LoginResultDto>> RefreshAsync(RefreshTokenDto dto, CancellationToken ct);
    Task<Result<UserInfoDto>> GetMeAsync(int accountId, CancellationToken ct);
    /// <summary>Dùng ở middleware JWT cho mọi request: stamp khớp và tài khoản + nhân viên còn ACTIVE.</summary>
    Task<bool> IsSessionValidAsync(int accountId, Guid stamp, CancellationToken ct);
}
