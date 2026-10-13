using System;

namespace Attendix.Services;

public static class Settings
{
    public static TimeOnly StartOfСlasses { get; set; } = new TimeOnly(9, 0);
    public static TimeOnly EndOfСlasses { get; set; } = new TimeOnly(16, 30);
    
}