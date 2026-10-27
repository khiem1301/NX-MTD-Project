using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-09 (UC56, UC07 cắt bù): bộ kiểm đơn cắt bù gắn với phiếu hàng hỏng Scrap.</summary>
public class ReplacementCuttingRequestHandlerTests : ProductionTestBase
{
    private const int TeamId = 5;
    private const int LineId = 11;
    private const int OrderId = 1;

    private readonly List<DefectRecord> _defects = [];
    private readonly List<Request> _requests = [];
    private readonly List<ProductComponent> _components =
    [
        new() { ProductComponentId = 7, ProductId = 1, ComponentName = "Thân trước", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
        new() { ProductComponentId = 8, ProductId = 1, ComponentName = "Cổ", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
        new() { ProductComponentId = 9, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
        new() { ProductComponentId = 10, ProductId = 1, ComponentName = "Khóa", ComponentType = ComponentTypes.Trim, Status = RecordStatus.Active },
        new() { ProductComponentId = 11, ProductId = 2, ComponentName = "Thân khác mã", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
    ];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly ReplacementCuttingRequestHandler _sut;

    public ReplacementCuttingRequestHandlerTests()
    {
        SetupQueryable(_defects);
        SetupQueryable(_requests);
        SetupQueryable(_components);
        // Mỗi chi tiết có một dòng định mức (Id = Id chi tiết) của biến thể 21 (mã hàng 1) hoặc 22 (mã hàng khác).
        _norms.AddRange(_components.Select(c => new ProductComponentMaterial
        {
            ProductComponentMaterialId = c.ProductComponentId, ProductId = c.ProductId, ProductComponentId = c.ProductComponentId,
            ProductComponent = c, ProductVariantId = c.ProductId == 1 ? 21 : 22, MaterialId = 1,
        }));
        _orderLines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = 1, ProductVariantId = 21 });
        SetupQueryable(_norms);
        SetupQueryable(_orderLines);
        AsTeamLeader(TeamId);
        _sut = new ReplacementCuttingRequestHandler(Uow, User);
    }

    private DefectRecord Defect(int id = 1, string type = DefectTypes.Scrap, string status = DefectStatuses.Recorded,
        int teamId = TeamId, int quantity = 3)
    {
        var d = new DefectRecord
        {
            DefectRecordId = id, DefectType = type, Status = status, SewingTeamId = teamId, OrderLineId = LineId, Quantity = quantity,
            OrderLine = new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = 1 },
        };
        _defects.Add(d);
        return d;
    }

    private static RequestDraft Draft(int? defectId = 1, int? orderId = OrderId, int? requestId = null, params RequestLineInput[] lines) =>
        new(RequestTypes.ReplacementCutting, orderId, null, null, null, null, defectId, null, "Cắt bù", null,
            lines.Length > 0 ? lines : [Line()], requestId);

    private static RequestLineInput Line(int? component = 7, int? lineId = LineId, decimal qty = 2) =>
        new(null, lineId, component, null, qty);

    [Fact]
    public async Task Valid_ScrapDefectOfOwnTeam_Passes()
    {
        Defect();

        var result = await _sut.ValidateAsync(Draft(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task NoDefect_IsRejected()
    {
        var result = await _sut.ValidateAsync(Draft(defectId: null), default);

        result.ErrorCode.Should().Be(ErrorCodes.ReplacementCuttingInvalid);
        result.Message.Should().Be(Messages.ReplacementCuttingNeedsDefect);
    }

    [Fact]
    public async Task UnknownDefect_ReturnsNotFound()
    {
        var result = await _sut.ValidateAsync(Draft(defectId: 99), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Theory]
    [InlineData(DefectTypes.Rework, DefectStatuses.Recorded, "Chỉ phiếu hàng hỏng loại Hủy")]
    [InlineData(DefectTypes.Scrap, DefectStatuses.Cancelled, "đã bị hủy")]
    public async Task ReworkOrCancelledDefect_IsRejected(string type, string status, string expected)
    {
        Defect(type: type, status: status);

        var result = await _sut.ValidateAsync(Draft(), default);

        result.Message.Should().Contain(expected);
    }

    [Fact]
    public async Task DefectOfAnotherTeam_IsRejectedForTeamLeader()
    {
        Defect(teamId: 99);

        var result = await _sut.ValidateAsync(Draft(), default);

        result.Message.Should().Be(Messages.ReplacementCuttingOtherTeam);
    }

    [Fact]
    public async Task OrderOfRequestMustMatchTheDefect()
    {
        Defect();

        var result = await _sut.ValidateAsync(Draft(orderId: 2), default);

        result.Message.Should().Be(Messages.ReplacementCuttingOrderMismatch);
    }

    [Fact]
    public async Task Lines_MustHavePanelRightOrderLineAndQuantityWithinScrapQty()
    {
        Defect(quantity: 3);

        (await _sut.ValidateAsync(Draft(lines: Line(component: null)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: Line(lineId: 999)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: Line(qty: 0)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: Line(qty: 4)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: Line(qty: 1.5m)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: [Line(8), Line(8)]), default)).Message.Should().Be(Messages.ReplacementCuttingLines);
        (await _sut.ValidateAsync(Draft(lines: Line(component: 10)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);   // phụ liệu, không phải chi tiết cắt
        (await _sut.ValidateAsync(Draft(lines: Line(component: 11)), default)).Message.Should().Be(Messages.ReplacementCuttingLines);   // chi tiết của mã hàng khác
        (await _sut.ValidateAsync(Draft(lines: [Line(8), Line(9)]), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SecondRequestForTheSameDefect_IsRejected_ButEditingTheOwnOneIsAllowed()
    {
        Defect();
        _requests.Add(new Request { RequestId = 7, RequestTypeCode = RequestTypes.ReplacementCutting, DefectRecordId = 1 });

        (await _sut.ValidateAsync(Draft(), default)).Message.Should().Be(Messages.ReplacementCuttingDuplicate);
        (await _sut.ValidateAsync(Draft(requestId: 7), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void HandlerIsForReplacementCutting_AndCanBeCompletedByHand()
    {
        _sut.RequestType.Should().Be(RequestTypes.ReplacementCutting);
        _sut.AllowManualComplete.Should().BeTrue();
    }
}
