using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Menu
{
    public int? Machine { get; set; }

    public int? Product { get; set; }

    public int? CycleTime { get; set; }

    public int? Reason { get; set; }

    public int? Oee { get; set; }

    public int? MachineLog { get; set; }

    public int? UserId { get; set; }

    public int? LevelUser { get; set; }

    public virtual Pengguna? User { get; set; }
}
