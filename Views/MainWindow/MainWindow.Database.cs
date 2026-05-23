using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;

namespace VictusLounge;

public partial class MainWindow
{
    private void LoadDatabaseState()
    {
        try
        {
            using var dbContext = new AppDbContext();
            NormalizeDatabaseState(dbContext);
            if (IsLoaded)
            {
                RefreshBookingDatesIfStale();
            }

            _computers.Clear();
            _computers.AddRange(dbContext.Computers
                .AsNoTracking()
                .OrderBy(computer => computer.Id)
                .ToList());

            _tariffs.Clear();
            _tariffs.AddRange(dbContext.Tariffs
                .AsNoTracking()
                .Where(tariff => tariff.IsActive)
                .OrderBy(tariff => tariff.Id)
                .ToList());

            RefreshEffectiveComputerStatuses(dbContext);

            var now = DateTime.Now;
            var activeSessions = dbContext.GameSessions.Count(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now));
            var pendingBookings = dbContext.Bookings.Count(booking => booking.Status == BookingStatuses.PendingPayment
                && booking.EndTime > now);
            var pendingSessions = dbContext.GameSessions.Count(session => session.Status == SessionStatuses.AwaitingPayment
                && (session.EndTime == null || session.EndTime > now));
            var pendingTopups = dbContext.Payments.Count(payment =>
                payment.PaymentType.StartsWith(PaymentTypes.Pending)
                && payment.CreatedAt.Date == DateTime.Today);
            var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
            var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
            var today = DateTime.Today;
            _adminActiveSessions = activeSessions;
            _adminPaymentQueue = pendingBookings + pendingSessions + pendingTopups;
            _adminFreePcs = freePcs;
            _adminSupportQueue = servicePcs;
            _shiftCash = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.CreatedAt.Date == today)
                .Where(payment => payment.PaymentType == PaymentTypes.Cash)
                .Sum(payment => payment.Amount);
            _shiftOnline = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.CreatedAt.Date == today)
                .Where(payment => payment.Amount > 0
                    && (payment.PaymentType == PaymentTypes.Card || payment.PaymentType == PaymentTypes.Online))
                .Sum(payment => payment.Amount);
            SyncAdminViewModel();

            UpdateDashboardSummary(dbContext);
            if (IsLoaded)
            {
                AdminPaymentQueueHintText.Text = $"{pendingBookings} броней, {pendingSessions} сессий, {pendingTopups} пополнений";
            }

            if (_currentUserId > 0)
            {
                var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
                if (currentUser is not null)
                {
                    _currentUserFullName = currentUser.FullName;
                    _currentUserLogin = currentUser.Login;
                    _currentRole = NormalizeRole(currentUser.Role);
                    _balanceAmount = currentUser.Balance;
                    SyncCurrentUserViewModel();
                    UpdateCurrentBalanceText();
                    UpdateCurrentUserUi();
                    RefreshClientUx(dbContext, currentUser);
                }
            }

            _standardRate = GetTariffPrice("Standard", _standardRate);
            _vipRate = GetTariffPrice("VIP", _vipRate);
            _royalRate = GetTariffPrice("Royal", _royalRate);
            _bootcampRate = GetTariffPrice("Bootcamp", _bootcampRate);
            _bookingTariff = GetTariffPrice(_bookingZoneKey, _bookingTariff);
            SyncBookingViewModel();
            UpdateDashboardLoadBars();
            UpdateAnnouncementText();
            UpdateCabinetNextBenefit();
            RebuildTodayClubList(dbContext);
            RebuildOwnerStaffList(dbContext);
            RefreshLiveViewsAfterDatabaseChange();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки БД", ex);
            // If SQL Server is unavailable, the screen keeps the last loaded values.
        }
    }

    private void ShowDatabaseError(string title, Exception ex)
    {
        if (IsLoaded)
        {
            ShowStatus(title, $"Не удалось выполнить операцию SQL Server. Проверь строку подключения и доступность сервера. {ex.Message}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"{title}: {ex}");
        }
    }

    private int GetTariffPrice(string namePart, int fallback)
    {
        var tariff = _tariffs.FirstOrDefault(item =>
            item.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));

        return tariff is null ? fallback : (int)Math.Round(tariff.PricePerHour);
    }

    private bool EnsureSignedInForDatabaseWrite()
    {
        if (_currentUserId > 0)
        {
            return true;
        }

        ShowStatus("Войдите в систему", "Операция не сохранена: пользователь не авторизован.");
        return false;
    }

    private int? ResolveCurrentOrAdminUserId(AppDbContext dbContext)
    {
        if (_currentUserId > 0 && dbContext.Users.Any(user => user.Id == _currentUserId))
        {
            return _currentUserId;
        }

        var adminId = dbContext.Users
            .Where(user => user.Role == "Admin")
            .OrderBy(user => user.Id)
            .Select(user => (int?)user.Id)
            .FirstOrDefault();
        return adminId;
    }
}
