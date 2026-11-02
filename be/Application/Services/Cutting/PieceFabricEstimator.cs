namespace NX_MTD.Application.Services;

/// <summary>
/// Số mét vải ước tính cho một hình chữ nhật dài × rộng: bìa của lệnh cắt cả bộ, chi tiết của cắt lẻ và cắt bù (cùng công thức <c>vw_RecutFabricNeed</c>).
/// Số miếng xếp ngang khổ vải = làm tròn xuống (khổ / rộng); số hàng = làm tròn lên (số miếng / số miếng một hàng);
/// số mét = số hàng × dài / 100. Không xoay, không lồng. Null = thiếu kích thước hoặc rộng hơn khổ vải.
/// </summary>
public static class PieceFabricEstimator
{
    public static decimal? NeedMeters(int pieces, decimal? pieceLengthCm, decimal? pieceWidthCm, decimal? fabricWidthCm)
    {
        if (pieces <= 0 || pieceLengthCm is not > 0 || pieceWidthCm is not > 0 || fabricWidthCm is null || fabricWidthCm < pieceWidthCm)
            return null;
        var perRow = (int)Math.Floor(fabricWidthCm.Value / pieceWidthCm.Value);
        if (perRow <= 0) return null;
        var rows = Math.Ceiling(pieces / (decimal)perRow);
        return Math.Round(rows * pieceLengthCm.Value / 100m, 3);
    }
}
