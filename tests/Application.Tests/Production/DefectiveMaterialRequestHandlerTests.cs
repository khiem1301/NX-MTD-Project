using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>NVL lỗi: Quản đốc duyệt → kiểm tồn → đủ thì tự tạo đơn xin NVL, thiếu thì tự tạo đơn từ "Mua NVL" cho Quản đốc (không tự tạo đơn mua).</summary>
public class DefectiveMaterialRequestHandlerTests : ProductionTestBase
{
    private const int TeamId = 2, OrderId = 1, VaiA = 100, VaiB = 101;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly List<RequestLine> _lines = [];
    private readonly List<Material> _materials = [];
    private readonly Request _request;

    public DefectiveMaterialRequestHandlerTests()
    {
        SetupQueryable(_lines); SetupQueryable(_materials);
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>(), Arg.Any<int?>())
            .Returns(new Request { RequestCode = "YC-TEST-001", RequestTypeCode = RequestTypes.MaterialRequisition });
        _materials.Add(new Material { MaterialId = VaiA, MaterialCode = "VA", MaterialName = "Vải A" });
        _materials.Add(new Material { MaterialId = VaiB, MaterialCode = "VB", MaterialName = "Vải B" });
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
        return new DefectiveMaterialRequestHandler(Uow, _stock, Notifier, sp);
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
        _request.ResultNote.Should().Contain("YC-TEST-001").And.Contain("đủ tồn");
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortStock_RequisitionsTheEnoughPart_AndStagesAMaterialPurchaseRequestForTheMissingPart()
    {
        Wants(VaiA, 30); Wants(VaiB, 5);
        Stock(VaiA, 20);     // thiếu 10 của vải A
        Stock(VaiB, 0);      // vải B hết hẳn
        _requests.StageSystemRequestAsync(Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.MaterialPurchase), Arg.Any<CancellationToken>(),
                Arg.Any<int?>())
            .Returns(new Request { RequestCode = "YC-MUA-001", RequestTypeCode = RequestTypes.MaterialPurchase });

        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);

        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.MaterialRequisition && d.Lines.Count == 1
                && d.Lines[0].MaterialId == VaiA && d.Lines[0].Quantity == 20),
            Arg.Any<CancellationToken>(), TeamId);
        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.MaterialPurchase && d.OrderId == OrderId && d.Lines.Count == 2
                && d.Lines.Any(l => l.MaterialId == VaiA && l.Quantity == 10) && d.Lines.Any(l => l.MaterialId == VaiB && l.Quantity == 5)
                && d.Content!.Contains("YC-9") && d.Content.Contains("VA 10") && d.Content.Contains("VB 5")),
            Arg.Any<CancellationToken>(), Arg.Any<int?>());
        await Uow.DidNotReceive().Repository<PurchaseOrder>().AddAsync(Arg.Any<PurchaseOrder>());   // không tự tạo đơn mua
        _request.ResultNote.Should().Contain("thiếu tồn").And.Contain("YC-MUA-001");
    }

    [Fact]
    public async Task NoStockAtAll_OnlyStagesTheMaterialPurchaseRequest()
    {
        Wants(VaiB, 8); Stock(VaiB, 0);
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>(), Arg.Any<int?>())
            .Returns(new Request { RequestCode = "YC-MUA-002", RequestTypeCode = RequestTypes.MaterialPurchase });

        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);

        await _requests.Received(1).StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>(), Arg.Any<int?>());
        await _requests.DidNotReceive().StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.MaterialRequisition), Arg.Any<CancellationToken>(), Arg.Any<int?>());
        _request.ResultNote.Should().Contain("thiếu tồn");
    }

    [Fact]
    public async Task OnlyAnApprovalTriggersIt_RejectionOrOtherTransitionsDoNothing()
    {
        Wants(VaiA, 10); Stock(VaiA, 0);

        _request.Status = RequestStatuses.Rejected;
        await Sut().OnStatusChangedAsync(_request, RequestStatuses.PendingApproval, default);
        _request.Status = RequestStatuses.Completed;
        await Sut().OnStatusChangedAsync(_request, RequestStatuses.Approved, default);

        await _requests.DidNotReceiveWithAnyArgs().StageSystemRequestAsync(default!, default);
        _request.ResultNote.Should().BeNull();
    }
}
