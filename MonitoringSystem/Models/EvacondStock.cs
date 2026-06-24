using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class EvacondStock
{
    public int Id { get; set; }

    public DateOnly? Date { get; set; }

    public string? Location { get; set; }

    public string? Model { get; set; }

    public string? Type { get; set; }

    public int? Stock { get; set; }

    public string? Issue { get; set; }

    public string? Remark { get; set; }
}
