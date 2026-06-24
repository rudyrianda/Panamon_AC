using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Sap
{
    public int Id { get; set; }

    public string Model { get; set; } = null!;

    public DateOnly Date { get; set; }

    public int Plan { get; set; }

    public int Result { get; set; }

    public int Buffer { get; set; }

    public double DifferentPercentage { get; set; }

    public double PlanPercentage { get; set; }

    public double FullfillmentPercentage { get; set; }
}
