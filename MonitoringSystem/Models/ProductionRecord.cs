using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ProductionRecord
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string? ProductName { get; set; }

    public string? MachineCode { get; set; }

    public int? Quantity { get; set; }

    public string? Lot { get; set; }

    public string? Remark { get; set; }

    public int? Overtime { get; set; }

    public string? Shift { get; set; }

    public int? NoDirectOfWorker { get; set; }

    public int? NoDirectOfWorkerOvertime { get; set; }

    public virtual ProductionPlan Plan { get; set; } = null!;
}
