using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class DetailNg
{
    public int DetailNgId { get; set; }

    public string DetailNgName { get; set; } = null!;

    public string? Remark { get; set; }

    public string? RCode { get; set; }
}
