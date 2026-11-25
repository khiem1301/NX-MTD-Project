using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>
/// ID động của dữ liệu nền IT_B2 (S0 Batch 2: Master Catalog). Test chỉ dùng các thuộc tính này — không hardcode ID.
/// Tên thuộc tính khớp alias trong tài liệu (CU1 → Cu1, MA1 → Ma1, PR1 → Pr1, VE1 → Ve1, OD1 → Od1 …).
/// </summary>
public sealed class B2Context
{
    // --- Customer ---
    public Customer Cu1 { get; set; } = null!;   // "Công ty May Việt", 0901000001, cu1@mayviet.vn, có đơn OD1
    public Customer Cu2 { get; set; } = null!;   // "Garment ABC", Phone/Email NULL
    public Customer Cu3 { get; set; } = null!;   // "Khách Cũ", DISCONTINUED, "Hết hợp tác"
    public CustomerOrder Od1 { get; set; } = null!;
    public List<OrderLine> Od1Lines { get; } = [];

    // --- Supplier ---
    public Supplier Su1 { get; set; } = null!;   // "Vải Thành Công", 0902000001, su1@tc.vn, 2 PurchaseOrder
    public Supplier Su2 { get; set; } = null!;   // "Phụ Liệu Minh"
    public Supplier Su3 { get; set; } = null!;   // "NCC Cũ", DISCONTINUED
    public List<PurchaseOrder> Su1Pos { get; } = [];

    // --- Material ---
    public Material Ma1 { get; set; } = null!;   // V-0001 Kaki FABRIC Đen 1.50 — đang được dùng
    public Material Ma2 { get; set; } = null!;   // V-0002 Kaki FABRIC '' 1.60 — chưa dùng
    public Material Ma3 { get; set; } = null!;   // PL-0001 Nút 4 lỗ TRIM ''
    public Material Ma4 { get; set; } = null!;   // V-0003 Lụa FABRIC Trắng — ngừng dùng
    public StockBalance Ma1Stock { get; set; } = null!;
    public OrderMaterial Ma1OrderMaterial { get; set; } = null!;

    // --- Product ---
    public Product Pr1 { get; set; } = null!;    // SP001 — có đơn OD1, 2 ảnh phụ, spec đầy đủ
    public Product Pr2 { get; set; } = null!;    // SP002 — không đơn, không biến thể, không ảnh phụ
    public Product Pr3 { get; set; } = null!;    // SP003 — ngừng
    public ProductColor Pr1Black { get; set; } = null!;
    public ProductVariant Pr1VarM { get; set; } = null!;
    public ProductVariant Pr1VarL { get; set; } = null!;
    public ProductComponent Pr1Front { get; set; } = null!;   // Thân trước (PANEL, đang dùng)
    public ProductComponent Pr1Pocket { get; set; } = null!;  // Túi (PANEL, chưa dùng)
    public ProductComponent Pr1Label { get; set; } = null!;   // Nhãn (TRIM)
    public ProductComponentMaterial Pr1Norm { get; set; } = null!; // (Thân trước, Đen, MA1)
    public ProductImage Pr1Img1 { get; set; } = null!;
    public ProductImage Pr1Img2 { get; set; } = null!;

    // --- Vehicle ---
    public Vehicle Ve1 { get; set; } = null!;    // 51C-123.45 ACTIVE, 1 chuyến STARTED
    public Vehicle Ve2 { get; set; } = null!;    // 51C-678.90 ACTIVE, không chuyến đang chạy
    public Vehicle Ve3 { get; set; } = null!;    // 51C-000.01 DISCONTINUED
    public Vehicle Ve4 { get; set; } = null!;    // 51C-999.99 ACTIVE, 2 chuyến STARTED (dữ liệu bất thường)
    public List<VehicleTrip> Ve1Trips { get; } = [];
    public List<VehicleTrip> Ve4Trips { get; } = [];
}
