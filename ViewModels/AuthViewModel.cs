using System;
using System.Windows.Input;
using VictusLounge.Models;
using VictusLounge.Services;

namespace VictusLounge.ViewModels;

public sealed record AuthSubmitRequest(string LoginPassword, string RegisterPassword);

public sealed class AuthViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly Action<User> _signedIn;
    private readonly Action? _stateChanged;
    private readonly Action<string>? _roleSelected;
    private bool _isRegisterMode;
    private string _login = "client1";
    private string _loginPassword = "client123";
    private string _registerFullName = string.Empty;
    private string _registerLogin = string.Empty;
    private string _registerPassword = string.Empty;
    private string _selectedRole = "client";
    private string _errorMessage = string.Empty;
    private string _registerErrorMessage = string.Empty;

    public AuthViewModel(AuthService authService, Action<User> signedIn, Action? stateChanged = null, Action<string>? roleSelected = null)
    {
        _authService = authService;
        _signedIn = signedIn;
        _stateChanged = stateChanged;
        _roleSelected = roleSelected;
        ShowLoginCommand = new RelayCommand(_ =>
        {
            IsRegisterMode = false;
            _stateChanged?.Invoke();
        });
        ShowRegisterCommand = new RelayCommand(_ =>
        {
            IsRegisterMode = true;
            _stateChanged?.Invoke();
        });
        SelectRoleCommand = new RelayCommand(role =>
        {
            SelectedRole = role?.ToString() ?? "client";
            _roleSelected?.Invoke(SelectedRole);
            _stateChanged?.Invoke();
        });
        SubmitCommand = new RelayCommand(Submit);
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (SetProperty(ref _isRegisterMode, value))
            {
                ErrorMessage = string.Empty;
                RegisterErrorMessage = string.Empty;
                OnPropertyChanged(nameof(AuthTitle));
            }
        }
    }

    public string AuthTitle => IsRegisterMode
        ? "Регистрация в Elite Gaming Lounge"
        : "Вход в Elite Gaming Lounge";

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => SetProperty(ref _loginPassword, value);
    }

    public string RegisterFullName
    {
        get => _registerFullName;
        set => SetProperty(ref _registerFullName, value);
    }

    public string RegisterLogin
    {
        get => _registerLogin;
        set => SetProperty(ref _registerLogin, value);
    }

    public string RegisterPassword
    {
        get => _registerPassword;
        set => SetProperty(ref _registerPassword, value);
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string RegisterErrorMessage
    {
        get => _registerErrorMessage;
        set
        {
            if (SetProperty(ref _registerErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasRegisterError));
            }
        }
    }

    public bool HasRegisterError => !string.IsNullOrWhiteSpace(RegisterErrorMessage);

    public ICommand ShowLoginCommand { get; }
    public ICommand ShowRegisterCommand { get; }
    public ICommand SelectRoleCommand { get; }
    public ICommand SubmitCommand { get; }

    private void Submit(object? parameter)
    {
        var result = IsRegisterMode
            ? _authService.Register(RegisterFullName, RegisterLogin, RegisterPassword)
            : _authService.Login(Login, LoginPassword);

        if (!result.IsSuccess || result.User is null)
        {
            if (IsRegisterMode)
            {
                RegisterErrorMessage = result.ErrorMessage ?? "Не удалось зарегистрироваться.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось войти.";
            }

            return;
        }

        ErrorMessage = string.Empty;
        RegisterErrorMessage = string.Empty;
        _signedIn(result.User);
        _stateChanged?.Invoke();
    }
}
