using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Schedule
{
    public int? CycleId { get; set; }

    public int? SPlan { get; set; }

    public int? SActual { get; set; }

    public int? SId { get; set; }

    public string? SData { get; set; }
}
