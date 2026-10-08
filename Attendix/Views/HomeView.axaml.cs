using Attendix.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace Attendix.Views;

public partial class HomeView : ContentPage
{
    public HomeView()
    {
        InitializeComponent();
        DataContext = new HomeViewModel();
    }
}