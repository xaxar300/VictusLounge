using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VictusLounge.Services;
using VictusLounge.Services.Pricing;

namespace VictusLounge.ViewModels;

public sealed class BookingStateViewModel : ViewModelBase
{
    private DateTime _date = DateTime.Today;
    private string _zoneKey = "Standard";
    private string _zoneName = "Standard Hall";
    private int _tariff = 8;
    private int _duration = 1;
    private int _hour = 18;
    private int _minute;
    private string _package = "regular";
    private string _loyaltyTier = "Bronze";
    private bool _isCompanyBooking;
    private string[] _selectedSeats = [];
    private string _seatsText = "ПК не выбран";
    private string _timeText = "18:00-19:00";
    private string _durationText = "1 ч";
    private string _tariffText = "8 BYN/час · Без скидки статуса";
    private string _baseTotalText = "0 BYN";
    private string _discountText = "Без скидки статуса";
    private string _totalText = "0 BYN";
    private string _timePickerText = "Выбрать время: 18:00";
    private string _packageHintText = "Обычный тариф: 1 ч, скидка статуса пока не открыта.";
    private string _warningText = string.Empty;
    private string _errorText = string.Empty;
    private string _confirmationText = string.Empty;
    private bool _hasWarning;
    private bool _hasError;

    private Action<string>? _selectMode;
    private Action<string>? _selectDate;
    private Action<string>? _selectZone;
    private Action<string>? _selectDuration;
    private Action<int>? _selectHour;
    private Action<int>? _selectMinute;
    private Action<string>? _selectSeat;
    private Action? _toggleTimePicker;
    private Action? _confirmBooking;
    private Action? _clearBooking;
    private Action? _closeConfirmation;

    public BookingStateViewModel()
    {
        SelectModeCommand = new RelayCommand(parameter => ExecuteString(parameter, _selectMode));
        SelectDateCommand = new RelayCommand(parameter => ExecuteString(parameter, _selectDate));
        SelectZoneCommand = new RelayCommand(parameter => ExecuteString(parameter, _selectZone));
        SelectDurationCommand = new RelayCommand(parameter => ExecuteString(parameter, _selectDuration));
        SelectHourCommand = new RelayCommand(parameter => ExecuteInt(parameter, _selectHour));
        SelectMinuteCommand = new RelayCommand(parameter => ExecuteInt(parameter, _selectMinute));
        SelectSeatCommand = new RelayCommand(parameter => ExecuteString(parameter, _selectSeat));
        ToggleTimePickerCommand = new RelayCommand(_ => _toggleTimePicker?.Invoke());
        ConfirmCommand = new RelayCommand(_ => _confirmBooking?.Invoke());
        ClearCommand = new RelayCommand(_ => _clearBooking?.Invoke());
        CloseConfirmationCommand = new RelayCommand(_ => _closeConfirmation?.Invoke());
        RefreshSummary();
    }

    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public string ZoneKey
    {
        get => _zoneKey;
        set => SetProperty(ref _zoneKey, value);
    }

    public string ZoneName
    {
        get => _zoneName;
        set => SetProperty(ref _zoneName, value);
    }

    public int Tariff
    {
        get => _tariff;
        set => SetProperty(ref _tariff, value);
    }

    public int Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public int Hour
    {
        get => _hour;
        set => SetProperty(ref _hour, value);
    }

    public int Minute
    {
        get => _minute;
        set => SetProperty(ref _minute, value);
    }

    public string Package
    {
        get => _package;
        set => SetProperty(ref _package, value);
    }

    public string LoyaltyTier
    {
        get => _loyaltyTier;
        set => SetProperty(ref _loyaltyTier, string.IsNullOrWhiteSpace(value) ? "Bronze" : value);
    }

    public bool IsCompanyBooking
    {
        get => _isCompanyBooking;
        set => SetProperty(ref _isCompanyBooking, value);
    }

