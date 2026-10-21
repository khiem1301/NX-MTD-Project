namespace NX_MTD.Domain.Constants;

/// <summary>VehicleTrip.Status (UC35/UC36, bảng do Thảo sở hữu — chỉ khai hằng số để đọc, không đổi
/// schema). Chưa có interface/entity nghiệp vụ nào khác khai các mã này trước MI-19 (AccountingReportService
/// đọc read-only cho báo cáo kế toán, UC36).</summary>
public static class VehicleTripStatus
{
    public const string Started = "STARTED";
    public const string Completed = "COMPLETED";
}
