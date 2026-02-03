using System;
using System.IO;
using System.Windows.Controls;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class ServerCard : UserControl
{
    public ServerCard()
    {
        try
        {
            File.AppendAllText("C:\\temp\\servercard_debug.log", $"ServerCard constructor started\n");
            InitializeComponent();
            File.AppendAllText("C:\\temp\\servercard_debug.log", $"ServerCard InitializeComponent completed\n");
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("C:\\temp\\servercard_debug.log", $"ServerCard ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            throw;
        }
    }

    private void ShutdownButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("scheduled_shutdown");
        }
    }
}
