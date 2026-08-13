using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class AdditionalBreakTime
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
