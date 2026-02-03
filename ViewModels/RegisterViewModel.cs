using System.Text.RegularExpressions;
using System.Windows.Input;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class RegisterViewModel : ViewModelBase
{
    private readonly UserService _userService;
    private string _username = string.Empty;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string? _phoneNumber;
    private string? _companyName;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _acceptedTerms = false;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;

    public event EventHandler<string>? RegisterSuccessful;

    public RegisterViewModel()
    {
        _userService = new UserService();
        RegisterCommand = new RelayCommandSync(() => Register());
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string? PhoneNumber
    {
        get => _phoneNumber;
        set => SetProperty(ref _phoneNumber, value);
    }

    public string? CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public bool AcceptedTerms
    {
        get => _acceptedTerms;
        set => SetProperty(ref _acceptedTerms, value);
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

    public ICommand RegisterCommand { get; }

    public void Register()
    {
        ClearError();

        // Validáció
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowError("A felhasználónév kötelező!");
            return;
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ShowError("A teljes név kötelező!");
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ShowError("Az e-mail cím kötelező!");
            return;
        }

        if (!IsValidEmail(Email))
        {
            ShowError("Érvénytelen e-mail cím formátum!");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowError("A jelszó kötelező!");
            return;
        }

        if (Password.Length < 6)
        {
            ShowError("A jelszónak legalább 6 karakter hosszúnak kell lennie!");
            return;
        }

        if (Password != ConfirmPassword)
        {
            ShowError("A jelszavak nem egyeznek meg!");
            return;
        }

        if (!AcceptedTerms)
        {
            ShowError("Az Általános Szerződési Feltételek elfogadása kötelező!");
            return;
        }

        bool success = _userService.RegisterUser(
            Username, 
            Password, 
            FullName, 
            Email, 
            PhoneNumber, 
            CompanyName);

        if (success)
        {
            RegisterSuccessful?.Invoke(this, Username);
        }
        else
        {
            ShowError("Ez a felhasználónév már foglalt!");
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(email);
        }
        catch
        {
            return false;
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
}
