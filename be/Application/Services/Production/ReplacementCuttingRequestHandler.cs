using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// KI-09 (UC56, UC07 "cắt bù") — kiểm đơn cắt bù do tổ trưởng / tổ phó tổ may tạo từ một phiếu hàng hỏng Scrap, Quản đốc
/// duyệt. Đơn phải gắn đúng một phiếu Scrap còn hiệu lực của tổ mình (DB cho mỗi phiếu tối đa một đơn: UX_Request_Defect),
/// và mỗi dòng là một chi tiết cần cắt bù (<c>ProductComponentId</c>, chi tiết PANEL của mã hàng) của đúng dòng đơn đó, số lượng từ 1 đến số hàng đã hủy.
/// Chưa có lệnh cắt bù của tổ cắt tự hoàn thành đơn nên <see cref="AllowManualComplete"/> = true (DEV-KI-09-1).
/// </summary>
public sealed class ReplacementCuttingRequestHandler : IRequestTypeHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public ReplacementCuttingRequestHandler(IUnitOfWork uow, ICurrentUser user)
    {
        _uow = uow; _user = user;
    }

    public string RequestType => RequestTypes.ReplacementCutting;

    public bool AllowManualComplete => true;

    public async Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.DefectRecordId is null) return Invalid(Messages.ReplacementCuttingNeedsDefect);

        var defect = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.DefectRecordId == draft.DefectRecordId)
            .Select(d => new { d.DefectType, d.Status, d.SewingTeamId, d.OrderLineId, d.OrderLine.OrderId, d.OrderLine.ProductId, d.Quantity })
            .FirstOrDefaultAsync(ct);
        if (defect is null) return Fail.NotFound();
        if (defect.Status == DefectStatuses.Cancelled) return Invalid(Messages.ReplacementCuttingDefectCancelled);
        if (defect.DefectType != DefectTypes.Scrap) return Invalid(Messages.ReplacementCuttingDefectNotScrap);

        // Tổ trưởng / tổ phó chỉ tạo cho phiếu của tổ mình (người xử lý khác không đi qua bước tạo).
        if (_user.Role is Roles.TeamLeader or Roles.TeamDeputy && defect.SewingTeamId != _user.TeamId)
            return Invalid(Messages.ReplacementCuttingOtherTeam);

        if (draft.OrderId != defect.OrderId) return Invalid(Messages.ReplacementCuttingOrderMismatch);

        if (draft.Lines.Count == 0
            || draft.Lines.Any(l => l.ProductComponentMaterialId is null || l.OrderLineId != defect.OrderLineId
                                    || l.Quantity < 1 || l.Quantity != decimal.Truncate(l.Quantity) || l.Quantity > defect.Quantity)
            || draft.Lines.GroupBy(l => l.ProductComponentMaterialId).Any(g => g.Count() > 1))
            return Invalid(Messages.ReplacementCuttingLines);

        // Dòng cắt bù phải là dòng định mức (chi tiết PANEL × vải) đang dùng của đúng biến thể của phiếu hàng hỏng
        // (trg_RequestLine_Check cũng chặn lệch màu × size).
        var normIds = draft.Lines.Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToList();
        var variantId = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderLineId == defect.OrderLineId).Select(l => l.ProductVariantId).FirstAsync(ct);
        var validNorms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .CountAsync(p => normIds.Contains(p.ProductComponentMaterialId) && p.ProductVariantId == variantId
                             && p.ProductComponent.ComponentType == ComponentTypes.Panel
                             && p.ProductComponent.Status == RecordStatus.Active, ct);
        if (validNorms != normIds.Count) return Invalid(Messages.ReplacementCuttingLines);

        var duplicate = await _uow.Repository<Request>().Query().AsNoTracking()
            .AnyAsync(r => r.DefectRecordId == draft.DefectRecordId && (draft.RequestId == null || r.RequestId != draft.RequestId), ct);
        return duplicate ? Invalid(Messages.ReplacementCuttingDuplicate) : Result.Success();
    }

    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) =>
        Task.FromResult(Result.Success());

    private static Result Invalid(string message) =>
        Result.Failure(ErrorCodes.ReplacementCuttingInvalid, message, ResultKind.Validation);
}
