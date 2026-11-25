using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        _ctx.CustomerParent = new Customer
        {
            CustomerName = "CUST-PARENT",
            Phone = "0900000001",
            DefaultAddress = "IT Parent Co Address",
            Status = RecordStatus.Active,
        };
        _db.Customers.Add(_ctx.CustomerParent);

        _ctx.MatFab01 = new Material
        {
            MaterialCode = "MAT-FAB-01",
            MaterialName = "Vải đủ tồn",
            Color = "Đen",
            MaterialType = MaterialTypes.Fabric,
            Unit = MaterialTypes.FabricUnit,
            WidthM = 1.50m,
            Status = MaterialStatuses.Active,
            CreatedAt = _now,
        };
        _ctx.MatFab02 = new Material
        {
            MaterialCode = "MAT-FAB-02",
            MaterialName = "Vải tồn thấp",
            Color = "Trắng",
            MaterialType = MaterialTypes.Fabric,
            Unit = MaterialTypes.FabricUnit,
            WidthM = 1.50m,
            Status = MaterialStatuses.Active,
            CreatedAt = _now,
        };
        _ctx.MatTrimOld = new Material
        {
            MaterialCode = "MAT-TRIM-OLD",
            MaterialName = "Phụ liệu ngừng",
            Color = "Xám",
            MaterialType = MaterialTypes.Trim,
            Unit = "cái",
            WidthM = null,
            Status = MaterialStatuses.Discontinued,
            CreatedAt = _now,
        };
        _db.Materials.AddRange(_ctx.MatFab01, _ctx.MatFab02, _ctx.MatTrimOld);

        _ctx.Product001 = new Product
        {
            ProductCode = "PRD-001",
            Description = "IT product with full specs",
            Status = RecordStatus.Active,
            CreatedAt = _now,
        };
        _ctx.Product002 = new Product
        {
            ProductCode = "PRD-002",
            Description = "IT product missing markers",
            Status = RecordStatus.Active,
            CreatedAt = _now,
        };
        _db.Products.AddRange(_ctx.Product001, _ctx.Product002);
        await _db.SaveChangesAsync(ct);

        _ctx.ColorBlack = new ProductColor { ProductId = _ctx.Product001.ProductId, Color = "Đen", CreatedAt = _now };
        _ctx.ColorWhite = new ProductColor { ProductId = _ctx.Product001.ProductId, Color = "Trắng", CreatedAt = _now };
        _db.ProductColors.AddRange(_ctx.ColorBlack, _ctx.ColorWhite);
        await _db.SaveChangesAsync(ct);

        _ctx.VariantBlackS = Variant(_ctx.ColorBlack, "S");
        _ctx.VariantBlackM = Variant(_ctx.ColorBlack, "M");
        _ctx.VariantWhiteS = Variant(_ctx.ColorWhite, "S");
        _ctx.VariantWhiteM = Variant(_ctx.ColorWhite, "M");
        _db.ProductVariants.AddRange(
            _ctx.VariantBlackS, _ctx.VariantBlackM, _ctx.VariantWhiteS, _ctx.VariantWhiteM);

        _ctx.PanelComponent = new ProductComponent
        {
            ProductId = _ctx.Product001.ProductId,
            ComponentName = "Thân trước",
            ComponentType = ComponentTypes.Panel,
            PiecesPerGarment = 1,
            Status = RecordStatus.Active,
        };
        _ctx.PanelComponentMax = new ProductComponent
        {
            ProductId = _ctx.Product001.ProductId,
            ComponentName = "Chi tiết max pieces",
            ComponentType = ComponentTypes.Panel,
            PiecesPerGarment = 99,
            Status = RecordStatus.Active,
        };
        _ctx.TrimComponent = new ProductComponent
        {
            ProductId = _ctx.Product001.ProductId,
            ComponentName = "Cúc",
            ComponentType = ComponentTypes.Trim,
            PiecesPerGarment = 1,
            Status = RecordStatus.Active,
        };
        _db.ProductComponents.AddRange(_ctx.PanelComponent, _ctx.PanelComponentMax, _ctx.TrimComponent);
        await _db.SaveChangesAsync(ct);
    }

    private ProductVariant Variant(ProductColor color, string size) => new()
    {
        ProductId = _ctx.Product001.ProductId,
        ProductColorId = color.ProductColorId,
        SizeCode = size,
        Status = RecordStatus.Active,
    };
}
