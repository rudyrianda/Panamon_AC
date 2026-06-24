using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Product
{
    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public string? MachineCode { get; set; }

    public string? Marking { get; set; }

    public string? ProductId { get; set; }

    public virtual Machine? MachineCodeNavigation { get; set; }
}
