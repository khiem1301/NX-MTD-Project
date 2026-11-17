using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-118: nhập NVL của đơn từ Excel (Mã NVL, Số cần, Khách cấp, Ghi chú). Gộp theo NVL: NVL đã có trong đơn thì cập nhật theo
/// file, NVL chưa có thì thêm, NVL không có trong file giữ nguyên. Mỗi dòng qua đúng kiểm tra của thêm / sửa tay (ApplyAsync).
/// Các dòng hợp lệ có thay đổi được lưu bằng một lần SaveChanges, nên có lỗi thì không lưu gì.
/// </summary>
public sealed partial class OrderMaterialService
{
    private const string ActionNew = "New";
    private const string ActionUpdate = "Update";
    private const string ActionUnchanged = "Unchanged";
    private const string ActionError = "Error";

    /// <summary>Dòng đã kiểm: Entity là bản sẽ ghi (mới hoặc dòng đang có đã gán giá trị file); null khi lỗi / không đổi.</summary>
    private sealed record ImportPlanRow(OrderMaterialImportRowDto Preview, OrderMaterial? Entity, bool IsNew, decimal OldCustomerQty);

    private sealed record ImportPlan(CustomerOrder Order, List<ImportPlanRow> Rows, int Duplicates);

    public async Task<Result<OrderMaterialImportPreviewDto>> PreviewImportAsync(int orderId, Stream stream, CancellationToken ct)
    {
        var plan = await PlanImportAsync(orderId, stream, tracked: false, ct);
        if (!plan.IsSuccess) return Fail.As<OrderMaterialImportPreviewDto>(plan);
        var p = plan.Data!;
        var errors = p.Rows.Count(r => r.Preview.Action == ActionError);
        return Result<OrderMaterialImportPreviewDto>.Success(new OrderMaterialImportPreviewDto(p.Rows.Select(r => r.Preview).ToList(),
            p.Rows.Count - errors, errors - p.Duplicates, p.Duplicates, p.Rows.Count(r => r.Entity is not null)));
    }

    public async Task<Result<OrderMaterialImportResultDto>> ImportAsync(int orderId, Stream stream, CancellationToken ct)
    {
        var plan = await PlanImportAsync(orderId, stream, tracked: true, ct);
        if (!plan.IsSuccess) return Fail.As<OrderMaterialImportResultDto>(plan);
        var changes = plan.Data!.Rows.Where(r => r.Entity is not null).ToList();
        if (changes.Count == 0)
            return Result<OrderMaterialImportResultDto>.Failure(ErrorCodes.OrderMaterialImportInvalid, WarehouseMessages.OrderMaterialImportNothing,
                ResultKind.Validation);

        var order = plan.Data.Order;
        foreach (var row in changes)
        {
            var entity = row.Entity!;
            if (row.IsNew) await _uow.Repository<OrderMaterial>().AddAsync(entity);
            if (entity.CustomerQty > 0 && entity.CustomerQty != row.OldCustomerQty)
                await NotifyCustomerSupplyAsync(order.OrderId, order.OrderCode, entity, ct);
        }
        await _uow.SaveChangesAsync();

        var created = changes.Count(r => r.IsNew);
        var result = new OrderMaterialImportResultDto(created, changes.Count - created);
        return Result<OrderMaterialImportResultDto>.Success(result,
            string.Format(WarehouseMessages.OrderMaterialImportDone, result.Created, result.Updated));
    }

    private async Task<Result<ImportPlan>> PlanImportAsync(int orderId, Stream stream, bool tracked, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return Fail.NotFound<ImportPlan>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<ImportPlan>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed);

        IReadOnlyList<OrderMaterialExcelRow> excel;
        try
        {
            excel = _excel.Parse(stream);
        }
        catch (InvalidDataException ex)
        {
            return Result<ImportPlan>.Failure(ErrorCodes.OrderMaterialImportInvalid, ex.Message, ResultKind.Validation);
        }

        var codes = excel.Select(r => r.MaterialCode).Where(c => c.Length > 0).Distinct().ToList();
        var materials = (await _uow.Repository<Material>().Query().AsNoTracking().Where(m => codes.Contains(m.MaterialCode))
                .ToListAsync(ct))
            .GroupBy(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var query = OrderMaterials.Where(om => om.OrderId == orderId);
        var existing = (tracked ? await query.ToListAsync(ct) : await query.AsNoTracking().ToListAsync(ct))
            .ToDictionary(om => om.MaterialId);

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var rows = new List<ImportPlanRow>(excel.Count);
        foreach (var row in excel)
        {
            var dup = row.ParseError is null && seen.TryGetValue(row.MaterialCode, out var first)
                ? string.Format(Messages.SpecImportDuplicateRow, first) : null;
            if (dup is not null) duplicates++;
            else if (row.ParseError is null) seen[row.MaterialCode] = row.RowNumber;
            rows.Add(await PlanRowAsync(order.OrderId, row, dup, materials, existing, ct));
        }
        return Result<ImportPlan>.Success(new ImportPlan(order, rows, duplicates));
    }

    private async Task<ImportPlanRow> PlanRowAsync(int orderId, OrderMaterialExcelRow row, string? duplicate,
        Dictionary<string, Material> materials, Dictionary<int, OrderMaterial> existing, CancellationToken ct)
    {
        materials.TryGetValue(row.MaterialCode, out var material);
        var required = row.RequiredQty ?? 0;
        var customer = row.CustomerQty ?? 0;
        var input = new CreateOrderMaterialLineDto(material?.MaterialId ?? 0, required, customer, row.Note);
        var error = row.ParseError ?? duplicate
                    ?? (material is null ? string.Format(WarehouseMessages.OrderMaterialImportUnknown, row.MaterialCode) : null)
                    ?? new OrderMaterialInputValidator().Validate(input).Errors.Select(e => e.ErrorMessage).FirstOrDefault();

        OrderMaterial? current = material is null ? null : existing.GetValueOrDefault(material.MaterialId);
        var entity = current ?? new OrderMaterial { OrderId = orderId };
        var oldCustomer = entity.CustomerQty;
        var unchanged = current is not null && current.RequiredQty == required && current.CustomerQty == customer
                        && (current.Note ?? string.Empty) == (row.Note ?? string.Empty);
        if (error is null && !unchanged)
        {
            // Đúng kiểm tra của thêm / sửa tay: NVL ngừng dùng, trùng, khách đã giao đủ số lần mà tăng khách cấp.
            var check = await ApplyAsync(entity, input, ct);
            if (check is not null) error = check.Message;
        }

        var action = error is not null ? ActionError : unchanged ? ActionUnchanged : current is null ? ActionNew : ActionUpdate;
        var preview = new OrderMaterialImportRowDto(row.RowNumber, action, error, row.MaterialCode, material?.MaterialName,
            material?.Color, material?.Unit, row.RequiredQty, row.CustomerQty,
            row.RequiredQty is { } r && row.CustomerQty is { } c ? Math.Max(r - c, 0) : null, row.Note);
        return new ImportPlanRow(preview, action is ActionNew or ActionUpdate ? entity : null, current is null, oldCustomer);
    }
}
