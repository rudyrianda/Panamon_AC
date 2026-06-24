using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class DownTime
{
    public DateTime? Date { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? MachineCode { get; set; }

    public string? State { get; set; }

    public int? ReasonId { get; set; }

    public string? Duration { get; set; }

    public virtual Machine? MachineCodeNavigation { get; set; }

    public virtual Reason? Reason { get; set; }
}
