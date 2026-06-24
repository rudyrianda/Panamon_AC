using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ProductionDatum
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public string? MachineCode { get; set; }

    public double WorkingTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public double? Overtime { get; set; }
}
