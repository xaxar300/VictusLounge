using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VictusLounge.Helpers;

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
        ActionCommand = RelayCommand.ForString(action => _executeAction?.Invoke(action));
        ShiftTaskCommand = RelayCommand.ForString(taskKey => executeShiftTask?.Invoke(taskKey));
    }

    public ICommand ActionCommand { get; }
    public ICommand ShiftTaskCommand { get; }
    public ObservableCollection<AdminSessionRowViewModel> Sessions { get; } = [];
    public ObservableCollection<AdminTaskQueueItemViewModel> TaskQueue { get; } = [];

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

public sealed class AdminSessionRowViewModel
{
    public required string ComputerName { get; init; }
    public required string ClientName { get; init; }
    public required string EndText { get; init; }
    public required SessionStatus Status { get; init; }
    public required string StatusText { get; init; }
    public required string ActionText { get; init; }
    public required string ActionCommandParameter { get; init; }
    public bool IsPrimaryAction { get; init; }
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionCommandParameter);
}

public sealed class AdminTaskQueueItemViewModel
{
    public required string Title { get; init; }
    public required string Details { get; init; }
    public required AdminTaskKind Kind { get; init; }
    public required string ActionText { get; init; }
    public required string ActionCommandParameter { get; init; }
    public bool IsPrimaryAction { get; init; }
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionCommandParameter);
}

public enum AdminTaskKind
{
    Payment,
    Service
}
