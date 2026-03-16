using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NotesAvalonia.Helper;

public class Popup(Action<Exception>? OnError, Window? OriginWindow, Control? FlyoutOrigin)
{
    Window? currentWindow;
    Flyout? currentFlyout;

    public void Show(string title, string message, bool AlwaysAsFlyout = false)
    {
        try
        {
            if (Globals.IsDesktop && !AlwaysAsFlyout)
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
            OnError?.Invoke(ex);
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

        currentWindow?.Close();
        currentWindow = new Window
        {
            Title = title,
            //CanResize = false,
            Content = grid,
            Width = 400,
            Height = 115,
            Padding = new Thickness(10)
        };
        button.Click += (s, e) => currentWindow.Close();

        currentWindow.Show(OriginWindow!);
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

        currentFlyout?.Hide();
        currentFlyout = new Flyout
        {
            Content = grid,
            Placement = PlacementMode.Center,
            ShowMode = FlyoutShowMode.Transient,
        };
        Flyout.SetAttachedFlyout(FlyoutOrigin!, currentFlyout);
        currentFlyout.ShowAt(FlyoutOrigin!);
    }
}