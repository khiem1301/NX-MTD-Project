using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// File mẫu nhập Excel theo từng chức năng. Mỗi bảng: nếu mã hàng / đơn hàng đang mở đã có dữ liệu thì ghi dữ liệu đó (chữ thường),
/// chưa có thì ghi dòng ví dụ (chữ xám nghiêng) để người dùng biết cách điền. Tên cột khớp đúng các bộ đọc file nhập.
/// </summary>
public sealed class ImportTemplateService : IImportTemplateService
{
    private const string XlsxName = ".xlsx";

    private const string DemoNote =
        "Chữ thường là dữ liệu hiện có trong hệ thống. Chữ xám nghiêng là dòng ví dụ (bảng này chưa có dữ liệu): xóa trước khi nhập. " +
        "Màu tiêu đề: xanh đậm = cột bắt buộc, xanh nhạt = cột tùy chọn, xanh lá = cột size / cột tùy chọn mới.";

    private static readonly string[] DemoSizes = ["S", "M", "L", "XL"];

    private static readonly string[] KnownSizes = ["XXS", "XS", "S", "M", "L", "XL", "XXL", "XXXL", "3XL", "4XL"];

    private readonly IUnitOfWork _uow;
    private readonly IImportTemplateWriter _writer;
    private readonly IClock _clock;

    public ImportTemplateService(IUnitOfWork uow, IImportTemplateWriter writer, IClock clock)
    {
        _uow = uow; _writer = writer; _clock = clock;
    }

    public async Task<Result<ExcelFileDto>> BuildAsync(string kind, int? productId, int? orderId, CancellationToken ct)
    {
        if (productId is { } pid && !await _uow.Repository<Product>().Query().AnyAsync(p => p.ProductId == pid, ct))
            return Fail.NotFound<ExcelFileDto>();
        if (orderId is { } oid && !await _uow.Repository<CustomerOrder>().Query().AnyAsync(o => o.OrderId == oid, ct))
            return Fail.NotFound<ExcelFileDto>();

        ImportTemplateModel? model = kind switch
        {
            ImportTemplateKinds.Spec => await SpecAsync(productId, orderId, ct),
            ImportTemplateKinds.OrderLines => await OrderLinesAsync(orderId, ct),
            ImportTemplateKinds.OrderMaterials => await OrderMaterialsAsync(orderId, ct),
            ImportTemplateKinds.Operations => await OperationsAsync(productId, ct),
            ImportTemplateKinds.ProductForm => await ProductFormAsync(productId, ct),
            _ => null,
        };
        if (model is null) return Fail.NotFound<ExcelFileDto>();
        return Result<ExcelFileDto>.Success(new ExcelFileDto(_writer.Write(model), model.FileName), Messages.ExcelExported);
    }

    // ---------------------------------------------------------------- Từng file mẫu

    private async Task<ImportTemplateModel> SpecAsync(int? productId, int? orderId, CancellationToken ct) =>
        new("Mau_Dinh_Muc_Bia_Cat" + XlsxName, "MẪU NHẬP ĐỊNH MỨC MÃ HÀNG VÀ BÌA CẮT",
        [
            new("Dùng ở đâu", "Nút 'Nhập từ Excel' ở Định mức mã hàng (Admin) hoặc tab 'Bìa cắt' của đơn hàng (Admin, Quản đốc)."),
            new("Một file một mã hàng", "Hệ thống tìm dòng tiêu đề theo TÊN cột (không phân biệt hoa thường, có dấu hay không) trong 25 dòng đầu; tên sheet đặt tùy ý. Thiếu bảng nào thì bỏ qua bảng đó."),
            new("Gộp, không xóa", "Dòng trong file thêm mới hoặc ghi đè dòng cùng khóa; dữ liệu không có trong file giữ nguyên. Khóa: thành phần = tên chi tiết; định mức = màu + chi tiết (thay cả bộ NVL của cặp đó); nhóm bìa = số nhóm; bìa của đơn = nhóm + mã bìa."),
            new("Xem trước rồi mới lưu", "Chọn file là xem trước ngay: dòng lỗi có ghi lý do, hệ thống chạy đủ kiểm tra như nút Lưu. Bấm Nhập thì lưu định mức và bìa cùng một lần; còn lỗi thì không lưu gì."),
            new("1 · Thành phần", "Chi tiết cắt (PANEL) và phụ liệu (TRIM) của mã hàng, số chi tiết trên một áo."),
            new("2 · Định mức NVL", "Mỗi dòng = một NVL của một chi tiết ở một màu áo. Dùng MÃ NVL, không gõ tên."),
            new("3 · Nhóm bìa", "Bìa loại 1, khai ở mã hàng: chi tiết trên nhóm và các màu áo dùng nhóm. Vải của nhóm lấy từ định mức."),
            new("4 · Bìa của đơn", "Bìa loại 2, khai theo đơn hàng. Ở màn Mã hàng phải chọn đơn thì bảng này mới được nhập."),
            new("Giới hạn", "Màu áo phải là màu đã có trong đơn hàng của mã hàng (nhập Excel không tạo màu mới). Mã NVL phải có trong danh mục NVL đang dùng. Mỗi chi tiết PANEL chỉ một vải ở mỗi màu. Khổ bìa tối đa 3,5 m."),
            new("Dữ liệu trong file", DemoNote),
        ],
        [
            await ComponentsAsync(productId, "1 · ", ct), await NormsAsync(productId, "2 · ", ct),
            await GroupsAsync(productId, "3 · ", ct), await MarkersAsync(productId, orderId, "4 · ", ct),
        ]);

