using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>Số mét vải hệ thống tính cho lệnh cắt và phần còn xin được theo trần của lệnh (DEV-ALL-92).</summary>
public interface IWorkOrderFabricNeeds
{
    /// <summary>Kèm dòng lệnh → dòng đơn → màu, size.</summary>
    Task<WorkOrder?> LoadAsync(int workOrderId, CancellationToken ct);

    /// <param name="exceptRequestId">Đơn đang sửa: không tính số của chính đơn đó vào "đã xin".</param>
    Task<IReadOnlyList<WorkOrderMaterialAllowanceDto>> AllowanceAsync(WorkOrder wo, int? exceptRequestId, CancellationToken ct);
}
