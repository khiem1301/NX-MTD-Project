using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Bàn cắt chỉ ghi khi đã cắt: ngày cắt không sau hôm nay, không trước ngày lập lệnh cắt.
/// DEV-ALL-90: số mét vải đã dùng của bàn không vượt vải còn lại của tổ trên đơn (đang giữ − các bàn khác đã dùng).
/// </summary>
public class CuttingLayServiceTests : ProductionTestBase
{
    private const int TeamId = 2;
    private const int OrderId = 3;
    private const int FabricId = 1;
    private const int ColorId = 4;

    private readonly List<CuttingLay> _lays = new();
    private readonly List<ProductComponentMaterial> _norms = new();
    private readonly List<MaterialIssueLine> _issueLines = new();

    public CuttingLayServiceTests()
    {
        var wo = new WorkOrder
        {
            WorkOrderId = 7, OrderId = OrderId, CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting,
            CreatedAt = new DateTime(2026, 10, 15, 8, 0, 0),
        };
        wo.WorkOrderLines.Add(new WorkOrderLine
        {
            WorkOrderId = 7, OrderLineId = 11, PlannedQty = 100,
            OrderLine = new OrderLine
            {
                OrderLineId = 11,
                ProductVariant = new ProductVariant { ProductColorId = ColorId, ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "M" },
            },
        });
        SetupQueryable(new List<WorkOrder> { wo });
        SetupQueryable(_lays);
        SetupQueryable(_norms);
        SetupQueryable(_issueLines);
        SetupQueryable(new List<MaterialReturnLine>());
        SetupQueryable(new List<CuttingLayLine>());
        SetupQueryable(new List<SystemSetting>());
    }

    private Task<Result<CuttingLayDto>> Create(DateOnly date, decimal usedM = 2m, int qty = 10) =>
        new CuttingLayService(Uow, Substitute.For<IAppLock>(), Clock, new FabricPlanner(Uow)).CreateAsync(
            new SaveCuttingLayDto(7, date, ShiftCodes.Morning, FabricId, usedM, null, [new SaveLayLineDto(11, null, qty)]),
            TeamId, default);

    /// <summary>Vải của bàn có trong định mức; tổ đã nhận <paramref name="receivedM"/> m của đơn.</summary>
    private void FabricInNormAndHeld(decimal receivedM)
    {
        _norms.Add(new ProductComponentMaterial
        {
            ProductColorId = ColorId, MaterialId = FabricId,
            Material = new Material { MaterialId = FabricId, MaterialType = MaterialTypes.Fabric },
            ProductComponent = new ProductComponent { ComponentType = ComponentTypes.Panel },
        });
        var issue = new MaterialIssue
        {
            ReceivingTeamId = TeamId, OrderId = OrderId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received,
        };
        _issueLines.Add(new MaterialIssueLine { MaterialId = FabricId, IssuedQty = receivedM, ConfirmedQty = receivedM, MaterialIssue = issue });
    }

    private void OtherLay(decimal? usedM, int team = TeamId, int order = OrderId, int fabric = FabricId) =>
        _lays.Add(new CuttingLay
        {
            CuttingLayId = 50 + _lays.Count, CuttingTeamId = team, FabricMaterialId = fabric, FabricUsedM = usedM,
            WorkOrder = new WorkOrder { OrderId = order },
        });

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

    [Fact]
    public async Task CreateAsync_FabricUsedOverTeamRemaining_ReturnsFailure()
    {
        FabricInNormAndHeld(100);
        OtherLay(60);   // còn 40 m

        var result = await Create(Today, usedM: 40.5m);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        result.Message.Should().Be(string.Format(WarehouseMessages.LayFabricOverRemaining, "40,5", "40"));
    }

    [Fact]
    public async Task CreateAsync_FabricUsedEqualsRemaining_SavesQtyAndMeters()
    {
        FabricInNormAndHeld(100);
        OtherLay(60);
        OtherLay(500, team: 99);
        OtherLay(500, order: 99);
        OtherLay(500, fabric: 99);
        OtherLay(null);   // bàn cũ chưa có số mét: không trừ

        await Create(Today, usedM: 40m, qty: 25);

        await Repo<CuttingLay>().Received(1).AddAsync(Arg.Is<CuttingLay>(l =>
            l.FabricUsedM == 40m && l.CuttingLayLines.Single().ActualQty == 25 && l.CuttingLayLines.Single().OrderLineId == 11));
    }

    [Fact]
    public async Task UpdateAsync_DoesNotCountTheLayBeingEdited()
    {
        FabricInNormAndHeld(100);
        _lays.Add(new CuttingLay
        {
            CuttingLayId = 9, CuttingTeamId = TeamId, FabricMaterialId = FabricId, FabricUsedM = 90m, WorkDate = Today,
            ShiftCode = ShiftCodes.Morning, Status = CuttingLayStatuses.Recorded, WorkOrder = new WorkOrder { OrderId = OrderId },
        });

        // Số lượng vượt trần để dừng ngay sau kiểm vải: lỗi là vượt kế hoạch nghĩa là 100 m đã qua kiểm vải còn lại (100 − 0).
        var result = await new CuttingLayService(Uow, Substitute.For<IAppLock>(), Clock, new FabricPlanner(Uow)).UpdateAsync(9,
            new SaveCuttingLayDto(7, Today, ShiftCodes.Morning, FabricId, 100m, null, [new SaveLayLineDto(11, null, 1000)]), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingQtyExceedsPlan);
    }

    [Fact]
    public async Task CreateAsync_QtyOverPlanPlusSurplus_ReturnsFailure()
    {
        FabricInNormAndHeld(100);

        (await Create(Today, usedM: 10m, qty: 101)).ErrorCode.Should().Be(ErrorCodes.CuttingQtyExceedsPlan);
    }
}
