using System;

namespace VictusLounge.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public CurrentUserViewModel CurrentUser { get; } = new();
    public BookingStateViewModel Booking { get; } = new();
    public AdminDashboardViewModel Admin { get; } = new();
    public OwnerDashboardViewModel Owner { get; } = new();
}

public sealed class CurrentUserViewModel : ViewModelBase
{
    private int _id;
    private string _fullName = "Not signed in";
    private string _login = string.Empty;
    private string _role = "client";
    private decimal _balance;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public decimal Balance
    {
        get => _balance;
        set => SetProperty(ref _balance, value);
    }
}

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
    private bool _isCompanyBooking;

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

    public bool IsCompanyBooking
    {
        get => _isCompanyBooking;
        set => SetProperty(ref _isCompanyBooking, value);
    }
}

public sealed class AdminDashboardViewModel : ViewModelBase
{
    private int _activeSessions;
    private int _paymentQueue;
    private int _freePcs;
    private int _supportQueue;
    private decimal _shiftCash;
    private decimal _shiftOnline;
    private bool _shiftClosed;

    public int ActiveSessions
    {
        get => _activeSessions;
        set => SetProperty(ref _activeSessions, value);
    }

    public int PaymentQueue
    {
        get => _paymentQueue;
        set => SetProperty(ref _paymentQueue, value);
    }

    public int FreePcs
    {
        get => _freePcs;
        set => SetProperty(ref _freePcs, value);
    }

    public int SupportQueue
    {
        get => _supportQueue;
        set => SetProperty(ref _supportQueue, value);
    }

    public decimal ShiftCash
    {
        get => _shiftCash;
        set => SetProperty(ref _shiftCash, value);
    }

    public decimal ShiftOnline
    {
        get => _shiftOnline;
        set => SetProperty(ref _shiftOnline, value);
    }

    public bool ShiftClosed
    {
        get => _shiftClosed;
        set => SetProperty(ref _shiftClosed, value);
    }
}

public sealed class OwnerDashboardViewModel : ViewModelBase
{
    private int _revenue;
    private int _load;
    private int _averageCheck;
    private int _repeatRate;
    private int _standardRate = 8;
    private int _vipRate = 14;
    private int _royalRate = 24;
    private int _bootcampRate = 50;
    private string _demandMode = "normal";

    public int Revenue
    {
        get => _revenue;
        set => SetProperty(ref _revenue, value);
    }

    public int Load
    {
        get => _load;
        set => SetProperty(ref _load, value);
    }

    public int AverageCheck
    {
        get => _averageCheck;
        set => SetProperty(ref _averageCheck, value);
    }

    public int RepeatRate
    {
        get => _repeatRate;
        set => SetProperty(ref _repeatRate, value);
    }

    public int StandardRate
    {
        get => _standardRate;
        set => SetProperty(ref _standardRate, value);
    }

    public int VipRate
    {
        get => _vipRate;
        set => SetProperty(ref _vipRate, value);
    }

    public int RoyalRate
    {
        get => _royalRate;
        set => SetProperty(ref _royalRate, value);
    }

    public int BootcampRate
    {
        get => _bootcampRate;
        set => SetProperty(ref _bootcampRate, value);
    }

    public string DemandMode
    {
        get => _demandMode;
        set => SetProperty(ref _demandMode, value);
    }
}
