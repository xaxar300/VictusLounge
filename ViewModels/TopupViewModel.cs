using System;
using System.Windows;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class TopupViewModel : ViewModelBase
{
    private Action? _close;
    private Action? _confirm;
    private Action<string>? _selectMethod;
    private Action<string>? _selectAmountPreset;
    private Action? _amountChanged;
    private string _amountText = "50";
    private string _cardNumber = "4242 4242 4242 4242";
    private string _method = "card";
    private string _methodHint = "Карта: платеж проверяется по номеру карты и сразу зачисляется на баланс.";
    private string _confirmText = "Пополнить";
    private string _summaryText = "50 BYN";
    private string _bonusText = "Бонус зависит от статуса по часам";
    private string _errorText = string.Empty;
    private bool _hasError;

    public TopupViewModel()
    {
        CloseCommand = new RelayCommand(() => _close?.Invoke());
        ConfirmCommand = new RelayCommand(() => _confirm?.Invoke());
        SelectMethodCommand = new RelayCommand(parameter =>
        {
            if (parameter is string method && !string.IsNullOrWhiteSpace(method))
            {
                _selectMethod?.Invoke(method);
            }
        });
        SelectAmountPresetCommand = new RelayCommand(parameter =>
        {
            if (parameter is string amount && !string.IsNullOrWhiteSpace(amount))
            {
                _selectAmountPreset?.Invoke(amount);
            }
        });
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                _amountChanged?.Invoke();
            }
        }
    }

    public string CardNumber
    {
        get => _cardNumber;
        set => SetProperty(ref _cardNumber, value);
    }

    public string Method
    {
        get => _method;
        set
        {
            if (SetProperty(ref _method, value))
            {
                OnPropertyChanged(nameof(CardFieldsVisibility));
            }
        }
    }

    public string MethodHint
    {
        get => _methodHint;
        set => SetProperty(ref _methodHint, value);
    }

    public string ConfirmText
    {
        get => _confirmText;
        set => SetProperty(ref _confirmText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public string BonusText
    {
        get => _bonusText;
        set => SetProperty(ref _bonusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public Visibility CardFieldsVisibility => Method == "card" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;
    public ICommand CloseCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand SelectMethodCommand { get; }
    public ICommand SelectAmountPresetCommand { get; }

    public void ConfigureActions(
        Action close,
        Action confirm,
        Action<string> selectMethod,
        Action<string> selectAmountPreset,
        Action amountChanged)
    {
        _close = close;
        _confirm = confirm;
        _selectMethod = selectMethod;
        _selectAmountPreset = selectAmountPreset;
        _amountChanged = amountChanged;
    }

    public void ShowError(string message)
    {
        ErrorText = message;
        HasError = true;
    }

    public void ClearError()
    {
        ErrorText = string.Empty;
        HasError = false;
    }
}
