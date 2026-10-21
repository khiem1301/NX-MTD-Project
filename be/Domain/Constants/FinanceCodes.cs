namespace NX_MTD.Domain.Constants;

/// <summary>Cách phân bổ định phí khi tính hòa vốn (BreakEvenResult.AllocationBasis) — UC11, GBR-17.
/// Admin chọn khi chạy tính, cả hai công thức đều phải cài đặt (mục 12 AI Context: "chưa chốt" chọn 1
/// trong 2, nhưng task MI-13 đã chốt làm cả hai, không mặc định một cái rồi bỏ cái kia).</summary>
public static class AllocationBasis
{
    public const string Output = "OUTPUT";
    public const string LabourHours = "LABOUR_HOURS";

    public static readonly string[] All = [Output, LabourHours];
}
