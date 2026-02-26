using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.Globalization;

namespace NotesAvalonia.Converters;

public class TextBoxDoneRectangleFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            bool isChecked = System.Convert.ToBoolean(value);

            return isChecked ? new SolidColorBrush(Color.FromRgb(92, 92, 92)) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }
        catch (Exception e) { Debug.WriteLine(e); }
        return new SolidColorBrush(Color.FromRgb(0, 0, 0));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TextBoxDoneOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            bool isChecked = System.Convert.ToBoolean(value);

            return isChecked ? 0.3 : 1;
        }
        catch (Exception e) { Debug.WriteLine(e); }
        return 1;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TextBoxDoneGridAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            bool isChecked = System.Convert.ToBoolean(value);

            return isChecked ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Stretch;
        }
        catch (Exception e) { Debug.WriteLine(e); }
        return Avalonia.Layout.HorizontalAlignment.Stretch;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}