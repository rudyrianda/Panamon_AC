using System;
using System.Collections.Generic;

namespace MonitoringSystem.ModelsMachine;

public partial class MachineList
{
    public int IdMachine { get; set; }

    public string MachineName { get; set; } = null!;

    public virtual ICollection<Efficiency> Efficiencies { get; set; } = new List<Efficiency>();
}
