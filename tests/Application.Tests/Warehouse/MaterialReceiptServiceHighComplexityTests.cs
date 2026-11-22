using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID71–UTCID134: MaterialReceiptService per UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md.</summary>
public class MaterialReceiptServiceHighComplexityTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 22);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<MaterialReceipt> _receipts = new();
    private readonly List<MaterialReceiptLine> _receiptLines = new();
    private readonly List<PurchaseOrder> _pos = new();
    private readonly List<PurchaseOrderLine> _poLines = new();
    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderMaterial> _orderMaterials = new();
    private readonly List<Request> _requestList = new();
    private readonly List<RequestLine> _requestLines = new();
    private readonly List<Attachment> _attachments = new();

    private static readonly Material Fabric = new() { MaterialId = 1, MaterialCode = "V-0001", MaterialName = "Vải", Unit = "m" };

    public MaterialReceiptServiceHighComplexityTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _requests.MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>())
            .Returns(_ => new Request { RequestId = 900, RequestCode = "YC261122-001" });
        _codes.NextAsync<MaterialReceipt>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialReceipt, string>>>(), Arg.Any<CancellationToken>())
            .Returns("PN2611-001");
        Setup(_receipts); Setup(_receiptLines); Setup(_pos); Setup(_poLines); Setup(_orders); Setup(_orderMaterials);
        Setup(_requestList); Setup(_requestLines); Setup(_attachments);
        _uow.SaveChangesAsync().Returns(1);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private MaterialSupplyUpdater Supply() => new(_uow);

    private ShortageTracker Shortages() => new(_uow, _requests, Supply(), new DeliveryCountGuard(_uow));

    private MaterialReceiptService Sut() =>
        new(_uow, _stock, _codes, _requests, Supply(), _clock, _notifier, new DeliveryCountGuard(_uow), Shortages(),
            NullLogger<MaterialReceiptService>.Instance);

    private int _nextReceiptId = 500;

    private void CaptureReceiptAdds(OrderMaterial? om = null, PurchaseOrderLine? pol = null, bool addToBacking = true)
    {
        _uow.Repository<MaterialReceipt>().AddAsync(Arg.Do<MaterialReceipt>(r =>
        {
            if (r.MaterialReceiptId == 0) r.MaterialReceiptId = ++_nextReceiptId;
            foreach (var l in r.MaterialReceiptLines)
            {
                l.MaterialReceipt = r;
                l.MaterialReceiptId = r.MaterialReceiptId;
                if (om is not null) l.OrderMaterial = om;
                if (pol is not null) l.PurchaseOrderLine = pol;
            }
            if (!addToBacking) return; // giả lập rollback: không commit vào list
            _receipts.Add(r);
            _receiptLines.AddRange(r.MaterialReceiptLines);
        }));
    }

    /// <summary>Giả lập EF fix-up navigation sau SaveChanges (GetById cần OrderMaterial.Material / PurchaseOrderLine).</summary>
    private void FixupReceiptLinesOnSave(MaterialReceipt receipt)
    {
        _uow.When(u => u.SaveChangesAsync()).Do(_ =>
        {
            foreach (var l in receipt.MaterialReceiptLines)
            {
                l.MaterialReceipt = receipt;
                if (l.PurchaseOrderLineId is { } polId)
                {
                    l.PurchaseOrderLine ??= _poLines.FirstOrDefault(p => p.PurchaseOrderLineId == polId);
                    if (l.PurchaseOrderLine?.OrderMaterial is not null)
                    {
                        l.OrderMaterial ??= l.PurchaseOrderLine.OrderMaterial;
                        l.OrderMaterialId ??= l.PurchaseOrderLine.OrderMaterialId;
                    }
                }
                if (l.OrderMaterialId is { } omId)
                    l.OrderMaterial ??= _orderMaterials.FirstOrDefault(m => m.OrderMaterialId == omId);
                if (l.OrderMaterial is not null)
                    l.OrderMaterial.Material ??= Fabric;
                if (l.MaterialId is { } mid && l.Material is null)
                    l.Material = mid == Fabric.MaterialId ? Fabric : new Material { MaterialId = mid, MaterialCode = "M", MaterialName = "M", Unit = "m" };
            }
        });
    }

    private (PurchaseOrder Po, PurchaseOrderLine Line, OrderMaterial Om, CustomerOrder Order) PlacePo(
        int orderId = 1, int poId = 5, decimal ordered = 200, short expectedDeliveries = 3,
        int omId = 1, int polId = 11, string orderCode = "DH2611-001", decimal customerQty = 200)
    {
        var order = new CustomerOrder
        {
            OrderId = orderId, OrderCode = orderCode, Status = OrderStatuses.InProduction,
            MaterialStatus = OrderMaterialStatuses.Awaiting, ReceivedDate = new DateOnly(2026, 11, 1),
            CustomerDeliveryCount = expectedDeliveries,
        };
        var om = new OrderMaterial { OrderMaterialId = omId, OrderId = orderId, MaterialId = 1, Material = Fabric, RequiredQty = ordered, CustomerQty = customerQty };
        var po = new PurchaseOrder
        {
            PurchaseOrderId = poId, PoCode = "DM-001", OrderId = orderId, SupplierId = 1,
            GoodsStatus = PurchaseGoodsStatuses.Ordered, ExpectedDeliveryCount = expectedDeliveries,
            OrderDate = new DateOnly(2026, 11, 1),
        };
        var line = new PurchaseOrderLine { PurchaseOrderLineId = polId, PurchaseOrderId = poId, PurchaseOrder = po, OrderMaterialId = omId, OrderMaterial = om, OrderedQty = ordered };
        po.PurchaseOrderLines.Add(line);
        _orders.Add(order); _orderMaterials.Add(om); _pos.Add(po); _poLines.Add(line);
        return (po, line, om, order);
    }

    private MaterialReceipt SeedReceipt(int id, string status, string? code = null, int? orderId = 1, int? poId = 5,
        DateTime? confirmedAt = null, int? shortageRequestId = null, string source = MaterialSources.Supplier)
    {
        var r = new MaterialReceipt
        {
            MaterialReceiptId = id, ReceiptCode = code ?? $"PN-{id}", Status = status, Source = source,
            OrderId = orderId, PurchaseOrderId = poId, ShortageRequestId = shortageRequestId,
            ReceiptDate = Today, ConfirmedAt = confirmedAt,
        };
        if (orderId is not null)
            r.Order = _orders.FirstOrDefault(o => o.OrderId == orderId) ?? new CustomerOrder { OrderId = orderId.Value, OrderCode = "DH2611-001" };
        if (poId is not null)
            r.PurchaseOrder = _pos.FirstOrDefault(p => p.PurchaseOrderId == poId) ?? new PurchaseOrder { PurchaseOrderId = poId.Value, PoCode = "DM-001" };
        _receipts.Add(r);
        return r;
    }

    private MaterialReceiptLine SeedLine(MaterialReceipt receipt, decimal qty, int lineId, int? omId = 1, int? polId = 11)
    {
        var om = omId is null ? null : (_orderMaterials.FirstOrDefault(m => m.OrderMaterialId == omId)
            ?? new OrderMaterial { OrderMaterialId = omId.Value, MaterialId = 1, Material = Fabric });
        var pol = polId is null ? null : (_poLines.FirstOrDefault(p => p.PurchaseOrderLineId == polId)
            ?? new PurchaseOrderLine { PurchaseOrderLineId = polId.Value, PurchaseOrderId = 5 });
        var line = new MaterialReceiptLine
        {
            MaterialReceiptLineId = lineId, MaterialReceiptId = receipt.MaterialReceiptId, MaterialReceipt = receipt,
            OrderMaterialId = omId, OrderMaterial = om, PurchaseOrderLineId = polId, PurchaseOrderLine = pol,
            AcceptedQty = qty,
        };
        receipt.MaterialReceiptLines.Add(line);
        _receiptLines.Add(line);
        return line;
    }

    private void AddPhoto(int receiptId) => _attachments.Add(new Attachment
    {
        OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = receiptId, Purpose = AttachmentPurposes.DocumentPhoto, IsCurrent = true,
    });

    private void SeedFivePagingReceipts()
    {
        PlacePo();
        for (var i = 1; i <= 5; i++)
        {
            var status = i <= 3 ? ReceiptStatuses.Pending : ReceiptStatuses.Confirmed;
            var r = SeedReceipt(100 + i, status, $"PN2611-{i:D3}");
            SeedLine(r, 10, (100 + i) * 10);
        }
    }

    private static SaveMaterialReceiptDto CustomerDto(int orderId, int omId, decimal qty) =>
        new(orderId, MaterialSources.Customer, null, Today, null, new[] { new SaveReceiptLineDto(omId, null, qty) });

    private static SaveMaterialReceiptDto SupplierDto(int poId, int polId, decimal qty) =>
        new(null, MaterialSources.Supplier, poId, Today, null, new[] { new SaveReceiptLineDto(null, polId, qty) });

    private (Request Req, RequestLine Rl) OpenReplenishRequest(int requestId, MaterialReceipt original, int rlId, decimal replenish)
    {
        var req = new Request
        {
            RequestId = requestId, RequestCode = "YC" + requestId, RequestTypeCode = RequestTypes.ShortDelivery,
            Status = RequestStatuses.PendingProcessing, ShortageResolution = ShortageResolutions.Replenish,
            MaterialReceiptId = original.MaterialReceiptId, MaterialReceipt = original, OrderId = original.OrderId,
            CreatedAt = Now.AddDays(-2),
        };
        var src = original.MaterialReceiptLines.First();
        var rl = new RequestLine
        {
            RequestLineId = rlId, RequestId = requestId, Request = req, ReplenishQty = replenish,
            PurchaseOrderLineId = src.PurchaseOrderLineId, OrderMaterialId = src.OrderMaterialId,
            MaterialReceiptLineId = src.MaterialReceiptLineId, MaterialReceiptLine = src,
        };
        req.RequestLines.Add(rl);
        _requestList.Add(req);
        _requestLines.Add(rl);
        return (req, rl);
    }

    [Fact]
    public async Task UTCID71_GetPagedAsync_FilterPending_ReturnsThree()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Status = ReceiptStatuses.Pending, Page = 1, PageSize = 10 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(3);
        result.Data.Items.Should().OnlyContain(i => i.Status == ReceiptStatuses.Pending);
        result.Data.TotalItems.Should().Be(3);
    }

    [Fact]
    public async Task UTCID72_GetPagedAsync_KeywordMatchesOrderPoOrCode()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Keyword = "DH2611", Page = 1, PageSize = 10 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(5);
        result.Data.Items.Should().OnlyContain(i => i.OrderCode != null && i.OrderCode.Contains("DH2611"));
    }

    [Fact]
    public async Task UTCID73_GetPagedAsync_PageTwoPageSizeOne_ReturnsSecondItem()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Page = 2, PageSize = 1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.TotalItems.Should().Be(5);
        result.Data.Items.Single().MaterialReceiptId.Should().Be(104);
    }

    [Fact]
    public async Task UTCID74_GetPagedAsync_PageBeyondData_ReturnsEmptyItems()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Page = 10, PageSize = 10 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(5);
    }

    [Fact]
    public async Task UTCID75_GetPagedAsync_BlankKeyword_ReturnsAll()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Keyword = "   ", Page = 1, PageSize = 10 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task UTCID76_GetPagedAsync_EmptyDb_ReturnsEmptySuccess()
    {
        // Arrange — no receipts

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task UTCID77_GetPagedAsync_OrderIdNotFound_ReturnsEmptySuccess()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { OrderId = 999, Page = 1, PageSize = 10 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID78_GetPagedAsync_PageZeroPageSizeZero_NormalizesWithoutException()
    {
        // Arrange
        SeedFivePagingReceipts();

        // Act
        var result = await Sut().GetPagedAsync(new MaterialReceiptQuery { Page = 0, PageSize = 0 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Page.Should().BeGreaterThan(0);
        result.Data.PageSize.Should().BeGreaterThan(0);
        result.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTCID79_GetByIdAsync_ConfirmedSecondDelivery_ReturnsTwoLinesDeliveryNo2()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(expectedDeliveries: 2);
        var earlier = SeedReceipt(9, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(earlier, 50, 91, polId: line.PurchaseOrderLineId);
        var receipt = SeedReceipt(10, ReceiptStatuses.Confirmed, confirmedAt: Now);
        SeedLine(receipt, 60, 101, polId: line.PurchaseOrderLineId);
        SeedLine(receipt, 20, 102, polId: line.PurchaseOrderLineId);

        // Act
        var result = await Sut().GetByIdAsync(10, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().HaveCount(2);
        result.Data.Lines.Should().OnlyContain(l => l.DeliveryNo == 2);
    }

    [Fact]
    public async Task UTCID80_GetByIdAsync_ShortDeliveryRequest_ShowsReplenishQty()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending, shortageRequestId: 20);
        var mrl = SeedLine(receipt, 30, 101, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, receipt, rlId: 201, replenish: 50);
        _requestList.Single(r => r.RequestId == 20).MaterialReceiptId = 10;
        _requestLines.Single().MaterialReceiptLineId = mrl.MaterialReceiptLineId;

        // Act
        var result = await Sut().GetByIdAsync(10, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ShortDeliveryRequestId.Should().NotBeNull();
        result.Data.Lines.Should().ContainSingle(l => l.ToReplenishQty == 50);
    }

    [Fact]
    public async Task UTCID81_GetByIdAsync_TieBreakConfirmedAt_UsesMaterialReceiptId()
    {
        // Arrange
        var (po, line, _, _) = PlacePo(expectedDeliveries: 2);
        var at = Now.AddDays(-1);
        var r10 = SeedReceipt(10, ReceiptStatuses.Confirmed, confirmedAt: at);
        SeedLine(r10, 50, 101, polId: line.PurchaseOrderLineId);
        var r11 = SeedReceipt(11, ReceiptStatuses.Confirmed, confirmedAt: at);
        SeedLine(r11, 30, 111, polId: line.PurchaseOrderLineId);

        // Act
        var dto10 = (await Sut().GetByIdAsync(10, default)).Data!;
        var dto11 = (await Sut().GetByIdAsync(11, default)).Data!;

        // Assert
        dto10.Lines.Single().DeliveryNo.Should().Be(1);
        dto11.Lines.Single().DeliveryNo.Should().Be(dto10.Lines.Single().DeliveryNo!.Value + 1);
    }

    [Fact]
    public async Task UTCID82_GetByIdAsync_PendingNoLines_ReturnsEmptyLines()
    {
        // Arrange
        SeedReceipt(12, ReceiptStatuses.Pending);

        // Act
        var result = await Sut().GetByIdAsync(12, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID83_GetByIdAsync_NotFound_ReturnsFailure()
    {
        // Arrange

        // Act
        var result = await Sut().GetByIdAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID84_GetByIdAsync_IdZero_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await Sut().GetByIdAsync(0, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID85_GetByIdAsync_NullOrderMaterial_SafeOrFailure()
    {
        // Arrange
        var receipt = SeedReceipt(13, ReceiptStatuses.Pending);
        var line = new MaterialReceiptLine
        {
            MaterialReceiptLineId = 1301, MaterialReceiptId = 13, MaterialReceipt = receipt,
            MaterialId = 1, Material = Fabric, OrderMaterialId = null, AcceptedQty = 1,
        };
        receipt.MaterialReceiptLines.Add(line);
        _receiptLines.Add(line);

        // Act
        var result = await Sut().GetByIdAsync(13, default);

        // Assert — line without OrderMaterial should still expose MaterialId from Material navigation
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Single().MaterialId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID86_CreateAsync_CustomerReceipt_NotifiesManager()
    {
        // Arrange
        var (_, _, om, order) = PlacePo(customerQty: 200);
        CaptureReceiptAdds(om, null);

        // Act
        var result = await Sut().CreateAsync(CustomerDto(order.OrderId, om.OrderMaterialId, 100), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(ReceiptStatuses.Pending);
        result.Data.ReceiptCode.Should().Be("PN2611-001");
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID87_CreateAsync_SupplierReceipt_SetsSupplierFromPo()
    {
        // Arrange
        var (po, line, om, _) = PlacePo(ordered: 300);
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 150), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PurchaseOrderId.Should().Be(5);
        po.SupplierId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID88_CreateAsync_ExactOutstanding_Succeeds()
    {
        // Arrange
        var (_, _, om, order) = PlacePo(customerQty: 100);
        CaptureReceiptAdds(om, null);

        // Act
        var result = await Sut().CreateAsync(CustomerDto(order.OrderId, om.OrderMaterialId, 100), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID89_CreateAsync_EmptyLines_FailureValidation()
    {
        // Arrange
        var (_, _, om, order) = PlacePo();
        var validator = new SaveMaterialReceiptDtoValidator();
        var dto = new SaveMaterialReceiptDto(order.OrderId, MaterialSources.Customer, null, Today, null, []);

        // Act
        var valid = validator.Validate(dto);
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        valid.IsValid.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID90_CreateAsync_ThirdDelivery_Succeeds()
    {
        // Arrange
        var (po, line, om, _) = PlacePo(expectedDeliveries: 3);
        var r1 = SeedReceipt(41, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-2));
        SeedLine(r1, 50, 410, polId: line.PurchaseOrderLineId);
        var r2 = SeedReceipt(42, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(r2, 50, 420, polId: line.PurchaseOrderLineId);
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 50), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID91_CreateAsync_DeliveryCountReached_Failure()
    {
        // Arrange
        var (po, line, om, _) = PlacePo(expectedDeliveries: 3);
        foreach (var (id, day) in new[] { (41, -3), (42, -2), (43, -1) })
        {
            var r = SeedReceipt(id, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(day));
            SeedLine(r, 30, id * 10, polId: line.PurchaseOrderLineId);
        }
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptDeliveryCountReached);
    }

    [Fact]
    public async Task UTCID92_CreateAsync_InvalidSource_FailureValidation()
    {
        // Arrange
        var dto = new SaveMaterialReceiptDto(1, "OTHER", null, Today, null, new[] { new SaveReceiptLineDto(1, null, 1) });

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().BeOneOf(ResultKind.Validation, ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID93_CreateAsync_OrderNotFound_FailureNotFound()
    {
        // Arrange
        CaptureReceiptAdds();

        // Act
        var result = await Sut().CreateAsync(CustomerDto(999, 1, 50), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID94_CreateAsync_DuplicatePending_Failure()
    {
        // Arrange
        var (po, line, om, order) = PlacePo();
        var pending = SeedReceipt(70, ReceiptStatuses.Pending);
        SeedLine(pending, 50, 701, omId: om.OrderMaterialId, polId: null);
        pending.Source = MaterialSources.Customer;
        pending.OrderId = order.OrderId;
        CaptureReceiptAdds(om, null);

        // Act
        var result = await Sut().CreateAsync(CustomerDto(order.OrderId, om.OrderMaterialId, 50), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptDuplicatePending);
    }

    [Fact]
    public async Task UTCID95_CreateAsync_ShortageClosed_Failure()
    {
        // Arrange — ClosedWithoutReplenishAsync cần RequestLine.Request navigation + PurchaseOrderLineId=null
        var (_, _, om, order) = PlacePo(customerQty: 100);
        om.CustomerQty = 50;
        var req = new Request
        {
            RequestId = 1, RequestTypeCode = RequestTypes.ShortDelivery, Status = RequestStatuses.Completed,
            ShortageResolution = ShortageResolutions.NoReplenish,
        };
        var rl = new RequestLine
        {
            RequestLineId = 1, RequestId = 1, Request = req, OrderMaterialId = om.OrderMaterialId,
            PurchaseOrderLineId = null, ReplenishQty = 0,
        };
        req.RequestLines.Add(rl);
        _requestList.Add(req);
        _requestLines.Add(rl);
        CaptureReceiptAdds(om, null);

        // Act
        var result = await Sut().CreateAsync(CustomerDto(order.OrderId, om.OrderMaterialId, 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptShortageClosed);
    }

    [Fact]
    public async Task UTCID96_CreateAsync_TransactionFailure_DoesNotCallGetById()
    {
        // Arrange — không ghi list khi Add (giả lập rollback); SaveChanges ném lỗi
        var (_, _, om, order) = PlacePo();
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        CaptureReceiptAdds(om, null, addToBacking: false);
        _uow.When(u => u.SaveChangesAsync()).Do(_ => throw new InvalidOperationException("db"));

        // Act
        var act = () => Sut().CreateAsync(CustomerDto(order.OrderId, om.OrderMaterialId, 10), default);

        // Assert — transaction failure must not surface as Success from GetById
        await act.Should().ThrowAsync<InvalidOperationException>();
        var peek = _receipts.Where(r => r.Status == ReceiptStatuses.Pending).ToList();
        peek.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID97_UpdateAsync_Pending_ChangesLines()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        var om2 = new OrderMaterial { OrderMaterialId = 2, OrderId = om.OrderId, MaterialId = 2, Material = Fabric, CustomerQty = 100 };
        _orderMaterials.Add(om2);
        var pol2 = new PurchaseOrderLine
        {
            PurchaseOrderLineId = 12, PurchaseOrderId = po.PurchaseOrderId, PurchaseOrder = po,
            OrderMaterialId = 2, OrderMaterial = om2, OrderedQty = 100,
        };
        _poLines.Add(pol2);
        po.PurchaseOrderLines.Add(pol2);
        FixupReceiptLinesOnSave(receipt);

        // Act
        var dto = SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 80) with
        {
            Lines = new[] { new SaveReceiptLineDto(null, line.PurchaseOrderLineId, 80), new SaveReceiptLineDto(null, pol2.PurchaseOrderLineId, 20) },
        };
        var result = await Sut().UpdateAsync(10, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().HaveCount(2);
        await _notifier.Received().NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID98_UpdateAsync_ReturnedStatus_Allowed()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Returned);
        SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        FixupReceiptLinesOnSave(receipt);

        // Act
        var result = await Sut().UpdateAsync(10, SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 80), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(ReceiptStatuses.Pending);
    }

    [Fact]
    public async Task UTCID99_UpdateAsync_ExcludesCurrentReceiptOutstanding_Succeeds()
    {
        // Arrange
        var (po, line, om, _) = PlacePo(ordered: 100);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        FixupReceiptLinesOnSave(receipt);

        // Act
        var result = await Sut().UpdateAsync(10, SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 100), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID100_UpdateAsync_EmptyLines_FailureValidation()
    {
        // Arrange
        var (po, line, _, _) = PlacePo();
        SeedReceipt(10, ReceiptStatuses.Pending);

        // Act
        var dto = SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 0) with { Lines = [] };
        var result = await Sut().UpdateAsync(10, dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID101_UpdateAsync_Confirmed_FailureInvalidStatus()
    {
        // Arrange
        var (po, line, _, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Confirmed);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);

        // Act
        var result = await Sut().UpdateAsync(10, SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 5), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task UTCID102_UpdateAsync_SupplementaryPending_FailureInvalidStatus()
    {
        // Arrange
        SeedReceipt(14, ReceiptStatuses.Pending, shortageRequestId: 20);

        // Act
        var result = await Sut().UpdateAsync(14, CustomerDto(1, 1, 1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task UTCID103_UpdateAsync_NotFound_Failure()
    {
        // Arrange

        // Act
        var result = await Sut().UpdateAsync(999, CustomerDto(1, 1, 1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID104_UpdateAsync_ReplacesLines_PreservesReferences()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        var old = SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        old.OriginalReceiptLineId = 500;
        _attachments.Add(new Attachment { OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = old.MaterialReceiptLineId, IsCurrent = true });
        FixupReceiptLinesOnSave(receipt);

        // Act
        var result = await Sut().UpdateAsync(10, SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 80), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _uow.Repository<MaterialReceiptLine>().Received(1).RemoveRange(Arg.Any<IEnumerable<MaterialReceiptLine>>());
        result.Data!.Lines.Should().ContainSingle(l => l.AcceptedQty == 80);
    }

    [Fact]
    public async Task UTCID105_CreateSupplementaryAsync_Valid_SuccessPending()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        var src = SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, original, 201, 50);
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(201, 30) }), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(ReceiptStatuses.Pending);
        result.Data.ShortageRequestId.Should().Be(20);
    }

    [Fact]
    public async Task UTCID106_CreateSupplementaryAsync_ExactRemaining_Succeeds()
    {
        // Arrange
        var (po, line, om, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, original, 201, 50);
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(201, 50) }), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID107_CreateSupplementaryAsync_ExceedsReplenish_Failure()
    {
        // Arrange
        var (_, line, om, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, original, 201, 50);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(201, 51) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ShortageExceeded);
    }

    [Fact]
    public async Task UTCID108_CreateSupplementaryAsync_IncludesPendingRemaining_Failure()
    {
        // Arrange
        var (_, line, om, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, original, 201, 50);
        var pending = SeedReceipt(60, ReceiptStatuses.Pending, shortageRequestId: 20);
        SeedLine(pending, 20, 601, polId: line.PurchaseOrderLineId);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(201, 31) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ShortageExceeded);
    }

    [Fact]
    public async Task UTCID109_CreateSupplementaryAsync_SameRequestDate_Succeeds()
    {
        // Arrange
        var (_, line, om, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        var (req, _) = OpenReplenishRequest(20, original, 201, 50);
        req.CreatedAt = new DateTime(2026, 11, 20, 8, 0, 0);
        CaptureReceiptAdds(om, line);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, new DateOnly(2026, 11, 20), null,
            new[] { new SupplementaryLineDto(201, 10) }), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID110_CreateSupplementaryAsync_ReceiptDateBeforeRequest_Failure()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        var (req, _) = OpenReplenishRequest(20, original, 201, 50);
        req.CreatedAt = new DateTime(2026, 11, 20, 8, 0, 0);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, new DateOnly(2026, 11, 19), null,
            new[] { new SupplementaryLineDto(201, 10) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID111_CreateSupplementaryAsync_RequestNotFound_FailureNotFound()
    {
        // Arrange

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(999, Today, null,
            new[] { new SupplementaryLineDto(1, 1) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID112_CreateSupplementaryAsync_RequestClosed_FailureNotResolvable()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        var (req, rl) = OpenReplenishRequest(20, original, 201, 50);
        req.Status = RequestStatuses.Completed;

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(rl.RequestLineId, 10) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ShortageNotResolvable);
    }

    [Fact]
    public async Task UTCID113_CreateSupplementaryAsync_DeliveryCountReached_Failure()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(expectedDeliveries: 1);
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-2));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        var (req, rl) = OpenReplenishRequest(20, original, 201, 50);
        req.ExpectedDeliveryCount = 1;
        for (var i = 0; i < 2; i++)
        {
            var sup = SeedReceipt(70 + i, ReceiptStatuses.Confirmed, shortageRequestId: 20, confirmedAt: Now.AddDays(-1));
            SeedLine(sup, 10, 700 + i, polId: line.PurchaseOrderLineId);
        }

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(rl.RequestLineId, 5) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptDeliveryCountReached);
    }

    [Fact]
    public async Task UTCID114_CreateSupplementaryAsync_LineNotInRequest_FailureValidation()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var original = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(original, 50, 11, polId: line.PurchaseOrderLineId);
        OpenReplenishRequest(20, original, 201, 50);

        // Act
        var result = await Sut().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(20, Today, null,
            new[] { new SupplementaryLineDto(999, 10) }), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task UTCID115_ConfirmAsync_Valid_PostsStockAndRecalculates()
    {
        // Arrange
        var (po, line, om, order) = PlacePo(ordered: 200);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        receipt.Status.Should().Be(ReceiptStatuses.Confirmed);
        receipt.ConfirmedBy.Should().Be(7);
        receipt.ConfirmedAt.Should().Be(Now);
        await _stock.Received(1).PostAsync(Arg.Is<IReadOnlyList<StockMovement>>(m => m.Single().QtyChange == 100), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID116_ConfirmAsync_FinalDeliveryShortfall_StagesShortDelivery()
    {
        // Arrange
        var (po, line, om, _) = PlacePo(ordered: 200, expectedDeliveries: 3);
        var r1 = SeedReceipt(1, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-2));
        SeedLine(r1, 75, 11, polId: line.PurchaseOrderLineId);
        var r2 = SeedReceipt(2, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(r2, 75, 21, polId: line.PurchaseOrderLineId);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 30, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain(WarehouseMessages.ShortDeliveryCreated);
        await _requests.Received(1).StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID117_ConfirmAsync_ExactOutstanding_Succeeds()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(ordered: 100);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 100, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID118_ConfirmAsync_ExceedsPurchase_FailureNoStock()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(ordered: 100);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 101, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptExceedsPurchase);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID119_ConfirmAsync_ExceedsCustomer_FailureNoStock()
    {
        // Arrange
        var (_, _, om, _) = PlacePo(customerQty: 100);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending, source: MaterialSources.Customer, poId: null);
        SeedLine(receipt, 101, 1001, omId: om.OrderMaterialId, polId: null);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(WarehouseMessages.ReceiptExceedsCustomer);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID120_ConfirmAsync_AllZeroQty_FailureOrNoDeliveryCount()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(expectedDeliveries: 3);
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 0, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID121_ConfirmAsync_NegativeOutstandingMessage_NoNegativeInText()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(ordered: 100);
        line.NotReplenishedQty = 120;
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("còn -");
    }

    [Fact]
    public async Task UTCID122_ConfirmAsync_NotFound_Failure()
    {
        // Act
        var result = await Sut().ConfirmAsync(999, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID123_ConfirmAsync_AlreadyConfirmed_FailureNoStock()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Confirmed);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID124_ConfirmAsync_ReturnedStatus_Failure()
    {
        // Arrange
        SeedReceipt(10, ReceiptStatuses.Returned);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task UTCID125_ConfirmAsync_NoPhoto_Failure()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptPhotoRequired);
    }

    [Fact]
    public async Task UTCID126_ConfirmAsync_ConfirmedByZero_FailureValidation()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);

        // Act
        var result = await Sut().ConfirmAsync(10, 0, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID127_ConfirmAsync_StockFailure_RollsBackPending()
    {
        // Arrange
        var (_, line, _, _) = PlacePo();
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending);
        SeedLine(receipt, 10, 1001, polId: line.PurchaseOrderLineId);
        AddPhoto(10);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, "fail"));

        // Act
        var result = await Sut().ConfirmAsync(10, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        receipt.Status.Should().Be(ReceiptStatuses.Pending);
        await _uow.Received().RollbackTransactionAsync();
    }

    [Fact]
    public async Task UTCID128_ConfirmAsync_SecondReceiptExceedsAfterFirstConfirmed_Failure()
    {
        // Arrange
        var (_, line, _, _) = PlacePo(ordered: 100);
        var first = SeedReceipt(10, ReceiptStatuses.Confirmed, confirmedAt: Now.AddDays(-1));
        SeedLine(first, 80, 1001, polId: line.PurchaseOrderLineId);
        var second = SeedReceipt(11, ReceiptStatuses.Pending);
        SeedLine(second, 80, 1101, polId: line.PurchaseOrderLineId);
        AddPhoto(11);

        // Act
        var result = await Sut().ConfirmAsync(11, 7, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptExceedsPurchase);
    }

    [Fact]
    public async Task UTCID129_ReturnAsync_Valid_SetsReturnedAndNotifies()
    {
        // Arrange
        var receipt = SeedReceipt(10, ReceiptStatuses.Pending, code: "PN-10");

        // Act
        var result = await Sut().ReturnAsync(10, new ReasonDto("Sai số lượng"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        receipt.Status.Should().Be(ReceiptStatuses.Returned);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID130_ReturnAsync_ReasonMaxLength_Succeeds()
    {
        // Arrange
        SeedReceipt(10, ReceiptStatuses.Pending);
        var reason = new string('x', 500);

        // Act
        var result = await Sut().ReturnAsync(10, new ReasonDto(reason), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID131_ReturnAsync_EmptyReason_FailureValidation()
    {
        // Arrange
        SeedReceipt(10, ReceiptStatuses.Pending);
        var validator = new ReasonDtoValidator();

        // Act
        var valid = validator.Validate(new ReasonDto(""));
        var result = await Sut().ReturnAsync(10, new ReasonDto(""), default);

        // Assert
        valid.IsValid.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID132_ReturnAsync_NullReason_FailureValidation()
    {
        // Arrange
        SeedReceipt(10, ReceiptStatuses.Pending);

        // Act
        var result = await Sut().ReturnAsync(10, new ReasonDto(null!), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID133_ReturnAsync_Confirmed_FailureInvalidStatus()
    {
        // Arrange
        SeedReceipt(10, ReceiptStatuses.Confirmed);

        // Act
        var result = await Sut().ReturnAsync(10, new ReasonDto("x"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task UTCID134_ReturnAsync_NotFound_Failure()
    {
        // Act
        var result = await Sut().ReturnAsync(999, new ReasonDto("x"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }
}