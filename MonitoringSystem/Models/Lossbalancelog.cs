using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Lossbalancelog
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? Edate { get; set; }

    public int? PId { get; set; }

    public string MachineId { get; set; } = null!;

    public int? SId { get; set; }

    public int? ReasonId { get; set; }

    public string? NumbOfopr { get; set; }

    public string? Duration { get; set; }

    public int? DurationRt { get; set; }

    public decimal? DurationAcc { get; set; }

    public decimal? CycleTime { get; set; }

    public virtual Machine Machine { get; set; } = null!;

    public virtual Reason? Reason { get; set; }

    public virtual MState? SIdNavigation { get; set; }
}
