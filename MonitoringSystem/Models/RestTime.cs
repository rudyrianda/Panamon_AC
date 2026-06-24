using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class RestTime
{
    public int Id { get; set; }

    public string? DayType { get; set; }

    public int? Duration { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }
}
