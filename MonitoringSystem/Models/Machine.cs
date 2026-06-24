using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Machine
{
    public string MachineCode { get; set; } = null!;

    public string? MachineName { get; set; }

    public string? Remark { get; set; }

    public virtual ICollection<CycleTime> CycleTimes { get; set; } = new List<CycleTime>();
}
