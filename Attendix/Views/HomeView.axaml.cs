using Attendix.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DocumentFormat.OpenXml.Drawing.Charts;

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
}