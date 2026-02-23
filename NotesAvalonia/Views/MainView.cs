using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    ScrollViewer? scrollViewer;
    MainViewModel? viewModel => DataContext as MainViewModel;

    public MainView()
    {
        InitializeComponent();
        Loaded += MainView_Loaded;

        try
        {
            InitCommunicatorBasedOnConfig();
        }
        catch (Exception ex)
        {
            ShowPopup("Initialization Error", $"Failed to initialize communicator: {ex.ToString()}");
        }

        // Set platform ui scale
        var layoutTransformControl = this.GetLogicalDescendants()
            .OfType<LayoutTransformControl>()
            .FirstOrDefault();
        if (layoutTransformControl != null)
            layoutTransformControl.LayoutTransform = new Avalonia.Media.ScaleTransform(Globals.LayoutScale, Globals.LayoutScale);

        Handle_AndroidCompat_On_Constructor();
    }

    void ShowPopup(string title, string message)
    {
        // Create a new window
        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, // Centers text horizontally
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,     // Centers text vertically
            Width = 120,
            Height = 30
        };
        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var grid = new Grid
        {
            Margin = new Thickness(10),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = new GridLength(40) },
            }
        };
        grid.Children.Add(message.Length > 1000 ? new ScrollViewer { Content = textBlock, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch } : textBlock);
        grid.Children.Add(button);
        Grid.SetRow(grid.Children[0], 0);
        Grid.SetRow(grid.Children[1], 1);
        var popupWindow = new Window
        {
            Title = title,
            //CanResize = false,
            Content = grid,
            Width = 400,
            Height = 115,
            Padding = new Thickness(10)
        };
        button.Click += (s, e) => popupWindow.Close();

        if (Globals.IsDesktop)
        {
            var window = this.GetVisualRoot() as Window;
            if (window != null)
                popupWindow.ShowDialog(window);
        }
        else popupWindow.Show();
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        Debug.WriteLine("MainView loaded!");
#if DEBUG
        var window = this.GetVisualRoot() as Window;
        window?.AttachDevTools();
#endif
        Handle_Communicator_On_MainView_Loaded(sender, e);

        // Handler
        this.AddHandler(
            InputElement.PointerReleasedEvent,
            MainView_PointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        this.AddHandler(
            InputElement.KeyDownEvent,
            MainView_KeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );

        Handle_AndroidCompat_On_MainView_Loaded(sender, e);
    }

    private void MainView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"MainView_PointerReleased: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");

        Handle_AndroidCompat_On_MainView_PointerReleased(sender, e);
        Handle_Reordering_On_MainView_PointerReleased(sender, e);
    }

    private void Border_ContextMenu_Close_Click(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
        var window = this.GetVisualRoot() as Window;
        window?.Close();
    }
}