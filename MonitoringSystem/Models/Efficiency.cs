using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Efficiency
{
    public int Id { get; set; }

    public int IdMachine { get; set; }

    public DateTime Date { get; set; }

    public string? Shift { get; set; }

    public DateTime? CreatedAt { get; set; }

    public double? Overtime { get; set; }

    public virtual ICollection<EfficiencyLoss> EfficiencyLosses { get; set; } = new List<EfficiencyLoss>();

    public virtual MachineList IdMachineNavigation { get; set; } = null!;
}
