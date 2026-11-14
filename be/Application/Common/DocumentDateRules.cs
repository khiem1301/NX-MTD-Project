namespace NX_MTD.Application.Common;

/// <summary>
/// Quy tắc ngày dùng chung cho chứng từ kho (phiếu cấp, phiếu nhập, kiểm kê) và ngày dự kiến giao hàng (đơn mua, khách cấp).
/// Trả về thông báo lỗi, null nếu hợp lệ.
/// </summary>
public static class DocumentDateRules
{
    /// <summary>Ngày chứng từ không được lùi quá số ngày này so với hôm nay (khai muộn tối đa một tuần).</summary>
    public const int MaxBackDays = 7;

    /// <summary>
    /// Ngày chứng từ: không sau hôm nay, không trước hôm nay quá <see cref="MaxBackDays"/> ngày, không trước ngày nhận đơn
    /// (<paramref name="orderReceivedDate"/>, null khi chứng từ không gắn đơn).
    /// </summary>
    public static string? CheckDocumentDate(DateOnly date, DateOnly today, DateOnly? orderReceivedDate, string label)
    {
        if (date > today) return string.Format(WarehouseMessages.DocDateFuture, label);
        if (date < today.AddDays(-MaxBackDays)) return string.Format(WarehouseMessages.DocDateTooOld, label, MaxBackDays);
        if (orderReceivedDate is { } received && date < received)
            return string.Format(WarehouseMessages.DocDateBeforeOrder, label, received);
        return null;
    }

    /// <summary>
    /// Ngày dự kiến giao hàng: không trước hôm nay, không trước <paramref name="notBefore"/> (ngày đặt hàng của đơn mua),
    /// không sau hạn giao của đơn hàng (<paramref name="orderDueDate"/>, null khi không gắn đơn).
    /// </summary>
    public static string? CheckExpectedDelivery(DateOnly date, DateOnly today, DateOnly? notBefore, DateOnly? orderDueDate)
    {
        if (date < today) return WarehouseMessages.ExpectedDeliveryPast;
        if (notBefore is { } min && date < min) return string.Format(WarehouseMessages.ExpectedDeliveryBeforeOrderDate, min);
        if (orderDueDate is { } due && date > due) return string.Format(WarehouseMessages.ExpectedDeliveryAfterDue, due);
        return null;
    }

    /// <summary>
    /// Lịch giao: số lần giao = số ngày phải nhập (mỗi lần giao một ngày), ngày sau không trước ngày liền trước (DEV-ALL-113).
    /// Chưa khai số lần hoặc chưa nhập ngày thì bỏ qua (bắt buộc kiểm ở validator).
    /// </summary>
    public static string? CheckDeliveryDates(short? count, IReadOnlyList<DateOnly>? dates)
    {
        if (count is not { } expected || dates is null) return null;
        if (dates.Count != expected) return string.Format(WarehouseMessages.DeliveryDatesCountMismatch, expected);
        for (var i = 1; i < dates.Count; i++)
            if (dates[i] < dates[i - 1]) return string.Format(WarehouseMessages.DeliveryDateBeforePrevious, i + 1, i);
        return null;
    }

    /// <summary>Kiểm từng ngày giao như ngày dự kiến giao (không trước hôm nay / ngày đặt, không sau hạn giao). Null nếu hợp lệ.</summary>
    public static string? CheckExpectedDeliveries(IEnumerable<DateOnly> dates, DateOnly today, DateOnly? notBefore, DateOnly? orderDueDate) =>
        dates.Select(d => CheckExpectedDelivery(d, today, notBefore, orderDueDate)).FirstOrDefault(e => e is not null);

    /// <summary>Danh sách ngày giao hiển thị trong thông báo: "10/12/2026, 15/12/2026".</summary>
    public static string FormatDates(IEnumerable<DateOnly> dates) => string.Join(", ", dates.Select(d => d.ToString("dd/MM/yyyy")));
}
