using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class CycleTime
{
    public int CycleId { get; set; }

    public string MachineCode { get; set; } = null!;

    public int ProductId { get; set; }

    public int CycleTime1 { get; set; }

    public int? Operator { get; set; }

    public int? ProdPlan { get; set; }

    public int? WorkHour { get; set; }

    public string? Remark { get; set; }

    public decimal? ProdTarget { get; set; }

    public int? CycleTimeVaccum { get; set; }

    public virtual Machine MachineCodeNavigation { get; set; } = null!;
}
