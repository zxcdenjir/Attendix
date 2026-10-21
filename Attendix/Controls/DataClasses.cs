using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendix.Controls;

public sealed class DataClasses : AvaloniaObject
{
    public static readonly AttachedProperty<bool> LateProperty =
        AvaloniaProperty.RegisterAttached<DataClasses, Control, bool>("Late");

    public static readonly AttachedProperty<bool> EarlyProperty =
        AvaloniaProperty.RegisterAttached<DataClasses, Control, bool>("Early");

    public static readonly AttachedProperty<bool> AbsentProperty =
        AvaloniaProperty.RegisterAttached<DataClasses, Control, bool>("Absent");

    static DataClasses()
    {
        LateProperty.Changed.AddClassHandler<Control, bool>((c, e) => c.Classes.Set("late", e.NewValue.Value));
        EarlyProperty.Changed.AddClassHandler<Control, bool>((c, e) => c.Classes.Set("early", e.NewValue.Value));
        AbsentProperty.Changed.AddClassHandler<Control, bool>((c, e) => c.Classes.Set("absent", e.NewValue.Value));
    }

    public static bool GetLate(Control c) => c.GetValue(LateProperty);
    public static void SetLate(Control c, bool value) => c.SetValue(LateProperty, value);

    public static bool GetEarly(Control c) => c.GetValue(EarlyProperty);
    public static void SetEarly(Control c, bool value) => c.SetValue(EarlyProperty, value);

    public static bool GetAbsent(Control c) => c.GetValue(AbsentProperty);
    public static void SetAbsent(Control c, bool value) => c.SetValue(AbsentProperty, value);
}