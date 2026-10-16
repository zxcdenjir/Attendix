using System;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendix.Services;

public partial class Settings : ObservableObject
{
    public static Settings Current { get; } = new();

    [ObservableProperty] public partial TimeOnly StartOfСlasses { get; set; } = new TimeOnly(9, 0);

    [ObservableProperty] public partial TimeOnly EndOfСlasses { get; set; } = new TimeOnly(16, 30);

    [ObservableProperty] public partial ThemeVariant Theme { get; set; } = ThemeVariant.Light;
}