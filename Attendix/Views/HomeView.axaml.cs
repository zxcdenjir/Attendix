using Attendix.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Attendix.Views;

public partial class HomeView : ContentPage
{
    public static HomeView Current { get; private set; } = null!;
    
    public HomeView()
    {
        InitializeComponent();
        Current = this;
        DataContext = new HomeViewModel();
    }

    public void HideSettingsFlyout() => SettingsButton.Flyout?.Hide();
}