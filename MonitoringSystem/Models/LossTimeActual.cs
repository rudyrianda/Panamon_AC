using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class LossTimeActual
{
    public int Id { get; set; }

    public string Category { get; set; } = null!;

    public string MachineLine { get; set; } = null!;

    public int Day { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public double Minutes { get; set; }

    public string? Shift { get; set; }

    public string? DetailedReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
