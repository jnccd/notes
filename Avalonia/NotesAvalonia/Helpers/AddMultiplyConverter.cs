using Avalonia.Data.Converters;
using System;
using System.Diagnostics;
using System.Globalization;

namespace NotesAvalonia.Helpers;

public class AddMultiplyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            double width = System.Convert.ToDouble(value);
            string[] @params = (parameter as string)!.Split(",");

            double.TryParse(@params[0], out var add);
            double.TryParse(@params[1], out var mult);

            return Math.Max(0, (width + add) * mult);
        }
        catch (Exception e) { Debug.WriteLine(e); }
        return 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}