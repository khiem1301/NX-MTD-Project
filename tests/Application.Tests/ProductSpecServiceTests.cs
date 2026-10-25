using FluentAssertions;
using NSubstitute;
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
                     typeof(StocktakeLine), typeof(StockTransaction) })
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

    private ProductSpecService Create() => new(_uow, _audit);

    private static SaveProductComponentDto Panel(string name, short pieces = 1, short sort = 1, int? id = null) =>
        new(id, name, "Panel", pieces, sort);

    private static SaveProductNormDto Norm(string component, string color, string size, decimal qty = 1m, int material = 1) =>
        new(component, color, size, material, "Đen", qty, 5m, null, null, null);

    private static SaveProductSpecDto Spec(IReadOnlyList<SaveProductComponentDto> components,
        IReadOnlyList<SaveProductVariantDto> variants, IReadOnlyList<SaveProductNormDto> norms) => new(components, variants, norms);

    [Fact]
    public async Task Save_creates_components_variants_and_the_norm_rows_of_every_variant()
    {
        var dto = Spec(
            [Panel("Thân"), Panel("Tay", 2, 2)],
            [new(null, "Đen", "m"), new(null, "Đen", "L")],
            [Norm("Thân", "Đen", "M"), Norm("Thân", "Đen", "L"), Norm("Tay", "Đen", "M", 0.5m), Norm("Tay", "Đen", "L", 0.5m)]);

        var result = await Create().SaveAsync(1, dto, default);

        result.IsSuccess.Should().BeTrue();
        _components.Select(c => c.ComponentName).Should().BeEquivalentTo("Thân", "Tay");
        _components.Single(c => c.ComponentName == "Tay").PiecesPerGarment.Should().Be(2);
        _variants.Select(v => v.SizeCode).Should().BeEquivalentTo("M", "L");   // size luôn viết hoa
        _norms.Should().HaveCount(4).And.OnlyContain(n => n.ProductId == 1);
        _norms.Select(n => n.ProductVariantId).Distinct().Should().BeEquivalentTo(_variants.Select(v => v.ProductVariantId));
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Product) && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Save_replaces_the_norms_of_kept_variants_and_deletes_unused_dropped_items()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân", ComponentType = "PANEL", Status = RecordStatus.Active };
        var gone = new ProductComponent { ProductComponentId = 12, ProductId = 1, ComponentName = "Cổ", ComponentType = "PANEL", Status = RecordStatus.Active };
        var variant = new ProductVariant { ProductVariantId = 21, ProductId = 1, Color = "Đen", SizeCode = "M", Status = RecordStatus.Active };
        _components.AddRange([comp, gone]); _variants.Add(variant);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 1, ProductId = 1, ProductVariantId = 21, ProductComponentId = 11, MaterialId = 1, QtyPerGarment = 9 });
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 2, ProductId = 1, ProductVariantId = 21, ProductComponentId = 12, MaterialId = 1, QtyPerGarment = 3 });

        var result = await Create().SaveAsync(1, Spec([Panel("Thân", id: 11)], [new(21, "Đen", "M")], [Norm("Thân", "Đen", "M", 1.4m)]), default);

        result.IsSuccess.Should().BeTrue();
        _components.Should().ContainSingle().Which.ComponentName.Should().Be("Thân");   // "Cổ" chưa dùng ở đâu nên bị xóa
        _norms.Should().ContainSingle().Which.QtyPerGarment.Should().Be(1.4m);
    }

    [Fact]
    public async Task Save_discontinues_instead_of_deleting_a_component_or_variant_that_is_already_used()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        var variant = new ProductVariant { ProductVariantId = 21, ProductId = 1, Color = "Đỏ", SizeCode = "S", Status = RecordStatus.Active };
        _components.Add(comp); _variants.Add(variant);
        _workOrderLines.Add(new WorkOrderLine { ProductComponentId = 11 });
        _orderLines.Add(new OrderLine { OrderLineId = 5, ProductVariantId = 21 });

        var result = await Create().SaveAsync(1, Spec([], [], []), default);

        result.IsSuccess.Should().BeTrue();
        comp.Status.Should().Be(RecordStatus.Discontinued);
        variant.Status.Should().Be(RecordStatus.Discontinued);
        _components.Should().Contain(comp);
        _variants.Should().Contain(variant);
    }

    [Fact]
    public async Task Save_rejects_duplicate_names_unknown_references_duplicate_norm_rows_and_inactive_materials()
    {
        var variants = new SaveProductVariantDto[] { new(null, "Đen", "M") };

        (await Create().SaveAsync(1, Spec([Panel("A"), Panel("a")], variants, []), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], [new(null, "Đen", "M"), new(null, "đen", "m")], []), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("Không có", "Đen", "M")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M"), Norm("A", "Đen", "M")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M", material: 2)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", "M", material: 99)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        _norms.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_returns_components_variants_and_norms_with_pascal_case_codes()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân", ComponentType = "PANEL", PiecesPerGarment = 1, Status = RecordStatus.Active };
        var variant = new ProductVariant { ProductVariantId = 21, ProductId = 1, Color = "Đen", SizeCode = "M", Status = RecordStatus.Active };
        _components.Add(comp); _variants.Add(variant);
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 1, ProductId = 1, ProductVariantId = 21, ProductVariant = variant, ProductComponentId = 11,
            ProductComponent = comp, MaterialId = 1, Material = _materials[0], MaterialColor = "Đen", QtyPerGarment = 1.2m, WastagePercent = 5,
        });

        var result = await Create().GetAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Components.Single().ComponentType.Should().Be("Panel");
        result.Data.Variants.Single().Status.Should().Be("Active");
        result.Data.Norms.Single().Should().Match<ProductNormDto>(n => n.ComponentName == "Thân" && n.Color == "Đen" && n.QtyPerGarment == 1.2m
            && n.MaterialCode == "V1");
        (await Create().GetAsync(99, default)).Kind.Should().Be(NX_MTD.Application.Common.ResultKind.NotFound);
    }
}
