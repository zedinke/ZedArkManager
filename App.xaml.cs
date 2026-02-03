using System.IO;
using System.Windows;
using System.Windows.Threading;
using ZedASAManager.Services;
using ZedASAManager.Views;

namespace ZedASAManager;

public partial class App : Application
{
    private bool _startupHandled = false;

    public App()
    {
        // Startup event is already hooked up in App.xaml, no need to add it here
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // Prevent multiple calls
        if (_startupHandled)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"App_Startup already handled, skipping\n");
            }
            catch { }
            return;
        }
        
        _startupHandled = true;
        
        try
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"App_Startup called\n");
            }
            catch { }
            
            // Global exception handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Create MainWindow FIRST (hidden) to prevent app shutdown
            // This ensures the app has a MainWindow even before login
            var mainWindow = new MainWindow
            {
                Visibility = Visibility.Hidden,
                WindowState = WindowState.Minimized,
                ShowInTaskbar = false
            };
            this.MainWindow = mainWindow;
            
            File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow created (hidden) to prevent app shutdown\n");

            // Check for updates before showing login window
            // Use Dispatcher.BeginInvoke to ensure we're on the UI thread
            // This prevents the app from shutting down before LoginWindow is shown
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await CheckForUpdatesAndShowLoginAsync();
            }), DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            
            MessageBox.Show(
                $"Alkalmazás indítási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}",
                "Kritikus Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task CheckForUpdatesAndShowLoginAsync()
    {
        try
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"CheckForUpdatesAndShowLoginAsync: Starting update check\n");
            }
            catch { }
            
            var updateService = new UpdateService();
            var (hasUpdate, isRequired, latestVersion, releaseNotes) = await updateService.CheckForUpdatesAsync();
            
            try
            {
                var currentVersion = updateService.GetCurrentVersion();
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Update check result: currentVersion={currentVersion}, hasUpdate={hasUpdate}, isRequired={isRequired}, latestVersion={latestVersion}\n");
            }
            catch { }

            if (hasUpdate && isRequired)
            {
                try
                {
                    File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Update required! Showing update window\n");
                }
                catch { }
                
                // Show update window and wait for it
                var currentVersion = updateService.GetCurrentVersion();
                var updateWindow = new UpdateWindow(updateService, true, currentVersion, latestVersion ?? "unknown", releaseNotes);
                updateWindow.ShowDialog();
                
                // If update was successful, the app will restart, so we don't need to continue
                // If update was cancelled or failed, we should still allow the app to start
            }
            else
            {
                try
                {
                    File.AppendAllText("C:\\temp\\zedasa_startup.log", $"No update required (hasUpdate={hasUpdate}, isRequired={isRequired})\n");
                }
                catch { }
            }
        }
        catch (Exception updateEx)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Update check error: {updateEx.Message}\n{updateEx.StackTrace}\nInner: {updateEx.InnerException?.Message}\n");
            }
            catch { }
            // Don't block startup if update check fails
        }
        finally
        {
            // Show login window after update check (or if it failed)
            // Use Dispatcher to ensure we're on the UI thread
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"CheckForUpdatesAndShowLoginAsync: Showing login window\n");
            }
            catch { }
            
            // Ensure we're on the UI thread and app is still running before showing the login window
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current != null && Application.Current.ShutdownMode != ShutdownMode.OnExplicitShutdown)
                    {
                        ShowLoginWindow();
                    }
                    else
                    {
                        try
                        {
                            File.AppendAllText("C:\\temp\\zedasa_startup.log", $"CheckForUpdatesAndShowLoginAsync: Application is shutting down, cannot show login window\n");
                        }
                        catch { }
                    }
                });
            }
            else
            {
                ShowLoginWindow();
            }
        }
    }

    private void ShowLoginWindow()
    {
        // If MainWindow already exists and is visible, don't show login again
        if (MainWindow != null && MainWindow.IsVisible)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"ShowLoginWindow: MainWindow already exists and visible, skipping\n");
            }
            catch { }
            return;
        }
        
        try
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"ShowLoginWindow: Creating login window\n");
            }
            catch { }
            
            var loginWindow = new Views.LoginWindow();
            
            // Ensure window is visible and on top
            loginWindow.WindowState = WindowState.Normal;
            loginWindow.ShowInTaskbar = true;
            loginWindow.Topmost = true;
            loginWindow.Activate();
            loginWindow.Focus();
            
            bool? loginResult = loginWindow.ShowDialog();
            
            loginWindow.Topmost = false;
            
            // Store username before checking
            string? loggedInUsername = loginWindow.LoggedInUsername;
            
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"After ShowDialog: loginResult={loginResult}, LoggedInUsername={loggedInUsername}\n");
            }
            catch { }
            
            // Check login result
            if ((loginResult == true || !string.IsNullOrEmpty(loggedInUsername)) && !string.IsNullOrEmpty(loggedInUsername))
            {
                // Login successful - configure and show existing MainWindow
                try
                {
                    File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Configuring MainWindow for user: {loggedInUsername}\n");
                    
                    // Get existing MainWindow (created in App_Startup)
                    var mainWindow = this.MainWindow;
                    if (mainWindow == null)
                    {
                        File.AppendAllText("C:\\temp\\zedasa_startup.log", "ERROR: MainWindow is null!\n");
                        throw new InvalidOperationException("MainWindow is null");
                    }
                    
                    // Set username
                    if (mainWindow is MainWindow mw)
                    {
                        mw.LoggedInUsername = loggedInUsername;
                    }
                    
                    // Configure and show window
                    mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ShowInTaskbar = true;
                    mainWindow.Visibility = Visibility.Visible;
                    
                    // Show window
                    mainWindow.Show();
                    mainWindow.Activate();
                    mainWindow.Focus();
                    mainWindow.BringIntoView();
                    
                    File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow shown, IsVisible={mainWindow.IsVisible}, Visibility={mainWindow.Visibility}\n");
                }
                catch (Exception mainEx)
                {
                    try
                    {
                        File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MAIN WINDOW ERROR: {mainEx.Message}\n{mainEx.StackTrace}\n");
                    }
                    catch { }
                    
                    MessageBox.Show(
                        $"Főoldal betöltési hiba:\n\n{mainEx.Message}",
                        "Hiba",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown();
                }
            }
            else
            {
                try
                {
                    File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Login cancelled or failed, shutting down\n");
                }
                catch { }
                
                // Login cancelled or failed, exit application
                Shutdown();
            }
        }
        catch (Exception loginEx)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"LOGIN ERROR: {loginEx.Message}\n{loginEx.StackTrace}\n");
            }
            catch { }
            
            MessageBox.Show(
                $"Login ablak hiba:\n\n{loginEx.Message}",
                "Login Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Nem kezelt hiba történt:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "Hiba",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"Kritikus hiba történt:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Kritikus Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
