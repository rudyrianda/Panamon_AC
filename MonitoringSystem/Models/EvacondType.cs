using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class EvacondType
{
    public int Id { get; set; }

    public string? Type { get; set; }

    public virtual ICollection<EvacondMasterDatum> EvacondMasterData { get; set; } = new List<EvacondMasterDatum>();
}
