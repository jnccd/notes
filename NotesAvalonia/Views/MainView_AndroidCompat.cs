using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    DateTime lastTextBoxPointerPressed = DateTime.MinValue;

    TextBox? focusedTextBox = null;
    DateTime lastTextBoxFocusTime = DateTime.MinValue;

    private void Handle_AndroidCompat_On_Constructor()
    {
        if (!Globals.IsDesktop)
        {
            var windowBorder = this.GetLogicalDescendants()
                .OfType<Border>()
                .FirstOrDefault(x => x.Name == "WindowBorder");
            if (windowBorder != null)
                windowBorder.ContextMenu = null;
        }
    }

    private void Handle_AndroidCompat_On_MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (!Globals.IsDesktop)
        {
            // Mobile scrolling without triggering textboxes
            var textBoxes = this.GetLogicalDescendants()
                .OfType<TextBox>();
            foreach (var tb in textBoxes)
            {
                tb.Focusable = false;
            }

            // No scroll while dragging
            scrollViewer = this.GetLogicalDescendants()
                .OfType<ScrollViewer>()
                .First();
            scrollViewer.PropertyChanged += (s, e) =>
            {
                if (e.Property == ScrollViewer.OffsetProperty && disableScrolling)
                    scrollViewer.Offset = new Avalonia.Vector(0, lockedY);
            };
        }
    }

    private void TextBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DateTime.Now - lastTextBoxPointerPressed < TimeSpan.FromMilliseconds(50))
            return;
        lastTextBoxPointerPressed = DateTime.Now;

        var model = DataContext as MainViewModel;
        var tb = sender as TextBox;
        if (model != null)
            model.AddDebugText($"TextBox_PointerPressed: Text={tb?.Text} LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure}");

        focusedTextBox = tb;
        lastTextBoxFocusTime = DateTime.Now;
        if (model != null)
            model.AddDebugText($"setting focus... {lastTextBoxFocusTime}");
    }

    private void Handle_AndroidCompat_On_MainView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (DateTime.Now - lastTextBoxFocusTime < TimeSpan.FromMilliseconds(400))
        {
            if (model != null)
                model.AddDebugText($"TRYING TO FOCUS HNGGGGGGGG!!");
            if (focusedTextBox != null)
                focusedTextBox.Focusable = true;
            focusedTextBox?.Focus();
        }
        else
        {
            if (model != null)
                model.AddDebugText($"focus too old... {lastTextBoxFocusTime} {DateTime.Now}");
        }
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!Globals.IsDesktop)
        {
            var model = DataContext as MainViewModel;
            if (model != null)
                model.AddDebugText($"ScrollViewer_ScrollChanged: ViewportDelta={e.ViewportDelta}, Offset={scrollViewer?.Offset}");

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