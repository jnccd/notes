using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using NotesAvalonia.ViewModels;
using NotesAvalonia.Views;
using System;
using System.IO;
using Avalonia.Controls;
using NotesAvalonia.Configuration;
using Notes.Interface;
using System.Collections.Generic;

namespace NotesAvalonia;

public partial class CrossPlatformAvaloniaApp : Application
{
    public MainViewModel MainViewModel { get; private set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
        // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
        DisableAvaloniaDataAnnotationValidation();

        // Init config
        Config.Load();
        if (Config.Data.Notes == null || Config.Data.Notes.Count == 0)
        {
            Config.Data.Notes = new List<Note>() { new Note() };
        }

        // Init platform specific wrapper element
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = MainViewModel
            };
            MainViewModel.MainView = (MainView)singleViewPlatform.MainView;
        }
        else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Content = new MainView
                {
                    DataContext = MainViewModel
                },
                Position = Config.Data.Pos ?? new PixelPoint(100, 100),
                Width = Config.Data.Width ?? Globals.InitialWindowSize.X,
                Height = Config.Data.Height ?? Globals.InitialWindowSize.Y,
                ShowInTaskbar = false,
                Title = "Notes",
                ExtendClientAreaToDecorationsHint = true,
                ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome,
                ExtendClientAreaTitleBarHeightHint = -1,
                SystemDecorations = SystemDecorations.None,
                Clip = new Avalonia.Media.RectangleGeometry
                {
                    Rect = new Avalonia.Rect(0, 0, Config.Data.Width ?? Globals.InitialWindowSize.X, Config.Data.Height ?? Globals.InitialWindowSize.Y),
                    RadiusX = Globals.WindowBorderRadius,
                    RadiusY = Globals.WindowBorderRadius
                }
            };
            MainViewModel.MainView = (MainView)desktop.MainWindow.Content;
        }
        else
        {
            File.AppendAllText("error.log", "ApplicationLifetime start failed!\n");
            throw new NotSupportedException("ApplicationLifetime start failed!");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}