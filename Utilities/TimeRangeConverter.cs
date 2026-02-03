using System.Globalization;
using System.Windows.Data;
using ZedASAManager.ViewModels;

namespace ZedASAManager.Utilities;

public class TimeRangeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChartViewModel.TimeRange timeRange)
        {
            return timeRange switch
            {
                ChartViewModel.TimeRange.Hours24 => "24 óra",
                ChartViewModel.TimeRange.Week1 => "1 hét",
                ChartViewModel.TimeRange.Days30 => "30 nap",
                ChartViewModel.TimeRange.Months3 => "3 hónap",
                ChartViewModel.TimeRange.Months6 => "6 hónap",
                ChartViewModel.TimeRange.Year1 => "1 év",
                ChartViewModel.TimeRange.Years2 => "2 év",
                ChartViewModel.TimeRange.Years3 => "3 év",
                _ => timeRange.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
