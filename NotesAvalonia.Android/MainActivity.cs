using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Uri = Android.Net.Uri;

namespace NotesAvalonia.Android;

[Activity(
    Label = "Notes",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
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

    void UpdateWidget()
    {
        try
        {
            var app = (CrossPlatformAvaloniaApp)Avalonia.Application.Current!;
            if (app.MainViewModel.VirtualRoot == null)
                return;

            var widgetText = WidgetDataRepository.BuildWidgetText(app.MainViewModel.VirtualRoot);
            if (widgetText == null)
                return; // nothing to show yet; keep whatever the widget currently displays

            WidgetDataRepository.SaveData(this, widgetText);
            WidgetDataRepository.RequestUpdate(this);
        }
        catch (Exception ex)
        {
            // Never let a widget hiccup disturb the activity lifecycle.
            try { Notes.Interface.Logger.WriteLine(DateTime.Now.ToString() + $": Failed to update widget {ex}\n"); } catch { }
        }
    }
}
