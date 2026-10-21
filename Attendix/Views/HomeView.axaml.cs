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

    // Флай не закрывается сам при открытии диалога. Прятать его можно только ПОСЛЕ
    // показа диалога (HomeViewModel вызывает это через диспетчер): синхронное
    // скрытие в клике мешает Ursa найти хост оверлея, и диалог не открывается
    public void HideSettingsFlyout() => SettingsButton.Flyout?.Hide();
}