using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

    public void ShowPopup(string title, string message)
    {
        try
        {
            if (Globals.IsDesktop)
            {
                ShowPopupWindow(title, message);
            }
            else
            {
                ShowPopupFlyout(title, message);
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel model)
                model.AddDebugText($"Failed to show popup: {ex} {ex.StackTrace}");
        }
    }
    private void ShowPopupWindow(string title, string message)
    {
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

        var window = this.GetVisualRoot() as Window;
        if (window != null)
            popupWindow.ShowDialog(window);
    }
    private void ShowPopupFlyout(string title, string message)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 18,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var contentBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        var grid = new Grid
        {
            Margin = new Thickness(10),
            RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Star },
                },
            RowSpacing = 4,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        grid.Children.Add(titleBlock);
        grid.Children.Add(message.Length > 1000 ? new ScrollViewer { Content = contentBlock, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch } : contentBlock);
        Grid.SetRow(grid.Children[0], 0);
        Grid.SetRow(grid.Children[1], 1);

        var flyout = new Flyout
        {
            Content = grid,
            Placement = PlacementMode.Center,
            ShowMode = FlyoutShowMode.Transient,
        };
        var flyoutOrigin = this.FindControl<Border>("WindowBorder");
        Flyout.SetAttachedFlyout(flyoutOrigin!, flyout);
        flyout.ShowAt(flyoutOrigin!);
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
            InputElement.PointerPressedEvent,
            MainView_PointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
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

    private void MainView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"MainView_PointerPressed: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");
        Debug.WriteLine($"MainView_PointerPressed: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");

        foreach (var fnvm in viewModel?.FlattenedNotes ?? [])
        {
            fnvm.NotTemporarilyUnHidden = true;
        }
    }

    private void Border_ContextMenu_Close_Click(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
        var window = this.GetVisualRoot() as Window;
        window?.Close();
    }

    private void Note_Spoiler_Rectangle_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var rect = sender as Rectangle;
        var nvm = rect!.DataContext as FlattenedNoteViewModel;
        nvm!.NotTemporarilyUnHidden = false;
    }
}