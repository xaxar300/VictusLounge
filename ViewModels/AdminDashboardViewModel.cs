using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class AdminDashboardViewModel : ViewModelBase
{
    private readonly Action<string>? _executeAction;
    private int _activeSessions;
    private int _paymentQueue;
    private int _freePcs;
    private int _supportQueue;
    private decimal _shiftCash;
    private decimal _shiftOnline;
    private bool _shiftClosed;
    private bool _isVipTaskDone = true;
    private bool _isBootcampTaskDone;
    private bool _isPaymentTaskDone = true;

    public AdminDashboardViewModel(Action<string>? executeAction = null, Action<string>? executeShiftTask = null)
    {
        _executeAction = executeAction;
        ActionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string action && !string.IsNullOrWhiteSpace(action))
            {
                _executeAction?.Invoke(action);
            }
        });
        ShiftTaskCommand = new RelayCommand(parameter =>
        {
            if (parameter is string taskKey && !string.IsNullOrWhiteSpace(taskKey))
            {
                executeShiftTask?.Invoke(taskKey);
            }
        });
    }

    public ICommand ActionCommand { get; }
    public ICommand ShiftTaskCommand { get; }

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

    public bool IsVipTaskDone
    {
        get => _isVipTaskDone;
        set => SetProperty(ref _isVipTaskDone, value);
    }

    public bool IsBootcampTaskDone
    {
        get => _isBootcampTaskDone;
        set => SetProperty(ref _isBootcampTaskDone, value);
    }

    public bool IsPaymentTaskDone
    {
        get => _isPaymentTaskDone;
        set => SetProperty(ref _isPaymentTaskDone, value);
    }
}
