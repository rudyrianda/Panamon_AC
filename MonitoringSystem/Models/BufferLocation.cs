using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class BufferLocation
{
    public int Id { get; set; }

    public string? Location { get; set; }

    public virtual ICollection<EvacondMasterDatum> EvacondMasterData { get; set; } = new List<EvacondMasterDatum>();
}
