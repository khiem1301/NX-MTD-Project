namespace NX_MTD.Application.DTOs;

/// <summary>Một dòng tổng hợp tiến độ của một đơn hàng (cộng các dòng đơn trong vw_OrderLineProgress).</summary>
public sealed record OrderProgressDto(
    int OrderId, string OrderCode, string CustomerName, DateOnly DueDate, int DaysLeft, string Status, string MaterialStatus,
    bool IsDelayRisk, DateOnly? DelayRiskSince, int OrderedQty, int CutQty, int CutHandedToWarehouseQty, int IssuedToSewingQty,
    int FinishedQty, int FinishedReceivedQty, int DeliveredQty, int ReworkQty, int ScrapQty, decimal DefectRatePct);
