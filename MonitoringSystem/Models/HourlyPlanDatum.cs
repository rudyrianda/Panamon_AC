using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class HourlyPlanDatum
{
    public int Id { get; set; }

    public string? MachineCode { get; set; }

    public DateOnly? SelectedDate { get; set; }

    public int? TotalPlan { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
