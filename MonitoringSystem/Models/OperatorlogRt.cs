using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class OperatorlogRt
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? Edate { get; set; }

    public int? PId { get; set; }

    public string MachineId { get; set; } = null!;

    public string? NumbOfopr { get; set; }

    public int? NumbOfProd { get; set; }

    public int? WorkTime { get; set; }

    public int? ReasonId { get; set; }

    public int? Status { get; set; }
}
