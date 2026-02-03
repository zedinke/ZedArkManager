using System.Windows.Input;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly UserService _userService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;
    private bool _rememberMe = false;
    private readonly RememberMeService _rememberMeService;

    public event EventHandler<string>? LoginSuccessful;
    public event EventHandler<string>? RegisterSuccessful;

    public LoginViewModel()
    {
        _userService = new UserService();
        _rememberMeService = new RememberMeService();
        // Remove CanExecute restriction - let the methods handle validation
        LoginCommand = new RelayCommandSync(() => Login());
        RegisterCommand = new RelayCommandSync(() => Register());
        
        // Load saved credentials
        LoadRememberedCredentials();
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ((RelayCommandSync)LoginCommand).RaiseCanExecuteChanged();
                ((RelayCommandSync)RegisterCommand).RaiseCanExecuteChanged();
                ClearError();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ((RelayCommandSync)LoginCommand).RaiseCanExecuteChanged();
                ((RelayCommandSync)RegisterCommand).RaiseCanExecuteChanged();
                ClearError();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }

    public void Login()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Kérjük, töltse ki az összes mezőt!");
            return;
        }

        bool success = _userService.LoginUser(Username, Password);

        if (success)
        {
            // Save credentials if RememberMe is checked
            if (RememberMe)
            {
                _rememberMeService.SaveCredentials(Username, Password);
            }
            else
            {
                // Clear saved credentials if user unchecks RememberMe
                _rememberMeService.ClearCredentials();
            }
            
            LoginSuccessful?.Invoke(this, Username);
        }
        else
        {
            ShowError("Hibás felhasználónév vagy jelszó!");
        }
    }

    public void Register()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Kérjük, töltse ki az összes mezőt!");
            return;
        }

        if (Password.Length < 4)
        {
            ShowError("A jelszónak legalább 4 karakter hosszúnak kell lennie!");
            return;
        }

        bool success = _userService.RegisterUser(Username, Password);

        if (success)
        {
            RegisterSuccessful?.Invoke(this, Username);
            ClearError();
        }
        else
        {
            ShowError("Ez a felhasználónév már foglalt!");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void LoadRememberedCredentials()
    {
        var (username, password) = _rememberMeService.LoadCredentials();
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            Username = username;
            Password = password;
            RememberMe = true;
        }
    }
}
