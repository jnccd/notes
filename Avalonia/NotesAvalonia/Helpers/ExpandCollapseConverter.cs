using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NotesAvalonia.Helpers;

public class ExpandCollapseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? "/Assets/expanded.svg" : "/Assets/collapsed.svg";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}