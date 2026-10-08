using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Ursa.Controls;

namespace Attendix.Controls;

public class UrsaNavigationPage : NavigationPage
{
    /// <summary>Имя диалогового хоста (для консистентности с UrsaView / UrsaWindow).</summary>
    public const string PART_DialogHost = "PART_DialogHost";

    public const int DefaultDialogHostZIndex = 1000;

    public static readonly StyledProperty<bool> IsDialogHostTopLevelProperty =
        AvaloniaProperty.Register<UrsaNavigationPage, bool>(
            nameof(IsDialogHostTopLevel), defaultValue: true);

    public static readonly StyledProperty<int> DialogHostZIndexProperty =
        AvaloniaProperty.Register<UrsaNavigationPage, int>(
            nameof(DialogHostZIndex), defaultValue: DefaultDialogHostZIndex);

    /// <summary>
    /// true  — OverlayDialogHost покрывает весь TopLevel (окно) как в UrsaWindow;
    /// false — OverlayDialogHost покрывает только границы этого NavigationPage.
    /// </summary>
    public bool IsDialogHostTopLevel
    {
        get => GetValue(IsDialogHostTopLevelProperty);
        set => SetValue(IsDialogHostTopLevelProperty, value);
    }

    /// <summary>ZIndex, с которым OverlayDialogHost добавляется в корневую Panel.</summary>
    public int DialogHostZIndex
    {
        get => GetValue(DialogHostZIndexProperty);
        set => SetValue(DialogHostZIndexProperty, value);
    }

    /// <summary>Доступ к созданному OverlayDialogHost (null до ApplyTemplate).</summary>
    public OverlayDialogHost? DialogHost => _dialogHost;

    private OverlayDialogHost? _dialogHost;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_dialogHost is not null)
        {
            if (_dialogHost.Parent is Panel parentPanel)
                parentPanel.Children.Remove(_dialogHost);
            LogicalChildren.Remove(_dialogHost);
            _dialogHost = null;
        }

        base.OnApplyTemplate(e);

        var rootPanel = VisualChildren.OfType<Panel>().FirstOrDefault();

        if (rootPanel is null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                "UrsaNavigationPage: корневой элемент шаблона NavigationPage не является Panel. " +
                "OverlayDialogHost не создан.");
#endif
            return;
        }

        _dialogHost = new OverlayDialogHost
        {
            Name = PART_DialogHost,
            IsTopLevel = IsDialogHostTopLevel,
        };

        rootPanel.Children.Add(_dialogHost);
        _dialogHost.ZIndex = DialogHostZIndex;

        LogicalChildren.Add(_dialogHost);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsDialogHostTopLevelProperty && _dialogHost is not null)
        {
            _dialogHost.IsTopLevel = IsDialogHostTopLevel;
        }
    }
}