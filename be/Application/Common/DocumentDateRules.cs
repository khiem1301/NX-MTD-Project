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

    /// <summary>Giao từ số lần này trở lên thì ngày dự kiến giao là một khoảng (từ ngày – đến ngày).</summary>
    public const int RangeDeliveryCount = 2;

    /// <summary>Có nhập khoảng ngày giao không (giao từ <see cref="RangeDeliveryCount"/> lần trở lên).</summary>
    public static bool IsRangeDelivery(short? count) => count >= RangeDeliveryCount;

    /// <summary>
    /// Khoảng ngày giao: giao từ <see cref="RangeDeliveryCount"/> lần trở lên thì bắt buộc có ngày giao đến, không trước ngày giao từ.
    /// Giao một lần thì bỏ qua ngày giao đến (service tự để trống).
    /// </summary>
    public static string? CheckDeliveryRange(short? count, DateOnly? from, DateOnly? to)
    {
        if (!IsRangeDelivery(count)) return null;
        if (to is not { } end) return WarehouseMessages.DeliveryDateToRequired;
        return from is { } start && end < start ? WarehouseMessages.DeliveryDateToBeforeFrom : null;
    }
}
