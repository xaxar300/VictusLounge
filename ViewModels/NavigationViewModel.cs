using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class NavigationViewModel : ViewModelBase
{
    private string _currentView = "dashboard";
    private string _currentTitle = "Главная";
    private string _currentRole = "client";
    private bool _isAuthOverlayVisible = true;
    private bool _isSidebarCollapsed;

    public string CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsDashboardActive));
                OnPropertyChanged(nameof(IsMapActive));
                OnPropertyChanged(nameof(IsBookingActive));
                OnPropertyChanged(nameof(IsCabinetActive));
                OnPropertyChanged(nameof(IsBalanceActive));
                OnPropertyChanged(nameof(IsEventsActive));
                OnPropertyChanged(nameof(IsAdminActive));
                OnPropertyChanged(nameof(IsShiftActive));
                OnPropertyChanged(nameof(IsOwnerActive));
                OnPropertyChanged(nameof(IsSettingsActive));
            }
        }
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        set => SetProperty(ref _currentTitle, value);
    }

    public string CurrentRole
    {
        get => _currentRole;
        set
        {
            if (SetProperty(ref _currentRole, value))
            {
                OnPropertyChanged(nameof(CanOpenBooking));
                OnPropertyChanged(nameof(CanOpenCabinet));
                OnPropertyChanged(nameof(CanOpenBalance));
                OnPropertyChanged(nameof(CanOpenAdmin));
                OnPropertyChanged(nameof(CanOpenShift));
                OnPropertyChanged(nameof(CanOpenOwner));
                OnPropertyChanged(nameof(WorkspaceTitle));
            }
        }
    }

    public string WorkspaceTitle => $"{GetRoleTitle(CurrentRole)} workspace";

    public bool CanOpenBooking => CurrentRole != "owner";
    public bool CanOpenCabinet => CurrentRole == "client";
    public bool CanOpenBalance => CurrentRole is "client" or "admin";
    public bool CanOpenAdmin => CurrentRole is "admin" or "owner";
    public bool CanOpenShift => CurrentRole is "admin" or "owner";
    public bool CanOpenOwner => CurrentRole == "owner";

    public bool IsDashboardActive => CurrentView == "dashboard";
    public bool IsMapActive => CurrentView == "map";
    public bool IsBookingActive => CurrentView == "booking";
    public bool IsCabinetActive => CurrentView == "cabinet";
    public bool IsBalanceActive => CurrentView == "balance";
    public bool IsEventsActive => CurrentView == "events";
    public bool IsAdminActive => CurrentView == "admin";
    public bool IsShiftActive => CurrentView == "shift";
    public bool IsOwnerActive => CurrentView == "owner";
    public bool IsSettingsActive => CurrentView == "settings";

    public bool IsAuthOverlayVisible
    {
        get => _isAuthOverlayVisible;
        set => SetProperty(ref _isAuthOverlayVisible, value);
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set => SetProperty(ref _isSidebarCollapsed, value);
    }

    public ICommand? NavigateCommand { get; set; }
    public ICommand? LogoutCommand { get; set; }
    public ICommand? ToggleSidebarCommand { get; set; }

    private static string GetRoleTitle(string role)
    {
        return role switch
        {
            "admin" => "Admin",
            "owner" => "Owner",
            _ => "Client"
        };
    }
}
