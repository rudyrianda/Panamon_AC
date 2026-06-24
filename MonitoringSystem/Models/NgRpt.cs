using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class NgRpt
{
    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? MachineCode { get; set; }

    public int? ProductId { get; set; }

    public string? SerialNumber { get; set; }

    public string? Pic { get; set; }

    public string? Cause { get; set; }

    public string? Detail { get; set; }

    public string? Defect { get; set; }

    public string? ActionDefect { get; set; }

    public string? ConfirmByLeader { get; set; }

    public string? Station { get; set; }
}
