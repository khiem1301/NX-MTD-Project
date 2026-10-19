using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwWorkerDailyEstimatedPay
{
    public int WorkerId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public bool? IsSunday { get; set; }

    public int OutputQty { get; set; }

    public int ScrapQty { get; set; }

    public decimal OutputAmount { get; set; }

    public decimal? SundayBonus { get; set; }

    public decimal ScrapAmount { get; set; }

    public decimal? EstimatedPay { get; set; }
}
