using System;
using System.Collections.Generic;

namespace DeviceDB;

public partial class DeviceConfig
{
    public string ConfigId { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string Type { get; set; } = null!;
}
