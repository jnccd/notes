using System;
using System.Linq;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Work;
using Avalonia;
using Avalonia.Android;

namespace NotesAvalonia.Android;

[Activity(
    Label = "Notes",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<CrossPlatformAvaloniaApp>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        WidgetUpdateWorker.Init(this);

        // Make the window resize when the keyboard appears
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
    }

    protected override void OnResume()
    {
        base.OnResume();
        UpdateWidget();
    }

    override protected void OnPause()
    {
        base.OnPause();
        UpdateWidget();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    void UpdateWidget()
    {
        var app = (CrossPlatformAvaloniaApp)Avalonia.Application.Current!;
        var dataToShow = app.MainViewModel.FlattenedNotes.Count > 0 ?
        app.MainViewModel.FlattenedNotes
            .Select(x =>
                Enumerable
                    .Repeat("  ", (int)x.Depth)
                    .Aggregate((x, y) => x + y)
                + " " +
                (x.Expanded ? "▼" : "▶") +
                (x.Done ?
                    x.Text.Select(x => x + "" + (char)822).Aggregate((x, y) => x + y) : // Cross through if done
                    x.Text))
            .Aggregate((x, y) => x + "\n" + y)
        : "No notes available.";
        WidgetDataRepository.SaveData(this, dataToShow);
        WidgetDataRepository.RequestUpdate(this);
    }
}
