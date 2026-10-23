using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC48 / UC49: kiểm kê kho. Thủ kho lập phiếu (số sổ chụp lúc lập), Quản đốc / Admin duyệt (điều chỉnh kho qua
/// IStockService) hoặc trả lại có lý do.</summary>
public interface IStocktakeService
{
    Task<Result<PagedResult<StocktakeListItemDto>>> GetPagedAsync(StocktakeQuery query, CancellationToken ct);
    Task<Result<StocktakeDto>> GetByIdAsync(int stocktakeId, CancellationToken ct);
    Task<Result<StocktakeDto>> CreateAsync(SaveStocktakeDto dto, CancellationToken ct);

    /// <summary>Sửa phiếu bị trả lại rồi gửi lại duyệt; số sổ được chụp lại.</summary>
    Task<Result<StocktakeDto>> UpdateAsync(int stocktakeId, SaveStocktakeDto dto, CancellationToken ct);

    Task<Result> ApproveAsync(int stocktakeId, CancellationToken ct);
    Task<Result> ReturnAsync(int stocktakeId, ReasonDto dto, CancellationToken ct);
}
