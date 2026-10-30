using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>NVL lỗi: Quản đốc duyệt → kiểm tồn → đủ thì tự tạo đơn xin NVL, thiếu thì tự tạo đơn mua cho phần thiếu.</summary>
public class DefectiveMaterialRequestHandlerTests : ProductionTestBase
{
    private const int TeamId = 2, OrderId = 1, VaiA = 100, VaiB = 101;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly List<RequestLine> _lines = [];
    private readonly List<Material> _materials = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<PurchaseOrder> _orders = [];
    private readonly Request _request;

    public DefectiveMaterialRequestHandlerTests()
    {
        SetupQueryable(_lines); SetupQueryable(_materials); SetupQueryable(_orderMaterials); SetupQueryable(_orders);
        Repo<PurchaseOrder>().When(r => r.AddAsync(Arg.Any<PurchaseOrder>())).Do(c => _orders.Add(c.Arg<PurchaseOrder>()));
        _codes.NextAsync<PurchaseOrder>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, string>>>(),
            Arg.Any<CancellationToken>()).Returns("DM-TEST-001");
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>(), Arg.Any<int?>())
            .Returns(new Request { RequestCode = "YC-TEST-001", RequestTypeCode = RequestTypes.MaterialRequisition });
        _materials.Add(new Material { MaterialId = VaiA, MaterialCode = "VA", MaterialName = "Vải A" });
        _materials.Add(new Material { MaterialId = VaiB, MaterialCode = "VB", MaterialName = "Vải B" });
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 55, OrderId = OrderId, MaterialId = VaiA });
        _request = new Request
        {
            RequestId = 9, RequestCode = "YC-9", RequestTypeCode = RequestTypes.DefectiveMaterial, Status = RequestStatuses.Approved,
            OrderId = OrderId, WorkOrderId = 7, CreatedByTeamId = TeamId,
        };
    }

    private DefectiveMaterialRequestHandler Sut()
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IRequestService)).Returns(_requests);
        return new DefectiveMaterialRequestHandler(Uow, _stock, _codes, Notifier, Clock, sp);
    }

    private void Wants(int materialId, decimal qty) => _lines.Add(new RequestLine { RequestId = 9, MaterialId = materialId, Quantity = qty });
    private void Stock(int materialId, decimal available) => _stock.GetAvailableAsync(StockKey.Material(materialId), Arg.Any<CancellationToken>()).Returns(available);

    [Fact]
    public async Task EnoughStock_CreatesARequisitionForTheCuttingTeam_AndNoPurchaseOrder()
    {
        Wants(VaiA, 10); Stock(VaiA, 50);

        var result = await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);

        result.IsSuccess.Should().BeTrue();
        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.MaterialRequisition && d.WorkOrderId == 7
                && d.Lines.Single().MaterialId == VaiA && d.Lines.Single().Quantity == 10),
            Arg.Any<CancellationToken>(), TeamId);
        _orders.Should().BeEmpty();
        _request.ResultNote.Should().Contain("YC-TEST-001").And.Contain("đủ tồn");
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortStock_CreatesAPurchaseOrderForTheMissingPart_OnlyTheEnoughPartIsRequisitioned()
    {
        Wants(VaiA, 30); Wants(VaiB, 5);
        Stock(VaiA, 20);     // thiếu 10 của vải A
        Stock(VaiB, 0);      // vải B hết hẳn

        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);

        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.Lines.Count == 1 && d.Lines[0].MaterialId == VaiA && d.Lines[0].Quantity == 20),
            Arg.Any<CancellationToken>(), TeamId);
        var po = _orders.Should().ContainSingle().Subject;
        po.SourceRequestId.Should().Be(9);
        po.OrderId.Should().Be(OrderId);
        po.GoodsStatus.Should().Be(PurchaseOrderStatuses.PendingFinalisation);
        po.PurchaseOrderLines.Should().HaveCount(2);
        po.PurchaseOrderLines.Single(l => l.OrderMaterialId == 55).OrderedQty.Should().Be(10);          // vải A có trong NVL của đơn
        po.PurchaseOrderLines.Single(l => l.MaterialId == VaiB).OrderedQty.Should().Be(5);              // vải B dòng dùng chung
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoStockAtAll_OnlyCreatesThePurchaseOrder()
    {
        Wants(VaiB, 8); Stock(VaiB, 0);

        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);

        await _requests.DidNotReceiveWithAnyArgs().StageSystemRequestAsync(default!, default);
        _orders.Should().ContainSingle();
        _request.ResultNote.Should().Contain("DM-TEST-001").And.Contain("thiếu tồn");
    }

    [Fact]
    public async Task OnlyAnApprovalTriggersIt_RejectionOrOtherTransitionsDoNothing()
    {
        Wants(VaiA, 10); Stock(VaiA, 0);

        _request.Status = RequestStatuses.Rejected;
        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);
        _request.Status = RequestStatuses.Completed;
        await Sut().OnStatusChangedAsync(_request, RequestStatuses.Approved, default);

        _orders.Should().BeEmpty();
        await _requests.DidNotReceiveWithAnyArgs().StageSystemRequestAsync(default!, default);
        _request.ResultNote.Should().BeNull();
    }
}
