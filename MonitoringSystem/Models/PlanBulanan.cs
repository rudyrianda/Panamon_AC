using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class PlanBulanan
{
    public string Machinecode { get; set; } = null!;

    public int? BDailyplan { get; set; }

    public int? BDailyAcc { get; set; }

    public int? BDate { get; set; }

    public virtual Machine MachinecodeNavigation { get; set; } = null!;
}
