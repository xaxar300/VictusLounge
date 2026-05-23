namespace VictusLounge.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public CurrentUserViewModel CurrentUser { get; } = new();
    public BookingStateViewModel Booking { get; } = new();
    public AdminDashboardViewModel Admin { get; } = new();
    public OwnerDashboardViewModel Owner { get; } = new();
}
