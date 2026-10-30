namespace NX_MTD.Application.Services;

/// <summary>
/// Số mét vải ước tính để cắt bù một chi tiết (cùng công thức <c>vw_RecutFabricNeed</c>): chi tiết là hình chữ nhật dài × rộng; số miếng
/// xếp ngang khổ vải = làm tròn xuống (khổ / rộng chi tiết); số hàng = làm tròn lên (số chi tiết / số miếng một hàng);
/// số mét = số hàng × dài chi tiết / 100. Không xoay, không lồng chi tiết. Null = thiếu kích thước hoặc chi tiết rộng hơn khổ vải.
/// </summary>
public static class RecutFabricEstimator
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
