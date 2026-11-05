using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>TH-03 (UC37): ảnh chứng từ phiếu nhập NVL — ai xem, ai thêm / gỡ.</summary>
public class MaterialReceiptAttachmentAccessTests : ProductionTestBase
{
    private readonly List<MaterialReceipt> _receipts = [];

    private MaterialReceiptAttachmentAccess Sut(string role, string status)
    {
        _receipts.Clear();
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 4, Status = status });
        SetupQueryable(_receipts);
        User.Role.Returns(role);
        return new MaterialReceiptAttachmentAccess(Uow, User);
    }

    [Theory]
    [InlineData(Roles.WarehouseKeeper, true)]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.TeamLeader, false)]
    [InlineData(Roles.Worker, false)]
    public async Task CanRead_OnlyWarehouseSide(string role, bool expected) =>
        (await Sut(role, ReceiptStatuses.Pending).CanReadAsync(4, default)).Should().Be(expected);

    [Fact]
    public async Task CanRead_UnknownReceipt_IsFalse() =>
        (await Sut(Roles.WarehouseKeeper, ReceiptStatuses.Pending).CanReadAsync(999, default)).Should().BeFalse();

    [Theory]
    [InlineData(Roles.WarehouseKeeper, ReceiptStatuses.Pending, true)]
    [InlineData(Roles.WarehouseKeeper, ReceiptStatuses.Returned, true)]
    [InlineData(Roles.WarehouseKeeper, ReceiptStatuses.Confirmed, false)]
    [InlineData(Roles.Manager, ReceiptStatuses.Pending, false)]
    public async Task CanWrite_OnlyKeeperWhileEditable(string role, string status, bool expected) =>
        (await Sut(role, status).CanWriteAsync(4, default)).Should().Be(expected);

    [Fact]
    public void OwnerType_IsMaterialReceipt() =>
        Sut(Roles.WarehouseKeeper, ReceiptStatuses.Pending).OwnerType.Should().Be(AttachmentOwnerTypes.MaterialReceipt);
}
