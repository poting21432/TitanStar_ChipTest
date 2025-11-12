using System;
using System.Collections.Generic;
namespace DeviceDB;
public partial class Config
{
    public string Id { get; set; } = null!;
    public string? Group { get; set; }

    public string? Title { get; set; }

    public string? Type { get; set; }

    public string? Value { get; set; }
}
