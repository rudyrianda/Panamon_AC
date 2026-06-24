using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class SapPlan
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string MachineCode { get; set; } = null!;

    public int SapPlanNormal { get; set; }

    public int? SapPlanOvertime { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? ProductName { get; set; }

    public virtual ProductionPlan Plan { get; set; } = null!;
}
