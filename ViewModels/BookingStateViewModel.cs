using System;

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
