using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NotesAvalonia.Helper;

public class Popup(Action<Exception>? OnError, Window? OriginWindow, Control? FlyoutOrigin)
{
    Window? currentWindow;
    Flyout? currentFlyout;

    public void Show(string title, string message, bool AlwaysAsFlyout = false, bool TakeFocus = true, bool SelectableText = false)
    {
        try
        {
            if (Globals.IsDesktop && !AlwaysAsFlyout)
            {
                ShowPopupWindow(title, message, TakeFocus, SelectableText);
            }
            else
            {
                ShowPopupFlyout(title, message, SelectableText);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    // Builds the message content: a plain TextBlock, or a read-only but selectable/copyable TextBox
    // (used for log output so the text can be copied out).
    static Control BuildMessageContent(string message, bool selectable)
    {
        if (!selectable)
            return new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        return new TextBox
        {
            Text = message,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
    }

    private void ShowPopupWindow(string title, string message, bool TakeFocus = true, bool selectable = false)
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
        var messageContent = BuildMessageContent(message, selectable);
        var grid = new Grid
        {
            Margin = new Thickness(10),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = new GridLength(40) },
            }
        };
        // TextBlocks scroll in a ScrollViewer when long; a selectable TextBox scrolls itself.
        Control messageHost = (message.Length > 1000 && !selectable)
            ? new ScrollViewer { Content = messageContent, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch }
            : messageContent;
        grid.Children.Add(messageHost);
        grid.Children.Add(button);
        Grid.SetRow(grid.Children[0], 0);
        Grid.SetRow(grid.Children[1], 1);

        currentWindow?.Close();
        currentWindow = new Window
        {
            Title = title,
            //CanResize = false,
            Content = grid,
            Width = selectable ? 560 : 400,
            Height = selectable ? 360 : 115,
            Padding = new Thickness(10)
        };
        button.Click += (s, e) => currentWindow.Close();

        currentWindow.ShowActivated = TakeFocus;
        currentWindow.Show(OriginWindow!);
        if (selectable && messageContent is TextBox logTextBox)
            logTextBox.Focus();
    }

    private void ShowPopupFlyout(string title, string message, bool selectable = false)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 18,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var messageContent = BuildMessageContent(message, selectable);
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
        Control contentHost = (message.Length > 1000 && !selectable)
            ? new ScrollViewer { Content = messageContent, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch }
            : messageContent;
        grid.Children.Add(contentHost);
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

    /// <summary>Shows a popup with a multi-line text input and OK/Cancel buttons. The entered text
    /// (or null when cancelled) is delivered through <paramref name="onResult"/>.</summary>
    public void ShowTextInput(string title, string label, string initialText, Action<string?> onResult)
    {
        try
        {
            if (Globals.IsDesktop)
                ShowTextInputWindow(title, label, initialText, onResult);
            else
                ShowTextInputFlyout(title, label, initialText, onResult);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    private void ShowTextInputWindow(string title, string label, string initialText, Action<string?> onResult)
    {
        var textBox = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 120
        };
        var okButton = new Button { Content = "OK", Width = 90 };
        var cancelButton = new Button { Content = "Cancel", Width = 90 };
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttonRow.Children.Add(okButton);
        buttonRow.Children.Add(cancelButton);

        var labelBlock = new TextBlock
        {
            Text = label,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var grid = new Grid
        {
            Margin = new Thickness(10),
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            RowSpacing = 8
        };
        grid.Children.Add(labelBlock);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonRow);
        Grid.SetRow(labelBlock, 0);
        Grid.SetRow(textBox, 1);
        Grid.SetRow(buttonRow, 2);

        currentWindow?.Close();
        currentWindow = new Window
        {
            Title = title,
            Content = grid,
            Width = 520,
            Height = 320,
            Padding = new Thickness(10)
        };
        okButton.Click += (s, e) =>
        {
            var result = textBox.Text;
            currentWindow.Close();
            onResult(result);
        };
        cancelButton.Click += (s, e) =>
        {
            currentWindow.Close();
            onResult(null);
        };
        currentWindow.ShowActivated = true;
        currentWindow.Show(OriginWindow!);
        textBox.Focus();
    }

    private void ShowTextInputFlyout(string title, string label, string initialText, Action<string?> onResult)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 18,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var textBox = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 120
        };
        var okButton = new Button { Content = "OK" };
        var cancelButton = new Button { Content = "Cancel" };
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttonRow.Children.Add(okButton);
        buttonRow.Children.Add(cancelButton);

        var grid = new Grid
        {
            Margin = new Thickness(10),
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            RowSpacing = 8,
            MinWidth = 300
        };
        grid.Children.Add(titleBlock);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonRow);
        Grid.SetRow(titleBlock, 0);
        Grid.SetRow(textBox, 1);
        Grid.SetRow(buttonRow, 2);

        okButton.Click += (s, e) =>
        {
            var result = textBox.Text;
            currentFlyout?.Hide();
            onResult(result);
        };
        cancelButton.Click += (s, e) =>
        {
            currentFlyout?.Hide();
            onResult(null);
        };

        currentFlyout?.Hide();
        currentFlyout = new Flyout
        {
            Content = grid,
            Placement = PlacementMode.Center,
            ShowMode = FlyoutShowMode.Transient,
        };
        Flyout.SetAttachedFlyout(FlyoutOrigin!, currentFlyout);
        currentFlyout.ShowAt(FlyoutOrigin!);
        textBox.Focus();
    }
}