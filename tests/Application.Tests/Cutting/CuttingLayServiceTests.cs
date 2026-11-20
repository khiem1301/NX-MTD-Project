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
/// DEV-ALL-93: bàn nhập đủ đúng bộ vải các dòng cần (cả bộ: mọi vải chi tiết của màu áo; cắt bù: vải của chi tiết),
/// mỗi vải không vượt vải còn lại của tổ trên đơn (đang giữ − các bàn khác đã dùng).
/// </summary>
public class CuttingLayServiceTests : ProductionTestBase
{
    private const int TeamId = 2;
    private const int OrderId = 3;
    private const int MainId = 1;
    private const int LiningId = 2;
    private const int ColorId = 4;
    private const int LiningPcmId = 20;

    private readonly WorkOrder _wo;
    private readonly List<CuttingLay> _lays = new();
    private readonly List<CuttingLayFabric> _layFabrics = new();
    private readonly List<ProductComponentMaterial> _norms = new();
    private readonly List<MaterialIssueLine> _issueLines = new();

    public CuttingLayServiceTests()
    {
        _wo = new WorkOrder
        {
            WorkOrderId = 7, OrderId = OrderId, CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting,
            CreatedAt = new DateTime(2026, 10, 15, 8, 0, 0),
        };
        AddWorkOrderLine(null);
        SetupQueryable(new List<WorkOrder> { _wo });
        SetupQueryable(_lays);
        SetupQueryable(_layFabrics);
        SetupQueryable(_norms);
        SetupQueryable(_issueLines);
        SetupQueryable(new List<Material>
        {
            new() { MaterialId = MainId, MaterialCode = "V1" },
            new() { MaterialId = LiningId, MaterialCode = "V2" },
        });
        SetupQueryable(new List<MaterialReturnLine>());
        SetupQueryable(new List<CuttingLayLine>());
        SetupQueryable(new List<SystemSetting>());
    }

    private void AddWorkOrderLine(int? pcmId) => _wo.WorkOrderLines.Add(new WorkOrderLine
    {
        WorkOrderId = 7, OrderLineId = 11, PlannedQty = 100, ProductComponentMaterialId = pcmId,
        OrderLine = new OrderLine
        {
            OrderLineId = 11,
            ProductVariant = new ProductVariant { ProductColorId = ColorId, ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "M" },
        },
    });

    private CuttingLayService Service() => new(Uow, Substitute.For<IAppLock>(), Clock, new FabricPlanner(Uow));

    private static SaveCuttingLayDto Dto(DateOnly date, (int Id, decimal M)[] fabrics, int qty = 10, int? pcmId = null) =>
        new(7, date, ShiftCodes.Morning, fabrics.Select(f => new SaveLayFabricDto(f.Id, f.M)).ToList(), null,
            [new SaveLayLineDto(11, pcmId, qty)]);

    private Task<Result<CuttingLayDto>> Create(DateOnly date, params (int Id, decimal M)[] fabrics) =>
        Service().CreateAsync(Dto(date, fabrics), TeamId, default);

    /// <summary>Áo Đen: thân vải chính, lót vải lót; tổ đã nhận mỗi vải 100 m của đơn.</summary>
    private void TwoFabricsInNormAndHeld()
    {
        Norm(10, MainId);
        Norm(LiningPcmId, LiningId);
        Received(MainId, 100);
        Received(LiningId, 100);
    }

    private void Norm(int pcmId, int materialId) => _norms.Add(new ProductComponentMaterial
    {
        ProductComponentMaterialId = pcmId, ProductColorId = ColorId, MaterialId = materialId,
        Material = new Material { MaterialId = materialId, MaterialType = MaterialTypes.Fabric },
        ProductComponent = new ProductComponent { ComponentType = ComponentTypes.Panel },
    });

    private void Received(int materialId, decimal qty) => _issueLines.Add(new MaterialIssueLine
    {
        MaterialId = materialId, IssuedQty = qty, ConfirmedQty = qty,
        MaterialIssue = new MaterialIssue
        {
            ReceivingTeamId = TeamId, OrderId = OrderId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received,
        },
    });

    private void OtherLay(int materialId, decimal? usedM, int team = TeamId, int order = OrderId, int layId = 50) =>
        _layFabrics.Add(new CuttingLayFabric
        {
            CuttingLayId = layId, MaterialId = materialId, UsedM = usedM,
            CuttingLay = new CuttingLay { CuttingLayId = layId, CuttingTeamId = team, WorkOrder = new WorkOrder { OrderId = order } },
        });

    [Fact]
    public async Task WorkDate_InTheFuture_IsRefused() =>
        (await Create(Today.AddDays(1), (MainId, 2m))).Message.Should().Be(WarehouseMessages.LayWorkDateFuture);