    private async Task<ImportTemplateModel> OrderLinesAsync(int? orderId, CancellationToken ct) =>
        new("Mau_Dong_Hang_Don" + XlsxName, "MẪU NHẬP DÒNG HÀNG CỦA ĐƠN",
        [
            new("Dùng ở đâu", "Form tạo / sửa đơn hàng → nút 'Nhập từ Excel' (Admin, khi đơn còn Mới)."),
            new("Cách nhập", "Chọn file là xem trước ngay. Bấm Áp dụng thì dòng hàng được điền vào form; xem lại rồi bấm Lưu đơn."),
            new("Sheet cần có", "Đơn hàng: mỗi dòng là một mã hàng + màu, các cột size là số lượng đặt (trống = không đặt size đó). Hệ thống tìm dòng tiêu đề theo TÊN cột trong 25 dòng đầu."),
            new("Dữ liệu trong file", DemoNote),
        ],
        [await OrderLinesTableAsync(orderId, "", ct)]);

    private async Task<ImportTemplateModel> OrderMaterialsAsync(int? orderId, CancellationToken ct) =>
        new("Mau_NVL_Cua_Don" + XlsxName, "MẪU NHẬP NVL CỦA ĐƠN",
        [
            new("Dùng ở đâu", "Chi tiết đơn hàng → tab 'NVL của đơn' → nút 'Nhập từ Excel' (Admin, Quản đốc; ẩn khi đơn đã đóng)."),
            new("Cách nhập", "Chọn file là xem trước ngay. NVL đã có trong đơn được cập nhật theo file, NVL không có trong file giữ nguyên."),
            new("Sheet cần có", "NVL của đơn: Mã NVL, Số cần, Khách cấp (tùy chọn), Ghi chú (tùy chọn). Phải mua = Số cần − Khách cấp, hệ thống tự tính."),
            new("Dữ liệu trong file", DemoNote),
        ],
        [await OrderMaterialsTableAsync(orderId, "", ct)]);

    private async Task<ImportTemplateModel> OperationsAsync(int? productId, CancellationToken ct) =>
        new("Mau_Cong_Doan" + XlsxName, "MẪU NHẬP CÔNG ĐOẠN CỦA MÃ HÀNG",
        [
            new("Dùng ở đâu", "Màn Công đoạn và đơn giá → nút 'Nhập từ Excel' (Kế toán)."),
            new("Cách nhập", "Chọn file là xem trước ngay (thêm mới / cập nhật / không đổi / lỗi), chọn sheet nếu file có nhiều bảng, chọn công đoạn cuối (chọn sẵn theo cột CUỐI). Cột TIỀN được đọc thành đơn giá công đoạn hiệu lực từ ngày chọn (Nháp, hoặc gửi Admin duyệt luôn)."),
            new("Sheet cần có", "Công đoạn: các cột của file quy trình công nghệ (STT, BƯỚC CÔNG VIỆC, BẬC, TGCT, TIỀN) và 3 cột tùy chọn NHÓM CÔNG ĐOẠN, CHI TIẾT, CUỐI."),
            new("Dữ liệu trong file", DemoNote),
        ],
        [await OperationsTableAsync(productId, "", ct)]);

