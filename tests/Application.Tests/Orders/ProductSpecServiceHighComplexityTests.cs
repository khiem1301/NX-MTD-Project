using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID292–UTCID314 — ProductSpecService high-complexity cases from UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md.</summary>
[Trait("Category", "HighComplexity")]
public class ProductSpecServiceHighComplexityTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "AO-01" }, new() { ProductId = 2, ProductCode = "AO-02" }];
    private readonly List<ProductComponent> _components = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<Material> _materials =
    [
        new() { MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Color = "", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
        new() { MaterialId = 2, MaterialCode = "K1", MaterialName = "Khóa", Unit = "cái", Status = MaterialStatuses.Active, MaterialType = MaterialTypes.Trim },
    ];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<ProductColor> _colors = [];
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private readonly List<OperationComponent> _operationLinks = [];
    private bool _hideProductAfterCommit;
    private bool _hideProductOnCommitAfterSave;
    private int _nextId = 100;

    public ProductSpecServiceHighComplexityTests()
    {
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(ci =>
        {
            if (_hideProductOnCommitAfterSave) _hideProductAfterCommit = true;
            return Task.CompletedTask;
        });
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _uow.SaveChangesAsync().Returns(1);

        Setup(_products, onQuery: () => _hideProductAfterCommit ? [] : _products);
        Setup(_materials);
        Setup(_orderLines);
        Setup(_workOrderLines);
        Setup(_markers);
        Setup(_colors);
        Setup(_operationLinks);
        Setup(_components, c => c.ProductComponentId = ++_nextId);
        Setup(_variants, v => v.ProductVariantId = ++_nextId);
        Setup(_norms, n =>
        {
            n.ProductColor = _colors.First(c => c.ProductColorId == n.ProductColorId);
            n.ProductComponent = _components.First(c => c.ProductComponentId == n.ProductComponentId);
            n.Material = _materials.First(m => m.MaterialId == n.MaterialId);
        });
        Setup(_markerFabrics, f =>
        {
            f.Material = _materials.First(m => m.MaterialId == f.MaterialId);
            f.ProductColor = _colors.First(c => c.ProductColorId == f.ProductColorId);
        });
        Setup(_markerComponents, m => m.ProductComponent = _components.First(c => c.ProductComponentId == m.ProductComponentId));
        foreach (var empty in new[] { typeof(RequestLine), typeof(CuttingLayLine), typeof(CutHandoverLine), typeof(MaterialIssueLine), typeof(StockBalance) })
            SetupEmpty(empty);
    }

    private void Setup<T>(List<T> backing, Action<T>? onAdd = null, Func<IEnumerable<T>>? onQuery = null) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => (onQuery?.Invoke() ?? backing).AsTestAsyncQueryable());
        repo.AddAsync(Arg.Do<T>(e => { onAdd?.Invoke(e); backing.Add(e); })).Returns(Task.CompletedTask);
        repo.AddRangeAsync(Arg.Do<IEnumerable<T>>(es => { foreach (var e in es) { onAdd?.Invoke(e); backing.Add(e); } })).Returns(Task.CompletedTask);
        repo.When(r => r.Remove(Arg.Any<T>())).Do(c => backing.Remove(c.Arg<T>()));
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<T>>())).Do(c => { foreach (var e in c.Arg<IEnumerable<T>>().ToList()) backing.Remove(e); });
        _uow.Repository<T>().Returns(repo);
    }

    private void SetupEmpty(Type type) =>
        typeof(ProductSpecServiceHighComplexityTests).GetMethod(nameof(SetupEmptyOf), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(type).Invoke(this, null);

    private void SetupEmptyOf<T>() where T : class => Setup(new List<T>());

    private ProductSpecService Create() => new(_uow, _audit);

    private static SaveProductComponentDto Panel(string name, short pieces = 1, int? id = null) =>
        new(id, name, "Panel", pieces);

    private static SaveProductNormDto Norm(string component, string color, string? note = null, int material = 1) =>
        new(component, color, material, note);

    private static SaveProductVariantDto Variant(int? id, string color, string size) => new(id, color, size);

    private static SaveProductSpecDto Spec(IReadOnlyList<SaveProductComponentDto> components,
        IReadOnlyList<SaveProductVariantDto> variants, IReadOnlyList<SaveProductNormDto> norms) =>
        new(components, variants, norms, [], []);

    private SaveProductSpecDto SpecWithMarker(string component = "Thân", string color = "Đen")
    {
        if (_variants.Count == 0) AddVariant(21, color, "M");
        return Spec([Panel(component)], [Variant(_variants[0].ProductVariantId, color, _variants[0].SizeCode)],
            [Norm(component, color)]) with
        {
            MarkerComponents = [new SaveProductMarkerComponentDto(1, component)],
            MarkerColors = [new SaveProductMarkerColorDto(1, color)],
        };
    }

    private void MakeFabric() => _materials[0].MaterialType = MaterialTypes.Fabric;

    private ProductVariant AddVariant(int id, string color, string size, string status = RecordStatus.Active)
    {
        var v = new ProductVariant { ProductVariantId = id, ProductId = 1, ProductColor = ColorOf(color), SizeCode = size, Status = status };
        v.ProductColorId = v.ProductColor.ProductColorId;
        _variants.Add(v);
        return v;
    }

    private ProductColor ColorOf(string color)
    {
        var found = _colors.FirstOrDefault(c => string.Equals(c.Color, color, StringComparison.OrdinalIgnoreCase));
        if (found is not null) return found;
        var created = new ProductColor { ProductColorId = 500 + _colors.Count, ProductId = 1, Color = color };
        _colors.Add(created);
        return created;
    }

    private int ColorIdOf(int variantId) => _variants.First(v => v.ProductVariantId == variantId).ProductColorId;

    private SaveProductSpecDto ValidSaveDto()
    {
        MakeFabric();
        AddVariant(21, "Đỏ", "M");
        AddVariant(22, "Xanh", "L");
        return Spec([Panel("Thân"), Panel("Tay", 2)], [Variant(21, "Đỏ", "M"), Variant(22, "Xanh", "L")],
            [Norm("Thân", "Đỏ"), Norm("Tay", "Đỏ"), Norm("Thân", "Xanh"), Norm("Tay", "Xanh")]) with
        {
            MarkerComponents = [new SaveProductMarkerComponentDto(1, "Thân"), new SaveProductMarkerComponentDto(1, "Tay")],
            MarkerColors = [new SaveProductMarkerColorDto(1, "Đỏ"), new SaveProductMarkerColorDto(1, "Xanh")],
        };
    }

    [Fact]
    public async Task UTCID292_GetAsync_returns_full_spec_for_product_with_components_variants_and_norms()
    {
        // Arrange
        var c1 = new ProductComponent { ProductComponentId = 1, ProductId = 1, ComponentName = "A", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active };
        var c2 = new ProductComponent { ProductComponentId = 2, ProductId = 1, ComponentName = "B", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active };
        var c3 = new ProductComponent { ProductComponentId = 3, ProductId = 1, ComponentName = "C", ComponentType = ComponentTypes.Trim, PiecesPerGarment = 1, Status = RecordStatus.Active };
        _components.AddRange([c1, c2, c3]);
        var vRed = AddVariant(21, "Đỏ", "M");
        var vBlue = AddVariant(22, "Xanh", "L");
        _norms.AddRange([
            new() { ProductComponentMaterialId = 1, ProductId = 1, ProductColorId = vRed.ProductColorId, ProductColor = vRed.ProductColor, ProductComponentId = 1, ProductComponent = c1, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Active },
            new() { ProductComponentMaterialId = 2, ProductId = 1, ProductColorId = vRed.ProductColorId, ProductColor = vRed.ProductColor, ProductComponentId = 2, ProductComponent = c2, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Active },
            new() { ProductComponentMaterialId = 3, ProductId = 1, ProductColorId = vBlue.ProductColorId, ProductColor = vBlue.ProductColor, ProductComponentId = 1, ProductComponent = c1, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Active },
            new() { ProductComponentMaterialId = 4, ProductId = 1, ProductColorId = vBlue.ProductColorId, ProductColor = vBlue.ProductColor, ProductComponentId = 2, ProductComponent = c2, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Active },
        ]);

        // Act
        var result = await Create().GetAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Components.Should().HaveCount(3);
        result.Data.Variants.Should().HaveCount(2);
        result.Data.Norms.Should().HaveCount(4);
    }

    [Fact]
    public async Task UTCID293_GetAsync_returns_empty_lists_when_product_has_no_spec_yet()
    {
        // Arrange — product 2 exists, no components

        // Act
        var result = await Create().GetAsync(2, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Components.Should().BeEmpty();
        result.Data.Variants.Should().BeEmpty();
        result.Data.Norms.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID294_GetAsync_hides_discontinued_norms_and_surfaces_discontinued_component_status()
    {
        // Arrange
        var active = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Active", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
        var discontinued = new ProductComponent { ProductComponentId = 12, ProductId = 1, ComponentName = "Old", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Discontinued };
        _components.AddRange([active, discontinued]);
        var v = AddVariant(21, "Đen", "M");
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 1, ProductId = 1, ProductColorId = v.ProductColorId, ProductColor = v.ProductColor,
            ProductComponentId = 11, ProductComponent = active, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Active,
        });
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 2, ProductId = 1, ProductColorId = v.ProductColorId, ProductColor = v.ProductColor,
            ProductComponentId = 12, ProductComponent = discontinued, MaterialId = 1, Material = _materials[0], Status = RecordStatus.Discontinued,
        });

        // Act
        var result = await Create().GetAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Norms.Should().ContainSingle();
        result.Data.Components.Should().Contain(c => c.ComponentName == "Old" && c.Status == "Discontinued");
    }

    [Fact]
    public async Task UTCID295_GetAsync_returns_not_found_when_product_missing()
    {
        // Arrange
        // Act
        var result = await Create().GetAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID296_SaveAsync_persists_valid_spec_commits_transaction_and_audits()
    {
        // Arrange
        var dto = ValidSaveDto();
        var sut = Create();

        // Act
        var result = await sut.SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).CommitTransactionAsync();
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Product) && e.Action == AuditActions.Update));
        result.Data!.Components.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID297_SaveAsync_updates_existing_component_by_id_instead_of_creating_duplicate()
    {
        // Arrange
        MakeFabric();
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân trước", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
        _components.Add(comp);
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker("Thân trước trái") with
        {
            Components = [new SaveProductComponentDto(11, "Thân trước trái", "Panel", 1)],
        };

        // Act
        var result = await Create().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _components.Should().ContainSingle().Which.ComponentName.Should().Be("Thân trước trái");
        _components.Single().ProductComponentId.Should().Be(11);
    }

    [Fact]
    public async Task UTCID298_SaveAsync_matches_component_by_name_case_insensitive_without_new_row()
    {
        // Arrange
        MakeFabric();
        _components.Add(new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân Trước", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active });
        AddVariant(21, "Đen", "M");
        var dto = Spec([Panel("thân trước")], [Variant(21, "Đen", "M")], [Norm("thân trước", "Đen")]) with
        {
            MarkerComponents = [new SaveProductMarkerComponentDto(1, "thân trước")],
            MarkerColors = [new SaveProductMarkerColorDto(1, "Đen")],
        };

        // Act
        await Create().SaveAsync(1, dto, default);

        // Assert
        _components.Should().ContainSingle().Which.ProductComponentId.Should().Be(11);
    }

    [Fact]
    public async Task UTCID299_SaveAsync_discontinues_component_dropped_from_dto_when_used_in_operation_link()
    {
        // Arrange
        MakeFabric();
        var comp = new ProductComponent { ProductComponentId = 12, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
        _components.Add(comp);
        _operationLinks.Add(new OperationComponent { OperationId = 5, ProductId = 1, ProductComponentId = 12 });
        AddVariant(21, "Đen", "M");

        // Act
        var result = await Create().SaveAsync(1, Spec([], [Variant(21, "Đen", "M")], []), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        comp.Status.Should().Be(RecordStatus.Discontinued);
        _components.Should().Contain(comp);
    }

    [Fact]
    public async Task UTCID300_SaveAsync_accepts_pieces_per_garment_equal_one()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker() with { Components = [Panel("Thân", pieces: 1)] };

        // Act
        var result = await Create().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _components.Single().PiecesPerGarment.Should().Be(1);
    }

    [Fact]
    public async Task UTCID301_SaveAsync_rejects_pieces_per_garment_zero()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker() with { Components = [Panel("Thân", pieces: 0)] };

        // Act
        var result = await Create().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID302_SaveAsync_removes_unused_components_when_components_list_empty()
    {
        // Arrange
        MakeFabric();
        var unused = new ProductComponent { ProductComponentId = 20, ProductId = 1, ComponentName = "Cổ", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
        _components.Add(unused);
        AddVariant(21, "Đen", "M");

        // Act
        var result = await Create().SaveAsync(1, Spec([], [Variant(21, "Đen", "M")], []), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _components.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID303_SaveAsync_rejects_variant_color_not_from_orders()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đỏ", "M");
        AddVariant(22, "Xanh", "M");

        // Act
        var result = await Create().SaveAsync(1, Spec([Panel("Thân")],
            [Variant(21, "Đỏ", "M"), Variant(22, "Xanh", "M"), Variant(null, "Vàng", "M")],
            [Norm("Thân", "Đỏ"), Norm("Thân", "Xanh")]) with
        {
            MarkerComponents = [new SaveProductMarkerComponentDto(1, "Thân")],
            MarkerColors = [new SaveProductMarkerColorDto(1, "Đỏ"), new SaveProductMarkerColorDto(1, "Xanh")],
        }, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.ProductSpecVariantFromOrders);
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID304_SaveAsync_does_not_open_transaction_when_check_fails()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = Spec([Panel("Tay"), Panel("tay ")], [Variant(21, "Đen", "M")], []);

        // Act
        var result = await Create().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().BeginTransactionAsync();
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID305_SaveAsync_returns_failure_when_get_after_save_fails()
    {
        // Arrange
        _hideProductOnCommitAfterSave = true;
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker();

        // Act
        var result = await Create().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID306_SaveAsync_returns_not_found_for_missing_product()
    {
        // Arrange
        MakeFabric();

        // Act
        var result = await Create().SaveAsync(999, SpecWithMarker(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID307_CheckAsync_succeeds_for_valid_dto_without_save_changes()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker();

        // Act
        var result = await Create().CheckAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID308_CheckAsync_rejects_duplicate_component_names()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");

        // Act
        var result = await Create().CheckAsync(1, Spec([Panel("Tay"), Panel("tay ")], [Variant(21, "Đen", "M")], []), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.ProductSpecComponentDuplicate);
    }

    [Fact]
    public async Task UTCID309_CheckAsync_returns_not_found_for_missing_product()
    {
        // Act
        var result = await Create().CheckAsync(999, Spec([], [], []), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID310_CheckAsync_throws_or_fails_when_dto_is_null()
    {
        // Act
        var act = () => Create().CheckAsync(1, null!, default);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UTCID311_WriteAsync_writes_spec_when_check_passed_and_caller_commits()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = SpecWithMarker();
        var sut = Create();
        (await sut.CheckAsync(1, dto, default)).IsSuccess.Should().BeTrue();
        await _uow.BeginTransactionAsync();

        // Act
        var result = await sut.WriteAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _components.Should().NotBeEmpty();
        await _uow.CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID312_WriteAsync_succeeds_with_empty_norm_list()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = Spec([Panel("Thân")], [Variant(21, "Đen", "M")], []);

        // Act
        var result = await Create().WriteAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _norms.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID313_WriteAsync_fails_safely_when_norm_references_missing_component_without_prior_check()
    {
        // Arrange
        MakeFabric();
        AddVariant(21, "Đen", "M");
        var dto = Spec([Panel("Thân")], [Variant(21, "Đen", "M")], [Norm("Không tồn tại", "Đen")]);

        // Act
        var act = () => Create().WriteAsync(1, dto, default);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UTCID314_WriteAsync_fails_safely_when_trim_operation_link_references_renamed_component()
    {
        // Arrange
        MakeFabric();
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Nhãn", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
        _components.Add(comp);
        _operationLinks.Add(new OperationComponent { OperationId = 1, ProductId = 1, ProductComponentId = 11 });
        AddVariant(21, "Đen", "M");

        // Act
        var act = () => Create().WriteAsync(1, Spec(
            [new SaveProductComponentDto(11, "Nhãn mới", "Trim", 1)],
            [Variant(21, "Đen", "M")], []), default);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }
}