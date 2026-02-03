using System.Collections.ObjectModel;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class ChartViewModel : ViewModelBase
{
    private int _maxDataPoints = 3600; // Alapértelmezett: 1 óra (3600 másodperc)
    private TimeSpan _selectedTimeRange = TimeSpan.FromHours(24);

    public enum TimeRange
    {
        Hours24,
        Week1,
        Days30,
        Months3,
        Months6,
        Year1,
        Years2,
        Years3
    }

    private TimeRange _currentTimeRange = TimeRange.Hours24;

    public ChartViewModel()
    {
        // Don't initialize series here - wait until connection is established
        // Series will be initialized via InitializeSeries() when connected
        
        Labels = new ObservableCollection<string>();
        Formatter = value => value.ToString("F1");
        
        // Initialize time range
        UpdateTimeRange();
    }

    private SeriesCollection? _cpuSeries;
    private SeriesCollection? _memorySeries;
    private SeriesCollection? _diskSeries;
    private SeriesCollection? _networkSeries;

    public SeriesCollection? CpuSeries 
    { 
        get => _cpuSeries;
        private set => SetProperty(ref _cpuSeries, value);
    }
    
    public SeriesCollection? MemorySeries 
    { 
        get => _memorySeries;
        private set => SetProperty(ref _memorySeries, value);
    }
    
    public SeriesCollection? DiskSeries 
    { 
        get => _diskSeries;
        private set => SetProperty(ref _diskSeries, value);
    }
    
    public SeriesCollection? NetworkSeries 
    { 
        get => _networkSeries;
        private set => SetProperty(ref _networkSeries, value);
    }
    
    public ObservableCollection<string> Labels { get; }
    public Func<double, string> Formatter { get; }

    public void InitializeSeries()
    {
        if (_cpuSeries != null)
            return; // Already initialized

        _cpuSeries = new SeriesCollection
        {
            new LineSeries
            {
                Title = LocalizationHelper.GetString("cpu_usage"),
                Values = new ChartValues<double>(),
                Stroke = Brushes.Blue,
                Fill = Brushes.Transparent,
                PointGeometry = null
            }
        };

        _memorySeries = new SeriesCollection
        {
            new LineSeries
            {
                Title = LocalizationHelper.GetString("memory_usage"),
                Values = new ChartValues<double>(),
                Stroke = Brushes.Green,
                Fill = Brushes.Transparent,
                PointGeometry = null
            }
        };

        _diskSeries = new SeriesCollection
        {
            new LineSeries
            {
                Title = LocalizationHelper.GetString("disk_usage"),
                Values = new ChartValues<double>(),
                Stroke = Brushes.Orange,
                Fill = Brushes.Transparent,
                PointGeometry = null
            }
        };

        _networkSeries = new SeriesCollection
        {
            new LineSeries
            {
                Title = LocalizationHelper.GetString("network_download"),
                Values = new ChartValues<double>(),
                Stroke = Brushes.Cyan,
                Fill = Brushes.Transparent,
                PointGeometry = null
            },
            new LineSeries
            {
                Title = LocalizationHelper.GetString("network_upload"),
                Values = new ChartValues<double>(),
                Stroke = Brushes.Magenta,
                Fill = Brushes.Transparent,
                PointGeometry = null
            }
        };

        // Notify property changes after initialization
        OnPropertyChanged(nameof(CpuSeries));
        OnPropertyChanged(nameof(MemorySeries));
        OnPropertyChanged(nameof(DiskSeries));
        OnPropertyChanged(nameof(NetworkSeries));
    }

    public TimeRange CurrentTimeRange
    {
        get => _currentTimeRange;
        set
        {
            if (SetProperty(ref _currentTimeRange, value))
            {
                UpdateTimeRange();
            }
        }
    }

    public Array TimeRangeOptions => Enum.GetValues(typeof(TimeRange));

    private void UpdateTimeRange()
    {
        // Reduce data points for better performance - sample every 5 seconds instead of every second
        _maxDataPoints = _currentTimeRange switch
        {
            TimeRange.Hours24 => 720,      // 24 óra / 5 másodperc = 17280, de limitáljuk 720-ra (1 óra adat)
            TimeRange.Week1 => 2016,       // 1 hét / 5 másodperc = 12096, limitáljuk 2016-ra (1 nap adat)
            TimeRange.Days30 => 5184,      // 30 nap / 5 másodperc = 518400, limitáljuk 5184-ra (3 nap adat)
            TimeRange.Months3 => 15552,    // 3 hónap / 5 másodperc = 1555200, limitáljuk 15552-ra (9 nap adat)
            TimeRange.Months6 => 31104,    // 6 hónap / 5 másodperc = 3110400, limitáljuk 31104-ra (18 nap adat)
            TimeRange.Year1 => 63072,      // 1 év / 5 másodperc = 6307200, limitáljuk 63072-ra (36 nap adat)
            TimeRange.Years2 => 126144,    // 2 év / 5 másodperc = 12614400, limitáljuk 126144-ra (72 nap adat)
            TimeRange.Years3 => 189216,    // 3 év / 5 másodperc = 18921600, limitáljuk 189216-ra (108 nap adat)
            _ => 720
        };

        _selectedTimeRange = _currentTimeRange switch
        {
            TimeRange.Hours24 => TimeSpan.FromHours(24),
            TimeRange.Week1 => TimeSpan.FromDays(7),
            TimeRange.Days30 => TimeSpan.FromDays(30),
            TimeRange.Months3 => TimeSpan.FromDays(90),
            TimeRange.Months6 => TimeSpan.FromDays(180),
            TimeRange.Year1 => TimeSpan.FromDays(365),
            TimeRange.Years2 => TimeSpan.FromDays(730),
            TimeRange.Years3 => TimeSpan.FromDays(1095),
            _ => TimeSpan.FromHours(24)
        };

        // Clear old data beyond the new range
        ClearOldData();
        OnPropertyChanged(nameof(CurrentTimeRange));
    }

    private void ClearOldData()
    {
        // Remove data points beyond the max
        try
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (_cpuSeries != null && _cpuSeries.Count > 0 && _cpuSeries[0].Values is ChartValues<double> cpuValues)
                {
                    while (cpuValues.Count > _maxDataPoints)
                        cpuValues.RemoveAt(0);
                }
                if (_memorySeries != null && _memorySeries.Count > 0 && _memorySeries[0].Values is ChartValues<double> memoryValues)
                {
                    while (memoryValues.Count > _maxDataPoints)
                        memoryValues.RemoveAt(0);
                }
                if (_diskSeries != null && _diskSeries.Count > 0 && _diskSeries[0].Values is ChartValues<double> diskValues)
                {
                    while (diskValues.Count > _maxDataPoints)
                        diskValues.RemoveAt(0);
                }
                if (_networkSeries != null && _networkSeries.Count > 0 && _networkSeries[0].Values is ChartValues<double> networkRxValues)
                {
                    while (networkRxValues.Count > _maxDataPoints)
                        networkRxValues.RemoveAt(0);
                }
                if (_networkSeries != null && _networkSeries.Count > 1 && _networkSeries[1].Values is ChartValues<double> networkTxValues)
                {
                    while (networkTxValues.Count > _maxDataPoints)
                        networkTxValues.RemoveAt(0);
                }
                if (Labels != null)
                {
                    while (Labels.Count > _maxDataPoints)
                        Labels.RemoveAt(0);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClearOldData hiba: {ex.Message}");
        }
    }

    private DateTime _lastUpdateTime = DateTime.MinValue;
    private const int UpdateIntervalSeconds = 5; // Frissítés 5 másodpercenként

    public void AddDataPoint(double cpu, double memory, double disk, double networkRx, double networkTx)
    {
        try
        {
            // Check if series are initialized
            if (_cpuSeries == null || _cpuSeries.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Chart series not initialized, skipping AddDataPoint");
                return;
            }

            // Throttle updates: only update every 5 seconds to improve performance
            var now = DateTime.Now;
            if ((now - _lastUpdateTime).TotalSeconds < UpdateIntervalSeconds)
                return;
            
            _lastUpdateTime = now;
            var timestamp = now.ToString("HH:mm:ss");

            System.Diagnostics.Debug.WriteLine($"AddDataPoint called: CPU={cpu:F2}, Memory={memory:F2}, Disk={disk:F2}, NetworkRx={networkRx:F2}, NetworkTx={networkTx:F2}");

            // Use BeginInvoke for better performance (non-blocking)
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                try
                {
                    // CPU
                    if (_cpuSeries != null && _cpuSeries.Count > 0 && _cpuSeries[0].Values is ChartValues<double> cpuValues)
                    {
                        cpuValues.Add(cpu);
                        if (cpuValues.Count > _maxDataPoints)
                            cpuValues.RemoveAt(0);
                        System.Diagnostics.Debug.WriteLine($"CPU value added: {cpu:F2}, total points: {cpuValues.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("CPU series is null or empty");
                    }

                    // Memory - already a percentage (double)
                    if (_memorySeries != null && _memorySeries.Count > 0 && _memorySeries[0].Values is ChartValues<double> memoryValues)
                    {
                        memoryValues.Add(memory); // memory is already a percentage
                        if (memoryValues.Count > _maxDataPoints)
                            memoryValues.RemoveAt(0);
                        System.Diagnostics.Debug.WriteLine($"Memory value added: {memory:F2}, total points: {memoryValues.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Memory series is null or empty");
                    }

                    // Disk
                    if (_diskSeries != null && _diskSeries.Count > 0 && _diskSeries[0].Values is ChartValues<double> diskValues)
                    {
                        diskValues.Add(disk);
                        if (diskValues.Count > _maxDataPoints)
                            diskValues.RemoveAt(0);
                    }

                    // Network
                    if (_networkSeries != null && _networkSeries.Count > 0 && _networkSeries[0].Values is ChartValues<double> networkRxValues)
                    {
                        networkRxValues.Add(networkRx);
                        if (networkRxValues.Count > _maxDataPoints)
                            networkRxValues.RemoveAt(0);
                    }

                    if (_networkSeries != null && _networkSeries.Count > 1 && _networkSeries[1].Values is ChartValues<double> networkTxValues)
                    {
                        networkTxValues.Add(networkTx);
                        if (networkTxValues.Count > _maxDataPoints)
                            networkTxValues.RemoveAt(0);
                    }

                    // Labels
                    if (Labels != null)
                    {
                        Labels.Add(timestamp);
                        if (Labels.Count > _maxDataPoints)
                            Labels.RemoveAt(0);
                    }

                    // Don't call OnPropertyChanged - ChartValues automatically notifies when items are added/removed
                    // This reduces unnecessary redraws
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Chart adat hozzáadási hiba: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chart adat hozzáadási hiba: {ex.Message}");
        }
    }

    private double ParseMemoryPercentage(double memoryValue)
    {
        // A memoryValue most már double, de ha string lenne, akkor parse-olni kellene
        // Most egyszerűsítve: ha < 100, akkor százalék, különben parse-olni kell
        return memoryValue;
    }
}
