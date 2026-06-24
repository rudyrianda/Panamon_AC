using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ScheduleByModel
{
    public int? ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public string? MachineCode { get; set; }

    public int? Marking { get; set; }

    public int CycleTime { get; set; }

    public int? Operator { get; set; }

    public int? ProdPlan { get; set; }

    public int? ProdTarget { get; set; }

    public int? CycleTimeVaccum { get; set; }

    public int? WorkHour { get; set; }

    public int? Sut { get; set; }
}
