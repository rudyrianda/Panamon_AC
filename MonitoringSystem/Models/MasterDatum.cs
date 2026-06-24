using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class MasterDatum
{
    public string? ProductId { get; set; }

    public string? Marking { get; set; }

    public string? ProductName { get; set; }

    public string? MachineCode { get; set; }

    public string? Description { get; set; }

    public int? ProdPlan { get; set; }

    public int? Sut { get; set; }

    public int? NoOfOperator { get; set; }

    public int? QtyHour { get; set; }

    public int? ProdHeadHour { get; set; }

    public int? CycleTimeVacum { get; set; }

    public int? WorkHour { get; set; }
}
