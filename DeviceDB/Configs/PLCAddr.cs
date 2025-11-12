using System;
using System.Collections.Generic;
namespace DeviceDB;

public partial class PLCAddr
{
    public string Id { get; set; } = null!;

    public string? Title { get; set; }

    public string? Group { get; set; }

    public int? StationId { get; set; }

    public string? Address { get; set; }

    public string? Tag { get; set; }
}
