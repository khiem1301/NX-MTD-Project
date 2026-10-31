using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Màn "NVL chờ nhận" của Thủ kho: NVL khách sẽ cấp và đơn mua đã đặt mà kho chưa nhận đủ. Chỉ đọc.</summary>
public interface IIncomingMaterialService
{
    Task<Result<IncomingMaterialsDto>> GetAsync(CancellationToken ct);
}
