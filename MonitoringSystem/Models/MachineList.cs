using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class MachineList
{
    public int IdMachine { get; set; }

    public string MachineName { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Efficiency> Efficiencies { get; set; } = new List<Efficiency>();
}
