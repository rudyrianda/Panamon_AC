using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class EvacondMasterDatum
{
    public int Id { get; set; }

    public string? Model { get; set; }

    public int? BufferLocation { get; set; }

    public string? CodePart { get; set; }

    public int? Type { get; set; }

    public virtual BufferLocation? BufferLocationNavigation { get; set; }

    public virtual EvacondType? TypeNavigation { get; set; }
}
