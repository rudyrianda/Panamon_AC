using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class CsMasterDatum
{
    public int Id { get; set; }

    public string? Model { get; set; }

    public string? Idcompresor { get; set; }

    public string? IdfanMotor { get; set; }

    public string? Tipe { get; set; }

    public string? KodePart { get; set; }

    public string? SerialNumber { get; set; }

    public string? KodePartCb { get; set; }
}
