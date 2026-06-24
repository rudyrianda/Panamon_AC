using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class EmailTrigger
{
    public int Id { get; set; }

    public DateOnly? Date { get; set; }

    public int? Flag { get; set; }

    public decimal? Threshold { get; set; }
}
