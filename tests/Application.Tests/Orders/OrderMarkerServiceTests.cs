using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Bìa cắt của đơn (DEV-ALL-85): nhóm bìa phải đã khai vải, size thuộc đơn, mã bìa không trùng, gửi đủ danh sách thì thay bìa.</summary>
public class OrderMarkerServiceTests
{
    private const int OrderId = 1, ProductId = 7;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly List<CustomerOrder> _orders = [new() { OrderId = OrderId, OrderCode = "DH1", Status = OrderStatuses.InProduction }];
    private readonly List<OrderLine> _lines = [];
    private readonly List<ProductMarkerFabric> _fabrics = [new() { ProductId = ProductId, MarkerNo = 1 }, new() { ProductId = ProductId, MarkerNo = 2 }];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerRatio> _ratios = [];
    private int _nextId = 100;

    public OrderMarkerServiceTests()
    {
        foreach (var size in new[] { "S", "M", "L" })
            _lines.Add(new OrderLine { OrderLineId = _lines.Count + 1, OrderId = OrderId, OrderedQty = 10 }
                .WithVariant(TestVariant.Make(new Product { ProductId = ProductId, ProductCode = "AO-01", ProductName = "Áo" }, "Đen", size)));
        Setup(_orders); Setup(_lines); Setup(_fabrics);
        Setup(_markers, m => m.ProductMarkerId = ++_nextId);
        Setup(_ratios);
    }

    private void Setup<T>(List<T> backing, Action<T>? onAdd = null) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.AddAsync(Arg.Do<T>(e => { onAdd?.Invoke(e); backing.Add(e); })).Returns(Task.CompletedTask);
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<T>>())).Do(c => { foreach (var e in c.Arg<IEnumerable<T>>().ToList()) backing.Remove(e); });
        _uow.Repository<T>().Returns(repo);
    }

    private OrderMarkerService Sut() => new(_uow, _audit);

    private static SaveOrderMarkerDto Marker(string code = "1A", int group = 1, decimal length = 2.2m, int? id = null,
        params (string Size, short Pieces)[] ratio) =>
        new(id, group, code, length, "Active",
            (ratio.Length == 0 ? [("S", (short)1), ("M", (short)2), ("L", (short)1)] : ratio).Select(r => new MarkerRatioDto(r.Size, r.Pieces)).ToList());

    private Task<Result<OrderMarkersDto>> Save(params SaveOrderMarkerDto[] markers) =>
        Sut().SaveAsync(OrderId, ProductId, new SaveOrderMarkersDto(markers), default);

    [Fact]
    public async Task Save_AddsMarkersWithTheirRatio_UpperCasingTheCode()
    {
        var result = await Save(Marker("1a"), Marker("2A", 2, 1.6m));

        result.IsSuccess.Should().BeTrue();
        _markers.Select(m => (m.MarkerNo, m.MarkerCode, m.LengthM, m.Status)).Should()
            .BeEquivalentTo([((byte)1, "1A", 2.2m, RecordStatus.Active), ((byte)2, "2A", 1.6m, RecordStatus.Active)]);
        _markers[0].ProductMarkerRatios.Select(r => (r.SizeCode, r.PiecesPerPly)).Should()
            .BeEquivalentTo([("S", (short)1), ("M", (short)2), ("L", (short)1)]);
    }

    [Fact]
    public async Task Save_SendsTheWholeList_UpdatingKeptMarkersAndDeletingTheRest()
    {
        var kept = new ProductMarker { ProductMarkerId = 1, OrderId = OrderId, ProductId = ProductId, MarkerNo = 1, MarkerCode = "1A", LengthM = 1, Status = RecordStatus.Active };
        var dropped = new ProductMarker { ProductMarkerId = 2, OrderId = OrderId, ProductId = ProductId, MarkerNo = 1, MarkerCode = "1B", LengthM = 1, Status = RecordStatus.Active };
        _markers.AddRange([kept, dropped]);

        (await Save(Marker(length: 3m, id: 1))).IsSuccess.Should().BeTrue();

        _markers.Should().ContainSingle().Which.Should().BeSameAs(kept);
        kept.LengthM.Should().Be(3m);
    }

    [Fact]
    public async Task Save_GroupWithoutFabric_IsRejected() =>
        (await Save(Marker("3A", 3))).Message.Should().Be(string.Format(Messages.OrderMarkerGroupWithoutFabric, "3"));

    [Fact]
    public async Task Save_SizeNotInTheOrder_IsRejected() =>
        (await Save(Marker(ratio: ("XL", 1)))).Message.Should().Be(string.Format(Messages.OrderMarkerSizeNotInOrder, "XL"));

    [Fact]
    public async Task Save_SameCodeTwiceInAGroup_IsRejected() =>
        (await Save(Marker("1A"), Marker("1a"))).Message.Should().Be(Messages.OrderMarkerCodeDuplicate);

    [Fact]
    public async Task Save_ProductNotInTheOrder_IsRejected() =>
        (await Sut().SaveAsync(OrderId, 99, new SaveOrderMarkersDto([Marker()]), default)).Message.Should().Be(Messages.OrderMarkerProductNotInOrder);

    [Fact]
    public async Task Save_ClosedOrder_IsRejected()
    {
        _orders[0].Status = OrderStatuses.Completed;

        (await Save(Marker())).Message.Should().Be(Messages.OrderMarkerOrderClosed);
    }

    [Fact]
    public async Task Save_MarkerOfAnotherOrder_IsNotFound()
    {
        _markers.Add(new ProductMarker { ProductMarkerId = 5, OrderId = 9, ProductId = ProductId, MarkerNo = 1, MarkerCode = "1A", Status = RecordStatus.Active });

        (await Save(Marker(id: 5))).Kind.Should().Be(ResultKind.NotFound);
    }

    [Theory]
    [InlineData("2.2", 2, true)]
    [InlineData("99999.99", 999, true)]
    [InlineData("0", 1, false)]
    [InlineData("2.234", 1, false)]
    [InlineData("100000", 1, false)]
    [InlineData("2", 0, false)]
    [InlineData("2", 1000, false)]
    public void Validator_LengthAndPiecesBoundaries(string length, short pieces, bool expected)
    {
        var dto = Marker(length: decimal.Parse(length, System.Globalization.CultureInfo.InvariantCulture), ratio: ("M", pieces));

        new SaveOrderMarkerDtoValidator().Validate(dto).IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData("1A", true)]
    [InlineData("", false)]
    [InlineData("1 A", false)]
    [InlineData("12345678901234567890", true)]
    [InlineData("123456789012345678901", false)]
    public void Validator_MarkerCode(string code, bool expected) =>
        new SaveOrderMarkerDtoValidator().Validate(Marker(code)).IsValid.Should().Be(expected);

    [Fact]
    public void Validator_NeedsAtLeastOneSize_AndNoSizeTwice()
    {
        var validator = new SaveOrderMarkerDtoValidator();

        validator.Validate(Marker() with { Ratios = [] }).IsValid.Should().BeFalse();
        validator.Validate(Marker(ratio: [("M", 1), ("m", 2)])).IsValid.Should().BeFalse();
    }
}
