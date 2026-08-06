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
        // Init config
        Config.Load();

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
                ExtendClientAreaTitleBarHeightHint = -1,
                WindowDecorations = WindowDecorations.None,
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
}