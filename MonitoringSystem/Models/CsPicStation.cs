using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class CsPicStation
{
    public int Id { get; set; }

    public int IdCard { get; set; }

    public string PicName { get; set; } = null!;

    public string CsStation { get; set; } = null!;
}
