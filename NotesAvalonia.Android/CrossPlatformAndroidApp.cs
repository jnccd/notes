using Android.App;
using Avalonia;
using Avalonia.Android;
using Android.Runtime;

namespace NotesAvalonia.Android;

[Application]
public class CrossPlatformAndroidApp : AvaloniaAndroidApplication<CrossPlatformAvaloniaApp>
{
    protected CrossPlatformAndroidApp(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}