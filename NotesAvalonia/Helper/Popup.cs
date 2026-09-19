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

    /// <summary>Shows a popup for editing a note's due timeframe: a date and a time for each end
    /// (either may be left empty, which leaves that end open) with Save, Clear and Cancel. Clearing
    /// delivers null/null; cancelling delivers nothing.</summary>
    public void ShowDueTimeframe(string title, DateTimeOffset? from, DateTimeOffset? to, Action<DateTimeOffset?, DateTimeOffset?> onSaved)
    {
        try
        {
            var editor = new DueTimeframeEditor(from, to);
            Action close = () => { };

            void Save()
            {
                if (!editor.TryRead(out var newFrom, out var newTo))
                    return; // the editor explains why not, and stays open
                close();
                onSaved(newFrom, newTo);
            }

            void Clear()
            {
                close();
                onSaved(null, null);
            }

            if (Globals.IsDesktop)
            {
                var content = editor.BuildContent(title, Save, Clear, () => close());
                currentWindow?.Close();
                currentWindow = new Window
                {
                    Title = title,
                    Content = content,
                    // The four pickers need their own width: a fixed size clipped and overlapped them
                    // until the window was dragged wider by hand.
                    SizeToContent = SizeToContent.WidthAndHeight,
                    // Content needs 420 plus the padding on both sides.
                    MinWidth = 440,
                    Padding = new Thickness(10)
                };
                close = () => currentWindow?.Close();
                currentWindow.ShowActivated = true;
                currentWindow.Show(OriginWindow!);
            }
            else
            {
                var content = editor.BuildContent(title, Save, Clear, () => close());
                currentFlyout?.Hide();
                currentFlyout = new Flyout
                {
                    Content = content,
                    Placement = PlacementMode.Center,
                    ShowMode = FlyoutShowMode.Transient,
                };
                close = () => currentFlyout?.Hide();
                Flyout.SetAttachedFlyout(FlyoutOrigin!, currentFlyout);
                currentFlyout.ShowAt(FlyoutOrigin!);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    /// <summary>The pickers and messages of the due timeframe dialog.</summary>
    sealed class DueTimeframeEditor(DateTimeOffset? from, DateTimeOffset? to)
    {
        readonly DatePicker fromDate = new() { SelectedDate = from };
        readonly TimePicker fromTime = new() { SelectedTime = from?.TimeOfDay };
        readonly DatePicker toDate = new() { SelectedDate = to };
        readonly TimePicker toTime = new() { SelectedTime = to?.TimeOfDay };
        readonly TextBlock error = new()
        {
            Foreground = Avalonia.Media.Brushes.OrangeRed,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            IsVisible = false
        };

        public Control BuildContent(string title, Action onSave, Action onClear, Action onCancel)
        {
            var grid = new Grid
            {
                Margin = new Thickness(10),
                ColumnDefinitions = new ColumnDefinitions("Auto,*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
                RowSpacing = 8,
                ColumnSpacing = 8,
                // Also the width the flyout variant gets (it has no window to size itself from).
                MinWidth = 420
            };

            void AddRow(int row, string label, Control datePicker, Control timePicker)
            {
                var labelBlock = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                grid.Children.Add(labelBlock);
                grid.Children.Add(datePicker);
                grid.Children.Add(timePicker);
                Grid.SetRow(labelBlock, row);
                Grid.SetColumn(labelBlock, 0);
                Grid.SetRow(datePicker, row);
                Grid.SetColumn(datePicker, 1);
                Grid.SetRow(timePicker, row);
                Grid.SetColumn(timePicker, 2);
            }

            AddRow(1, "From", fromDate, fromTime);
            AddRow(2, "To", toDate, toTime);

            var saveButton = new Button { Content = "Save", Width = 90 };
            var clearButton = new Button { Content = "Clear", Width = 90 };
            var cancelButton = new Button { Content = "Cancel", Width = 90 };
            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            buttonRow.Children.Add(saveButton);
            buttonRow.Children.Add(clearButton);
            buttonRow.Children.Add(cancelButton);

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 18,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var hint = new TextBlock
            {
                Text = "Leave a field empty to leave that end open; a date without a time covers that whole day.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 11,
                Opacity = 0.7
            };

            grid.Children.Add(titleBlock);
            grid.Children.Add(hint);
            grid.Children.Add(error);
            grid.Children.Add(buttonRow);
            Grid.SetRow(titleBlock, 0);
            Grid.SetColumnSpan(titleBlock, 3);
            Grid.SetRow(hint, 3);
            Grid.SetColumnSpan(hint, 3);
            Grid.SetRow(error, 4);
            Grid.SetColumnSpan(error, 3);
            Grid.SetRow(buttonRow, 5);
            Grid.SetColumnSpan(buttonRow, 3);

            saveButton.Click += (_, _) => onSave();
            clearButton.Click += (_, _) => onClear();
            cancelButton.Click += (_, _) => onCancel();
            return grid;
        }

        /// <summary>Reads the two ends, or explains why not (a timeframe ending before it starts).</summary>
        public bool TryRead(out DateTimeOffset? from, out DateTimeOffset? to)
        {
            from = Combine(fromDate.SelectedDate, fromTime.SelectedTime, endOfDay: false);
            to = Combine(toDate.SelectedDate, toTime.SelectedTime, endOfDay: true);

            error.IsVisible = false;
            if (from is { } start && to is { } end && start > end)
            {
                error.Text = "The timeframe ends before it starts.";
                error.IsVisible = true;
                return false;
            }
            return true;
        }

        // A date without a time covers the whole day: the start of it for the first end, the very end
        // of it for the second.
        static DateTimeOffset? Combine(DateTimeOffset? date, TimeSpan? time, bool endOfDay)
        {
            if (date is not { } day)
                return null;

            var timeOfDay = time ?? (endOfDay ? new TimeSpan(23, 59, 59) : TimeSpan.Zero);
            return new DateTimeOffset(DateTime.SpecifyKind(day.Date + timeOfDay, DateTimeKind.Local));
        }
    }
}