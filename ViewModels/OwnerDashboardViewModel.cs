using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class OwnerDashboardViewModel : ViewModelBase
{
    private readonly Action<string>? _executeAction;
    private int _revenue;
    private int _load;
    private int _averageCheck;
    private int _repeatRate;
    private int _standardRate = 8;
    private int _vipRate = 14;
    private int _royalRate = 24;
    private int _bootcampRate = 50;
    private string _demandMode = "normal";

    public OwnerDashboardViewModel(Action<string>? executeAction = null)
    {
        _executeAction = executeAction;
        ActionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string action && !string.IsNullOrWhiteSpace(action))
            {
                _executeAction?.Invoke(action);
            }
        });
    }

    public ICommand ActionCommand { get; }

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
