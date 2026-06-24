using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class LossTimePlan
{
    public int Id { get; set; }

    public string Category { get; set; } = null!;

    public string MachineLine { get; set; } = null!;

    public int Month { get; set; }

    public int Year { get; set; }

    public double TargetMinutes { get; set; }

    public decimal Ratio { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UploadedAt { get; set; }
}