    public string SeatsText
    {
        get => _seatsText;
        private set => SetProperty(ref _seatsText, value);
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public string TariffText
    {
        get => _tariffText;
        private set => SetProperty(ref _tariffText, value);
    }

    public string BaseTotalText
    {
        get => _baseTotalText;
        private set => SetProperty(ref _baseTotalText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetProperty(ref _discountText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string TimePickerText
    {
        get => _timePickerText;
        private set => SetProperty(ref _timePickerText, value);
    }

    public string PackageHintText
    {
        get => _packageHintText;
        private set => SetProperty(ref _packageHintText, value);
    }

    public string WarningText
    {
        get => _warningText;
        private set => SetProperty(ref _warningText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public string ConfirmationText
    {
        get => _confirmationText;
        private set => SetProperty(ref _confirmationText, value);
    }

    public bool HasWarning
    {
        get => _hasWarning;
        private set
        {
            if (SetProperty(ref _hasWarning, value))
            {
                OnPropertyChanged(nameof(WarningVisibility));
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public Visibility WarningVisibility => HasWarning ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public ICommand SelectModeCommand { get; }
    public ICommand SelectDateCommand { get; }
    public ICommand SelectZoneCommand { get; }
    public ICommand SelectDurationCommand { get; }
    public ICommand SelectHourCommand { get; }
    public ICommand SelectMinuteCommand { get; }
    public ICommand SelectSeatCommand { get; }
    public ICommand ToggleTimePickerCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CloseConfirmationCommand { get; }

    public void ConfigureActions(
        Action<string> selectMode,
        Action<string> selectDate,
        Action<string> selectZone,
        Action<string> selectDuration,
        Action<int> selectHour,
        Action<int> selectMinute,
        Action<string> selectSeat,
        Action toggleTimePicker,
        Action confirmBooking,
        Action clearBooking,
        Action closeConfirmation)
    {
        _selectMode = selectMode;
        _selectDate = selectDate;
        _selectZone = selectZone;
        _selectDuration = selectDuration;
        _selectHour = selectHour;
        _selectMinute = selectMinute;
        _selectSeat = selectSeat;
        _toggleTimePicker = toggleTimePicker;
        _confirmBooking = confirmBooking;
        _clearBooking = clearBooking;
        _closeConfirmation = closeConfirmation;
    }

    public void SetSelectedSeats(IEnumerable<string> seats)
    {
        _selectedSeats = seats.Order(StringComparer.Ordinal).ToArray();
        RefreshSummary();
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

    public void RefreshConfirmationText()
    {
        ConfirmationText =
            $"ПК: {SeatsText}\n" +
            $"Зона: {ZoneName}\n" +
            $"Дата: {Date:yyyy-MM-dd}\n" +
            $"Время: {TimeText}\n" +
            $"Тариф: {TariffText}\n" +
            $"Итого: {TotalText}";
    }

    public void RefreshSummary()
    {
        var start = Date.Date.AddHours(Hour).AddMinutes(Minute);
        var end = start.AddHours(Duration);
        var seatsCount = Math.Max(_selectedSeats.Length, 1);
        var baseTotal = Tariff * Duration * seatsCount;
        var pricingStrategy = BookingPricingStrategyFactory.Create(Package, LoyaltyTier);
        var total = pricingStrategy.Calculate(new BookingPriceContext(Tariff, Duration, seatsCount));
        var discount = baseTotal - total;
        var tariffLabel = pricingStrategy.Label;

        SeatsText = _selectedSeats.Length == 0 ? "ПК не выбран" : string.Join(", ", _selectedSeats);
        TimeText = $"{start:HH:mm}-{end:HH:mm}";
        DurationText = $"{Duration} ч";
        TariffText = $"{Tariff} BYN/час · {tariffLabel}";
        BaseTotalText = _selectedSeats.Length == 0 ? "0 BYN" : $"{baseTotal:0.##} BYN";
        DiscountText = _selectedSeats.Length == 0 ? tariffLabel : $"{tariffLabel} · −{discount:0.##} BYN";
        TotalText = _selectedSeats.Length == 0 ? "0 BYN" : $"{total:0.##} BYN";
        TimePickerText = $"Выбрать время: {Hour:00}:{Minute:00}";
        PackageHintText = GetPackageDescription();
        WarningText = $"Бронь закончится на следующий день: {end:dd.MM HH:mm}.";
        HasWarning = end.Date > start.Date;
        ClearError();
    }

    private static void ExecuteString(object? parameter, Action<string>? action)
    {
        if (parameter is string value && !string.IsNullOrWhiteSpace(value))
        {
            action?.Invoke(value);
        }
    }

    private static void ExecuteInt(object? parameter, Action<int>? action)
    {
        if (parameter is int value)
        {
            action?.Invoke(value);
            return;
        }

        if (int.TryParse(parameter?.ToString(), out var parsed))
        {
            action?.Invoke(parsed);
        }
    }

    private string GetPackageDescription()
    {
        return BookingRules.GetPackageDescription(Package, Duration, LoyaltyTier);
    }
}
