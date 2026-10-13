using System.Threading.Tasks;
using Avalonia.Controls;
using Ursa.Controls;

namespace Attendix.Views;

public partial class MainWindow : UrsaWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    protected override async Task<bool> CanClose()
    {
        var result = await OverlayMessageBox.ShowAsync("Вы уверены, что хотите выйти?", "Выход", button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}