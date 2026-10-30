using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Orders;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UC10 (định mức): thành phần PANEL / TRIM, biến thể màu × size, định mức NVL theo biến thể.</summary>
public class ProductSpecServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "AO-01", ProductName = "Áo" }];
    private readonly List<ProductComponent> _components = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<Material> _materials =
    [
        new() { MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Status = MaterialStatuses.Active },
        new() { MaterialId = 2, MaterialCode = "K1", MaterialName = "Khóa", Unit = "cái", Status = MaterialStatuses.Discontinued },
    ];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private int _nextId = 100;

    public ProductSpecServiceTests()
    {
        Setup(_products); Setup(_materials); Setup(_orderLines); Setup(_workOrderLines);
        Setup(_components, c => c.ProductComponentId = ++_nextId);
        Setup(_variants, v => v.ProductVariantId = ++_nextId);
        // Quan hệ điều hướng do EF tự nối khi đọc lại; ở đây nối tay cho dòng định mức vừa thêm.
        Setup(_norms, n =>
        {
            n.ProductVariant = _variants.First(v => v.ProductVariantId == n.ProductVariantId);
            n.ProductComponent = _components.First(c => c.ProductComponentId == n.ProductComponentId);
            n.Material = _materials.First(m => m.MaterialId == n.MaterialId);
        });
        foreach (var empty in new[] { typeof(RequestLine), typeof(CuttingLayLine), typeof(CutHandoverLine), typeof(MaterialIssueLine),
                     typeof(StockBalance), typeof(OperationComponent) })
            SetupEmpty(empty);
    }

    private void Setup<T>(List<T> backing, Action<T>? onAdd = null) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.AddAsync(Arg.Do<T>(e => { onAdd?.Invoke(e); backing.Add(e); })).Returns(Task.CompletedTask);
        repo.AddRangeAsync(Arg.Do<IEnumerable<T>>(es => { foreach (var e in es) { onAdd?.Invoke(e); backing.Add(e); } })).Returns(Task.CompletedTask);
        repo.When(r => r.Remove(Arg.Any<T>())).Do(c => backing.Remove(c.Arg<T>()));
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<T>>())).Do(c => { foreach (var e in c.Arg<IEnumerable<T>>().ToList()) backing.Remove(e); });
        _uow.Repository<T>().Returns(repo);
    }

    private void SetupEmpty(Type type)
    {
        typeof(ProductSpecServiceTests).GetMethod(nameof(SetupEmptyOf), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(type).Invoke(this, null);
    }

    private void SetupEmptyOf<T>() where T : class => Setup(new List<T>());

    private ProductSpecService Create()
    {
        // Quan hệ điều hướng của dòng định mức đã seed: EF tự nối khi đọc, ở đây nối tay.
        foreach (var n in _norms)
        {
            n.ProductVariant ??= _variants.FirstOrDefault(v => v.ProductVariantId == n.ProductVariantId)!;
            n.ProductComponent ??= _components.FirstOrDefault(c => c.ProductComponentId == n.ProductComponentId)!;
            n.Material ??= _materials.FirstOrDefault(m => m.MaterialId == n.MaterialId)!;
        }
        return new ProductSpecService(_uow, _audit);
    }

    private static SaveProductComponentDto Panel(string name, short pieces = 1, int? id = null) =>
        new(id, name, "Panel", pieces);

    private static SaveProductNormDto Norm(string component, string color, string size, decimal? length = null, int material = 1) =>
        new(component, color, size, material, length, null, null);

    private static SaveProductVariantDto Variant(int? id, string color, string size, decimal? length = null, decimal? width = null) =>
        new(id, color, size, length, width);

    private static SaveProductSpecDto Spec(IReadOnlyList<SaveProductComponentDto> components,
        IReadOnlyList<SaveProductVariantDto> variants, IReadOnlyList<SaveProductNormDto> norms) => new(components, variants, norms);

    private ProductVariant AddVariant(int id, string color, string size, string status = RecordStatus.Active)
    {
        var v = new ProductVariant { ProductVariantId = id, ProductId = 1, Color = color, SizeCode = size, Status = status };
        _variants.Add(v);
        return v;
    }

    [Fact]
    public async Task Save_creates_components_and_the_norm_rows_of_every_existing_variant_and_stores_the_variant_size()
    {
        var m = AddVariant(21, "Đen", "M"); var l = AddVariant(22, "Đen", "L");
        var dto = Spec(
            [Panel("Thân"), Panel("Tay", 2)],
            [Variant(21, "Đen", "M", 68, 52), Variant(22, "Đen", "L")],
            [Norm("Thân", "Đen", "M", 68), Norm("Thân", "Đen", "L"), Norm("Tay", "Đen", "M"), Norm("Tay", "Đen", "L")]);

        var result = await Create().SaveAsync(1, dto, default);

        result.IsSuccess.Should().BeTrue();
        _components.Select(c => c.ComponentName).Should().BeEquivalentTo("Thân", "Tay");
        _components.Single(c => c.ComponentName == "Tay").PiecesPerGarment.Should().Be(2);
        _variants.Should().HaveCount(2);   // màu – size lấy từ đơn hàng: không tạo thêm
        (m.LengthCm, m.WidthCm).Should().Be((68m, 52m));
        (l.LengthCm, l.WidthCm).Should().Be((null, null));
        _norms.Should().HaveCount(4).And.OnlyContain(n => n.ProductId == 1);
        _norms.Single(n => n.ProductVariantId == 21 && n.ProductComponent.ComponentName == "Thân").LengthCm.Should().Be(68m);
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Product) && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Save_refuses_a_color_size_that_does_not_exist_yet_because_variants_come_from_orders()
    {
        AddVariant(21, "Đen", "M");

        var result = await Create().SaveAsync(1, Spec([Panel("Thân")], [Variant(null, "Xanh", "S")], []), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        result.Message.Should().Be(Messages.ProductSpecVariantFromOrders);
        _variants.Should().ContainSingle();
    }

    [Fact]
    public async Task Save_replaces_the_norms_of_kept_variants_and_deletes_unused_dropped_components()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân", ComponentType = "PANEL", Status = RecordStatus.Active };
        var gone = new ProductComponent { ProductComponentId = 12, ProductId = 1, ComponentName = "Cổ", ComponentType = "PANEL", Status = RecordStatus.Active };
        AddVariant(21, "Đen", "M");
        _components.AddRange([comp, gone]);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 1, ProductId = 1, ProductVariantId = 21, ProductComponentId = 11, MaterialId = 1, LengthCm = 9 });
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 2, ProductId = 1, ProductVariantId = 21, ProductComponentId = 12, MaterialId = 1, LengthCm = 3 });

        var result = await Create().SaveAsync(1, Spec([Panel("Thân", id: 11)], [Variant(21, "Đen", "M")], [Norm("Thân", "Đen", "M", 14)]), default);

        result.IsSuccess.Should().BeTrue();
        _components.Should().ContainSingle().Which.ComponentName.Should().Be("Thân");   // "Cổ" chưa dùng ở đâu nên bị xóa
        _norms.Should().ContainSingle().Which.LengthCm.Should().Be(14m);
    }

    [Fact]
    public async Task Save_discontinues_a_used_component_and_never_removes_a_variant_left_out_of_the_list()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        var variant = AddVariant(21, "Đỏ", "S");
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductVariantId = 21, ProductComponentId = 11, MaterialId = 1 });
        _workOrderLines.Add(new WorkOrderLine { ProductComponentMaterialId = 3 });   // lệnh cắt bù đang trỏ vào dòng định mức
        _orderLines.Add(new OrderLine { OrderLineId = 5, ProductVariantId = 21 });

        var result = await Create().SaveAsync(1, Spec([], [], []), default);

        result.IsSuccess.Should().BeTrue();
        comp.Status.Should().Be(RecordStatus.Discontinued);
        variant.Status.Should().Be(RecordStatus.Active);
        _variants.Should().Contain(variant);
    }

    [Fact]
    public async Task Save_updates_an_existing_norm_row_in_place_so_documents_pointing_at_it_stay_valid()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        AddVariant(21, "Đen", "M");
        _components.Add(comp);
        var norm = new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductVariantId = 21, ProductComponentId = 11, MaterialId = 1, LengthCm = 40 };
        _norms.Add(norm);
        _workOrderLines.Add(new WorkOrderLine { ProductComponentMaterialId = 3 });

        var result = await Create().SaveAsync(1, Spec([Panel("Tay", id: 11)], [Variant(21, "Đen", "M")], [Norm("Tay", "Đen", "M", 42.5m)]), default);

        result.IsSuccess.Should().BeTrue();
        _norms.Should().ContainSingle().Which.Should().BeSameAs(norm);
        norm.ProductComponentMaterialId.Should().Be(3);
        norm.LengthCm.Should().Be(42.5m);
    }

    [Fact]
    public async Task Save_refuses_to_delete_a_norm_row_that_is_already_used()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        AddVariant(21, "Đen", "M");
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductVariantId = 21, ProductComponentId = 11, MaterialId = 1 });
        _workOrderLines.Add(new WorkOrderLine { ProductComponentMaterialId = 3 });

        var result = await Create().SaveAsync(1, Spec([Panel("Tay", id: 11)], [Variant(21, "Đen", "M")], []), default);   // bỏ hết định mức

        result.ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        result.Message.Should().Be(Messages.ProductSpecNormInUse);
        _norms.Should().ContainSingle();
    }

    [Fact]
    public async Task Save_rejects_duplicate_names_unknown_references_duplicate_norm_rows_and_inactive_materials()
    {
        AddVariant(21, "Đen", "M");
        var variants = new[] { Variant(21, "Đen", "M") };

        (await Create().SaveAsync(1, Spec([Panel("A"), Panel("a")], variants, []), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], [Variant(21, "Đen", "M"), Variant(21, "đen", "m")], []), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("Không có", "Đen", "M")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M"), Norm("A", "Đen", "M")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M", material: 2)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M", material: 99)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        _norms.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_returns_components_variants_and_norms_with_pascal_case_codes_and_sizes()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân", ComponentType = "PANEL", PiecesPerGarment = 1, Status = RecordStatus.Active };
        var variant = AddVariant(21, "Đen", "M");
        variant.LengthCm = 68; variant.WidthCm = 52;
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 1, ProductId = 1, ProductVariantId = 21, ProductVariant = variant, ProductComponentId = 11,
            ProductComponent = comp, MaterialId = 1, Material = _materials[0], LengthCm = 66, WidthCm = 30,
        });

        var result = await Create().GetAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Components.Single().ComponentType.Should().Be("Panel");
        result.Data.Variants.Single().Should().Match<ProductVariantDto>(v => v.Status == "Active" && v.LengthCm == 68m && v.WidthCm == 52m);
        result.Data.Norms.Single().Should().Match<ProductNormDto>(n => n.ComponentName == "Thân" && n.Color == "Đen" && n.LengthCm == 66m
            && n.MaterialCode == "V1");
        (await Create().GetAsync(99, default)).Kind.Should().Be(NX_MTD.Application.Common.ResultKind.NotFound);
    }
}