    private async Task<ImportTemplateModel> ProductFormAsync(int? productId, CancellationToken ct)
    {
        var guide = new List<TemplateGuideRow>
        {
            new("Dùng ở đâu", "Form Thêm mới / Sửa mã hàng (màn Mã hàng hoặc form đơn hàng) → khung 'Nhập từ Excel' → nút 'Chọn 1 file chung cho tất cả'."),
            new("Cách nhập", "File này gom 4 sheet cho một mã hàng. Sheet Mã hàng tự điền Mã hàng, Mô tả của form (nếu có nhiều dòng chỉ lấy dòng đầu); mỗi sheet còn lại được nhập vào mục cùng tên. Sheet nào file không có thì bỏ qua. Sau khi chọn file, hệ thống mở bảng kiểm tra để xem và sửa; bấm Lưu thì lưu mã hàng rồi nhập các bảng."),
            new("Bắt buộc khi thêm mới", "Thành phần, Định mức NVL, Nhóm bìa phải có dữ liệu (thiếu thì không cho Lưu). Bìa của đơn, NVL của đơn không nhập ở form mã hàng: nhập ở chi tiết đơn hàng."),
            new("Dữ liệu trong file", DemoNote),
        };
        var tables = new List<TemplateTable>
        {
            await ProductTableAsync(productId, ct), await ComponentsAsync(productId, "", ct),
            await NormsAsync(productId, "", ct), await GroupsAsync(productId, "", ct),
        };
        return new ImportTemplateModel("Mau_Form_Ma_Hang" + XlsxName, "MẪU NHẬP MÃ HÀNG KÈM ĐỊNH MỨC (FORM MÃ HÀNG)", guide, tables);
    }

    // ---------------------------------------------------------------- Từng bảng: dữ liệu thật, hoặc ví dụ khi chưa có

