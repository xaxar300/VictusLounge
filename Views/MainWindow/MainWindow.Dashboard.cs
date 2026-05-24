using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge;

public partial class MainWindow
{
    private void UpdateDashboardLoadBars()
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateZoneLoad("Standard", StandardZoneLoadBar, StandardZoneLoadText);
        UpdateZoneLoad("VIP", VipZoneLoadBar, VipZoneLoadText);
        UpdateZoneLoad("Bootcamp", BootcampZoneLoadBar, BootcampZoneLoadText);
        UpdateZoneLoad("Royal", RoyalZoneLoadBar, RoyalZoneLoadText);
    }

    private void RefreshLiveViewsAfterDatabaseChange()
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshBalanceHistoryFromDatabase();
    }

    private void RefreshWorkspaceAfterStateChange()
    {
        LoadDatabaseState();
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshAdminUx();
    }

    private void RefreshEffectiveComputerStatuses(IUnitOfWork unitOfWork)
    {
        _pcStatusOverrides.Clear();

        var now = DateTime.Now;
        var activeSessionComputerIds = GetActiveSessionComputerIds(unitOfWork, now);
        var imminentBookingComputerIds = GetImminentBookingComputerIds(unitOfWork, now);

        foreach (var computer in _computers)
        {
            var effectiveStatus = ResolveEffectiveComputerStatus(computer, activeSessionComputerIds, imminentBookingComputerIds);
            computer.Status = effectiveStatus;
            _pcStatusOverrides[computer.Name] = effectiveStatus;
        }
    }

    private static void NormalizeDatabaseState(IUnitOfWork unitOfWork)
    {
        var now = DateTime.Now;
        var hasChanges = false;
        var closedSessionUserIds = new HashSet<int>();

        var expiredSessions = unitOfWork.GameSessions.Query()
            .Where(session => session.Status != SessionStatuses.Closed
                && session.Status != SessionStatuses.Team
                && session.EndTime != null
                && session.EndTime <= now)
            .ToList();
        foreach (var session in expiredSessions)
        {
            session.Status = SessionStatuses.Closed;
            closedSessionUserIds.Add(session.UserId);
            hasChanges = true;
        }

        var activeSessionComputerIds = GetActiveSessionComputerIds(unitOfWork, now);
        var imminentBookingComputerIds = GetImminentBookingComputerIds(unitOfWork, now);

        foreach (var computer in unitOfWork.Computers.Query())
        {
            var actualStatus = ResolveEffectiveComputerStatus(computer, activeSessionComputerIds, imminentBookingComputerIds);

            if (computer.Status != actualStatus)
            {
                computer.Status = actualStatus;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            unitOfWork.SaveChanges();
            foreach (var userId in closedSessionUserIds)
            {
                RefreshStoredClientTier(unitOfWork, userId);
            }
        }
    }

    private static HashSet<int> GetActiveSessionComputerIds(IUnitOfWork unitOfWork, DateTime now)
    {
        return unitOfWork.GameSessions
            .QueryNoTracking()
            .Where(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now))
            .Select(session => session.ComputerId)
            .ToHashSet();
    }

    private static HashSet<int> GetImminentBookingComputerIds(IUnitOfWork unitOfWork, DateTime now)
    {
        return unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.Status != BookingStatuses.Cancelled
                && booking.StartTime <= now.AddMinutes(15)
                && booking.EndTime > now)
            .Select(booking => booking.ComputerId)
            .ToHashSet();
    }

    private static string ResolveEffectiveComputerStatus(
        Computer computer,
        HashSet<int> activeSessionComputerIds,
        HashSet<int> imminentBookingComputerIds)
    {
        var savedStatus = NormalizePcStatus(computer.Status);
        return savedStatus == PcStatuses.Service
            ? PcStatuses.Service
            : activeSessionComputerIds.Contains(computer.Id)
                ? PcStatuses.Busy
                : imminentBookingComputerIds.Contains(computer.Id)
                    ? PcStatuses.Reserved
                    : PcStatuses.Free;
    }

    private void UpdateDashboardSummary(IUnitOfWork unitOfWork)
    {
        if (!IsLoaded)
        {
            return;
        }

        var totalPcs = _computers.Count;
        var freePcs = CountComputersByStatus(PcStatuses.Free);
        var availableZones = _computers
            .GroupBy(computer => NormalizeZoneGroup(computer.Zone))
            .Count(group => group.Any(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free));
        var totalZones = _computers
            .Select(computer => NormalizeZoneGroup(computer.Zone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        DashboardFreePcsValue.Text = freePcs.ToString();
        DashboardFreePcsHint.Text = $"из {totalPcs}";
        DashboardZonesValue.Text = $"{availableZones}/{totalZones}";
        DashboardZonesHint.Text = "зоны с доступными местами";

        var now = DateTime.Now;
        var activeBookingQuery = unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.Status != BookingStatuses.Cancelled && booking.EndTime >= now);

        if (_currentUserId > 0)
        {
            var myBookings = activeBookingQuery
                .Where(booking => booking.UserId == _currentUserId)
                .OrderBy(booking => booking.StartTime)
                .ToList();
            DashboardBookingLabel.Text = "Мои брони";
            DashboardBookingValue.Text = myBookings.Count.ToString();
            DashboardBookingHint.Text = myBookings.FirstOrDefault() is { } nextBooking
                ? $"ближайшая {nextBooking.StartTime:dd.MM HH:mm}"
                : "нет ближайшей брони";
        }
        else
        {
            var todayBookings = activeBookingQuery.Count(booking => booking.StartTime.Date == DateTime.Today);
            DashboardBookingLabel.Text = "Брони сегодня";
            DashboardBookingValue.Text = todayBookings.ToString();
            DashboardBookingHint.Text = "по данным SQL Server";
        }

        DashboardEventsValue.Text = "3";
        DashboardEventsHint.Text = "Dota 2 · CS2 · LAN";
    }

    private static string NormalizeZoneGroup(string zone)
    {
        if (zone.Contains("Royal", StringComparison.OrdinalIgnoreCase))
        {
            return "Royal";
        }

        if (zone.Contains("Bootcamp", StringComparison.OrdinalIgnoreCase))
        {
            return "Bootcamp";
        }

        if (zone.Contains("VIP", StringComparison.OrdinalIgnoreCase))
        {
            return "VIP";
        }

        return "Standard";
    }

    private void UpdateZoneLoad(string zonePart, ProgressBar bar, TextBlock label)
    {
        var total = 0;
        var free = 0;
        foreach (var computer in _computers)
        {
            if (!NormalizeZoneGroup(computer.Zone).Equals(zonePart, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            total++;
            if (NormalizePcStatus(computer.Status) == PcStatuses.Free)
            {
                free++;
            }
        }

        bar.Value = total == 0 ? 0 : Math.Round((double)(total - free) / total * 100);
        label.Text = $"{free}/{total} свободно";
    }

    private void UpdateAnnouncementText()
    {
        if (!IsLoaded)
        {
            return;
        }

        var freePcs = CountComputersByStatus(PcStatuses.Free);
        var busyPcs = CountComputersByStatus(PcStatuses.Busy);
        var servicePcs = CountComputersByStatus(PcStatuses.Service);
        AnnouncementTextA.Text =
            $"Свободно ПК: {freePcs} · занято: {busyPcs} · сервис: {servicePcs} · Standard {_standardRate} BYN/ч · VIP {_vipRate} BYN/ч · Royal {_royalRate} BYN/ч ·";
        AnnouncementTextB.Text = AnnouncementTextA.Text;
        ResetAnnouncementMarquee();
    }

    private int CountComputersByStatus(string status)
    {
        return _computers.Count(computer => NormalizePcStatus(computer.Status) == status);
    }

    private void UpdateCabinetNextBenefit()
    {
        if (!IsLoaded)
        {
            return;
        }

        const decimal eveningPackPrice = 29m;
        var regularFourHours = _standardRate * 4m;
        var saving = regularFourHours - eveningPackPrice;
        CabinetNextBenefitText.Text = saving > 0
            ? $"Evening Pack выгоднее обычной оплаты на {saving:0.##} BYN при игре 4 часа."
            : $"Для игры на 4 часа сейчас выгоднее обычный тариф Standard: {regularFourHours:0.##} BYN.";
    }

    private void RebuildTodayClubList(IUnitOfWork unitOfWork)
    {
        if (!IsLoaded)
        {
            return;
        }

        while (TodayClubList.Children.Count > 1)
        {
            TodayClubList.Children.RemoveAt(1);
        }

        var today = DateTime.Today;
        var computers = _computers.ToDictionary(computer => computer.Id);
        var users = unitOfWork.Users.QueryNoTracking().ToDictionary(user => user.Id);
        var items = unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.StartTime.Date == today && booking.Status != BookingStatuses.Cancelled)
            .OrderBy(booking => booking.StartTime)
            .Take(3)
            .ToList();

        if (items.Count == 0)
        {
            var activeSessions = unitOfWork.GameSessions
                .QueryNoTracking()
                .Where(session => session.Status != SessionStatuses.Closed
                    && session.StartTime <= DateTime.Now
                    && (session.EndTime == null || session.EndTime > DateTime.Now))
                .OrderBy(session => session.StartTime)
                .Take(3)
                .ToList();

            foreach (var session in activeSessions)
            {
                computers.TryGetValue(session.ComputerId, out var computer);
                users.TryGetValue(session.UserId, out var user);
                AddTodayClubItem(
                    session.StartTime.ToString("HH:mm"),
                    $"{computer?.Name ?? "ПК"} · {user?.FullName ?? "клиент"}",
                    $"{computer?.Zone ?? "-"} · активная сессия");
            }
        }
        else
        {
            foreach (var booking in items)
            {
                computers.TryGetValue(booking.ComputerId, out var computer);
                users.TryGetValue(booking.UserId, out var user);
                AddTodayClubItem(
                    booking.StartTime.ToString("HH:mm"),
                    $"{computer?.Name ?? "ПК"} · {user?.FullName ?? "клиент"}",
                    $"{computer?.Zone ?? "-"} · {booking.Status}");
            }
        }

        if (TodayClubList.Children.Count == 1)
        {
            TodayClubList.Children.Add(new TextBlock
            {
                Text = "На сегодня нет активных броней и сессий.",
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void RebuildOwnerStaffList(IUnitOfWork unitOfWork)
    {
        if (!IsLoaded || OwnerStaffList is null)
        {
            return;
        }

        OwnerStaffList.Children.Clear();
        var shifts = unitOfWork.Shifts.GetRecent(3);

        if (shifts.Count == 0)
        {
            OwnerStaffList.Children.Add(new TextBlock
            {
                Text = "Смены пока не заведены.",
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var shift in shifts)
        {
            var endText = shift.EndTime?.ToString("HH:mm") ?? "открыта";
            OwnerStaffList.Children.Add(new TextBlock
            {
                Text = $"{shift.EmployeeName} · {shift.StartTime:dd.MM HH:mm}-{endText} · касса {shift.CashTotal:0.##} BYN",
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void AddTodayClubItem(string time, string title, string subtitle)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, TodayClubList.Children.Count < 4 ? 13 : 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = time,
            Foreground = (Brush)FindResource("GoldLightBrush"),
            FontWeight = FontWeights.Bold
        });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        text.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        TodayClubList.Children.Add(row);
    }

}
