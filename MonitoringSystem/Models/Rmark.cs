using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Rmark
{
    public DateOnly? Date { get; set; }

    public string? Remark { get; set; }

    public string? Note { get; set; }

    public string? MachineCode { get; set; }
}
