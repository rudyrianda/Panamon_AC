using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class ProductionPlan
{
    public int Id { get; set; }

    public DateOnly? CurrentDate { get; set; }

    public string? CommentCu { get; set; }

    public string? CommentCs { get; set; }

    public virtual ICollection<ProductionRecord> ProductionRecords { get; set; } = new List<ProductionRecord>();

    public virtual ICollection<SapPlan> SapPlans { get; set; } = new List<SapPlan>();
}
