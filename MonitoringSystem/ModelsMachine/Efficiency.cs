using System;
using System.Collections.Generic;

namespace MonitoringSystem.ModelsMachine;

public partial class Efficiency
{
    public int Id { get; set; }

    public int IdMachine { get; set; }

    public DateOnly Date { get; set; }

    public string Shift { get; set; } = null!;

    public decimal? WorkingTime { get; set; }

    public decimal? PlanQty { get; set; }

    public decimal? GoodProductionQty { get; set; }

    public decimal? DefectQty { get; set; }

    public decimal? Oee { get; set; }

    public decimal? OperatingRatio { get; set; }

    public decimal? Ability { get; set; }

    public decimal? Quality { get; set; }

    public decimal? Achievement { get; set; }

    public string? Category { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<EfficiencyLoss> EfficiencyLosses { get; set; } = new List<EfficiencyLoss>();

    public virtual MachineList IdMachineNavigation { get; set; } = null!;
}
