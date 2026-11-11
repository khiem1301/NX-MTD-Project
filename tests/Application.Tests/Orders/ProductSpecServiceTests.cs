using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
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
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<ProductColor> _colors = [];
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private readonly List<OperationComponent> _operationLinks = [];
    private int _nextId = 100;

    public ProductSpecServiceTests()
    {
        Setup(_products); Setup(_materials); Setup(_orderLines); Setup(_workOrderLines); Setup(_markers);
        Setup(_colors); Setup(_operationLinks);
        Setup(_markerFabrics, f =>
        {
            f.Material = _materials.First(m => m.MaterialId == f.MaterialId);
            f.ProductColor = _colors.First(c => c.ProductColorId == f.ProductColorId);
        });
        Setup(_components, c => c.ProductComponentId = ++_nextId);
        Setup(_markerComponents, m => m.ProductComponent = _components.First(c => c.ProductComponentId == m.ProductComponentId));
        Setup(_variants, v => v.ProductVariantId = ++_nextId);
        // Quan hệ điều hướng do EF tự nối khi đọc lại; ở đây nối tay cho dòng định mức vừa thêm.
        Setup(_norms, n =>
        {
            n.ProductColor = _colors.First(c => c.ProductColorId == n.ProductColorId);
            n.ProductComponent = _components.First(c => c.ProductComponentId == n.ProductComponentId);
            n.Material = _materials.First(m => m.MaterialId == n.MaterialId);
        });
        foreach (var empty in new[] { typeof(RequestLine), typeof(CuttingLayLine), typeof(CutHandoverLine), typeof(MaterialIssueLine),
                     typeof(StockBalance) })
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
            n.ProductColor ??= _colors.FirstOrDefault(c => c.ProductColorId == n.ProductColorId)!;
            n.ProductComponent ??= _components.FirstOrDefault(c => c.ProductComponentId == n.ProductComponentId)!;
            n.Material ??= _materials.FirstOrDefault(m => m.MaterialId == n.MaterialId)!;
        }
        return new ProductSpecService(_uow, _audit);
    }

    private static SaveProductComponentDto Panel(string name, short pieces = 1, int? id = null) =>
        new(id, name, "Panel", pieces);

    private static SaveProductNormDto Norm(string component, string color, string? note = null, int material = 1) =>
        new(component, color, material, note);

    /// <summary>Màu áo của biến thể đã thêm (định mức theo màu, DEV-ALL-87).</summary>
    private int ColorIdOf(int variantId) => _variants.First(v => v.ProductVariantId == variantId).ProductColorId;

    private static SaveProductVariantDto Variant(int? id, string color, string size) => new(id, color, size);

    private static SaveProductSpecDto Spec(IReadOnlyList<SaveProductComponentDto> components,
        IReadOnlyList<SaveProductVariantDto> variants, IReadOnlyList<SaveProductNormDto> norms) => new(components, variants, norms, [], []);

    /// <summary>Áo một thành phần "Thân" màu Đen size M dùng vải 1, kèm nhóm bìa 1 có chi tiết Thân, màu Đen dùng nhóm.</summary>
    private SaveProductSpecDto SpecWithMarker(string component = "Thân", string color = "Đen", int colorMarkerNo = 1)
    {
        if (_variants.Count == 0) AddVariant(21, "Đen", "M");
        return Spec([Panel("Thân")], [Variant(21, "Đen", "M")], [Norm("Thân", "Đen")]) with
        {
            MarkerComponents = [new SaveProductMarkerComponentDto(1, component)],
            MarkerColors = [new SaveProductMarkerColorDto(colorMarkerNo, color)],
        };
    }

    private static SaveProductMarkerComponentDto OnGroup(int markerNo, string component) => new(markerNo, component);

    private static SaveProductMarkerColorDto GroupColor(int markerNo, string color) => new(markerNo, color);

    private void MakeFabric() { _materials[0].MaterialType = MaterialTypes.Fabric; _materials[0].Color = ""; }

    private ProductVariant AddVariant(int id, string color, string size, string status = RecordStatus.Active)
    {
        var v = new ProductVariant { ProductVariantId = id, ProductId = 1, ProductColor = ColorOf(color), SizeCode = size, Status = status };
        v.ProductColorId = v.ProductColor.ProductColorId;
        _variants.Add(v);
        return v;
    }

    /// <summary>Màu áo của mã hàng 1: cùng tên (không phân biệt hoa thường) dùng chung một dòng như database.</summary>
    private ProductColor ColorOf(string color)
    {
        var found = _colors.FirstOrDefault(c => string.Equals(c.Color, color, StringComparison.OrdinalIgnoreCase));
        if (found is not null) return found;
        var created = new ProductColor { ProductColorId = 500 + _colors.Count, ProductId = 1, Color = color };
        _colors.Add(created);
        return created;
    }

    [Fact]
    public async Task Save_creates_components_and_one_norm_row_per_color_shared_by_every_size()
    {
        AddVariant(21, "Đen", "M"); AddVariant(22, "Đen", "L");
        var dto = Spec(
            [Panel("Thân"), Panel("Tay", 2)],
            [Variant(21, "Đen", "M"), Variant(22, "Đen", "L")],
            [Norm("Thân", "Đen", "May viền"), Norm("Tay", "Đen")]);

        var result = await Create().SaveAsync(1, dto, default);

        result.IsSuccess.Should().BeTrue();
        _components.Select(c => c.ComponentName).Should().BeEquivalentTo("Thân", "Tay");
        _components.Single(c => c.ComponentName == "Tay").PiecesPerGarment.Should().Be(2);
        _variants.Should().HaveCount(2);   // màu – size lấy từ đơn hàng: không tạo thêm
        _norms.Should().HaveCount(2).And.OnlyContain(n => n.ProductId == 1 && n.ProductColorId == ColorIdOf(21));   // M và L cùng màu Đen
        _norms.Single(n => n.ProductComponent.ComponentName == "Thân").SpecNote.Should().Be("May viền");
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
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 1, ProductId = 1, ProductColorId = ColorIdOf(21), ProductComponentId = 11, MaterialId = 1, SpecNote = "Cũ" });
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 2, ProductId = 1, ProductColorId = ColorIdOf(21), ProductComponentId = 12, MaterialId = 1, SpecNote = "Cũ" });

        var result = await Create().SaveAsync(1, Spec([Panel("Thân", id: 11)], [Variant(21, "Đen", "M")], [Norm("Thân", "Đen", "Mới")]), default);

        result.IsSuccess.Should().BeTrue();
        _components.Should().ContainSingle().Which.ComponentName.Should().Be("Thân");   // "Cổ" chưa dùng ở đâu nên bị xóa
        _norms.Should().ContainSingle().Which.SpecNote.Should().Be("Mới");
    }

    [Fact]
    public async Task Save_discontinues_a_used_component_and_never_removes_a_variant_left_out_of_the_list()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        var variant = AddVariant(21, "Đỏ", "S");
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductColorId = ColorIdOf(21), ProductComponentId = 11, MaterialId = 1 });
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
        var norm = new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductColorId = ColorIdOf(21), ProductComponentId = 11, MaterialId = 1, SpecNote = "Cũ" };
        _norms.Add(norm);
        _workOrderLines.Add(new WorkOrderLine { ProductComponentMaterialId = 3 });

        var result = await Create().SaveAsync(1, Spec([Panel("Tay", id: 11)], [Variant(21, "Đen", "M")], [Norm("Tay", "Đen", "Mới")]), default);

        result.IsSuccess.Should().BeTrue();
        _norms.Should().ContainSingle().Which.Should().BeSameAs(norm);
        norm.ProductComponentMaterialId.Should().Be(3);
        norm.SpecNote.Should().Be("Mới");
    }

    [Fact]
    public async Task Save_refuses_to_delete_a_norm_row_that_is_already_used()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Tay", ComponentType = "PANEL", Status = RecordStatus.Active };
        AddVariant(21, "Đen", "M");
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial { ProductComponentMaterialId = 3, ProductId = 1, ProductColorId = ColorIdOf(21), ProductComponentId = 11, MaterialId = 1 });
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
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("Không có", "Đen")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen"), Norm("A", "Đen")]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", material: 2)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        (await Create().SaveAsync(1, Spec([Panel("A")], variants, [Norm("A", "Đen", material: 99)]), default)).ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        _norms.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_stores_the_marker_groups_with_the_fabric_taken_from_the_norm_and_get_returns_them()
    {
        MakeFabric();

        var result = await Create().SaveAsync(1, SpecWithMarker(), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.MarkerComponents.Should().ContainSingle().Which.Should().Be(new ProductMarkerComponentDto(1, "Thân"));
        result.Data.MarkerFabrics.Should().ContainSingle().Which.Should().Be(new ProductMarkerFabricDto(1, "Đen", 1, "V1", "Vải", ""));
    }

    [Fact]
    public async Task Save_replaces_the_marker_groups_of_the_product_and_leaves_other_products_alone()
    {
        MakeFabric();
        _markerFabrics.Add(new ProductMarkerFabric { ProductId = 1, MarkerNo = 2, MaterialId = 1 });
        _markerFabrics.Add(new ProductMarkerFabric { ProductId = 9, MarkerNo = 1, MaterialId = 1 });

        await Create().SaveAsync(1, SpecWithMarker(), default);

        _markerFabrics.Should().HaveCount(2);
        _markerFabrics.Single(m => m.ProductId == 1).MarkerNo.Should().Be(1);
        _markerFabrics.Should().Contain(m => m.ProductId == 9);
    }

    [Fact]
    public async Task Save_rejects_marker_groups_that_do_not_match_the_product()
    {
        MakeFabric();
        var sut = Create();

        (await sut.SaveAsync(1, SpecWithMarker(component: "Tay"), default)).Message.Should().Be(Messages.ProductSpecMarkerReference);
        (await sut.SaveAsync(1, SpecWithMarker(color: "Xanh"), default)).Message.Should().Be(Messages.ProductSpecMarkerReference);
        (await sut.SaveAsync(1, SpecWithMarker(colorMarkerNo: 2), default)).Message.Should().Be(Messages.ProductSpecMarkerIncomplete);

        var spec = SpecWithMarker();
        (await sut.SaveAsync(1, spec with { MarkerComponents = [spec.MarkerComponents[0], spec.MarkerComponents[0]] }, default)).Message
            .Should().Be(Messages.ProductSpecMarkerDuplicate);
        (await sut.SaveAsync(1, spec with { MarkerColors = [spec.MarkerColors[0], spec.MarkerColors[0]] }, default)).Message
            .Should().Be(Messages.ProductSpecMarkerDuplicate);
    }

    [Fact]
    public async Task Save_rejects_a_cut_panel_left_off_every_marker_group_of_a_color()
    {
        MakeFabric();
        var spec = SpecWithMarker();

        var result = await Create().SaveAsync(1, spec with
        {
            Components = [Panel("Thân"), Panel("Tay")],
            Norms = [Norm("Thân", "Đen"), Norm("Tay", "Đen")],
        }, default);

        result.Message.Should().Be(string.Format(Messages.ProductSpecPanelWithoutMarker, "Đen", "Tay"));
    }

    [Fact]
    public async Task Save_rejects_dropping_a_marker_group_that_orders_already_use()
    {
        MakeFabric();
        _markers.Add(new ProductMarker { ProductId = 1, OrderId = 5, MarkerNo = 2, MarkerCode = "2A", LengthM = 1, Status = RecordStatus.Active });

        (await Create().SaveAsync(1, SpecWithMarker(), default)).Message.Should().Be(Messages.ProductSpecMarkerGroupInUse);
    }

    [Fact]
    public async Task Save_rejects_a_marker_group_whose_panels_have_no_fabric_in_a_color_that_uses_it()
    {
        _materials[0].MaterialType = MaterialTypes.Trim;

        (await Create().SaveAsync(1, SpecWithMarker(), default)).Message
            .Should().Be(string.Format(Messages.ProductSpecMarkerNoFabric, 1, "Đen"));
    }

    // ========== DEV-ALL-89: màu dùng nhóm bìa, vải của nhóm lấy từ định mức, bìa của đơn chưa xong khóa nhóm ==========

    /// <summary>Áo Thân trước + Thân sau, màu Xanh hai chi tiết cùng vải 1, màu Đen thân sau vải 3.</summary>
    private SaveProductSpecDto TwoColorSpec(params SaveProductMarkerComponentDto[] groups) =>
        Spec([Panel("Thân trước"), Panel("Thân sau")], [Variant(21, "Xanh", "M"), Variant(22, "Đen", "M")],
            [Norm("Thân trước", "Xanh"), Norm("Thân sau", "Xanh"), Norm("Thân trước", "Đen"), Norm("Thân sau", "Đen", material: 3)]) with
        {
            MarkerComponents = groups,
        };

    private void TwoColorSetup()
    {
        MakeFabric();
        AddActiveMaterial(3, MaterialTypes.Fabric);
        AddVariant(21, "Xanh", "M");
        AddVariant(22, "Đen", "M");
    }

    [Fact]
    public async Task Save_rejects_a_marker_group_whose_panels_use_different_fabrics_in_its_color()
    {
        TwoColorSetup();

        var result = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Đen")],
        }, default);

        result.Message.Should().Be(string.Format(Messages.ProductSpecMarkerManyFabrics, 1, "Đen"));
    }

    [Fact]
    public async Task Save_rejects_a_color_that_shares_only_some_groups_of_a_layout()
    {
        // Đen không dùng được nhóm thân chung (thân sau khác vải) nên có nhóm thân riêng: khi đó Đen là dạng bìa riêng, không chung cả nhóm tay.
        TwoColorSetup();
        var spec = TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau"), OnGroup(2, "Thân trước"), OnGroup(3, "Thân sau"),
            OnGroup(4, "Tay")) with
        {
            Components = [Panel("Thân trước"), Panel("Thân sau"), Panel("Tay")],
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Đen"), GroupColor(3, "Đen"), GroupColor(4, "Xanh"), GroupColor(4, "Đen")],
        };

        var result = await Create().SaveAsync(1, spec with { Norms = [.. spec.Norms, Norm("Tay", "Xanh"), Norm("Tay", "Đen")] }, default);

        result.Message.Should().Be(string.Format(Messages.ProductSpecLayoutGroups, "Đen", "Xanh", 4));
    }

    [Fact]
    public async Task Save_accepts_two_colors_in_one_layout_when_every_group_has_one_fabric_in_each_color_even_if_they_split_fabrics_differently()
    {
        TwoColorSetup();
        var spec = TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(2, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(1, "Đen"), GroupColor(2, "Xanh"), GroupColor(2, "Đen")],
        };

        var result = await Create().SaveAsync(1, spec, default);

        result.IsSuccess.Should().BeTrue();   // Xanh: hai thân cùng vải; Đen: thân sau khác vải, nhưng mỗi nhóm vẫn một vải ở mỗi màu
    }

    [Fact]
    public async Task Save_lets_each_layout_split_its_panels_into_its_own_marker_groups_and_writes_the_fabric_of_each()
    {
        TwoColorSetup();

        var result = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau"),
            OnGroup(2, "Thân trước"), OnGroup(3, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Đen"), GroupColor(3, "Đen")],
        }, default);

        result.IsSuccess.Should().BeTrue();
        _markerFabrics.Select(f => (f.MarkerNo, f.ProductColorId, f.MaterialId)).Should().BeEquivalentTo(new[]
        {
            ((byte)1, ColorOf("Xanh").ProductColorId, 1), ((byte)2, ColorOf("Đen").ProductColorId, 1), ((byte)3, ColorOf("Đen").ProductColorId, 3),
        });
    }

    [Fact]
    public async Task Save_accepts_colors_with_the_same_fabric_split_in_one_layout_even_with_different_fabrics()
    {
        TwoColorSetup();
        var spec = TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(1, "Đen")],
            Norms = [Norm("Thân trước", "Xanh"), Norm("Thân sau", "Xanh"), Norm("Thân trước", "Đen", material: 3), Norm("Thân sau", "Đen", material: 3)],
        };

        (await Create().SaveAsync(1, spec, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_rejects_a_panel_on_two_marker_groups_of_the_same_color()
    {
        TwoColorSetup();

        var result = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau"), OnGroup(2, "Thân trước")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Xanh")],
        }, default);

        result.Message.Should().Be(string.Format(Messages.ProductSpecPanelManyMarkers, "Xanh", "Thân trước"));
    }

    /// <summary>Đơn DH9 có bìa 1A ở nhóm 1; nhóm 1 đang gồm thân trước + thân sau.</summary>
    private void OrderMarkerOnGroupOne(string status = OrderStatuses.InProduction)
    {
        var order = new CustomerOrder { OrderId = 9, OrderCode = "DH9", Status = status };
        _markers.Add(new ProductMarker { ProductId = 1, OrderId = 9, Order = order, MarkerNo = 1, MarkerCode = "1A", LengthM = 2, Status = RecordStatus.Active });
        foreach (var name in new[] { "Thân trước", "Thân sau" })
        {
            var component = new ProductComponent { ProductComponentId = ++_nextId, ProductId = 1, ComponentName = name, ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active };
            _components.Add(component);
            _markerComponents.Add(new ProductMarkerComponent { ProductId = 1, MarkerNo = 1, ProductComponentId = component.ProductComponentId, ProductComponent = component });
        }
    }

    [Fact]
    public async Task Save_rejects_changing_the_panels_of_a_group_with_a_marker_of_an_open_order_but_lets_its_colors_change()
    {
        TwoColorSetup();
        OrderMarkerOnGroupOne();

        var fewerPanels = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(2, "Thân sau"),
            OnGroup(3, "Thân trước"), OnGroup(4, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Xanh"), GroupColor(3, "Đen"), GroupColor(4, "Đen")],
        }, default);
        var otherColor = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(1, "Thân sau"),
            OnGroup(2, "Thân trước"), OnGroup(3, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Đen"), GroupColor(3, "Đen")],
        }, default);

        fewerPanels.Message.Should().Be(string.Format(Messages.ProductSpecMarkerGroupLocked, 1, "DH9"));
        otherColor.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_lets_a_group_change_its_panels_when_its_markers_belong_to_closed_orders()
    {
        TwoColorSetup();
        OrderMarkerOnGroupOne(OrderStatuses.Completed);

        var split = await Create().SaveAsync(1, TwoColorSpec(OnGroup(1, "Thân trước"), OnGroup(2, "Thân sau")) with
        {
            MarkerColors = [GroupColor(1, "Xanh"), GroupColor(2, "Xanh")],
        }, default);

        split.IsSuccess.Should().BeTrue();
    }

    // ========== DEV-ALL-86: mỗi chi tiết cắt một vải ở một màu – size, công đoạn chỉ gắn chi tiết vải ==========

    private void AddActiveMaterial(int id, string type) => _materials.Add(new Material
    {
        MaterialId = id, MaterialCode = $"M{id}", MaterialName = "NVL", Unit = "m", Color = "", MaterialType = type, Status = MaterialStatuses.Active,
    });

    [Fact]
    public async Task Save_rejects_a_cut_panel_with_two_fabrics_in_the_same_color()
    {
        MakeFabric();
        AddActiveMaterial(3, MaterialTypes.Fabric);
        AddVariant(21, "Đen", "M");

        var result = await Create().SaveAsync(1, Spec([Panel("Tay áo")], [Variant(21, "Đen", "M")],
            [Norm("Tay áo", "Đen"), Norm("Tay áo", "Đen", material: 3)]), default);

        result.Message.Should().Be(string.Format(Messages.ProductSpecPanelManyFabrics, "Tay áo", "Đen"));
    }

    [Fact]
    public async Task Save_accepts_a_lining_declared_as_its_own_panel_and_trims_on_a_panel()
    {
        MakeFabric();
        AddActiveMaterial(3, MaterialTypes.Fabric);
        AddActiveMaterial(4, MaterialTypes.Trim);
        AddVariant(21, "Đen", "M");

        var result = await Create().SaveAsync(1, Spec([Panel("Tay áo"), Panel("Lót tay áo")], [Variant(21, "Đen", "M")],
            [Norm("Tay áo", "Đen"), Norm("Tay áo", "Đen", material: 4), Norm("Lót tay áo", "Đen", material: 3)]), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_rejects_main_fabric_and_interlining_on_one_cut_panel_but_accepts_the_interlining_as_its_own_panel()
    {
        MakeFabric();
        AddActiveMaterial(3, MaterialTypes.Fabric);   // mex (INTERLINING) cũng là NVL loại vải
        AddVariant(21, "Đen", "M");
        var variants = new[] { Variant(21, "Đen", "M") };

        var together = await Create().SaveAsync(1, Spec([Panel("Cổ áo")], variants,
            [Norm("Cổ áo", "Đen"), Norm("Cổ áo", "Đen", material: 3)]), default);
        var apart = await Create().SaveAsync(1, Spec([Panel("Cổ áo"), Panel("Mex cổ")], variants,
            [Norm("Cổ áo", "Đen"), Norm("Mex cổ", "Đen", material: 3)]), default);

        together.Message.Should().Be(string.Format(Messages.ProductSpecPanelManyFabrics, "Cổ áo", "Đen"));
        apart.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_unlinks_operations_from_a_component_that_becomes_a_trim()
    {
        _components.Add(new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Nhãn", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active });
        _components.Add(new ProductComponent { ProductComponentId = 12, ProductId = 1, ComponentName = "Thân", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active });
        _operationLinks.AddRange([new OperationComponent { OperationId = 5, ProductComponentId = 11, ProductId = 1 },
            new OperationComponent { OperationId = 5, ProductComponentId = 12, ProductId = 1 }]);
        AddVariant(21, "Đen", "M");

        var result = await Create().SaveAsync(1, Spec([new SaveProductComponentDto(11, "Nhãn", "Trim", 1), Panel("Thân", id: 12)],
            [Variant(21, "Đen", "M")], []), default);

        result.IsSuccess.Should().BeTrue();
        _operationLinks.Should().ContainSingle().Which.ProductComponentId.Should().Be(12);
    }

    [Fact]
    public async Task Get_returns_components_variants_and_norms_with_pascal_case_codes_and_sizes()
    {
        var comp = new ProductComponent { ProductComponentId = 11, ProductId = 1, ComponentName = "Thân", ComponentType = "PANEL", PiecesPerGarment = 1, Status = RecordStatus.Active };
        var variant = AddVariant(21, "Đen", "M");
        _components.Add(comp);
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 1, ProductId = 1, ProductColorId = variant.ProductColorId, ProductColor = variant.ProductColor, ProductComponentId = 11,
            ProductComponent = comp, MaterialId = 1, Material = _materials[0], SpecNote = "May viền",
        });

        var result = await Create().GetAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Components.Single().ComponentType.Should().Be("Panel");
        result.Data.Variants.Single().Should().Match<ProductVariantDto>(v => v.Status == "Active");
        result.Data.Norms.Single().Should().Match<ProductNormDto>(n => n.ComponentName == "Thân" && n.Color == "Đen" && n.SpecNote == "May viền"
            && n.MaterialCode == "V1");
        (await Create().GetAsync(99, default)).Kind.Should().Be(NX_MTD.Application.Common.ResultKind.NotFound);
    }
}
