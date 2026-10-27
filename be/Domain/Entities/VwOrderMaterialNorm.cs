using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwOrderMaterialNorm
{
    public int? OrderId { get; set; }

    public int? MaterialId { get; set; }

    public int? OrderMaterialId { get; set; }

    public decimal? RequiredQty { get; set; }

    public decimal NormQty { get; set; }

    public decimal? DiffQty { get; set; }
}
