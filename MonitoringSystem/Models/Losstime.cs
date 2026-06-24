using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Losstime
{
    public DateOnly? Date { get; set; }

    public int? BplossTime { get; set; }

    public int? ActLossTime { get; set; }

    public string? Reason { get; set; }

    public string? MachineCode { get; set; }
}
