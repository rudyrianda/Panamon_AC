using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class AdditionalBreakTime
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly? BreakTime1Start { get; set; }

    public TimeOnly? BreakTime1End { get; set; }

    public TimeOnly? BreakTime2Start { get; set; }

    public TimeOnly? BreakTime2End { get; set; }

    public DateTime CreatedAt { get; set; }
}
