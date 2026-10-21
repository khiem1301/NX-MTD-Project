namespace NX_MTD.Domain.Constants;

public static class AttachmentOwnerTypes
{
    public const string Request = "REQUEST";
    public const string PurchaseOrder = "PURCHASE_ORDER";
    public const string MaterialReceipt = "MATERIAL_RECEIPT";
    public const string MaterialReturn = "MATERIAL_RETURN";
    public const string DefectRecord = "DEFECT_RECORD";
    public const string DispatchNote = "DISPATCH_NOTE";
    public const string VehicleTrip = "VEHICLE_TRIP";
    public const string Payroll = "PAYROLL";
    public const string AccountingPeriod = "ACCOUNTING_PERIOD";
    public const string AttendanceImport = "ATTENDANCE_IMPORT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Request, PurchaseOrder, MaterialReceipt, MaterialReturn, DefectRecord,
        DispatchNote, VehicleTrip, Payroll, AccountingPeriod, AttendanceImport,
    };
}

public static class AttachmentPurposes
{
    public const string Photo = "PHOTO";
    public const string DocumentPhoto = "DOCUMENT_PHOTO";
    public const string PaymentProof = "PAYMENT_PROOF";
    public const string SignedIssueSlip = "SIGNED_ISSUE_SLIP";
    public const string SignedDeliveryNote = "SIGNED_DELIVERY_NOTE";
    public const string FuelReceipt = "FUEL_RECEIPT";
    public const string ImportFile = "IMPORT_FILE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Photo, DocumentPhoto, PaymentProof, SignedIssueSlip, SignedDeliveryNote, FuelReceipt, ImportFile,
    };
}
