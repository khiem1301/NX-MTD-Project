using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Bàn cắt chỉ ghi khi đã cắt: ngày cắt không sau hôm nay, không trước ngày lập lệnh cắt.</summary>
public class CuttingLayServiceTests : ProductionTestBase
{
    private const int TeamId = 2;

    public CuttingLayServiceTests()
    {
        var wo = new WorkOrder
        {
            WorkOrderId = 7, CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting, CreatedAt = new DateTime(2026, 10, 15, 8, 0, 0),
        };
        wo.WorkOrderLines.Add(new WorkOrderLine
        {
            WorkOrderId = 7, OrderLineId = 11, PlannedQty = 100,
            OrderLine = new OrderLine { OrderLineId = 11, ProductVariant = new ProductVariant { ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "M" } },
        });
        SetupQueryable(new List<WorkOrder> { wo });
        SetupQueryable(new List<CuttingLay>());
        SetupQueryable(new List<ProductComponentMaterial>());
    }

    private Task<Result<CuttingLayDto>> Create(DateOnly date) =>
        new CuttingLayService(Uow, Substitute.For<IAppLock>(), Clock).CreateAsync(
            new SaveCuttingLayDto(7, date, ShiftCodes.Morning, 1, 10, 2m, null, [new SaveLayLineDto(11, null, 2, null, null)]), TeamId, default);

    [Fact]
    public async Task WorkDate_InTheFuture_IsRefused() =>
        (await Create(Today.AddDays(1))).Message.Should().Be(WarehouseMessages.LayWorkDateFuture);

    [Fact]
    public async Task WorkDate_BeforeTheWorkOrderWasCreated_IsRefused() =>
        (await Create(new DateOnly(2026, 10, 14))).Message.Should().Be(string.Format(WarehouseMessages.LayWorkDateBeforeWorkOrder, "15/10/2026"));

    [Fact]
    public async Task WorkDate_OnTheBoundaries_PassesTheDateCheck()
    {
        // Qua được kiểm ngày thì dừng ở kiểm vải (định mức trống), không phải lỗi ngày.
        (await Create(Today)).Message.Should().Be(WarehouseMessages.MaterialNotInWorkOrder);
        (await Create(new DateOnly(2026, 10, 15))).Message.Should().Be(WarehouseMessages.MaterialNotInWorkOrder);
    }
}
