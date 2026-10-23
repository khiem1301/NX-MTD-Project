using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Purchasing;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>MI-07: nhà cung cấp (UC14), đơn mua — chốt đặt hàng (UC23), thanh toán (UC34).</summary>
public class PurchaseFlowServiceTests
{
    private static readonly byte[] RowVer = { 7 };
    private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<PurchaseOrder> _poRepo = Substitute.For<IGenericRepository<PurchaseOrder>>();
    private readonly IGenericRepository<Supplier> _supplierRepo = Substitute.For<IGenericRepository<Supplier>>();
    private readonly IPurchaseOrderService _orders = Substitute.For<IPurchaseOrderService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<PurchaseOrder> _pos = new();
    private readonly List<Supplier> _suppliers = new();

    public PurchaseFlowServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.Role.Returns(Roles.Manager);
        _uow.Repository<PurchaseOrder>().Returns(_poRepo);
        _uow.Repository<Supplier>().Returns(_supplierRepo);
        _poRepo.Query().Returns(_ => _pos.AsTestAsyncQueryable());
        _supplierRepo.Query().Returns(_ => _suppliers.AsTestAsyncQueryable());
    }

    private PurchaseOrderFlowService Flow() => new(_uow, _orders, _audit, _notifier, _user, _clock);

    private SupplierService Suppliers() => new(_uow, _audit, _clock);

    private static PurchaseOrderLine Line(int id, decimal qty = 100, decimal price = 0) =>
        new() { PurchaseOrderLineId = id, OrderedQty = qty, UnitPrice = price };

    private PurchaseOrder Po(string goods = PurchaseOrderStatuses.PendingFinalisation,
        string payment = PurchaseOrderStatuses.Unpaid, int id = 5)
    {
        var po = new PurchaseOrder
        {
            PurchaseOrderId = id, PoCode = "DM-20261027-001", GoodsStatus = goods, PaymentStatus = payment,
            CreatedAt = Now, RowVer = RowVer,
            PurchaseOrderLines = new List<PurchaseOrderLine> { Line(11, 100, 0), Line(12, 50, 0) },
        };
        _pos.Add(po);
        _poRepo.GetByIdAsync(id).Returns(po);
        return po;
    }

    private Supplier Supplier(int id = 3, string status = RecordStatus.Active, string name = "Công ty Chỉ Việt")
    {
        var supplier = new Supplier { SupplierId = id, SupplierName = name, Status = status, CreatedAt = Now };
        _suppliers.Add(supplier);
        _supplierRepo.GetByIdAsync(id).Returns(supplier);
        return supplier;
    }

    private static PlacePurchaseOrderDto Place(int supplierId = 3, DateOnly? orderDate = null, decimal price = 12_000,
        IReadOnlyList<PlacePurchaseLineDto>? lines = null) =>
        new(supplierId, orderDate ?? Today, Today.AddDays(7), "Giao tại xưởng",
            lines ?? new[] { new PlacePurchaseLineDto(11, 100, price), new PlacePurchaseLineDto(12, 60, price) }, RowVer);

    private static ConfirmPaymentDto Pay(string method = "BankTransfer", DateOnly? date = null, decimal amount = 1_000_000) =>
        new(date ?? Today, amount, method, "Chuyển khoản VCB", RowVer);

    // ========== Nhà cung cấp (UC14) ==========

    [Fact]
    public async Task CreateSupplier_DuplicateName_ReturnsDuplicateError()
    {
        Supplier(name: "Công ty Chỉ Việt");

        var result = await Suppliers().CreateAsync(
            new SaveSupplierDto("  Công ty Chỉ Việt ", null, null, null, null, null), default);

        result.ErrorCode.Should().Be(ErrorCodes.SupplierNameDuplicate);
        await _supplierRepo.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateSupplier_Valid_IsActiveAndLogsInsert()
    {
        Supplier? added = null;
        _supplierRepo.When(r => r.AddAsync(Arg.Any<Supplier>())).Do(c => added = c.Arg<Supplier>());

        var result = await Suppliers().CreateAsync(
            new SaveSupplierDto("Kim Chỉ Hòa Bình", " 0901234567 ", " ", null, "Phụ liệu", null), default);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(RecordStatus.Active);
        added.SupplierName.Should().Be("Kim Chỉ Hòa Bình");
        added.Phone.Should().Be("0901234567");
        added.Email.Should().BeNull();   // chuỗi trắng lưu thành NULL
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Supplier) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task DiscontinueSupplier_SetsStatusAndLogsUpdate()
    {
        var supplier = Supplier();

        var result = await Suppliers().DiscontinueAsync(3, default);

        result.IsSuccess.Should().BeTrue();
        supplier.Status.Should().Be(RecordStatus.Discontinued);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Supplier) && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task ReactivateSupplier_AlreadyActive_ChangesNothing()
    {
        Supplier();

        var result = await Suppliers().ReactivateAsync(3, default);

        result.IsSuccess.Should().BeTrue();
        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task ActiveOptions_ExcludesDiscontinued()
    {
        Supplier(1, RecordStatus.Active, "B Supplier");
        Supplier(2, RecordStatus.Discontinued, "C Supplier");
        Supplier(3, RecordStatus.Active, "A Supplier");

        var result = await Suppliers().GetActiveOptionsAsync(default);

        result.Data!.Select(o => o.SupplierName).Should().Equal("A Supplier", "B Supplier");
    }

    // ========== Chốt đặt hàng (UC23) ==========

    [Fact]
    public async Task Place_Valid_OrdersWithSupplierPricesAndNotifiesAccountant()
    {
        var po = Po();
        Supplier();

        var result = await Flow().PlaceAsync(5, Place(), default);

        result.IsSuccess.Should().BeTrue();
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Ordered);
        po.SupplierId.Should().Be(3);
        po.OrderDate.Should().Be(Today);
        po.OrderedAt.Should().Be(Now);
        po.PurchaseOrderLines.Single(l => l.PurchaseOrderLineId == 12).OrderedQty.Should().Be(60);
        po.PurchaseOrderLines.Should().OnlyContain(l => l.UnitPrice == 12_000);
        _poRepo.Received(1).SetOriginalRowVersion(po, RowVer);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(PurchaseOrder) && e.Action == AuditActions.Update));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Place_NotPendingFinalisation_ReturnsInvalidStatus()
    {
        Po(PurchaseGoodsStatuses.Ordered);
        Supplier();

        var result = await Flow().PlaceAsync(5, Place(), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
    }

    [Fact]
    public async Task Place_MissingLine_ReturnsLinesMismatch()
    {
        Po();
        Supplier();

        var result = await Flow().PlaceAsync(5,
            Place(lines: new[] { new PlacePurchaseLineDto(11, 100, 12_000) }), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderLinesMismatch);
    }

    [Fact]
    public async Task Place_UnknownOrDuplicateLine_ReturnsLinesMismatch()
    {
        Po();
        Supplier();

        var result = await Flow().PlaceAsync(5,
            Place(lines: new[] { new PlacePurchaseLineDto(11, 100, 1), new PlacePurchaseLineDto(11, 100, 1) }), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderLinesMismatch);
    }

    [Fact]
    public async Task Place_ZeroPrice_ReturnsPriceRequired()
    {
        Po();
        Supplier();

        var result = await Flow().PlaceAsync(5, Place(price: 0), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderPriceRequired);
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task Place_FutureOrderDate_ReturnsDateInvalid()
    {
        Po();
        Supplier();

        var result = await Flow().PlaceAsync(5, Place(orderDate: Today.AddDays(1)), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderDateInvalid);
    }

    [Fact]
    public async Task Place_DeliveryBeforeOrderDate_ReturnsDateInvalid()
    {
        Po();
        Supplier();
        var dto = Place() with { ExpectedDeliveryDate = Today.AddDays(-1) };

        var result = await Flow().PlaceAsync(5, dto, default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderDateInvalid);
        result.Message.Should().Be(Messages.PurchaseOrderDeliveryBeforeOrder);
    }

    [Fact]
    public async Task Place_DiscontinuedSupplier_ReturnsSupplierNotActive()
    {
        var po = Po();
        Supplier(status: RecordStatus.Discontinued);

        var result = await Flow().PlaceAsync(5, Place(), default);

        result.ErrorCode.Should().Be(ErrorCodes.SupplierNotActive);
        po.GoodsStatus.Should().Be(PurchaseOrderStatuses.PendingFinalisation);
    }

    [Fact]
    public async Task Place_UnknownSupplier_ReturnsNotFound()
    {
        Po();

        var result = await Flow().PlaceAsync(5, Place(supplierId: 99), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Place_UnknownPurchaseOrder_ReturnsNotFound()
    {
        var result = await Flow().PlaceAsync(404, Place(), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Cancel_PendingFinalisation_RejectsWithReasonAndLogs()
    {
        var po = Po();

        var result = await Flow().CancelAsync(5, new CancelPurchaseOrderDto("  Khách đã cấp đủ NVL ", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Rejected);
        po.RejectReason.Should().Be("Khách đã cấp đủ NVL");
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Reject && e.Reason == "Khách đã cấp đủ NVL"));
    }

    [Fact]
    public async Task Cancel_AlreadyOrdered_ReturnsInvalidStatus()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered);

        var result = await Flow().CancelAsync(5, new CancelPurchaseOrderDto("Nhầm", RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Ordered);
    }

    [Fact]
    public async Task Reprice_AfterReturn_UpdatesPricesResendsAndKeepsQuantities()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Returned);
        po.PaymentReturnReason = "Sai đơn giá";
        po.PurchaseOrderLines.Single(l => l.PurchaseOrderLineId == 12).OrderedQty = 60;
        var dto = new RepricePurchaseOrderDto("Đã báo giá lại",
            new[] { new RepricePurchaseLineDto(11, 11_000), new RepricePurchaseLineDto(12, 11_500) }, RowVer);

        var result = await Flow().RepriceAsync(5, dto, default);

        result.IsSuccess.Should().BeTrue();
        po.PaymentStatus.Should().Be(PurchaseOrderStatuses.Unpaid);
        po.PaymentReturnReason.Should().BeNull();
        po.PurchaseOrderLines.Single(l => l.PurchaseOrderLineId == 11).UnitPrice.Should().Be(11_000);
        po.PurchaseOrderLines.Single(l => l.PurchaseOrderLineId == 12).OrderedQty.Should().Be(60);   // số lượng khóa
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reprice_NotReturned_ReturnsInvalidStatus()
    {
        Po(PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid);
        var dto = new RepricePurchaseOrderDto(null,
            new[] { new RepricePurchaseLineDto(11, 1), new RepricePurchaseLineDto(12, 1) }, RowVer);

        var result = await Flow().RepriceAsync(5, dto, default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
    }

    // ========== Thanh toán (UC34) ==========

    [Fact]
    public async Task ConfirmPayment_WithoutAnyPhoto_StillMarksPaidAndNotifiesManager()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered);

        var result = await Flow().ConfirmPaymentAsync(5, Pay(), default);

        result.IsSuccess.Should().BeTrue();
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.Paid);
        po.PaymentMethod.Should().Be(PaymentMethods.BankTransfer);   // PascalCase → mã DB
        po.PaidAmount.Should().Be(1_000_000);
        po.PaymentDate.Should().Be(Today);
        po.PaidAt.Should().Be(Now);
        _poRepo.Received(1).SetOriginalRowVersion(po, RowVer);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(PurchaseOrder) && e.Action == AuditActions.Confirm));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PurchaseOrderStatuses.PendingFinalisation, PurchaseOrderStatuses.Unpaid)]
    [InlineData(PurchaseGoodsStatuses.Rejected, PurchaseOrderStatuses.Unpaid)]
    [InlineData(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Paid)]
    [InlineData(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Returned)]
    public async Task ConfirmPayment_NotPayable_ReturnsInvalidStatus(string goods, string payment)
    {
        Po(goods, payment);

        var result = await Flow().ConfirmPaymentAsync(5, Pay(), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
    }

    [Theory]
    [InlineData("Barter", 1_000, false)]
    [InlineData("Cash", 0, false)]
    [InlineData("Cash", 1_000, true)]
    public async Task ConfirmPayment_MethodAndAmount_AreChecked(string method, double amount, bool ok)
    {
        Po(PurchaseGoodsStatuses.Ordered);

        var result = await Flow().ConfirmPaymentAsync(5, Pay(method, amount: (decimal)amount), default);

        result.IsSuccess.Should().Be(ok);
        if (!ok) result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
    }

    [Fact]
    public async Task ConfirmPayment_FutureDate_ReturnsPaymentInvalid()
    {
        Po(PurchaseGoodsStatuses.Ordered);

        var result = await Flow().ConfirmPaymentAsync(5, Pay(date: Today.AddDays(1)), default);

        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
    }

    [Fact]
    public async Task ReturnPayment_StoresReasonAndNotifiesManager()
    {
        var po = Po(PurchaseGoodsStatuses.PartiallyReceived);

        var result = await Flow().ReturnPaymentAsync(5, new ReturnPaymentDto(" Sai số tiền ", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.Returned);
        po.PaymentReturnReason.Should().Be("Sai số tiền");
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Reject && e.Reason == "Sai số tiền"));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnPayment_AlreadyPaid_ReturnsInvalidStatus()
    {
        Po(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Paid);

        var result = await Flow().ReturnPaymentAsync(5, new ReturnPaymentDto("Nhầm", RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
    }

    // ========== Xem ==========

    [Fact]
    public async Task List_PayableOnly_ExcludesPendingAndRejected()
    {
        Po(PurchaseOrderStatuses.PendingFinalisation, id: 1);
        Po(PurchaseGoodsStatuses.Rejected, id: 2);
        Po(PurchaseGoodsStatuses.Ordered, id: 3);
        Po(PurchaseGoodsStatuses.ClosedShort, id: 4);

        var result = await Flow().ListAsync(new PurchaseOrderFlowQuery { PayableOnly = true }, default);

        result.Data!.Items.Select(i => i.PurchaseOrderId).Should().BeEquivalentTo(new[] { 3, 4 });
        result.Data.Items.Should().OnlyContain(i => i.GoodsStatus == "Ordered" || i.GoodsStatus == "ClosedShort");
    }

    [Fact]
    public async Task List_FiltersByPascalCaseStatus()
    {
        Po(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Paid, id: 1);
        Po(PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid, id: 2);

        var result = await Flow().ListAsync(new PurchaseOrderFlowQuery { PaymentStatus = "Unpaid" }, default);

        result.Data!.Items.Should().ContainSingle().Which.PurchaseOrderId.Should().Be(2);
    }

    private void GivenDetailLines(PurchaseOrder po) =>
        _orders.GetByIdAsync(po.PurchaseOrderId, Arg.Any<CancellationToken>()).Returns(Result<PurchaseOrderDto>.Success(
            new PurchaseOrderDto(po.PurchaseOrderId, po.PoCode, null, null, null, null, po.GoodsStatus, po.PaymentStatus,
                null, Now, po.PurchaseOrderLines.Select(l => new PurchaseOrderLineDto(l.PurchaseOrderLineId, null, 1,
                    "PL-0001", "Chỉ", "Cuộn", "Trắng", l.OrderedQty, l.UnitPrice, null, 0, 0)).ToList())));

    [Fact]
    public async Task Get_ManagerOnPending_CanPlaceAndCancelButNotPay()
    {
        var po = Po();
        GivenDetailLines(po);

        var dto = (await Flow().GetAsync(5, default)).Data!;

        dto.GoodsStatus.Should().Be("PendingFinalisation");
        dto.Permissions.Should().Be(new PurchaseOrderPermissionsDto(true, true, false, false, false));
        dto.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_AccountantOnUnpaidOrdered_CanConfirmAndReturnOnly()
    {
        _user.Role.Returns(Roles.Accountant);
        var po = Po(PurchaseGoodsStatuses.Ordered);
        po.PurchaseOrderLines.ToList().ForEach(l => l.UnitPrice = 10);
        GivenDetailLines(po);

        var dto = (await Flow().GetAsync(5, default)).Data!;

        dto.Permissions.Should().Be(new PurchaseOrderPermissionsDto(false, false, false, true, true));
        dto.TotalAmount.Should().Be(1_500);   // 100×10 + 50×10
    }

    [Fact]
    public async Task Get_ManagerOnReturnedOrder_CanReprice()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Returned);
        GivenDetailLines(po);

        var dto = (await Flow().GetAsync(5, default)).Data!;

        dto.Permissions.CanReprice.Should().BeTrue();
        dto.Permissions.CanConfirmPayment.Should().BeFalse();
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNotFound()
    {
        var result = await Flow().GetAsync(404, default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    // ========== Quyền ảnh chứng từ (KH-07) ==========

    private PurchaseOrderAttachmentAccess Access() => new(_uow, _user);

    [Fact]
    public async Task ProofAccess_AccountantCanWriteOnlyWhenPayable()
    {
        _user.Role.Returns(Roles.Accountant);
        Po(PurchaseGoodsStatuses.Ordered, id: 1);
        Po(PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Paid, id: 2);
        Po(PurchaseOrderStatuses.PendingFinalisation, id: 3);

        (await Access().CanWriteAsync(1, default)).Should().BeTrue();
        (await Access().CanWriteAsync(2, default)).Should().BeFalse();   // đã thanh toán: khóa ảnh
        (await Access().CanWriteAsync(3, default)).Should().BeFalse();   // chưa đặt hàng
        (await Access().CanWriteAsync(404, default)).Should().BeFalse();
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.WarehouseKeeper)]
    public async Task ProofAccess_OtherRolesCannotWrite(string role)
    {
        _user.Role.Returns(role);
        Po(PurchaseGoodsStatuses.Ordered);

        (await Access().CanWriteAsync(5, default)).Should().BeFalse();
    }

    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.Accountant, true)]
    [InlineData(Roles.Worker, false)]
    [InlineData(Roles.WarehouseKeeper, false)]
    public async Task ProofAccess_ReadLimitedToPurchasingRoles(string role, bool canRead)
    {
        _user.Role.Returns(role);
        Po(PurchaseGoodsStatuses.Ordered);

        (await Access().CanReadAsync(5, default)).Should().Be(canRead);
        (await Access().CanReadAsync(404, default)).Should().BeFalse();
    }

    [Fact]
    public void ProofAccess_RegistersForPurchaseOrderOwnerType() =>
        Access().OwnerType.Should().Be(AttachmentOwnerTypes.PurchaseOrder);
}

public class PurchaseFlowValidatorTests
{
    private static readonly byte[] RowVer = { 1 };

    [Fact]
    public void ReturnAndCancel_RequireReason()
    {
        new ReturnPaymentDtoValidator().Validate(new ReturnPaymentDto(" ", RowVer)).IsValid.Should().BeFalse();
        new CancelPurchaseOrderDtoValidator().Validate(new CancelPurchaseOrderDto("", RowVer)).IsValid.Should().BeFalse();
        new ReturnPaymentDtoValidator().Validate(new ReturnPaymentDto("Sai số tiền", RowVer)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ConfirmPayment_MethodMustBeBankTransferOrCash()
    {
        var validator = new ConfirmPaymentDtoValidator();
        var today = new DateOnly(2026, 10, 27);

        validator.Validate(new ConfirmPaymentDto(today, 100, "Cash", null, RowVer)).IsValid.Should().BeTrue();
        validator.Validate(new ConfirmPaymentDto(today, 100, "BankTransfer", null, RowVer)).IsValid.Should().BeTrue();
        validator.Validate(new ConfirmPaymentDto(today, 100, "Cheque", null, RowVer)).IsValid.Should().BeFalse();
        validator.Validate(new ConfirmPaymentDto(today, 0, "Cash", null, RowVer)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Supplier_NameRequiredAndPhoneDigitsOnly()
    {
        var validator = new SaveSupplierDtoValidator();

        validator.Validate(new SaveSupplierDto("NCC A", "0901234567", "a@b.vn", null, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new SaveSupplierDto("", null, null, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new SaveSupplierDto("NCC A", "09-01 abc", null, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new SaveSupplierDto("NCC A", null, "khong-phai-email", null, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Place_RequiresPositivePriceAndSupplier()
    {
        var validator = new PlacePurchaseOrderDtoValidator();
        var today = new DateOnly(2026, 10, 27);
        PlacePurchaseOrderDto Dto(int supplier, decimal price) =>
            new(supplier, today, null, null, new[] { new PlacePurchaseLineDto(1, 10, price) }, RowVer);

        validator.Validate(Dto(3, 1000)).IsValid.Should().BeTrue();
        validator.Validate(Dto(0, 1000)).IsValid.Should().BeFalse();
        validator.Validate(Dto(3, 0)).IsValid.Should().BeFalse();
    }
}
