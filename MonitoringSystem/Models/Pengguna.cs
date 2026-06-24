using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models;

public partial class Pengguna
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string? Password { get; set; }
}
