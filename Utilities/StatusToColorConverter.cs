using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZedASAManager.Models;

namespace ZedASAManager.Utilities;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ServerStatus status)
        {
            return status switch
            {
                ServerStatus.Online => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                ServerStatus.Offline => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                ServerStatus.Busy => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
