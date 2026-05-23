namespace VictusLounge.ViewModels;

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
