using VictusLounge.Helpers;

namespace VictusLounge;

public partial class MainWindow
{
    private static string FormatMoney(decimal amount)
    {
        return MoneyFormatter.FormatByn(amount);
    }

    private void UpdateCurrentBalanceText()
    {
        _viewModel.Balance.CurrentBalance = _balanceAmount;
    }

    private void SyncCurrentUserViewModel()
    {
        _viewModel.CurrentUser.Id = _currentUserId;
        _viewModel.CurrentUser.FullName = _currentUserFullName;
        _viewModel.CurrentUser.Login = _currentUserLogin;
        _viewModel.CurrentUser.Role = _currentRole;
        _viewModel.CurrentUser.Balance = _balanceAmount;
        _viewModel.Navigation.CurrentRole = _currentRole;
        _viewModel.Balance.CurrentBalance = _balanceAmount;
    }

    private void SyncBookingViewModel()
    {
        _viewModel.Booking.Date = _bookingDate;
        _viewModel.Booking.ZoneKey = _bookingZoneKey;
        _viewModel.Booking.ZoneName = _bookingZoneName;
        _viewModel.Booking.Tariff = _bookingTariff;
        _viewModel.Booking.Duration = _bookingDuration;
        _viewModel.Booking.Hour = _bookingHour;
        _viewModel.Booking.Minute = _bookingMinute;
        _viewModel.Booking.Package = _bookingPackage;
        _viewModel.Booking.LoyaltyTier = _currentUserId > 0 ? GetStoredClientTier(_currentUserId) : "Bronze";
        _viewModel.Booking.IsCompanyBooking = _isCompanyBooking;
        _viewModel.Booking.SetSelectedSeats(_selectedSeats);
        _viewModel.Booking.RefreshSummary();
    }

    private void SyncAdminViewModel()
    {
        _viewModel.Admin.ActiveSessions = _adminActiveSessions;
        _viewModel.Admin.PaymentQueue = _adminPaymentQueue;
        _viewModel.Admin.FreePcs = _adminFreePcs;
        _viewModel.Admin.SupportQueue = _adminSupportQueue;
        _viewModel.Admin.ShiftCash = _shiftCash;
        _viewModel.Admin.ShiftOnline = _shiftOnline;
        _viewModel.Admin.ShiftClosed = _shiftClosed;
    }

    private void SyncOwnerViewModel()
    {
        _viewModel.Owner.Revenue = _ownerRevenue;
        _viewModel.Owner.Load = _ownerLoad;
        _viewModel.Owner.AverageCheck = _ownerAverageCheck;
        _viewModel.Owner.RepeatRate = _ownerRepeatRate;
        _viewModel.Owner.StandardRate = _standardRate;
        _viewModel.Owner.VipRate = _vipRate;
        _viewModel.Owner.RoyalRate = _royalRate;
        _viewModel.Owner.BootcampRate = _bootcampRate;
        _viewModel.Owner.DemandMode = _ownerDemandMode;
    }
}
