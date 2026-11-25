using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>
/// Dữ liệu nền S0 của IT_B2 (Master Catalog): Customer CU1–CU3, Supplier SU1–SU3, Material MA1–MA4, Product PR1–PR3, Vehicle VE1–VE4.
/// Giá trị "ngừng" = <see cref="RecordStatus.Discontinued"/> ("DISCONTINUED").
/// </summary>
public sealed partial class TestDataSeeder
{
    private readonly B2Context _b2 = new();

    /// <summary>
    /// Khóa UNIQUE của dòng có sẵn trong Test DB (dữ liệu DEMO) bị trùng với S0 đã được đổi tạm: (bảng, cột id, id, cột đổi, giá trị gốc).
    /// Chế độ ambient transaction được Rollback tự động; chế độ đồng thời (commit) phải khôi phục từ danh sách này.
    /// </summary>
    public List<(string Table, string IdColumn, int Id, string Column, string? Original)> MovedKeys { get; } = [];

    /// <summary>Nền tối thiểu cho IT_B2: chỉ Role (S0 của Batch 1 tự tạo Team/Employee/Account). Không seed Catalog/Orders chung để danh mục chỉ có đúng dữ liệu S0.</summary>
    public async Task<TestContext> SeedBatch2BaseAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        return _ctx;
    }

    /// <summary>Seed S0 của Batch 2. Chạy sau <see cref="SeedS0Async"/> (cần Employee cho chuyến xe). Mọi dòng nằm trong ambient transaction hoặc được commit (chế độ đồng thời).</summary>
    public async Task<B2Context> SeedB2Async(bool withStock = true, CancellationToken ct = default)
    {
        await MoveClashingKeysAsync(ct);

        // --- Customer ---
        _b2.Cu1 = new Customer
        {
            CustomerName = "Công ty May Việt", Phone = "0901000001", Email = "cu1@mayviet.vn",
            DefaultAddress = "KCN Tân Bình", Status = RecordStatus.Active,
        };
        _b2.Cu2 = new Customer { CustomerName = "Garment ABC", Phone = null, Email = null, Status = RecordStatus.Active };
        _b2.Cu3 = new Customer
        {
            CustomerName = "Khách Cũ", Status = RecordStatus.Discontinued, DiscontinueReason = "Hết hợp tác",
        };
        _db.Customers.AddRange(_b2.Cu1, _b2.Cu2, _b2.Cu3);

        // --- Supplier ---
        _b2.Su1 = new Supplier
        {
            SupplierName = "Vải Thành Công", Phone = "0902000001", Email = "su1@tc.vn", Status = RecordStatus.Active, CreatedAt = _now,
        };
        _b2.Su2 = new Supplier { SupplierName = "Phụ Liệu Minh", Status = RecordStatus.Active, CreatedAt = _now };
        _b2.Su3 = new Supplier
        {
            SupplierName = "NCC Cũ", Status = RecordStatus.Discontinued, DiscontinueReason = "Ngừng hợp tác", CreatedAt = _now,
        };
        _db.Suppliers.AddRange(_b2.Su1, _b2.Su2, _b2.Su3);

        // --- Material ---
        _b2.Ma1 = Mat("V-0001", "Kaki", MaterialTypes.Fabric, "Đen", MaterialTypes.FabricUnit, 1.50m, MaterialStatuses.Active);
        _b2.Ma2 = Mat("V-0002", "Kaki", MaterialTypes.Fabric, "", MaterialTypes.FabricUnit, 1.60m, MaterialStatuses.Active);
        _b2.Ma3 = Mat("PL-0001", "Nút 4 lỗ", MaterialTypes.Trim, "", "cái", null, MaterialStatuses.Active);
        _b2.Ma4 = Mat("V-0003", "Lụa", MaterialTypes.Fabric, "Trắng", MaterialTypes.FabricUnit, 1.40m, MaterialStatuses.Discontinued);
        _db.Materials.AddRange(_b2.Ma1, _b2.Ma2, _b2.Ma3, _b2.Ma4);

        // --- Product ---
        _b2.Pr1 = new Product { ProductCode = "SP001", Description = "Áo sơ mi IT", Status = RecordStatus.Active, CreatedAt = _now };
        _b2.Pr2 = new Product { ProductCode = "SP002", Description = "Quần IT", Status = RecordStatus.Active, CreatedAt = _now };
        _b2.Pr3 = new Product { ProductCode = "SP003", Description = "Mã hàng ngừng", Status = RecordStatus.Discontinued, CreatedAt = _now };
        _db.Products.AddRange(_b2.Pr1, _b2.Pr2, _b2.Pr3);

        // --- Vehicle ---
        _b2.Ve1 = Veh("51C-123.45", "Tải 1.5T", 12.5m, RecordStatus.Active);
        _b2.Ve2 = Veh("51C-678.90", "Tải 2.5T", 14m, RecordStatus.Active);
        _b2.Ve3 = Veh("51C-000.01", "Tải 1T", 10m, RecordStatus.Discontinued);
        _b2.Ve4 = Veh("51C-999.99", "Tải 5T", 20m, RecordStatus.Active);
        _db.Vehicles.AddRange(_b2.Ve1, _b2.Ve2, _b2.Ve3, _b2.Ve4);
        await _db.SaveChangesAsync(ct);

        // --- PR1: ảnh phụ, màu, biến thể, thành phần, định mức ---
        _b2.Pr1Img1 = new ProductImage { ProductId = _b2.Pr1.ProductId, ImageKey = "it-b2/pr1-img-1.jpg", SortOrder = 1, CreatedAt = _now };
        _b2.Pr1Img2 = new ProductImage { ProductId = _b2.Pr1.ProductId, ImageKey = "it-b2/pr1-img-2.jpg", SortOrder = 2, CreatedAt = _now };
        _db.ProductImages.AddRange(_b2.Pr1Img1, _b2.Pr1Img2);

        _b2.Pr1Black = new ProductColor { ProductId = _b2.Pr1.ProductId, Color = "Đen", CreatedAt = _now };
        _db.ProductColors.Add(_b2.Pr1Black);
        await _db.SaveChangesAsync(ct);

        _b2.Pr1VarM = new ProductVariant
        {
            ProductId = _b2.Pr1.ProductId, ProductColorId = _b2.Pr1Black.ProductColorId, SizeCode = "M", Status = RecordStatus.Active,
        };
        _b2.Pr1VarL = new ProductVariant
        {
            ProductId = _b2.Pr1.ProductId, ProductColorId = _b2.Pr1Black.ProductColorId, SizeCode = "L", Status = RecordStatus.Active,
        };
        _db.ProductVariants.AddRange(_b2.Pr1VarM, _b2.Pr1VarL);

        _b2.Pr1Front = Comp("Thân trước", ComponentTypes.Panel, 1);
        _b2.Pr1Pocket = Comp("Túi", ComponentTypes.Panel, 2);
        _b2.Pr1Label = Comp("Nhãn", ComponentTypes.Trim, 1);
        _db.ProductComponents.AddRange(_b2.Pr1Front, _b2.Pr1Pocket, _b2.Pr1Label);
        await _db.SaveChangesAsync(ct);

        _b2.Pr1Norm = new ProductComponentMaterial
        {
            ProductId = _b2.Pr1.ProductId, ProductColorId = _b2.Pr1Black.ProductColorId,
            ProductComponentId = _b2.Pr1Front.ProductComponentId, MaterialId = _b2.Ma1.MaterialId, Status = RecordStatus.Active,
        };
        _db.ProductComponentMaterials.Add(_b2.Pr1Norm);
        await _db.SaveChangesAsync(ct);

        // --- OD1 (CU1, PR1) + NVL của đơn + tồn kho MA1 ---
        _b2.Od1 = new CustomerOrder
        {
            OrderCode = "OD1-B2", CustomerId = _b2.Cu1.CustomerId, ReceivedDate = _t.AddDays(-14), DueDate = _t.AddDays(30),
            MaterialStatus = OrderMaterialStatuses.Partial, Status = OrderStatuses.InProduction, DeliveryAddress = "IT Delivery Address",
            CreatedAt = _now,
        };
        _db.CustomerOrders.Add(_b2.Od1);
        await _db.SaveChangesAsync(ct);

        foreach (var v in new[] { _b2.Pr1VarM, _b2.Pr1VarL })
        {
            var line = new OrderLine
            {
                OrderId = _b2.Od1.OrderId, ProductVariantId = v.ProductVariantId, ProductId = _b2.Pr1.ProductId,
                OrderedQty = 100, CmtUnitPrice = 25_000m,
            };
            _db.OrderLines.Add(line);
            _b2.Od1Lines.Add(line);
        }

        _b2.Ma1OrderMaterial = new OrderMaterial
        {
            OrderId = _b2.Od1.OrderId, MaterialId = _b2.Ma1.MaterialId, RequiredQty = 120m, CustomerQty = 0m,
        };
        _db.OrderMaterials.Add(_b2.Ma1OrderMaterial);

        // --- SU1: 2 PurchaseOrder ---
        for (var i = 1; i <= 2; i++)
        {
            var po = new PurchaseOrder
            {
                PoCode = $"PO-B2-{i:D2}", OrderId = _b2.Od1.OrderId, SupplierId = _b2.Su1.SupplierId,
                GoodsStatus = PurchaseGoodsStatuses.Ordered, PaymentStatus = PurchaseOrderStatuses.Unpaid,
                OrderDate = _t.AddDays(-5), CreatedAt = _now.AddDays(-5),
            };
            _db.PurchaseOrders.Add(po);
            _b2.Su1Pos.Add(po);
        }

        // --- Chuyến xe: VE1 có 1 STARTED, VE4 có 2 STARTED (bất thường) ---
        _b2.Ve1Trips.Add(Trip(_b2.Ve1, "Tài xế VE1", 10_000, 0));
        _b2.Ve4Trips.Add(Trip(_b2.Ve4, "Tài xế VE4 A", 20_000, 0));
        _db.VehicleTrips.AddRange(_b2.Ve1Trips);
        _db.VehicleTrips.AddRange(_b2.Ve4Trips);
        await _db.SaveChangesAsync(ct);

        if (!withStock) return _b2;

        // OnHandQty chỉ đổi qua usp_Stock_Post (trg_StockBalance_Check). Chế độ đồng thời không seed tồn kho vì StockTransaction là sổ chỉ-thêm (append-only, không xóa được khi dọn).
        await PostB2StockAsync(WarehouseTypes.Material, _b2.Ma1.MaterialId, null, null,
            StockDocTypes.MaterialReceipt, _b2.Od1.OrderId, 50m, ct);
        // "Thân trước" đang được dùng: định mức của nó có bán thành phẩm tồn theo dòng đơn OD1.
        await PostB2StockAsync(WarehouseTypes.SemiFinished, null, _b2.Od1Lines[0].OrderLineId, _b2.Pr1Norm.ProductComponentMaterialId,
            StockDocTypes.CutHandover, _b2.Od1.OrderId, 10m, ct);
        _b2.Ma1Stock = await _db.StockBalances.AsNoTracking()
            .SingleAsync(x => x.WarehouseType == WarehouseTypes.Material && x.MaterialId == _b2.Ma1.MaterialId, ct);
        return _b2;
    }

    /// <summary>
    /// IT_B2_191: VE4 có chuyến STARTED thứ hai (dữ liệu bất thường). DB chặn bằng UQ_VT_OneOpen nên chỉ tạo được bằng cách tạm vô hiệu hóa
    /// index trong ambient transaction (ALTER INDEX … DISABLE có tính giao dịch, được Rollback cùng test).
    /// </summary>
    public async Task<VehicleTrip> SeedB2SecondOpenTripForVe4Async(CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync("ALTER INDEX UQ_VT_OneOpen ON dbo.VehicleTrip DISABLE;", ct);
        var trip = Trip(_b2.Ve4, "Tài xế VE4 B", 20_500, 1);
        _db.VehicleTrips.Add(trip);
        await _db.SaveChangesAsync(ct);
        _b2.Ve4Trips.Add(trip);
        return trip;
    }

    private async Task PostB2StockAsync(string warehouseType, int? materialId, int? orderLineId, int? pcmId, string docType,
        int docId, decimal qty, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            EXEC dbo.usp_Stock_Post
                @WarehouseType = {warehouseType},
                @MaterialId = {materialId},
                @OrderLineId = {orderLineId},
                @ProductComponentMaterialId = {pcmId},
                @StockBalanceId = {null},
                @SourceDocType = {docType},
                @SourceDocId = {docId},
                @SourceLineId = {null},
                @QtyChange = {qty},
                @TransactionDate = {_t};
            """, ct);
    }

    private async Task MoveClashingKeysAsync(CancellationToken ct)
    {
        var plates = new[] { "51C-123.45", "51C-678.90", "51C-000.01", "51C-999.99", "51C-111.11", "51C-222.22", "51C-333.33" };
        foreach (var v in await _db.Vehicles.Where(x => plates.Contains(x.PlateNumber)).ToListAsync(ct))
        {
            MovedKeys.Add(("Vehicle", "VehicleId", v.VehicleId, "PlateNumber", v.PlateNumber));
            v.PlateNumber = $"MOVED-{v.VehicleId}";
        }

        // Số điện thoại / email dùng chung giữa Customer và Supplier (PARTNER_*_DUPLICATE): dòng có sẵn trùng với S0 hoặc với giá trị các ca test dùng bị bỏ trống tạm.
        var phones = new[] { "0901000001", "0902000001", "0903000001", "0904000001", "0905000001", "0909999999" };
        var emails = new[] { "cu1@mayviet.vn", "su1@tc.vn", "sg@may.vn", "pp@det.vn" };
        var customerNames = new[] { "Công ty May Việt", "Garment ABC", "Khách Cũ" };
        foreach (var c in await _db.Customers.Where(x => customerNames.Contains(x.CustomerName)
                     || (x.Phone != null && phones.Contains(x.Phone)) || (x.Email != null && emails.Contains(x.Email))).ToListAsync(ct))
        {
            if (customerNames.Contains(c.CustomerName)) { MovedKeys.Add(("Customer", "CustomerId", c.CustomerId, "CustomerName", c.CustomerName)); c.CustomerName = $"MOVED-{c.CustomerId}"; }
            if (c.Phone != null && phones.Contains(c.Phone)) { MovedKeys.Add(("Customer", "CustomerId", c.CustomerId, "Phone", c.Phone)); c.Phone = null; }
            if (c.Email != null && emails.Contains(c.Email)) { MovedKeys.Add(("Customer", "CustomerId", c.CustomerId, "Email", c.Email)); c.Email = null; }
        }

        var supplierNames = new[] { "Vải Thành Công", "Phụ Liệu Minh", "NCC Cũ" };
        foreach (var s in await _db.Suppliers.Where(x => supplierNames.Contains(x.SupplierName)
                     || (x.Phone != null && phones.Contains(x.Phone)) || (x.Email != null && emails.Contains(x.Email))).ToListAsync(ct))
        {
            if (supplierNames.Contains(s.SupplierName)) { MovedKeys.Add(("Supplier", "SupplierId", s.SupplierId, "SupplierName", s.SupplierName)); s.SupplierName = $"MOVED-{s.SupplierId}"; }
            if (s.Phone != null && phones.Contains(s.Phone)) { MovedKeys.Add(("Supplier", "SupplierId", s.SupplierId, "Phone", s.Phone)); s.Phone = null; }
            if (s.Email != null && emails.Contains(s.Email)) { MovedKeys.Add(("Supplier", "SupplierId", s.SupplierId, "Email", s.Email)); s.Email = null; }
        }

        // Mọi NVL mã V- / PL- có sẵn được đổi tạm để bộ đếm mã tự sinh khớp S0 (V-0003 / PL-0001 là lớn nhất), và tên trùng khóa (Name, Type, Color) của S0.
        var materialNames = new[] { "Kaki", "Lụa", "Nút 4 lỗ" };
        foreach (var m in await _db.Materials.Where(x => x.MaterialCode.StartsWith("V-") || x.MaterialCode.StartsWith("PL-")
                     || materialNames.Contains(x.MaterialName)).ToListAsync(ct))
        {
            if (m.MaterialCode.StartsWith("V-") || m.MaterialCode.StartsWith("PL-"))
            {
                MovedKeys.Add(("Material", "MaterialId", m.MaterialId, "MaterialCode", m.MaterialCode));
                m.MaterialCode = $"MOVED-{m.MaterialId}";
            }

            if (materialNames.Contains(m.MaterialName))
            {
                MovedKeys.Add(("Material", "MaterialId", m.MaterialId, "MaterialName", m.MaterialName));
                m.MaterialName = $"MOVED-{m.MaterialId}";
            }
        }

        foreach (var o in await _db.CustomerOrders.Where(x => x.OrderCode == "OD1-B2" || x.OrderCode == "OD2-B2").ToListAsync(ct))
        {
            MovedKeys.Add(("CustomerOrder", "OrderId", o.OrderId, "OrderCode", o.OrderCode));
            o.OrderCode = $"MOVED-{o.OrderId}";
        }

        foreach (var po in await _db.PurchaseOrders.Where(x => x.PoCode.StartsWith("PO-B2-")).ToListAsync(ct))
        {
            MovedKeys.Add(("PurchaseOrder", "PurchaseOrderId", po.PurchaseOrderId, "PoCode", po.PoCode));
            po.PoCode = $"MOVED-{po.PurchaseOrderId}";
        }

        var productCodes = new[] { "SP001", "SP002", "SP003" };
        foreach (var p in await _db.Products.Where(x => productCodes.Contains(x.ProductCode)).ToListAsync(ct))
        {
            MovedKeys.Add(("Product", "ProductId", p.ProductId, "ProductCode", p.ProductCode));
            p.ProductCode = $"MOVED-{p.ProductId}";
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Đơn OD2 thuộc PR2 (cho ca chéo dữ liệu IT_B2_172). Tạo theo yêu cầu vì S0 quy định PR2 không có đơn / biến thể (IT_B2_128).</summary>
    public async Task<CustomerOrder> SeedB2OrderForPr2Async(CancellationToken ct = default)
    {
        var color = new ProductColor { ProductId = _b2.Pr2.ProductId, Color = "Xanh", CreatedAt = _now };
        _db.ProductColors.Add(color);
        await _db.SaveChangesAsync(ct);
        var variant = new ProductVariant
        {
            ProductId = _b2.Pr2.ProductId, ProductColorId = color.ProductColorId, SizeCode = "M", Status = RecordStatus.Active,
        };
        _db.ProductVariants.Add(variant);
        var od2 = new CustomerOrder
        {
            OrderCode = "OD2-B2", CustomerId = _b2.Cu2.CustomerId, ReceivedDate = _t.AddDays(-7), DueDate = _t.AddDays(20),
            MaterialStatus = OrderMaterialStatuses.Awaiting, Status = OrderStatuses.New, DeliveryAddress = "IT Delivery OD2",
            CreatedAt = _now,
        };
        _db.CustomerOrders.Add(od2);
        await _db.SaveChangesAsync(ct);
        _db.OrderLines.Add(new OrderLine
        {
            OrderId = od2.OrderId, ProductVariantId = variant.ProductVariantId, ProductId = _b2.Pr2.ProductId,
            OrderedQty = 10, CmtUnitPrice = 20_000m,
        });
        await _db.SaveChangesAsync(ct);
        return od2;
    }

    private Material Mat(string code, string name, string type, string color, string unit, decimal? width, string status) => new()
    {
        MaterialCode = code, MaterialName = name, MaterialType = type, Color = color, Unit = unit, WidthM = width,
        Status = status, CreatedAt = _now,
    };

    private static Vehicle Veh(string plate, string type, decimal norm, string status) => new()
    {
        PlateNumber = plate, VehicleType = type, FuelNormPer100Km = norm, Status = status,
    };

    private ProductComponent Comp(string name, string type, short pieces) => new()
    {
        ProductId = _b2.Pr1.ProductId, ComponentName = name, ComponentType = type, PiecesPerGarment = pieces, Status = RecordStatus.Active,
    };

    private VehicleTrip Trip(Vehicle vehicle, string driver, int odometerStart, int dayOffset) => new()
    {
        VehicleId = vehicle.VehicleId, TripDate = _t.AddDays(-dayOffset), DriverName = driver, Purpose = "IT chuyến đang chạy",
        OdometerStart = odometerStart, Status = TripStatuses.Started, RecordedAt = _now.AddDays(-dayOffset),
    };
}
