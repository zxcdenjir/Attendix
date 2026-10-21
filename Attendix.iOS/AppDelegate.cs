using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;

namespace Attendix.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711 
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
