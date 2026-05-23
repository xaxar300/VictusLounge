using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Repositories;

namespace VictusLounge;

public partial class MainWindow
{
    private void LoadDatabaseState()
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            NormalizeDatabaseState(unitOfWork);
            if (IsLoaded)
            {
                RefreshBookingDatesIfStale();
            }

            _computers.Clear();
            _computers.AddRange(unitOfWork.Computers.GetOrderedNoTracking());

            _tariffs.Clear();
            _tariffs.AddRange(unitOfWork.Tariffs.GetActiveOrdered());

            RefreshEffectiveComputerStatuses(unitOfWork);

            var now = DateTime.Now;
            var activeSessions = unitOfWork.GameSessions.Count(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now));
            var pendingBookings = unitOfWork.Bookings.Count(booking => booking.Status == BookingStatuses.PendingPayment
                && booking.EndTime > now);
            var pendingSessions = unitOfWork.GameSessions.Count(session => session.Status == SessionStatuses.AwaitingPayment
                && (session.EndTime == null || session.EndTime > now));
            var pendingTopups = unitOfWork.Payments.CountPendingForDate(DateTime.Today);
            var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
            var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
            var today = DateTime.Today;
            _adminActiveSessions = activeSessions;
            _adminPaymentQueue = pendingBookings + pendingSessions + pendingTopups;
            _adminFreePcs = freePcs;
            _adminSupportQueue = servicePcs;
            _shiftCash = unitOfWork.Payments.SumForDate(today, PaymentTypes.Cash);
            _shiftOnline = unitOfWork.Payments.SumOnlineForDate(today);
            SyncAdminViewModel();

            UpdateDashboardSummary(unitOfWork);
            if (IsLoaded)
            {
                AdminPaymentQueueHintText.Text = $"{pendingBookings} Р±СЂРѕРЅРµР№, {pendingSessions} СЃРµСЃСЃРёР№, {pendingTopups} РїРѕРїРѕР»РЅРµРЅРёР№";
            }

            if (_currentUserId > 0)
            {
                var currentUser = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
                if (currentUser is not null)
                {
                    _currentUserFullName = currentUser.FullName;
                    _currentUserLogin = currentUser.Login;
                    _currentRole = NormalizeRole(currentUser.Role);
                    _balanceAmount = currentUser.Balance;
                    SyncCurrentUserViewModel();
                    UpdateCurrentBalanceText();
                    UpdateCurrentUserUi();
                    RefreshClientUx(unitOfWork, currentUser);
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
            RebuildTodayClubList(unitOfWork);
            RebuildOwnerStaffList(unitOfWork);
            RefreshLiveViewsAfterDatabaseChange();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё Р‘Р”", ex);
            // If SQL Server is unavailable, the screen keeps the last loaded values.
        }
    }

    private void ShowDatabaseError(string title, Exception ex)
    {
        if (IsLoaded)
        {
            ShowStatus(title, $"РќРµ СѓРґР°Р»РѕСЃСЊ РІС‹РїРѕР»РЅРёС‚СЊ РѕРїРµСЂР°С†РёСЋ SQL Server. РџСЂРѕРІРµСЂСЊ СЃС‚СЂРѕРєСѓ РїРѕРґРєР»СЋС‡РµРЅРёСЏ Рё РґРѕСЃС‚СѓРїРЅРѕСЃС‚СЊ СЃРµСЂРІРµСЂР°. {ex.Message}");
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

        ShowStatus("Р’РѕР№РґРёС‚Рµ РІ СЃРёСЃС‚РµРјСѓ", "РћРїРµСЂР°С†РёСЏ РЅРµ СЃРѕС…СЂР°РЅРµРЅР°: РїРѕР»СЊР·РѕРІР°С‚РµР»СЊ РЅРµ Р°РІС‚РѕСЂРёР·РѕРІР°РЅ.");
        return false;
    }

    private int? ResolveCurrentOrAdminUserId(IUnitOfWork unitOfWork)
    {
        if (_currentUserId > 0 && unitOfWork.Users.Any(user => user.Id == _currentUserId))
        {
            return _currentUserId;
        }

        return unitOfWork.Users.GetFirstAdminId();
    }
}
