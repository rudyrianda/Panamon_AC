using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Assembly
{
    public int? Ass1Plan { get; set; }

    public int? AssActual { get; set; }

    public string MachineId { get; set; } = null!;

    public int? AssPlan { get; set; }

    public int? AssProdplan { get; set; }

    public int? AssModel { get; set; }

    public float? AssProdtarget { get; set; }

    public float? AssProdactual { get; set; }

    public int? AssStatrun { get; set; }

    public int? AssStatidle { get; set; }

    public int? AssStatoff { get; set; }

    public int? AssWorkhour { get; set; }

    public string? BreakTime { get; set; }

    public int? Pulse { get; set; }

    public virtual Machine Machine { get; set; } = null!;
}
