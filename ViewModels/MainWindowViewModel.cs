using System;

namespace VictusLounge.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(
        AuthViewModel auth,
        BalanceViewModel balance,
        DashboardViewModel dashboard,
        ClientCabinetViewModel cabinet,
        EventsViewModel events,
        SettingsViewModel settings,
        NotificationCenterViewModel notifications,
        ShellViewModel shell,
        Action<string>? executeAdminAction = null,
        Action<string>? executeOwnerAction = null,
        Action<string>? executeShiftTask = null)
    {
        Auth = auth;
        Balance = balance;
        Dashboard = dashboard;
        Cabinet = cabinet;
        Events = events;
        Settings = settings;
        Notifications = notifications;
        Shell = shell;
        Admin = new AdminDashboardViewModel(executeAdminAction, executeShiftTask);
        Owner = new OwnerDashboardViewModel(executeOwnerAction);
    }

    public AuthViewModel Auth { get; }
    public BalanceViewModel Balance { get; }
    public DashboardViewModel Dashboard { get; }
    public ClientCabinetViewModel Cabinet { get; }
    public EventsViewModel Events { get; }
    public SettingsViewModel Settings { get; }
    public NotificationCenterViewModel Notifications { get; }
    public ShellViewModel Shell { get; }
    public NavigationViewModel Navigation { get; } = new();
    public CurrentUserViewModel CurrentUser { get; } = new();
    public ClubMapViewModel ClubMap { get; } = new();
    public BookingStateViewModel Booking { get; } = new();
    public TopupViewModel Topup { get; } = new();
    public AdminDashboardViewModel Admin { get; }
    public OwnerDashboardViewModel Owner { get; }
}
