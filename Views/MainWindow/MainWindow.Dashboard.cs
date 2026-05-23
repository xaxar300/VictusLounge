using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;

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

    private void RefreshEffectiveComputerStatuses(AppDbContext dbContext)
    {
        _pcStatusOverrides.Clear();

        var now = DateTime.Now;
        var activeSessionComputerIds = dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now))
            .Select(session => session.ComputerId)
            .ToHashSet();
        var imminentBookingComputerIds = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status != BookingStatuses.Cancelled
                && booking.StartTime <= now.AddMinutes(15)
                && booking.EndTime > now)
            .Select(booking => booking.ComputerId)
            .ToHashSet();

        foreach (var computer in _computers)
        {
            var savedStatus = NormalizePcStatus(computer.Status);
            var effectiveStatus = savedStatus == PcStatuses.Service
                ? PcStatuses.Service
                : activeSessionComputerIds.Contains(computer.Id)
                    ? PcStatuses.Busy
                    : imminentBookingComputerIds.Contains(computer.Id)
                        ? PcStatuses.Reserved
                        : PcStatuses.Free;

            computer.Status = effectiveStatus;
            _pcStatusOverrides[computer.Name] = effectiveStatus;
        }
    }

    private static void NormalizeDatabaseState(AppDbContext dbContext)
    {
        var now = DateTime.Now;
        var hasChanges = false;

        var expiredSessions = dbContext.GameSessions
            .Where(session => session.Status != SessionStatuses.Closed
                && session.Status != SessionStatuses.Team
                && session.EndTime != null
                && session.EndTime <= now)
            .ToList();
        foreach (var session in expiredSessions)
        {
            session.Status = SessionStatuses.Closed;
            hasChanges = true;
        }

        var activeSessionComputerIds = dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now))
            .Select(session => session.ComputerId)
            .ToHashSet();
        var imminentBookingComputerIds = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status != BookingStatuses.Cancelled
                && booking.StartTime <= now.AddMinutes(15)
                && booking.EndTime > now)
            .Select(booking => booking.ComputerId)
            .ToHashSet();

        foreach (var computer in dbContext.Computers)
        {
            var savedStatus = NormalizePcStatus(computer.Status);
            var actualStatus = savedStatus == PcStatuses.Service
                ? PcStatuses.Service
                : activeSessionComputerIds.Contains(computer.Id)
                    ? PcStatuses.Busy
                    : imminentBookingComputerIds.Contains(computer.Id)
                        ? PcStatuses.Reserved
                        : PcStatuses.Free;

            if (computer.Status != actualStatus)
            {
                computer.Status = actualStatus;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            dbContext.SaveChanges();
        }
    }

    private void UpdateDashboardSummary(AppDbContext dbContext)
    {
        if (!IsLoaded)
        {
            return;
        }

        var totalPcs = _computers.Count;
        var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
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
        var activeBookingQuery = dbContext.Bookings
            .AsNoTracking()
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

        var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
        var busyPcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Busy);
        var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
        AnnouncementTextA.Text =
            $"Свободно ПК: {freePcs} · занято: {busyPcs} · сервис: {servicePcs} · Standard {_standardRate} BYN/ч · VIP {_vipRate} BYN/ч · Royal {_royalRate} BYN/ч ·";
        AnnouncementTextB.Text = AnnouncementTextA.Text;
        ResetAnnouncementMarquee();
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

    private void RebuildTodayClubList(AppDbContext dbContext)
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
        var users = dbContext.Users.AsNoTracking().ToDictionary(user => user.Id);
        var items = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.StartTime.Date == today && booking.Status != BookingStatuses.Cancelled)
            .OrderBy(booking => booking.StartTime)
            .Take(3)
            .ToList();

        if (items.Count == 0)
        {
            var activeSessions = dbContext.GameSessions
                .AsNoTracking()
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

    private void RebuildOwnerStaffList(AppDbContext dbContext)
    {
        if (!IsLoaded || OwnerStaffList is null)
        {
            return;
        }

        OwnerStaffList.Children.Clear();
        var shifts = dbContext.Shifts
            .AsNoTracking()
            .OrderByDescending(shift => shift.StartTime)
            .Take(3)
            .ToList();

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
