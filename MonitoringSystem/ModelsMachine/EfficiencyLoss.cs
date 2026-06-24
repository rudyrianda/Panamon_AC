using System;
using System.Collections.Generic;

namespace MonitoringSystem.ModelsMachine;

public partial class EfficiencyLoss
{
    public int Id { get; set; }

    public int EfficiencyId { get; set; }

    public string LossCategory { get; set; } = null!;

    public string LossGroup { get; set; } = null!;

    public decimal? LossMinutes { get; set; }

    public virtual Efficiency Efficiency { get; set; } = null!;
}
