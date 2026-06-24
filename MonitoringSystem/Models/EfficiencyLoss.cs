using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class EfficiencyLoss
{
    public int Id { get; set; }

    public int EfficiencyId { get; set; }

    public string? LossCategory { get; set; }

    public string? LossGroup { get; set; }

    public double LossMinutes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Efficiency Efficiency { get; set; } = null!;
}
