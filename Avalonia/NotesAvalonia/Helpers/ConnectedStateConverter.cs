using Avalonia.Data.Converters;
using Notes.Interface;
using System;
using System.Globalization;

namespace NotesAvalonia.Helpers;

public class ConnectedStateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is CommsState cs && cs == CommsState.Disconnected ? "Disconnected" : "Connected";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}