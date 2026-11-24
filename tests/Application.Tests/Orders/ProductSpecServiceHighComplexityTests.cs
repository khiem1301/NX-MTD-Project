using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Rebaseline v3.1 — UTCID292, 294, 296, 301, 313 và UTCID533–UTCID550.
/// UTCID293, 295, 297–300, 302–312, 314 không sửa ở file này.
/// </summary>
[Trait("Category", "HighComplexity")]
public class ProductSpecServiceHighComplexityTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "AO-01" }];
    private readonly List<ProductComponent> _components = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<ProductColor> _colors = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<Material> _materials =
    [
        new()
        {
            MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Color = "",
            MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active,
        },
    ];
    private int _nextId = 100;

    public ProductSpecServiceHighComplexityTests()
    {
        _uow.SaveChangesAsync().Returns(1);
        Setup(_products);
        Setup(_materials);
        Setup(_colors);
        Setup(_markers);
        Setup(_components, c => { if (c.ProductComponentId == 0) c.ProductComponentId = ++_nextId; });
        Setup(_variants, v => { if (v.ProductVariantId == 0) v.ProductVariantId = ++_nextId; });
        Setup(_norms, n =>
        {
            if (n.ProductComponentMaterialId == 0) n.ProductComponentMaterialId = ++_nextId;
            n.ProductColor ??= _colors.FirstOrDefault(c => c.ProductColorId == n.ProductColorId)!;
            n.ProductComponent ??= _components.FirstOrDefault(c => c.ProductComponentId == n.ProductComponentId)!;
            n.Material ??= _materials.FirstOrDefault(m => m.MaterialId == n.MaterialId)!;
        });
        Setup(_markerComponents, m =>
            m.ProductComponent ??= _components.First(c => c.ProductComponentId == m.ProductComponentId));
        Setup(_markerFabrics, f =>
        {
            f.Material ??= _materials.First(m => m.MaterialId == f.MaterialId);
            f.ProductColor ??= _colors.First(c => c.ProductColorId == f.ProductColorId);
        });
        Setup(new List<OperationComponent>());
        Setup(new List<WorkOrderLine>());
        Setup(new List<RequestLine>());
        Setup(new List<CuttingLayLine>());
        Setup(new List<CutHandoverLine>());
        Setup(new List<MaterialIssueLine>());
        Setup(new List<StockBalance>());
    }

    [Fact]
    public async Task UTCID292_GetAsync_AllStatusesAndMarkerGroups_ReturnsFullSpec()
    {
        // Arrange — 3 component (2 Active, 1 Discontinued), Đỏ-M / Xanh-L, 4 norm Active, 1 nhóm bìa
        var red = AddColor(10, "Đỏ");
        var green = AddColor(11, "Xanh");
        var body = AddComponent(1, "Thân", ComponentTypes.Panel, 1, RecordStatus.Active);
        var sleeve = AddComponent(2, "Tay", ComponentTypes.Panel, 2, RecordStatus.Active);
        AddComponent(3, "Túi", ComponentTypes.Trim, 1, RecordStatus.Discontinued);
        AddVariant(21, red, "M");
        AddVariant(22, green, "L");
        AddNorm(1, body, red, _materials[0]);
        AddNorm(2, body, green, _materials[0]);
        AddNorm(3, sleeve, red, _materials[0]);
        AddNorm(4, sleeve, green, _materials[0]);
        _markerComponents.Add(new ProductMarkerComponent
        {
            ProductId = 1, MarkerNo = 1, ProductComponentId = body.ProductComponentId, ProductComponent = body,
        });
        _markerFabrics.Add(new ProductMarkerFabric
        {
            ProductId = 1, MarkerNo = 1, ProductColorId = red.ProductColorId, MaterialId = 1,
            ProductColor = red, Material = _materials[0],
        });

        // Act
        var result = await Sut().GetAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ProductId.Should().Be(1);
        result.Data.Components.Should().Equal(
            new ProductComponentDto(1, "Thân", "Panel", 1, "Active"),
            new ProductComponentDto(2, "Tay", "Panel", 2, "Active"),
            new ProductComponentDto(3, "Túi", "Trim", 1, "Discontinued"));
        result.Data.Variants.Should().BeEquivalentTo(
        [
            new ProductVariantDto(21, "Đỏ", "M", "Active"),
            new ProductVariantDto(22, "Xanh", "L", "Active"),
        ]);
        result.Data.Norms.Should().BeEquivalentTo(
        [
            new ProductNormDto(1, "Thân", "Đỏ", 1, "V1", "Vải", "m", "", null, MaterialTypes.Fabric),
            new ProductNormDto(2, "Thân", "Xanh", 1, "V1", "Vải", "m", "", null, MaterialTypes.Fabric),
            new ProductNormDto(3, "Tay", "Đỏ", 1, "V1", "Vải", "m", "", null, MaterialTypes.Fabric),
            new ProductNormDto(4, "Tay", "Xanh", 1, "V1", "Vải", "m", "", null, MaterialTypes.Fabric),
        ]);
        result.Data.MarkerComponents.Should().ContainSingle()
            .Which.Should().Be(new ProductMarkerComponentDto(1, "Thân"));
        result.Data.MarkerFabrics.Should().ContainSingle()
            .Which.Should().Be(new ProductMarkerFabricDto(1, "Đỏ", 1, "V1", "Vải", ""));
    }

    [Fact]
    public async Task UTCID294_GetAsync_DiscontinuedNormHidden_DiscontinuedComponentKept()
    {
        // Arrange — 1 norm Discontinued; component "Túi" Discontinued
        var red = AddColor(10, "Đỏ");
        var body = AddComponent(1, "Thân", ComponentTypes.Panel, 1, RecordStatus.Active);
        AddComponent(2, "Túi", ComponentTypes.Trim, 1, RecordStatus.Discontinued);
        AddNorm(1, body, red, _materials[0]);
        var hidden = AddNorm(2, _components.Single(c => c.ComponentName == "Túi"), red, _materials[0]);
        hidden.Status = RecordStatus.Discontinued;

        // Act
        var result = await Sut().GetAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Norms.Should().ContainSingle()
            .Which.Should().Be(new ProductNormDto(1, "Thân", "Đỏ", 1, "V1", "Vải", "m", "", null, MaterialTypes.Fabric));
        result.Data.Norms.Should().NotContain(n => n.ProductComponentMaterialId == hidden.ProductComponentMaterialId);
        result.Data.Components.Should().ContainSingle(c => c.ComponentName == "Túi")
            .Which.Status.Should().Be("Discontinued");
        _norms.Should().Contain(n => n.ProductComponentMaterialId == 2 && n.Status == RecordStatus.Discontinued);
    }

    [Fact]
    public async Task UTCID296_SaveAsync_ValidMarkerGroup_ChecksThenCommitsAuditsAndReturnsNewSpec()
    {
        // Arrange — spec cũ + dto hợp lệ: 1 PANEL, norm Đỏ/Xanh, nhóm bìa 1 một vải/màu
        var red = AddColor(10, "Đỏ");
        var green = AddColor(11, "Xanh");
        AddVariant(21, red, "M");
        AddVariant(22, green, "L");
        AddComponent(11, "Cũ", ComponentTypes.Panel, 1, RecordStatus.Active);
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M"), V(22, "Xanh", "L")],
            [N("Thân", "Đỏ"), N("Thân", "Xanh")],
            [Mc(1, "Thân")],
            [Mf(1, "Đỏ"), Mf(1, "Xanh")]);
        var trace = new List<string>();
        _uow.When(u => u.BeginTransactionAsync()).Do(_ => trace.Add("begin"));
        _uow.When(u => u.CommitTransactionAsync()).Do(_ => trace.Add("commit"));
        _audit.When(a => a.Add(Arg.Any<AuditEntry>())).Do(_ => trace.Add("audit"));
        _uow.SaveChangesAsync().Returns<Task<int>>(_ =>
        {
            trace.Add("save");
            return Task.FromResult(1);
        });

        // Act
        var result = await Sut().SaveAsync(1, dto, default);

        // Assert — Check (không ghi) → Begin → Apply → Commit → Get; audit đúng 1 lần
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.Saved);
        result.Data!.Components.Should().ContainSingle(c => c.ComponentName == "Thân" && c.ComponentType == "Panel" && c.Status == "Active");
        result.Data.Components.Should().NotContain(c => c.ComponentName == "Cũ");
        result.Data.Norms.Select(n => n.Color).Should().BeEquivalentTo(["Đỏ", "Xanh"]);
        result.Data.Norms.Should().OnlyContain(n => n.ComponentName == "Thân" && n.MaterialId == 1 && n.MaterialCode == "V1");
        result.Data.MarkerComponents.Should().ContainSingle().Which.Should().Be(new ProductMarkerComponentDto(1, "Thân"));
        result.Data.MarkerFabrics.Select(f => (f.Color, f.MaterialId, f.MaterialCode)).Should().BeEquivalentTo([("Đỏ", 1, "V1"), ("Xanh", 1, "V1")]);
        trace[0].Should().Be("begin");
        trace[^1].Should().Be("commit");
        trace.Count(s => s == "audit").Should().Be(1);
        trace.IndexOf("audit").Should().BeGreaterThan(0).And.BeLessThan(trace.Count - 1);
        trace.Where(s => s == "save").Should().NotBeEmpty();
        trace.Take(trace.IndexOf("commit")).Skip(1).Should().OnlyContain(s => s == "save" || s == "audit");
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(Product) && e.RecordId == 1 && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task UTCID301_SaveAsync_PiecesZero_ValidationAndNoTransaction()
    {
        // Arrange
        var dto = Spec([Panel("Thân", 0)], []);

        // Act
        var result = await Sut().SaveAsync(1, dto, default);

        // Assert — Pieces ∈ [1,99] chặn ở Check, không mở transaction
        AssertSpecInvalid(result, Messages.SpecImportPiecesRange);
        await AssertNotWritten();
        _components.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID313_WriteAsync_BypassCheck_TwoPanelFabricsAndUnknownColor_SafeValidation()
    {
        // Arrange — không gọi Check. PANEL màu Đỏ hai vải, norm màu "Tím" không có ProductColor.
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        AddMaterial(3, "V3", MaterialStatuses.Active);
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Tím"), N("Thân", "Đỏ", 1), N("Thân", "Đỏ", 3)],
            [Mc(1, "Thân")],
            [Mf(1, "Đỏ")]);

        // Act
        var result = await Sut().WriteAsync(1, dto, default);

        // Assert — Failure Validation an toàn (⚠ Single / KeyNotFound nếu bypass Check)
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID533_CheckAsync_ColorOnlyYellow_Succeeds()
    {
        // Arrange — Đỏ-M, Xanh-L; ProductColor thêm "Vàng" chưa có size
        var red = AddColor(10, "Đỏ");
        var green = AddColor(11, "Xanh");
        AddColor(12, "Vàng");
        AddVariant(21, red, "M");
        AddVariant(22, green, "L");
        var dto = Spec([Panel("Thân")], [V(21, "Đỏ", "M"), V(22, "Xanh", "L")], [N("Thân", "Vàng")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert — colors = Variants ∪ ProductColor
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
        await AssertNotWritten();
        _variants.Should().HaveCount(2);
        _variants.Should().NotContain(v => v.ProductColor.Color == "Vàng");
    }

    [Fact]
    public async Task UTCID534_SaveAsync_ColorOnlyYellow_WritesNormAndMarkerWithoutNewVariant()
    {
        // Arrange — cùng tiền đề UTCID533; dto norm + nhóm bìa cho "Vàng"
        var red = AddColor(10, "Đỏ");
        var green = AddColor(11, "Xanh");
        AddColor(12, "Vàng");
        AddVariant(21, red, "M");
        AddVariant(22, green, "L");
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M"), V(22, "Xanh", "L")],
            [N("Thân", "Vàng")],
            [Mc(1, "Thân")],
            [Mf(1, "Vàng")]);

        // Act
        var result = await Sut().SaveAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.Saved);
        var norm = result.Data!.Norms.Should().ContainSingle().Subject;
        norm.Color.Should().Be("Vàng");
        norm.ComponentName.Should().Be("Thân");
        norm.MaterialId.Should().Be(1);
        norm.MaterialCode.Should().Be("V1");
        result.Data.MarkerComponents.Should().ContainSingle().Which.ComponentName.Should().Be("Thân");
        result.Data.MarkerFabrics.Should().ContainSingle()
            .Which.Should().Be(new ProductMarkerFabricDto(1, "Vàng", 1, "V1", "Vải", ""));
        result.Data.Variants.Select(v => (v.Color, v.SizeCode)).Should().BeEquivalentTo([("Đỏ", "M"), ("Xanh", "L")]);
        _variants.Should().HaveCount(2);
        _variants.Should().NotContain(v => v.ProductColor.Color == "Vàng");
        _norms.Should().ContainSingle(n => n.ProductColorId == 12 && n.MaterialId == 1);
        _markerFabrics.Should().ContainSingle(f => f.ProductColorId == 12 && f.MaterialId == 1);
    }

    [Fact]
    public async Task UTCID535_CheckAsync_Pieces99_Succeeds()
    {
        // Arrange — biên trên [1,99]
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var dto = Spec([Panel("Thân", 99)], [V(21, "Đỏ", "M")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertNotWritten();
        _components.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID536_CheckAsync_Pieces100_Validation()
    {
        // Arrange
        var dto = Spec([Panel("Thân", 100)], []);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.SpecImportPiecesRange);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID537_CheckAsync_PiecesNegative_Validation()
    {
        // Arrange
        var dto = Spec([Panel("Thân", -1)], []);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.SpecImportPiecesRange);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID538_CheckAsync_DuplicateVariant_Fails()
    {
        // Arrange — hai dòng Đỏ-M
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var dto = Spec([Panel("Thân")], [V(21, "Đỏ", "M"), V(null, "Đỏ", "M")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecVariantDuplicate);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID539_CheckAsync_UnknownColor_Fails()
    {
        // Arrange — không có "Tím" ở variant lẫn ProductColor
        var red = AddColor(10, "Đỏ");
        var green = AddColor(11, "Xanh");
        AddVariant(21, red, "M");
        AddVariant(22, green, "L");
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M"), V(22, "Xanh", "L")],
            [N("Thân", "Tím")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecNormReference);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID540_CheckAsync_NormComponentMissing_Fails()
    {
        // Arrange — norm "Cầu vai" không có trong dto
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var dto = Spec([Panel("Thân")], [V(21, "Đỏ", "M")], [N("Cầu vai", "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecNormReference);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID541_CheckAsync_InactiveMaterial_Fails()
    {
        // Arrange — M7 Status=Inactive
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        AddMaterial(7, "M7", "INACTIVE");
        var dto = Spec([Panel("Thân")], [V(21, "Đỏ", "M")], [N("Thân", "Đỏ", 7)]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecMaterialInvalid);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID542_CheckAsync_PanelTwoFabricsSameColor_Fails()
    {
        // Arrange — PANEL màu Đỏ hai vải
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        AddMaterial(3, "V3", MaterialStatuses.Active);
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Đỏ", 1), N("Thân", "Đỏ", 3)]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, string.Format(Messages.ProductSpecPanelManyFabrics, "Thân", "Đỏ"));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID543_CheckAsync_MarkerLayoutWithoutPanel_Fails()
    {
        // Arrange — nhóm bìa không có component PANEL (chỉ TRIM)
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var dto = Spec(
            [Trim("Khóa")],
            [V(21, "Đỏ", "M")],
            [],
            [Mc(1, "Khóa")],
            [Mf(1, "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecMarkerReference);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID544_CheckAsync_MarkerCoverageMissing_Fails()
    {
        // Arrange — Tay có vải nhưng không nằm nhóm bìa của màu Đỏ
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var dto = Spec(
            [Panel("Thân"), Panel("Tay")],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Đỏ"), N("Tay", "Đỏ")],
            [Mc(1, "Thân")],
            [Mf(1, "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, string.Format(Messages.ProductSpecPanelWithoutMarker, "Đỏ", "Tay"));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID545_CheckAsync_MarkerGroupFabricMismatch_Fails()
    {
        // Arrange — một nhóm, hai chi tiết hai vải khác nhau ở màu Đỏ
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        AddMaterial(3, "V3", MaterialStatuses.Active);
        var dto = Spec(
            [Panel("Thân"), Panel("Tay")],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Đỏ", 1), N("Tay", "Đỏ", 3)],
            [Mc(1, "Thân"), Mc(1, "Tay")],
            [Mf(1, "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, string.Format(Messages.ProductSpecMarkerManyFabrics, 1, "Đỏ"));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID546_CheckAsync_LockedGroupChanged_Fails()
    {
        // Arrange — G1 đã khóa (đơn DH9 chưa xong); dto bỏ Tay khỏi G1
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        var body = AddComponent(11, "Thân", ComponentTypes.Panel, 1, RecordStatus.Active);
        var sleeve = AddComponent(12, "Tay", ComponentTypes.Panel, 1, RecordStatus.Active);
        _markerComponents.Add(Link(1, body));
        _markerComponents.Add(Link(1, sleeve));
        AddOrderMarker(1, "DH9");
        var dto = Spec(
            [Panel("Thân", id: 11), Panel("Tay", id: 12)],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Đỏ"), N("Tay", "Đỏ")],
            [Mc(1, "Thân"), Mc(2, "Tay")],
            [Mf(1, "Đỏ"), Mf(2, "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, string.Format(Messages.ProductSpecMarkerGroupLocked, 1, "DH9"));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID547_CheckAsync_MarkerGroupInUseDropped_Fails()
    {
        // Arrange — G2 đã có bìa của đơn; dto chỉ còn G1
        var red = AddColor(10, "Đỏ");
        AddVariant(21, red, "M");
        AddOrderMarker(2, "DH2");
        var dto = Spec(
            [Panel("Thân")],
            [V(21, "Đỏ", "M")],
            [N("Thân", "Đỏ")],
            [Mc(1, "Thân")],
            [Mf(1, "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert
        AssertSpecInvalid(result, Messages.ProductSpecMarkerGroupInUse);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID548_CheckAsync_DuplicateProductColorName_DoesNotAssignSilently()
    {
        // Arrange — hai ProductColor cùng tên "Đỏ" (Id 10 của biến thể, Id 20 lệch, đứng trước nên First() lấy 20)
        var stray = AddColor(20, "Đỏ");
        var canonical = AddColor(10, "Đỏ");
        AddVariant(21, canonical, "M");
        var dto = Spec([Panel("Thân")], [V(21, "Đỏ", "M")], [N("Thân", "Đỏ")]);

        // Act
        var result = await Sut().CheckAsync(1, dto, default);

        // Assert — báo lỗi trùng; Success im lặng là rủi ro First() (Id 20)
        result.IsSuccess.Should().BeFalse(
            "trùng tên ProductColor Đỏ (Id {0} và Id {1}) phải Validation, không First() im lặng",
            canonical.ProductColorId, stray.ProductColorId);
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID549_WriteAsync_SaveThrowsAfterClearMarkerGroups_PropagatesPartialDelete()
    {
        // Arrange — Write ngoài transaction; SaveChanges ném sau bước ClearMarkerGroups
        var red = AddColor(10, "Đỏ");
        var body = AddComponent(11, "Thân", ComponentTypes.Panel, 1, RecordStatus.Active);
        AddVariant(21, red, "M");
        _markerComponents.Add(Link(1, body));
        _markerComponents.Add(new ProductMarkerComponent
        {
            ProductId = 9, MarkerNo = 1, ProductComponentId = body.ProductComponentId, ProductComponent = body,
        });
        _markerFabrics.Add(FabricRow(1, red));
        _markerFabrics.Add(new ProductMarkerFabric
        {
            ProductId = 9, MarkerNo = 1, ProductColorId = red.ProductColorId, MaterialId = 1,
            ProductColor = red, Material = _materials[0],
        });
        var saves = 0;
        _uow.SaveChangesAsync().Returns<Task<int>>(_ =>
        {
            saves++;
            if (saves >= 2) throw new InvalidOperationException("save failed after ClearMarkerGroups");
            return Task.FromResult(1);
        });
        var dto = Spec([Panel("Thân", id: 11)], [V(21, "Đỏ", "M")], [N("Thân", "Đỏ")], [Mc(1, "Thân")], [Mf(1, "Đỏ")]);

        // Act
        var act = async () => await Sut().WriteAsync(1, dto, default);

        // Assert — exception propagate; nhóm cũ của mã 1 đã xóa (partial, không rollback)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("save failed after ClearMarkerGroups");
        _markerComponents.Should().ContainSingle().Which.ProductId.Should().Be(9);
        _markerFabrics.Should().ContainSingle().Which.ProductId.Should().Be(9);
        await _uow.DidNotReceive().BeginTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
    }

    [Fact]
    public async Task UTCID550_GetAsync_OrphanMaterial_ReturnsNullNameOrSkips()
    {
        // Arrange — norm Active trỏ MaterialId đã xóa (navigation null)
        var red = AddColor(10, "Đỏ");
        var body = AddComponent(1, "Thân", ComponentTypes.Panel, 1, RecordStatus.Active);
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 1,
            ProductId = 1,
            ProductColorId = red.ProductColorId,
            ProductColor = red,
            ProductComponentId = body.ProductComponentId,
            ProductComponent = body,
            MaterialId = 99,
            Material = null!,
            Status = RecordStatus.Active,
        });

        // Act
        var result = await Sut().GetAsync(1, default);

        // Assert — bỏ qua hoặc MaterialName null (⚠ NullReferenceException)
        result.IsSuccess.Should().BeTrue();
        var norms = result.Data!.Norms;
        norms.Count.Should().BeLessThanOrEqualTo(1);
        if (norms.Count == 1)
        {
            norms[0].MaterialName.Should().BeNull();
            norms[0].ComponentName.Should().Be("Thân");
        }
    }

    private ProductSpecService Sut() => new(_uow, _audit);

    private void Setup<T>(List<T> backing, Action<T>? onAdd = null) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.AddAsync(Arg.Do<T>(e => { onAdd?.Invoke(e); backing.Add(e); })).Returns(Task.CompletedTask);
        repo.AddRangeAsync(Arg.Do<IEnumerable<T>>(rows =>
        {
            foreach (var row in rows) { onAdd?.Invoke(row); backing.Add(row); }
        })).Returns(Task.CompletedTask);
        repo.When(r => r.Remove(Arg.Any<T>())).Do(c => backing.Remove(c.Arg<T>()));
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<T>>()))
            .Do(c => { foreach (var row in c.Arg<IEnumerable<T>>().ToList()) backing.Remove(row); });
        _uow.Repository<T>().Returns(repo);
    }

    private ProductColor AddColor(int id, string name)
    {
        var color = new ProductColor { ProductColorId = id, ProductId = 1, Color = name };
        _colors.Add(color);
        return color;
    }

    private ProductVariant AddVariant(int id, ProductColor color, string size)
    {
        var variant = new ProductVariant
        {
            ProductVariantId = id, ProductId = 1, ProductColorId = color.ProductColorId, ProductColor = color,
            SizeCode = size, Status = RecordStatus.Active,
        };
        _variants.Add(variant);
        return variant;
    }

    private ProductComponent AddComponent(int id, string name, string type, short pieces, string status)
    {
        var component = new ProductComponent
        {
            ProductComponentId = id, ProductId = 1, ComponentName = name, ComponentType = type,
            PiecesPerGarment = pieces, Status = status,
        };
        _components.Add(component);
        return component;
    }

    private ProductComponentMaterial AddNorm(int id, ProductComponent component, ProductColor color, Material material)
    {
        var norm = new ProductComponentMaterial
        {
            ProductComponentMaterialId = id, ProductId = 1, ProductColorId = color.ProductColorId, ProductColor = color,
            ProductComponentId = component.ProductComponentId, ProductComponent = component,
            MaterialId = material.MaterialId, Material = material, Status = RecordStatus.Active,
        };
        _norms.Add(norm);
        return norm;
    }

    private Material AddMaterial(int id, string code, string status)
    {
        var material = new Material
        {
            MaterialId = id, MaterialCode = code, MaterialName = code, Unit = "m", Color = "",
            MaterialType = MaterialTypes.Fabric, Status = status,
        };
        _materials.Add(material);
        return material;
    }

    private static ProductMarkerComponent Link(byte markerNo, ProductComponent component) => new()
    {
        ProductId = 1, MarkerNo = markerNo, ProductComponentId = component.ProductComponentId, ProductComponent = component,
    };

    private ProductMarkerFabric FabricRow(byte markerNo, ProductColor color) => new()
    {
        ProductId = 1, MarkerNo = markerNo, ProductColorId = color.ProductColorId, MaterialId = 1,
        ProductColor = color, Material = _materials[0],
    };

    private void AddOrderMarker(byte markerNo, string orderCode)
    {
        var order = new CustomerOrder { OrderId = markerNo, OrderCode = orderCode, Status = OrderStatuses.InProduction };
        _markers.Add(new ProductMarker
        {
            ProductMarkerId = markerNo, ProductId = 1, OrderId = order.OrderId, Order = order,
            MarkerNo = markerNo, MarkerCode = $"{markerNo}A", LengthM = 1, Status = RecordStatus.Active,
        });
    }

    private static void AssertSpecInvalid(Result result, string message)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.ProductSpecInvalid);
        result.Message.Should().Be(message);
    }

    private async Task AssertNotWritten()
    {
        await _uow.DidNotReceive().BeginTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
        await _uow.DidNotReceive().SaveChangesAsync();
        _audit.DidNotReceive().Add(Arg.Any<AuditEntry>());
    }

    private static SaveProductComponentDto Panel(string name, short pieces = 1, int? id = null) =>
        new(id, name, "Panel", pieces);

    private static SaveProductComponentDto Trim(string name, short pieces = 1, int? id = null) =>
        new(id, name, "Trim", pieces);

    private static SaveProductVariantDto V(int? id, string color, string size) => new(id, color, size);

    private static SaveProductNormDto N(string component, string color, int material = 1) =>
        new(component, color, material, null);

    private static SaveProductMarkerComponentDto Mc(int markerNo, string component) => new(markerNo, component);

    private static SaveProductMarkerColorDto Mf(int markerNo, string color) => new(markerNo, color);

    private static SaveProductSpecDto Spec(
        IReadOnlyList<SaveProductComponentDto> components,
        IReadOnlyList<SaveProductVariantDto> variants,
        IReadOnlyList<SaveProductNormDto>? norms = null,
        IReadOnlyList<SaveProductMarkerComponentDto>? markers = null,
        IReadOnlyList<SaveProductMarkerColorDto>? colors = null) =>
        new(components, variants, norms ?? [], markers ?? [], colors ?? []);
}
