using System.Text.RegularExpressions;

namespace NX_MTD.Application.Common;

/// <summary>
/// Biển số xe ô tô của xưởng: mã tỉnh 2 chữ số, 1 đến 2 chữ cái (có thể kèm 1 số ở biển cũ), gạch ngang, rồi 4 đến 5 số
/// (biển mới có dấu chấm trước 2 số cuối). Ví dụ: <c>51C-123.45</c>, <c>29LD-123.45</c>, <c>30F-1234</c>. FE dùng cùng mẫu ở <c>features/vehicle-log/constants.ts</c>.
/// </summary>
public static class VehiclePlate
{
    private static readonly Regex Pattern = new(@"^[1-9]\d[A-Z]{1,2}\d?-(\d{3}\.\d{2}|\d{4,5})$", RegexOptions.Compiled);

    public const int MaxLength = 20;

    public static string Normalize(string plate) => plate.Trim().ToUpperInvariant();

    public static bool IsValid(string? plate) => plate is not null && Pattern.IsMatch(Normalize(plate));
}
