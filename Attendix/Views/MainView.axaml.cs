using System;
using Attendix.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Ursa.Controls;

namespace Attendix.Views;

public partial class MainView : UrsaNavigationPage
{
    public static MainView Current { get; private set; } = null!;

    private WindowToastManager _toastManager = null!;

    public MainView()
    {
        InitializeComponent();
        Current = this;
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        _toastManager = new WindowToastManager(TopLevel.GetTopLevel(this))
        {
            Margin = new Thickness(0, EffectiveBarHeight, 0, 0),
        };
        
        PropertyChanged += OnMainViewPropertyChanged;
        
        await PushAsync(new HomeView());
    }
    
    public void ShowToast(string message, NotificationType type)
    {
        _toastManager.Show(
            content: message,
            showIcon: true,
            showClose: true,
            type: type,
            expiration: TimeSpan.FromSeconds(3),
            classes: ["Light"]);
    }
    
    private void OnMainViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == EffectiveBarHeightProperty && e.NewValue is double height)
        {
            _toastManager.Margin = new Thickness(0, height, 0, 0);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        PropertyChanged -= OnMainViewPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }
}