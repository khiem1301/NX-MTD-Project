namespace NX_MTD.Domain.Constants;

public static class RecordStatus
{
    public const string Active = "ACTIVE";
    public const string Locked = "LOCKED";
    public const string Resigned = "RESIGNED";
    public const string Discontinued = "DISCONTINUED";


    /// <summary>AccountingPeriod.Status khi chưa khóa (đối xứng với Locked ở trên).</summary>
    public const string Open = "OPEN";
}
