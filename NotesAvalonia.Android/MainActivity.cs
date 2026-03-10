using System;
using System.Linq;
using System.Net.Http;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Net;
using Android.Views;
using Android.Widget;
using AndroidX.Work;
using Avalonia;
using Avalonia.Android;
using Uri = Android.Net.Uri;

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

        // Add url open action
        var app = (CrossPlatformAvaloniaApp)Avalonia.Application.Current!;
        var mainView = ViewModels.ViewModelBase.MainView;
        mainView!.OpenUrlActionsOnSystem.Clear();
        mainView!.OpenUrlActionsOnSystem.Add(new(true, (url) =>
        {
            var intent = new Intent(Intent.ActionView, Uri.Parse(url));
            StartActivity(intent);
        }));
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
        if (app.MainViewModel.VirtualRoot == null || app.MainViewModel.VirtualRoot.SubNotes.Count == 0)
            return;

        var dataToShow = app.MainViewModel.VirtualRoot.SubtreeToStyledString() // TODO: Add a way to filter virtual root in data instead of string representation, and unify
                .Split('\n')
                .Skip(1)
                .Select(x => x[2..])
                .Aggregate((x, y) => x + "\n" + y);

        WidgetDataRepository.SaveData(this, dataToShow);
        WidgetDataRepository.RequestUpdate(this);
    }
}