    [Fact]
    public async Task WorkDate_BeforeTheWorkOrderWasCreated_IsRefused() =>
        (await Create(new DateOnly(2026, 10, 14), (MainId, 2m))).Message
            .Should().Be(string.Format(WarehouseMessages.LayWorkDateBeforeWorkOrder, "15/10/2026"));

    [Fact]
    public async Task WorkDate_OnTheBoundaries_PassesTheDateCheck()
    {
        // Qua được kiểm ngày thì dừng ở kiểm vải (định mức trống nên vải nhập là thừa), không phải lỗi ngày.
        var expected = string.Format(WarehouseMessages.LayFabricsMismatch, "—", "V1");
        (await Create(Today, (MainId, 2m))).Message.Should().Be(expected);
        (await Create(new DateOnly(2026, 10, 15), (MainId, 2m))).Message.Should().Be(expected);
    }

    [Fact]
    public async Task CreateAsync_WholeSetMissingAFabric_ReturnsFailure()
    {
        TwoFabricsInNormAndHeld();

        (await Create(Today, (MainId, 20m))).Message.Should().Be(string.Format(WarehouseMessages.LayFabricsMismatch, "V2", "—"));
    }

    [Fact]
    public async Task CreateAsync_FabricNotOfTheLines_ReturnsFailure()
    {
        Norm(10, MainId);
        Received(MainId, 100);

        (await Create(Today, (MainId, 20m), (LiningId, 5m))).Message
            .Should().Be(string.Format(WarehouseMessages.LayFabricsMismatch, "—", "V2"));
    }

    [Fact]
    public async Task CreateAsync_OneFabricOverTeamRemaining_ReturnsFailureNamingIt()
    {
        TwoFabricsInNormAndHeld();
        OtherLay(LiningId, 60m);   // lót còn 40 m

        var result = await Create(Today, (MainId, 90m), (LiningId, 40.5m));

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        result.Message.Should().Be(string.Format(WarehouseMessages.LayFabricOverRemaining,
            string.Format(WarehouseMessages.LayFabricOverRemainingLine, "V2", "40,5", "40")));
    }

    [Fact]
    public async Task CreateAsync_EveryFabricWithinRemaining_SavesEachFabricAndQty()
    {
        TwoFabricsInNormAndHeld();
        OtherLay(MainId, 60m);
        OtherLay(MainId, 500m, team: 99, layId: 51);
        OtherLay(MainId, 500m, order: 99, layId: 52);
        OtherLay(MainId, null, layId: 53);   // bàn cũ chưa có số mét: không trừ

        await Create(Today, (MainId, 40m), (LiningId, 100m));

        await Repo<CuttingLay>().Received(1).AddAsync(Arg.Is<CuttingLay>(l =>
            l.CuttingLayFabrics.Count == 2
            && l.CuttingLayFabrics.Single(f => f.MaterialId == MainId).UsedM == 40m
            && l.CuttingLayFabrics.Single(f => f.MaterialId == LiningId).UsedM == 100m
            && l.CuttingLayLines.Single().ActualQty == 10));
    }

    [Fact]
    public async Task CreateAsync_ComponentLine_NeedsOnlyThatComponentsFabric()
    {
        TwoFabricsInNormAndHeld();
        _wo.WorkOrderLines.Clear();
        AddWorkOrderLine(LiningPcmId);

        await Service().CreateAsync(Dto(Today, [(LiningId, 5m)], pcmId: LiningPcmId), TeamId, default);

        await Repo<CuttingLay>().Received(1).AddAsync(Arg.Is<CuttingLay>(l => l.CuttingLayFabrics.Single().MaterialId == LiningId));
    }

    [Fact]
    public async Task UpdateAsync_DoesNotCountTheLayBeingEdited()
    {
        TwoFabricsInNormAndHeld();
        var lay = new CuttingLay
        {
            CuttingLayId = 9, CuttingTeamId = TeamId, WorkDate = Today, ShiftCode = ShiftCodes.Morning,
            Status = CuttingLayStatuses.Recorded, WorkOrder = new WorkOrder { OrderId = OrderId },
        };
        _lays.Add(lay);
        OtherLay(MainId, 90m, layId: 9);

        // Số lượng vượt trần để dừng ngay sau kiểm vải: lỗi là vượt kế hoạch nghĩa là 100 m đã qua kiểm vải còn lại (100 − 0).
        var result = await Service().UpdateAsync(9, Dto(Today, [(MainId, 100m), (LiningId, 1m)], qty: 1000), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingQtyExceedsPlan);
    }

    [Fact]
    public async Task CreateAsync_QtyOverPlanPlusSurplus_ReturnsFailure()
    {
        TwoFabricsInNormAndHeld();

        (await Service().CreateAsync(Dto(Today, [(MainId, 10m), (LiningId, 1m)], qty: 101), TeamId, default))
            .ErrorCode.Should().Be(ErrorCodes.CuttingQtyExceedsPlan);
    }
}
