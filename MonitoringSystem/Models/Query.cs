using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Query
{
    public string? ProductId { get; set; }

    public string? Marking { get; set; }

    public string ProductName { get; set; } = null!;

    public string MachineName { get; set; } = null!;

    public string? Description { get; set; }

    public string? ProdPlanDay { get; set; }

    public string? Sut { get; set; }

    public string? NoOfOperator { get; set; }

    public string? QtyHour { get; set; }

    public string? ProdHeadHour { get; set; }

    public string? CycleTimeVaccum { get; set; }

    public string? WorkHour { get; set; }
}
