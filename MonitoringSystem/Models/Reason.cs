using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Reason
{
    public int ReasonId { get; set; }

    public string ReasonName { get; set; } = null!;

    public string? Remark { get; set; }

    public string? RCode { get; set; }
}
