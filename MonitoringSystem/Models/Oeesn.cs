using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Oeesn
{
    public DateTime? Date { get; set; }

    public DateTime? Sdate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? ProductTime { get; set; }

    public decimal? TotalDownTime { get; set; }

    public decimal? TargetUnit { get; set; }

    public decimal? GoodUnit { get; set; }

    public decimal? EjectUnit { get; set; }

    public decimal? TotalUnit { get; set; }

    public decimal? Oee { get; set; }

    public decimal? Availability { get; set; }

    public decimal? Performance { get; set; }

    public decimal? Quality { get; set; }

    public int? CycleTime { get; set; }

    public string? MachineCode { get; set; }

    public string? ProductId { get; set; }

    public int? NoOfOperator { get; set; }

    public decimal? PTarget { get; set; }

    public decimal? PActual { get; set; }

    public decimal? IdleTime { get; set; }

    public string? SnGood { get; set; }

    public int Id { get; set; }

    public string? ShiftMode { get; set; }
}
