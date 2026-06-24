using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Csoperatorlog
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? Edate { get; set; }

    public int? PId { get; set; }

    public string MachineId { get; set; } = null!;

    public int? SId { get; set; }

    public int? ReasonId { get; set; }

    public string? Duration { get; set; }

    public decimal? Duration1 { get; set; }

    public string? NumbOfsta { get; set; }

    public int? NumbOfStop { get; set; }

    public int? CycleTime { get; set; }

    public virtual Machine Machine { get; set; } = null!;

    public virtual Reason? Reason { get; set; }

    public virtual MState? SIdNavigation { get; set; }
}
