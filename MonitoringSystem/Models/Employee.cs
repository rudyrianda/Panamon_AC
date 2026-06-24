using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public string EmployeeName { get; set; } = null!;

    public string? Division { get; set; }
}
