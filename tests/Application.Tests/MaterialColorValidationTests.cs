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

/// <summary>
/// Database 26/10: màu NVL nằm trong Material.Color (mỗi màu một dòng NVL, UQ_Material_NameType = tên + loại + màu);
/// NVL của đơn, phiếu cấp… không còn cột màu riêng nên trùng NVL là trùng cả màu.
/// </summary>
public class MaterialColorValidationTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<Material> _materials = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<CustomerOrder> _orders = [];

    public MaterialColorValidationTests()
    {
        _materials.Add(Fabric(1, "V-0001", "Đen"));
        _orders.Add(new CustomerOrder { OrderId = 7, OrderCode = "DH-7", Status = OrderStatuses.New });
        Setup(_materials); Setup(_norms); Setup(_orderMaterials); Setup(_orders);
        Setup(new List<PurchaseOrderLine>()); Setup(new List<MaterialReceiptLine>()); Setup(new List<MaterialIssueLine>());
        Setup(new List<StockBalance>()); Setup(new List<OrderLine>()); Setup(new List<VwOrderMaterialDelivery>());
        _uow.Repository<Material>().AddAsync(Arg.Do<Material>(m => { m.MaterialId = 50; _materials.Add(m); })).Returns(Task.CompletedTask);
    }

    private static Material Fabric(int id, string code, string color) => new()
    {
        MaterialId = id, MaterialCode = code, MaterialName = "Vải gió", MaterialType = MaterialTypes.Fabric, Category = "MAIN",
        Unit = "m", Color = color, Status = MaterialStatuses.Active,
    };

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private MaterialService Materials() => new(_uow, _locks);

    private OrderMaterialService OrderMaterials() => new(_uow, _notifier);

    // ---------- Danh mục NVL ----------

    [Fact]
    public async Task Same_name_and_type_is_allowed_for_another_color_but_not_for_the_same_color()
    {
        var sameColor = await Materials().CreateAsync(new CreateMaterialDto(null, "Vải gió", MaterialTypes.Fabric, "MAIN", "m", " Đen "), default);
        sameColor.ErrorCode.Should().Be(ErrorCodes.WarehouseMaterialDuplicate);
        sameColor.Message.Should().Be(WarehouseMessages.MaterialNameDuplicate);

        var otherColor = await Materials().CreateAsync(new CreateMaterialDto(null, "Vải gió", MaterialTypes.Fabric, "MAIN", "m", "Xanh"), default);
        otherColor.IsSuccess.Should().BeTrue();
        otherColor.Data!.Color.Should().Be("Xanh");
    }

    [Fact]
    public async Task The_color_of_a_material_already_used_in_a_norm_cannot_be_changed()
    {
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 1, MaterialId = 1 });

        var result = await Materials().UpdateAsync(1, new UpdateMaterialDto("Vải gió", MaterialTypes.Fabric, "MAIN", "m", "Xanh"), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseMaterialInUse);
        _materials[0].Color.Should().Be("Đen");
    }

    [Fact]
    public async Task An_unused_material_can_change_color_and_a_missing_color_keeps_the_current_one()
    {
        (await Materials().UpdateAsync(1, new UpdateMaterialDto("Vải gió", MaterialTypes.Fabric, "MAIN", "m"), default)).IsSuccess.Should().BeTrue();
        _materials[0].Color.Should().Be("Đen");

        (await Materials().UpdateAsync(1, new UpdateMaterialDto("Vải gió", MaterialTypes.Fabric, "MAIN", "m", "Xanh"), default)).IsSuccess.Should().BeTrue();
        _materials[0].Color.Should().Be("Xanh");
    }

    [Fact]
    public void Material_validator_limits_the_color_length()
    {
        new CreateMaterialDtoValidator().Validate(new CreateMaterialDto(null, "Vải", MaterialTypes.Fabric, "MAIN", "m", new string('x', 51)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Material_validator_needs_positive_sizes_when_given()
    {
        var validator = new CreateMaterialDtoValidator();

        validator.Validate(new CreateMaterialDto(null, "Vải", MaterialTypes.Fabric, "MAIN_FABRIC", "m", null, 5000, 150)).IsValid.Should().BeTrue();
        validator.Validate(new CreateMaterialDto(null, "Vải", MaterialTypes.Fabric, "MAIN_FABRIC", "m", null, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new CreateMaterialDto(null, "Vải", MaterialTypes.Fabric, "MAIN_FABRIC", "m", null, 0, 150)).IsValid.Should().BeFalse();
        validator.Validate(new CreateMaterialDto(null, "Vải", MaterialTypes.Fabric, "MAIN_FABRIC", "m", null, 5000, -1)).IsValid.Should().BeFalse();
    }

    // ---------- NVL của đơn ----------

    [Fact]
    public async Task An_order_cannot_declare_the_same_material_twice_and_the_message_names_the_color()
    {
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 3, OrderId = 7, MaterialId = 1, Material = _materials[0], RequiredQty = 10 });

        var result = await OrderMaterials().CreateAsync(new CreateOrderMaterialDto(7, 1, 20, 0, null, null), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderMaterialDuplicate);
        result.Message.Should().Contain("V-0001").And.Contain("(Đen)");
    }

    [Fact]
    public void An_order_material_line_needs_a_required_or_customer_qty()
    {
        var validator = new CreateOrderMaterialDtoValidator();
        validator.Validate(new CreateOrderMaterialDto(7, 1, 0, 0, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new CreateOrderMaterialDto(7, 1, 10, 0, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new CreateOrderMaterialDto(7, 1, 0, 5, 1, null)).IsValid.Should().BeTrue();
    }

    // ---------- Phiếu cấp ----------

    [Fact]
    public void Issue_lines_must_match_the_warehouse_and_must_not_repeat()
    {
        var validator = new CreateMaterialIssueDtoValidator();
        var today = new DateOnly(2026, 10, 27);

        validator.Validate(new CreateMaterialIssueDto(WarehouseTypes.Material, 1, null, null, null, today,
            [new CreateIssueLineDto(1, null, null, 5)])).IsValid.Should().BeTrue();
        validator.Validate(new CreateMaterialIssueDto(WarehouseTypes.Material, 1, null, null, null, today,
            [new CreateIssueLineDto(1, 101, null, 5)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateMaterialIssueDto(WarehouseTypes.SemiFinished, null, 7, 3, 9, today,
            [new CreateIssueLineDto(1, 101, null, 5)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateMaterialIssueDto(WarehouseTypes.SemiFinished, null, 7, 3, 9, today,
            [new CreateIssueLineDto(null, 101, null, 5), new CreateIssueLineDto(null, 101, null, 2)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateMaterialIssueDto(WarehouseTypes.SemiFinished, null, 7, 3, 9, today,
            [new CreateIssueLineDto(null, 101, null, 5), new CreateIssueLineDto(null, 101, 12, 2)])).IsValid.Should().BeTrue();
    }
}
