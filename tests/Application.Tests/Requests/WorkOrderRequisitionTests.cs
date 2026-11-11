using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-92: đơn xin NVL theo lệnh cắt không vượt trần = số mét hệ thống tính × (1 + tỉ lệ sai lệch).</summary>
public class WorkOrderRequisitionTests : ProductionTestBase
{
    private const int FabricId = 1;
    private const int LiningId = 2;
    private const int OtherFabricId = 3;
    private const int CutTeam = 2;

    private readonly IWorkOrderFabricNeeds _needs = Substitute.For<IWorkOrderFabricNeeds>();
    private readonly WorkOrder _wo = new() { WorkOrderId = 7, OrderId = 4, CuttingTeamId = CutTeam, Status = WorkOrderStatuses.Open };

    public WorkOrderRequisitionTests()
    {
        AsCuttingTeamLeader();
        SetupQueryable(new List<Material>
        {
            new() { MaterialId = FabricId, MaterialName = "Vải kate", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
            new() { MaterialId = LiningId, MaterialName = "Vải lót", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
            new() { MaterialId = OtherFabricId, MaterialName = "Vải khác", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
        });
        _needs.LoadAsync(7, Arg.Any<CancellationToken>()).Returns(_wo);
        _needs.AllowanceAsync(_wo, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(new[]
        {
            Allow(FabricId, need: 100m, cap: 105m, requested: 60m),
            Allow(LiningId, need: null, cap: null, requested: 0m),
        });
    }

    private static WorkOrderMaterialAllowanceDto Allow(int id, decimal? need, decimal? cap, decimal requested) =>
        new(id, $"V{id}", $"Vải {id}", "", "m", need, cap, requested, cap is null ? null : cap - requested);

    private Task<Result> Check(int? workOrderId, params (int Id, decimal Qty)[] lines) =>
        new MaterialRequisitionRequestHandler(Clock, Uow, User, _needs).ValidateAsync(new RequestDraft(RequestTypes.MaterialRequisition,
            null, workOrderId, null, null, null, null, null, "Xin", "Nội dung",
            lines.Select(l => new RequestLineInput(l.Id, null, null, null, l.Qty)).ToList(), null), default);

    [Fact]
    public async Task ValidateAsync_NoWorkOrder_DoesNotCheckCap() =>
        (await Check(null, (OtherFabricId, 9999m))).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task ValidateAsync_WithinCapTogetherWithOtherRequests_Succeeds() =>
        (await Check(7, (FabricId, 45m))).IsSuccess.Should().BeTrue();   // 60 + 45 = 105 = trần

    [Fact]
    public async Task ValidateAsync_OverCap_ReturnsFailureNamingTheFabric()
    {
        var result = await Check(7, (FabricId, 45.01m));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Vải kate").And.Contain("105");
    }

    [Fact]
    public async Task ValidateAsync_FabricNotInWorkOrderOrNotComputed_ReturnsFailure()
    {
        (await Check(7, (OtherFabricId, 1m))).Message.Should().Be(string.Format(Messages.RequisitionNotWorkOrderFabric, "Vải khác"));
        (await Check(7, (LiningId, 1m))).Message.Should().Be(string.Format(Messages.RequisitionNotWorkOrderFabric, "Vải lót"));
    }

    [Fact]
    public async Task ValidateAsync_PartOfTheWorkOrderFabrics_Succeeds() =>
        (await Check(7, (FabricId, 10m))).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData(99, WorkOrderStatuses.Open)]          // lệnh của tổ khác
    [InlineData(CutTeam, WorkOrderStatuses.Completed)]
    [InlineData(CutTeam, WorkOrderStatuses.Cancelled)]
    public async Task ValidateAsync_WorkOrderNotOfTeamOrClosed_ReturnsFailure(int team, string status)
    {
        _wo.CuttingTeamId = team;
        _wo.Status = status;

        (await Check(7, (FabricId, 1m))).Message.Should().Be(Messages.RequisitionWorkOrderInvalid);
    }

    [Theory]
    [InlineData("100", "5", "105")]
    [InlineData("100", "0", "100")]
    [InlineData("33.333", "3", "34.33")]   // làm tròn xuống 2 số lẻ
    public void Cap_IsNeedTimesOnePlusTolerance(string need, string tolerance, string expected) =>
        WorkOrderFabricNeeds.Cap(decimal.Parse(need, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(tolerance, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public async Task AllowanceFromNeeds_CountsOnlyOpenRequisitionsOfThisWorkOrder_ExceptEdited()
    {
        Request Req(int id, string status, int wo = 7, string type = RequestTypes.MaterialRequisition) =>
            new() { RequestId = id, Status = status, WorkOrderId = wo, RequestTypeCode = type };
        RequestLine Line(Request r, decimal qty, int material = FabricId) =>
            new() { RequestId = r.RequestId, Request = r, MaterialId = material, Quantity = qty };
        SetupQueryable(new List<RequestLine>
        {
            Line(Req(1, RequestStatuses.PendingProcessing), 30m),
            Line(Req(2, RequestStatuses.Completed), 20m),
            Line(Req(3, RequestStatuses.Rejected), 500m),
            Line(Req(4, RequestStatuses.Cancelled), 500m),
            Line(Req(5, RequestStatuses.PendingProcessing, wo: 8), 500m),
            Line(Req(6, RequestStatuses.PendingProcessing), 15m),   // đơn đang sửa
        });
        _wo.FabricTolerancePercent = 10m;
        var needs = new[] { new WorkOrderFabricCheckDto(FabricId, "V1", "Vải kate", "", FabricCheckStatuses.Enough, 100m, null, null) };

        var row = (await new WorkOrderFabricNeeds(Uow, null!).AllowanceFromNeedsAsync(_wo, needs, 6, default)).Single();

        (row.NeedQty, row.CapQty, row.RequestedQty, row.RemainingQty).Should().Be((100m, 110m, 50m, 60m));
    }
}
