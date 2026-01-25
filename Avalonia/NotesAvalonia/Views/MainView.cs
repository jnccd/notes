using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NotesAvalonia.Helpers;
using NotesAvalonia.Models;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    ScrollViewer? scrollViewer;
    MainViewModel? viewModel => DataContext as MainViewModel;

    TextBox? focusedTextBox = null;
    DateTime lastTextBoxFocusTime = DateTime.MinValue;

    public MainView()
    {
        InitializeComponent();
        this.Loaded += MainView_Loaded;

        var layoutTransformControl = this.GetLogicalDescendants()
            .OfType<LayoutTransformControl>()
            .FirstOrDefault();
        if (layoutTransformControl != null)
            layoutTransformControl.LayoutTransform = new Avalonia.Media.ScaleTransform(Globals.LayoutScale, Globals.LayoutScale);

        Handle_AndroidCompat_On_Constructor();
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        Debug.WriteLine("MainView loaded!");

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

        // For reordering on mobile
        scrollViewer = this.GetLogicalDescendants()
            .OfType<ScrollViewer>()
            .First();
        scrollViewer.PropertyChanged += (s, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty && disableScrolling)
                scrollViewer.Offset = new Avalonia.Vector(0, lockedY);
        };

        Handle_AndroidCompat_On_MainView_Loaded(sender, e);
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!Globals.IsDesktop)
        {
            if (Convert.ToInt32(scrollViewer?.Offset.ToString().Last()) % 5 != 0)
                return;
            var model = DataContext as MainViewModel;
            if (model != null)
                model.AddDebugText($"ScrollViewer_ScrollChanged: ViewportDelta={e.ViewportDelta}, Offset={scrollViewer?.Offset}");
            if (!Globals.IsDesktop || true)
            {
                var textBoxes = this.GetLogicalDescendants()
                        .OfType<TextBox>();
                foreach (var tb in textBoxes)
                {
                    tb.Focusable = false;
                    tb.AddHandler(
                        InputElement.PointerPressedEvent,
                        TextBox_PointerPressed,
                        RoutingStrategies.Tunnel | RoutingStrategies.Bubble
                    );
                }
            }
        }
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
        var window = this.GetVisualRoot() as Window;
        window?.Close();
    }
}