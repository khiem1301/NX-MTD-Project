namespace NX_MTD.Application.DTOs;

// ---------- Sản lượng + lương tạm tính của tôi (UC59, MI-10) — chỉ công nhân may khoán sản phẩm ----------

/// <summary>Khoảng ngày xem lương tạm tính; trống = từ đầu tháng hiện tại đến hôm nay.</summary>
public sealed record MyEstimatedPayQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>Một ngày: Σ(SL × đơn giá khóa) − Σ(Scrap × đơn giá Scrap); Chủ nhật tiền sản phẩm × hệ số, khoản trừ giữ nguyên.</summary>
public sealed record MyEstimatedPayDayDto(
    DateOnly WorkDate, bool IsSunday, int OutputQty, int ScrapQty, decimal OutputAmount, decimal SundayBonus,
    decimal ScrapAmount, decimal EstimatedPay);

/// <summary>IsEstimate luôn true: đây là số tạm tính, không phải lương chính thức (GBR-12).</summary>
public sealed record MyEstimatedPayDto(
    DateOnly From, DateOnly To, bool IsEstimate, IReadOnlyList<MyEstimatedPayDayDto> Days, int TotalOutputQty,
    int TotalScrapQty, decimal TotalOutputAmount, decimal TotalSundayBonus, decimal TotalScrapAmount,
    decimal TotalEstimatedPay);

public sealed record MyOutputLineDto(
    string ShiftCode, string OperationCode, string OperationName, string OrderCode, string ProductCode,
    int Quantity, decimal UnitRate, decimal Amount);

public sealed record MyScrapLineDto(
    string OperationCode, string OperationName, string OrderCode, string ProductCode, int Quantity,
    decimal ScrapUnitRate, decimal Amount);

/// <summary>Chi tiết một ngày: từng dòng sản lượng theo công đoạn và từng dòng Scrap bị trừ.</summary>
public sealed record MyDayDetailDto(
    DateOnly WorkDate, IReadOnlyList<MyOutputLineDto> Outputs, IReadOnlyList<MyScrapLineDto> Scraps);
