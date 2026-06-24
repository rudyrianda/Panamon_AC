using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class WorkHourOpr
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? Edate { get; set; }

    public int? PId { get; set; }

    public string MachineId { get; set; } = null!;

    public string? NumbOfopr { get; set; }

    public int? TackTimeSet { get; set; }

    public int? TackTimeActual { get; set; }

    public int? TactTimeDiff { get; set; }

    public virtual Machine Machine { get; set; } = null!;
}
