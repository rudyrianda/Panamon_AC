using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class AssemblyLossTime
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public string MachineCode { get; set; } = null!;

    public TimeOnly Time { get; set; }

    public int? LossTime { get; set; }

    public string? Reason { get; set; }

    public TimeOnly? EndDateTime { get; set; }

    public string? DetailedReason { get; set; }
}
