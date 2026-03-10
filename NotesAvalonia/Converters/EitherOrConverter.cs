using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NotesAvalonia.Converters;

public class EitherOrConverter : IValueConverter
{

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            string[] @params = (parameter as string)!.Split("|");

            var obj1 = System.Convert.ChangeType(@params[0], targetType);
            var obj2 = System.Convert.ChangeType(@params[1], targetType);

            return (value is bool b) && b ? obj1 : obj2;
        }
        catch (Exception e) { Debug.WriteLine(e); }
        return default;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}