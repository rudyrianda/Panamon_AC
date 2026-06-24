using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ActionDefect
{
    public int ActionId { get; set; }

    public string ActionName { get; set; } = null!;

    public string? Remark { get; set; }

    public string? RCode { get; set; }
}