    private async Task<TemplateTable> ProductTableAsync(int? productId, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid)
            rows = (await _uow.Repository<Product>().Query().AsNoTracking().Where(p => p.ProductId == pid)
                .Select(p => new { p.ProductCode, p.Description }).ToListAsync(ct))
                .Select(p => (IReadOnlyList<object?>)[p.ProductCode, p.Description]).ToList();
        return Pick("Mã hàng", "MÃ HÀNG",
            "Mã hàng: chữ, số và . _ -, tối đa 30 ký tự; mã đã có trong hệ thống được bỏ qua (không sửa). Mô tả: tùy chọn. Chỉ lấy dòng đầu để điền form.",
            [new("Mã hàng", 'R', 18), new("Mô tả", 'O', 40)], rows,
            [["AO-POLO01", "Vải cá sấu, cổ dệt"]]);
    }

    private async Task<TemplateTable> ComponentsAsync(int? productId, string no, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid)
            rows = (await _uow.Repository<ProductComponent>().Query().AsNoTracking()
                .Where(c => c.ProductId == pid && c.Status == RecordStatus.Active).OrderBy(c => c.ProductComponentId)
                .Select(c => new { c.ComponentName, c.ComponentType, c.PiecesPerGarment }).ToListAsync(ct))
                .Select(c => (IReadOnlyList<object?>)[c.ComponentName, c.ComponentType, (int)c.PiecesPerGarment]).ToList();
        return Pick("Thành phần", no + "THÀNH PHẦN CỦA MÃ HÀNG",
            "Tên chi tiết: duy nhất trong mã hàng (≤ 100 ký tự). Loại: PANEL (chi tiết cắt từ vải, đi qua tổ cắt) hoặc TRIM (phụ liệu cấp thẳng cho tổ may); chấp nhận cả chữ 'Vải cắt' / 'Phụ liệu'. Số chi tiết / áo: số nguyên dương, vd Tay áo = 2.",
            [new("Tên chi tiết", 'R', 28), new("Loại", 'R', 14), new("Số chi tiết / áo", 'R', 18)], rows,
            [["Thân trước", "PANEL", 2], ["Thân sau", "PANEL", 1], ["Tay áo", "PANEL", 2], ["Cổ áo", "PANEL", 2],
             ["Nút", "TRIM", 9], ["Chỉ may", "TRIM", 1], ["Nhãn dệt", "TRIM", 1]]);
    }

    private async Task<TemplateTable> NormsAsync(int? productId, string no, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid)
            rows = (await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
                .Where(p => p.ProductId == pid && p.Status == RecordStatus.Active && p.ProductComponent.Status == RecordStatus.Active)
                .OrderBy(p => p.ProductColorId).ThenBy(p => p.ProductComponentId)
                .Select(p => new { p.ProductColor.Color, p.ProductComponent.ComponentName, p.Material.MaterialCode, p.SpecNote }).ToListAsync(ct))
                .Select(p => (IReadOnlyList<object?>)[p.Color, p.ComponentName, p.MaterialCode, p.SpecNote]).ToList();
        return Pick("Định mức NVL", no + "ĐỊNH MỨC NVL THEO MÀU ÁO",
            "Màu áo: phải là màu đã có trong đơn hàng của mã hàng. Tên chi tiết: phải có ở sheet Thành phần. Mã NVL: MaterialCode trong danh mục NVL (đã gồm màu). Chi tiết PANEL chỉ một vải mỗi màu; vải lót / mex / bo khai thành chi tiết riêng. Ghi chú quy cách: tùy chọn (≤ 200 ký tự).",
            [new("Màu áo", 'R', 16), new("Tên chi tiết", 'R', 26), new("Mã NVL", 'R', 18), new("Ghi chú quy cách", 'O', 34)], rows,
            [["Trắng", "Thân trước", "V-KATE-TR"], ["Trắng", "Thân sau", "V-KATE-TR"], ["Trắng", "Tay áo", "V-KATE-TR"],
             ["Trắng", "Cổ áo", "V-KATE-TR", "Cổ cắt dọc canh sợi"], ["Trắng", "Nút", "T-NUT-4L"], ["Trắng", "Chỉ may", "T-CHI-TR"],
             ["Trắng", "Nhãn dệt", "T-NHAN-DET"], ["Xanh nhạt", "Thân trước", "V-KATE-XN"], ["Xanh nhạt", "Thân sau", "V-KATE-XN"],
             ["Xanh nhạt", "Tay áo", "V-KATE-XN"], ["Xanh nhạt", "Cổ áo", "V-KATE-TR", "Cổ phối vải trắng"], ["Xanh nhạt", "Nút", "T-NUT-4L"],
             ["Xanh nhạt", "Chỉ may", "T-CHI-XN"], ["Xanh nhạt", "Nhãn dệt", "T-NHAN-DET"]]);
    }

    private async Task<TemplateTable> GroupsAsync(int? productId, string no, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid)
        {
            var comps = await _uow.Repository<ProductMarkerComponent>().Query().AsNoTracking().Where(m => m.ProductId == pid)
                .OrderBy(m => m.ProductComponentId).Select(m => new { m.MarkerNo, m.ProductComponent.ComponentName }).ToListAsync(ct);
            var colors = await _uow.Repository<ProductMarkerFabric>().Query().AsNoTracking().Where(f => f.ProductId == pid)
                .OrderBy(f => f.ProductColorId).Select(f => new { f.MarkerNo, f.ProductColor.Color }).ToListAsync(ct);
            rows = comps.GroupBy(c => c.MarkerNo).OrderBy(g => g.Key)
                .Select(g => (IReadOnlyList<object?>)[(int)g.Key, string.Join("; ", g.Select(c => c.ComponentName)),
                    string.Join("; ", colors.Where(c => c.MarkerNo == g.Key).Select(c => c.Color).Distinct())]).ToList();
        }
        return Pick("Nhóm bìa", no + "NHÓM BÌA CỦA MÃ HÀNG (loại bìa 1 – khai ở mã hàng)",
            "Nhóm bìa: số nguyên dương (1 = thân, 2 = tay…). Chi tiết trên nhóm: tên chi tiết PANEL, nhiều chi tiết cách nhau dấu ; (một chi tiết có thể nằm ở nhiều nhóm). Màu áo dùng nhóm: các màu cách nhau dấu ;. Vải của nhóm không khai ở đây: hệ thống lấy từ Định mức NVL và báo lỗi nếu các chi tiết trên nhóm khác vải ở cùng một màu.",
            [new("Nhóm bìa", 'R', 14), new("Chi tiết trên nhóm", 'R', 34), new("Màu áo dùng nhóm", 'R', 28)], rows,
            [[1, "Thân trước; Thân sau", "Trắng; Xanh nhạt"], [2, "Tay áo", "Trắng; Xanh nhạt"], [3, "Cổ áo", "Trắng; Xanh nhạt"]]);
    }

    private const string StatusActive = "Đang dùng";
    private const string StatusDiscontinued = "Ngừng dùng";
    private static readonly string[] StatusOptions = [StatusActive, StatusDiscontinued];

    /// <summary>Số sản phẩm mỗi size trên một lớp của 4 bìa ví dụ; đơn có bao nhiêu size thì lấy bấy nhiêu số (lặp lại khi nhiều size).</summary>
    private static readonly int?[][] DemoRatios = [[1, 2, 2, 1], [null, 4, 1, null], [1, 2, 2, 1], [2, 4, 4, 2]];

    private static IReadOnlyList<IReadOnlyList<object?>> DemoMarkerRows(int sizeCount) =>
    [
        DemoMarkerRow(1, "1A", 3.4, 0, sizeCount), DemoMarkerRow(1, "1B", 2.6, 1, sizeCount),
        DemoMarkerRow(2, "2A", 1.9, 2, sizeCount), DemoMarkerRow(3, "3A", 0.9, 3, sizeCount),
    ];

    private static IReadOnlyList<object?> DemoMarkerRow(int group, string code, double length, int pattern, int sizeCount)
    {
        var ratios = Enumerable.Range(0, sizeCount).Select(i => (object?)DemoRatios[pattern][i % DemoRatios[pattern].Length]).ToList();
        if (ratios.Count > 0 && ratios.All(r => r is null)) ratios[0] = 1;   // mỗi bìa phải có ít nhất một size
        return new object?[] { "AS-0412", group, code, length, 1.5, StatusActive }.Concat(ratios).ToList();
    }

    private static string StatusText(string status) => status == RecordStatus.Discontinued ? StatusDiscontinued : StatusActive;

    private async Task<TemplateTable> MarkersAsync(int? productId, int? orderId, string no, CancellationToken ct)
    {
        var sizes = new List<string>();
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid && orderId is { } oid)
        {
            var markers = await _uow.Repository<ProductMarker>().Query().AsNoTracking().Include(m => m.ProductMarkerRatios).Include(m => m.Product)
                .Where(m => m.OrderId == oid && m.ProductId == pid).OrderBy(m => m.MarkerNo).ThenBy(m => m.MarkerCode).ToListAsync(ct);
            var orderSizes = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == oid && l.ProductId == pid)
                .Select(l => l.ProductVariant.SizeCode).Distinct().ToListAsync(ct);
            sizes = SortSizes(orderSizes).ToList();   // chỉ size của đơn này
            rows = markers.Select(m => (IReadOnlyList<object?>)new object?[] { m.Product.ProductCode, (int)m.MarkerNo, m.MarkerCode, (double)m.LengthM, (double)m.WidthM, StatusText(m.Status) }
                .Concat(sizes.Select(s => m.ProductMarkerRatios.FirstOrDefault(r => r.SizeCode == s) is { } r ? (object?)(int)r.PiecesPerPly : null)).ToList()).ToList();
        }
        var cols = new List<TemplateColumn>
        {
            new("Mã hàng", 'O', 14), new("Nhóm bìa", 'R', 12), new("Mã bìa", 'R', 10), new("Dài (m)", 'R', 10), new("Khổ (m)", 'R', 10), new("Trạng thái", 'O', 14, StatusOptions),
        };
        var demoCols = new List<TemplateColumn>(cols);
        cols.AddRange(sizes.Select(s => new TemplateColumn(s, 'G', 6)));
        var demoSizes = sizes.Count > 0 ? sizes : [.. DemoSizes];
        demoCols.AddRange(demoSizes.Select(s => new TemplateColumn(s, 'G', 6)));
        var note = "Mã hàng: để trống nếu đơn chỉ có một mã hàng. Nhóm bìa: phải là nhóm đã khai ở mã hàng. Mã bìa: duy nhất trong nhóm của đơn (1A, 1B…). Dài / Khổ: mét, khổ ≤ 3,5. Trạng thái: chọn Đang dùng hoặc Ngừng dùng trong danh sách (để trống = Đang dùng). Các cột size (S, M, L…) = số áo của size đó trên 1 lớp vải; để trống = bìa không cắt size này. Tên cột size phải trùng size có trong đơn.";
        return rows.Count > 0
            ? new TemplateTable("Bìa của đơn", no + "BÌA CỦA ĐƠN HÀNG (loại bìa 2 – khai ở đơn)", note, cols, rows, false)
            : new TemplateTable("Bìa của đơn", no + "BÌA CỦA ĐƠN HÀNG (loại bìa 2 – khai ở đơn)  ·  ví dụ", note, demoCols,
                DemoMarkerRows(demoSizes.Count), true);
    }

    private async Task<TemplateTable> OrderLinesTableAsync(int? orderId, string no, CancellationToken ct)
    {
        var sizes = new List<string>();
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (orderId is { } oid)
        {
            var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == oid).OrderBy(l => l.OrderLineId)
                .Select(l => new { l.ProductId, l.ProductVariant.Product.ProductCode, l.ProductVariant.ProductColor.Color, l.ProductVariant.ProductColorId,
                    l.ProductVariant.SizeCode, l.OrderedQty, l.CmtUnitPrice }).ToListAsync(ct);
            sizes = SortSizes(lines.Select(l => l.SizeCode).Distinct()).ToList();
            rows = lines.GroupBy(l => (l.ProductId, l.ProductColorId))
                .Select(g => (IReadOnlyList<object?>)new object?[] { g.First().ProductCode, g.First().Color, (double)g.First().CmtUnitPrice }
                    .Concat(sizes.Select(s => g.Where(l => l.SizeCode == s).Sum(l => (int?)l.OrderedQty) is { } q and > 0 ? (object?)q : null)).ToList()).ToList();
        }
        var baseCols = new List<TemplateColumn> { new("Mã hàng", 'R', 14), new("Màu", 'R', 16), new("Đơn giá CMT", 'R', 14) };
        var cols = baseCols.Concat(sizes.Select(s => new TemplateColumn(s, 'G', 8))).ToList();
        var demoCols = baseCols.Concat(DemoSizes.Select(s => new TemplateColumn(s, 'G', 8))).ToList();
        var note = "Mã hàng: phải có trong danh mục và đang sản xuất. Màu: màu mới được phép (lưu đơn sẽ tạo màu). Đơn giá CMT: mỗi mã hàng một giá (đồng / sản phẩm). Mỗi cột size (S, M, L…) là số lượng đặt; trống = không đặt size đó. Áp dụng thì dòng hàng được điền vào form, xem lại rồi bấm Lưu.";
        return rows.Count > 0
            ? new TemplateTable("Đơn hàng", no + "DÒNG HÀNG CỦA ĐƠN (nhập ở form tạo / sửa đơn hàng)", note, cols, rows, false)
            : new TemplateTable("Đơn hàng", no + "DÒNG HÀNG CỦA ĐƠN (nhập ở form tạo / sửa đơn hàng)  ·  ví dụ", note, demoCols,
                [["AS-0412", "Trắng", 38000, 80, 160, 160, 80], ["AS-0412", "Xanh nhạt", 38000, null, 100, 100, null], ["AP-0715", "Hồng", 34000, 120, 200, 120, null]], true);
    }

    private async Task<TemplateTable> OrderMaterialsTableAsync(int? orderId, string no, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (orderId is { } oid)
            rows = (await _uow.Repository<OrderMaterial>().Query().AsNoTracking().Where(m => m.OrderId == oid).OrderBy(m => m.OrderMaterialId)
                .Select(m => new { m.Material.MaterialCode, m.RequiredQty, m.CustomerQty, m.Note }).ToListAsync(ct))
                .Select(m => (IReadOnlyList<object?>)[m.MaterialCode, (double)m.RequiredQty, m.CustomerQty > 0 ? (double)m.CustomerQty : null, m.Note]).ToList();
        return Pick("NVL của đơn", no + "NVL CỦA ĐƠN (nhập ở chi tiết đơn → tab NVL của đơn)",
            "Mã NVL: phải có trong danh mục NVL (đã gồm màu). Số cần: tổng số đơn cần theo số khách báo. Khách cấp: phần khách giao (trống = 0). Phải mua = Số cần − Khách cấp, Đã nhập (khách giao / mua về) và Còn thiếu do hệ thống tính. NVL đã có trong đơn được cập nhật theo file, NVL không có trong file giữ nguyên.",
            [new("Mã NVL", 'R', 18), new("Số cần", 'R', 12), new("Khách cấp", 'O', 12), new("Ghi chú", 'O', 34)], rows,
            [["V-KATE-TR", 1200, null, "Mua toàn bộ"], ["T-CHI-TR", 40, null, "Chỉ 40/2, tự mua"], ["T-NHAN-DET", 680, 680, "Khách cấp đủ"],
             ["T-NUT-4L", 6200, 2000, "Khách cấp 2.000, mua thêm phần còn lại"]]);
    }

    private async Task<TemplateTable> OperationsTableAsync(int? productId, string no, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<object?>> rows = [];
        if (productId is { } pid)
        {
            var today = _clock.Today;
            var ops = await _uow.Repository<Operation>().Query().AsNoTracking()
                .Where(o => o.ProductId == pid && o.Status == OperationStatuses.Active && o.TeamType == TeamTypes.Sewing)
                .OrderBy(o => o.StationSequence)
                .Select(o => new
                {
                    o.StationSequence, o.OperationName, o.SkillLevel, o.StandardTimeSec, o.IsFinalStep,
                    Group = o.OperationGroup == null ? null : o.OperationGroup.GroupName,
                    Comps = o.OperationComponents.OrderBy(c => c.ProductComponentId).Select(c => c.ProductComponent.ComponentName).ToList(),
                    Rate = o.PieceRates.Where(r => r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= today)
                        .OrderByDescending(r => r.EffectiveDate).Select(r => (decimal?)r.Rate).FirstOrDefault(),
                }).ToListAsync(ct);
            rows = ops.Select(o => (IReadOnlyList<object?>)[(int)o.StationSequence, o.OperationName, o.SkillLevel is { } s ? (int)s : null,
                o.StandardTimeSec is { } t ? (double)t : null, o.Rate is { } r ? (double)r : null, o.Group,
                o.Comps.Count > 0 ? string.Join("; ", o.Comps) : null, o.IsFinalStep ? "x" : null]).ToList();
        }
        return Pick("Công đoạn", no + "CÔNG ĐOẠN CỦA MÃ HÀNG",
            "STT, BƯỚC CÔNG VIỆC, BẬC (1–9), TGCT (giây), TIỀN (đồng / sản phẩm) giữ đúng như file quy trình công nghệ đang nhập. Cột mới, tùy chọn: NHÓM CÔNG ĐOẠN (công đoạn to, vd 'May tay'); CHI TIẾT (chi tiết PANEL công đoạn làm trên, cách nhau dấu ;, để trống nếu là là / kiểm hàng / gắn mác); CUỐI (đánh x ở đúng một công đoạn may cuối).",
            [new("STT", 'R', 8), new("BƯỚC CÔNG VIỆC", 'R', 30), new("BẬC", 'R', 8), new("TGCT", 'R', 10), new("TIỀN", 'R', 12),
             new("NHÓM CÔNG ĐOẠN", 'G', 20), new("CHI TIẾT", 'G', 34), new("CUỐI", 'G', 8)], rows,
            [[1, "Ráp vai", 2, 18, 450, "May thân", "Thân trước; Thân sau", null], [2, "Ráp sườn", 2, 22, 520, "May thân", "Thân trước; Thân sau", null],
             [3, "Lộn tay", 1, 15, 300, "May tay", "Tay áo", null], [4, "Tra tay", 3, 40, 900, "May tay", "Tay áo; Thân trước; Thân sau", null],
             [5, "Tra khóa", 3, 55, 1200, "Hoàn thiện", null, null], [6, "Gắn nhãn", 1, 8, 150, "Hoàn thiện", null, null],
             [7, "Vắt sổ", 1, 60, 600, "Hoàn thiện", null, null], [8, "Kiểm hàng", 2, 20, 250, "Hoàn thiện", null, "x"]]);
    }

    // ---------------------------------------------------------------- Tiện ích

    /// <summary>Có dữ liệu thật thì dùng, không thì dùng dòng ví dụ (đánh dấu IsDemo để ghi chữ khác màu).</summary>
    private static TemplateTable Pick(string sheet, string title, string note, IReadOnlyList<TemplateColumn> cols,
        IReadOnlyList<IReadOnlyList<object?>> real, IReadOnlyList<IReadOnlyList<object?>> demo) =>
        real.Count > 0
            ? new TemplateTable(sheet, title, note, cols, real, false)
            : new TemplateTable(sheet, title + "  ·  ví dụ", note, cols, demo, true);

    private static IEnumerable<string> SortSizes(IEnumerable<string> sizes) =>
        sizes.OrderBy(s => { var i = Array.IndexOf(KnownSizes, s.ToUpperInvariant()); return i >= 0 ? i : int.MaxValue; })
            .ThenBy(s => int.TryParse(s, out var n) ? n : int.MaxValue)
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase);
}
