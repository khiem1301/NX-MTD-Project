using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwProductLastSizeRatio
{
    public int ProductId { get; set; }

    public int CuttingLayId { get; set; }

    public string? SizeRatio { get; set; }
}
