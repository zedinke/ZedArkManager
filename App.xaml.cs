using System.IO;
using System.Windows;
using System.Windows.Threading;
using ZedASAManager.Services;
using ZedASAManager.Views;
using ZedASAManager.Utilities;

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
            LogHelper.WriteToStartupLog("App_Startup already handled, skipping");
            return;
        }
        
        _startupHandled = true;
        
        try
        {
            LogHelper.WriteToStartupLog("App_Startup called");
            
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
            
            LogHelper.WriteToStartupLog("MainWindow created (hidden) to prevent app shutdown");

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
            LogHelper.WriteToStartupLog($"ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
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
            LogHelper.WriteToStartupLog("CheckForUpdatesAndShowLoginAsync: Starting update check");
            
            var updateService = new UpdateService();
            var currentVersion = updateService.GetCurrentVersion();
            var (hasUpdate, isRequired, latestVersion, releaseNotes) = await updateService.CheckForUpdatesAsync();
            
            LogHelper.WriteToStartupLog($"Update check result: currentVersion={currentVersion}, hasUpdate={hasUpdate}, isRequired={isRequired}, latestVersion={latestVersion}");

            if (hasUpdate && isRequired)
            {
                LogHelper.WriteToStartupLog("Update required! Showing update window");
                
                // Show update window and wait for it
                var updateWindow = new UpdateWindow(updateService, true, currentVersion, latestVersion ?? "unknown", releaseNotes);
                updateWindow.ShowDialog();
                
                // If update was successful, the app will restart, so we don't need to continue
                // If update was cancelled or failed, we should still allow the app to start
            }
            else
            {
                LogHelper.WriteToStartupLog($"No update required (hasUpdate={hasUpdate}, isRequired={isRequired})");
            }
        }
        catch (Exception updateEx)
        {
            LogHelper.WriteToStartupLog($"Update check error: {updateEx.Message}\n{updateEx.StackTrace}\nInner: {updateEx.InnerException?.Message}");
            // Don't block startup if update check fails
        }
        finally
        {
            // Show login window after update check (or if it failed)
            // Use Dispatcher to ensure we're on the UI thread
            LogHelper.WriteToStartupLog("CheckForUpdatesAndShowLoginAsync: Showing login window");
            
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
                        LogHelper.WriteToStartupLog("CheckForUpdatesAndShowLoginAsync: Application is shutting down, cannot show login window");
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
            LogHelper.WriteToStartupLog("ShowLoginWindow: MainWindow already exists and visible, skipping");
            return;
        }
        
            try
            {
                LogHelper.WriteToStartupLog("ShowLoginWindow: Creating login window");
            
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
            
            LogHelper.WriteToStartupLog($"After ShowDialog: loginResult={loginResult}, LoggedInUsername={loggedInUsername}");
            
            // Check login result
            if ((loginResult == true || !string.IsNullOrEmpty(loggedInUsername)) && !string.IsNullOrEmpty(loggedInUsername))
            {
                // Login successful - configure and show existing MainWindow
                try
                {
                    LogHelper.WriteToStartupLog($"Configuring MainWindow for user: {loggedInUsername}");
                    
                    // Get existing MainWindow (created in App_Startup)
                    var mainWindow = this.MainWindow;
                    if (mainWindow == null)
                    {
                        LogHelper.WriteToStartupLog("ERROR: MainWindow is null!");
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
                    
                    LogHelper.WriteToStartupLog($"MainWindow shown, IsVisible={mainWindow.IsVisible}, Visibility={mainWindow.Visibility}");
                }
                catch (Exception mainEx)
                {
                    LogHelper.WriteToStartupLog($"MAIN WINDOW ERROR: {mainEx.Message}\n{mainEx.StackTrace}");
                    
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
                LogHelper.WriteToStartupLog("Login cancelled or failed, shutting down");
                
                // Login cancelled or failed, exit application
                Shutdown();
            }
        }
        catch (Exception loginEx)
        {
            LogHelper.WriteToStartupLog($"LOGIN ERROR: {loginEx.Message}\n{loginEx.StackTrace}");
            
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
