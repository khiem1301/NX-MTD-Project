using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-192: kiểm sheet NVL (thông tin đầy đủ của NVL) với danh mục NVL. Mã đã có trong hệ thống: mọi thông tin (tên, loại, đơn vị,
/// màu, khổ vải) phải trùng, khác thì lỗi "trùng mã nhưng khác thông tin". Mã chưa có: kiểm như tạo NVL tay, hợp lệ thì là NVL mới
/// (thêm vào danh mục khi lưu). Không ghi gì.
/// </summary>
internal sealed class MaterialSheetChecker
{
    private const string ActionNew = SpecImportBuilder.ActionNew;
    private const string ActionUnchanged = SpecImportBuilder.ActionUnchanged;
    private const string ActionError = SpecImportBuilder.ActionError;

    private readonly IUnitOfWork _uow;

    public MaterialSheetChecker(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<MaterialCheckRowDto>> CheckAsync(IReadOnlyList<MaterialExcelRow> rows, CancellationToken ct)
    {
        var codes = rows.Select(r => r.MaterialCode).Where(c => c.Length > 0).Distinct().ToList();
        var names = rows.Select(r => r.MaterialName?.Trim() ?? string.Empty).Where(n => n.Length > 0).Distinct().ToList();
        var found = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => codes.Contains(m.MaterialCode) || names.Contains(m.MaterialName)).ToListAsync(ct);
        var byCode = found.GroupBy(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<MaterialCheckRowDto>(rows.Count);
        foreach (var row in rows)
        {
            var duplicate = row.ParseError is null && seen.TryGetValue(row.MaterialCode, out var first)
                ? string.Format(Messages.SpecImportDuplicateRow, first) : null;
            if (row.ParseError is null && duplicate is null) seen[row.MaterialCode] = row.RowNumber;
            result.Add(CheckRow(row, duplicate, byCode, found));
        }
        return result;
    }

    private static MaterialCheckRowDto CheckRow(MaterialExcelRow row, string? duplicate, Dictionary<string, Material> byCode,
        List<Material> found)
    {
        var error = row.ParseError ?? duplicate;
        if (error is not null) return Error(row, error);
        if (byCode.TryGetValue(row.MaterialCode, out var existing))
        {
            var diffs = Differences(existing, row);
            return diffs.Count > 0
                ? Error(row, string.Format(Messages.MaterialSheetConflict, row.MaterialCode, string.Join(", ", diffs)))
                : new MaterialCheckRowDto(row.RowNumber, ActionUnchanged, null);
        }
        error = NewMaterialProblem(row, found);
        return error is not null ? Error(row, error)
            : new MaterialCheckRowDto(row.RowNumber, ActionNew, string.Format(Messages.MaterialSheetNew, row.MaterialCode));
    }

    private static MaterialCheckRowDto Error(MaterialExcelRow row, string error) => new(row.RowNumber, ActionError, error);

    /// <summary>Tên các thông tin của dòng khác với NVL đang có trong hệ thống (rỗng = trùng hoàn toàn). Vải luôn tính theo mét nên bỏ qua đơn vị.</summary>
    private static List<string> Differences(Material m, MaterialExcelRow row)
    {
        var diffs = new List<string>();
        static bool Same(string? a, string? b) => string.Equals(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var fabric = m.MaterialType == MaterialTypes.Fabric;
        if (!Same(m.MaterialName, row.MaterialName)) diffs.Add(Messages.MaterialFieldName);
        if (!Same(m.MaterialType, row.MaterialType)) diffs.Add(Messages.MaterialFieldType);
        if (!fabric && !Same(m.Unit, row.Unit)) diffs.Add(Messages.MaterialFieldUnit);
        if (!Same(m.Color, row.Color)) diffs.Add(Messages.MaterialFieldColor);
        if (fabric && (m.WidthM ?? 0m) != (row.WidthM ?? 0m)) diffs.Add(Messages.MaterialFieldWidth);
        return diffs;
    }

    /// <summary>Mã chưa có: kiểm như tạo NVL tay (validator tạo NVL) và không trùng (tên, loại, màu) với NVL khác.</summary>
    private static string? NewMaterialProblem(MaterialExcelRow row, List<Material> found)
    {
        var dto = new CreateMaterialDto(row.MaterialCode, row.MaterialName ?? string.Empty, row.MaterialType ?? string.Empty,
            row.Unit ?? string.Empty, row.Color, row.WidthM);
        var invalid = new CreateMaterialDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage).FirstOrDefault();
        if (invalid is not null) return invalid;
        var name = dto.MaterialName.Trim();
        var color = dto.Color?.Trim() ?? string.Empty;
        return found.Any(m => m.MaterialName == name && m.MaterialType == dto.MaterialType && m.Color == color)
            ? WarehouseMessages.MaterialNameDuplicate : null;
    }
}
