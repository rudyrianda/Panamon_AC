using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ReasonNgc
{
    public int ReasonNgId { get; set; }

    public string ReasonNgName { get; set; } = null!;

    public string? Remark { get; set; }

    public string? RCode { get; set; }
}
