using System;
using System.Globalization;
using AutoCinema.Pro.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AutoCinema.Desktop.Converters;

public class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Pending => Brushes.Gray,
                ProjectStatus.Processing => Brushes.SkyBlue,
                ProjectStatus.Completed => Brushes.LightGreen,
                ProjectStatus.Failed => Brushes.IndianRed,
                _ => Brushes.White
            };
        }

        return Brushes.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
