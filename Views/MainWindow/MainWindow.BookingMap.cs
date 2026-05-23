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
using VictusLounge.Repositories;

namespace VictusLounge;

public partial class MainWindow
{
    private void ZoneCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        ShowStatus($"Р’С‹Р±СЂР°РЅР° Р·РѕРЅР° {element.Tag}", GetZoneDetails(element.Tag?.ToString() ?? "Standard"));
    }

    private void PcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length < 3)
        {
            return;
        }

        var pc = parts[0];
        var zone = parts[1];
        var status = GetPcStatus(pc, parts[2]);
        _selectedMapPc = pc;
        _selectedMapZone = zone;
        _selectedMapStatus = status;
        var statusText = GetStatusText(status, true);

        PcDetailTitle.Text = pc;
        PcDetailSubtitle.Text = $"{zone}: СЃС‚Р°С‚СѓСЃ вЂ” {statusText}.";
        PcPhotoCaption.Text = $"{pc} В· {zone}";
        var specs = GetPcSpecs(zone);
        PcCpuText.Text = specs.Cpu;
        PcGpuText.Text = specs.Gpu;
        PcRamText.Text = specs.Ram;
        PcMonitorText.Text = specs.Monitor;
        PcIntervalsText.Text = status == PcStatuses.Free
            ? "РЎРІРѕР±РѕРґРЅРѕ СЃРµРіРѕРґРЅСЏ: 18:00-20:00, 21:00-23:00."
            : "Р‘Р»РёР¶Р°Р№С€РёР№ СЃРІРѕР±РѕРґРЅС‹Р№ РёРЅС‚РµСЂРІР°Р» РїРѕСЏРІРёС‚СЃСЏ РїРѕСЃР»Рµ Р·Р°РІРµСЂС€РµРЅРёСЏ С‚РµРєСѓС‰РµРіРѕ СЃС‚Р°С‚СѓСЃР°.";
        UpdateSelectedMapPcBookingButton(status);

        ShowStatus($"Р’С‹Р±СЂР°РЅ {pc}", $"{zone}, СЃС‚Р°С‚СѓСЃ: {statusText}.");
    }

    private void UpdateSelectedMapPcBookingButton(string status)
    {
        BookSelectedPcButton.IsEnabled = status == PcStatuses.Free;
        BookSelectedPcButton.Opacity = status == PcStatuses.Free ? 1 : 0.55;
        BookSelectedPcButton.Content = status == PcStatuses.Free
            ? T("Map.BookSelected")
            : T("Map.PcUnavailable");
    }

    private void BookSelectedPc_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedMapPc) || string.IsNullOrWhiteSpace(_selectedMapZone))
        {
            ShowStatus("РџРљ РЅРµ РІС‹Р±СЂР°РЅ", "РЎРЅР°С‡Р°Р»Р° РІС‹Р±РµСЂРёС‚Рµ РјРµСЃС‚Рѕ РЅР° СЃС…РµРјРµ РєР»СѓР±Р°.");
            return;
        }

        if (_selectedMapStatus != PcStatuses.Free)
        {
            ShowStatus("РџРљ РЅРµРґРѕСЃС‚СѓРїРµРЅ", "Р­С‚РѕС‚ РџРљ СЃРµР№С‡Р°СЃ РЅРµР»СЊР·СЏ Р·Р°Р±СЂРѕРЅРёСЂРѕРІР°С‚СЊ: РѕРЅ Р·Р°РЅСЏС‚, РІ Р±СЂРѕРЅРё РёР»Рё РЅР° РѕР±СЃР»СѓР¶РёРІР°РЅРёРё.");
            return;
        }

        ApplyZoneFromMap(_selectedMapZone);
        _selectedSeats.Clear();
        _selectedSeats.Add(_selectedMapPc);
        RebuildBookingSeatGrid();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        NavigateTo("booking");
        ShowStatus("РџРљ РїРµСЂРµРЅРµСЃРµРЅ РІ Р±СЂРѕРЅСЊ", $"{_selectedMapPc} СѓР¶Рµ РІС‹Р±СЂР°РЅ РІ С„РѕСЂРјРµ Р±СЂРѕРЅРёСЂРѕРІР°РЅРёСЏ.");
    }

    private void BookingMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _isCompanyBooking = button.Tag?.ToString() == "company";
        SingleModeButton.Style = (Style)FindResource(_isCompanyBooking ? "GhostButtonStyle" : "PrimaryButtonStyle");
        CompanyModeButton.Style = (Style)FindResource(_isCompanyBooking ? "PrimaryButtonStyle" : "GhostButtonStyle");

        if (!_isCompanyBooking && _selectedSeats.Count > 1)
        {
            var firstSeat = _selectedSeats.Order(StringComparer.Ordinal).First();
            _selectedSeats.Clear();
            _selectedSeats.Add(firstSeat);
        }

        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus(
            _isCompanyBooking ? "Р‘СЂРѕРЅСЊ РґР»СЏ РєРѕРјРїР°РЅРёРё" : "РћРґРёРЅРѕС‡РЅР°СЏ Р±СЂРѕРЅСЊ",
            _isCompanyBooking ? "РњРѕР¶РЅРѕ РІС‹Р±СЂР°С‚СЊ РЅРµСЃРєРѕР»СЊРєРѕ РџРљ." : "РђРєС‚РёРІРµРЅ РІС‹Р±РѕСЂ РѕРґРЅРѕРіРѕ РџРљ.");
    }

    private void BookingDate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DateTime.TryParse(button.Tag?.ToString(), out var date))
        {
            _bookingDate = date;
            SetActiveButton(button, DateTodayButton, DateTomorrowButton, DateThirdButton, DateCustomButton);
            UpdateBookingSummary();
            ShowStatus("Р”Р°С‚Р° РёР·РјРµРЅРµРЅР°", $"Р‘СЂРѕРЅСЊ РїРµСЂРµРЅРµСЃРµРЅР° РЅР° {_bookingDate:yyyy-MM-dd}.");
        }
    }

    private void BookingZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var tariff))
        {
            return;
        }

        _bookingZoneKey = parts[0];
        _bookingZoneName = parts[1];
        _bookingTariff = GetTariffPrice(parts[0], tariff);
        _selectedSeats.Clear();
        SetActiveButton(button, ZoneStandardButton, ZoneVipButton, ZoneBootcampButton, ZoneRoyalButton);
        RebuildBookingSeatGrid();
        UpdateBookingSummary();
        ShowStatus("Р—РѕРЅР° РёР·РјРµРЅРµРЅР°", $"РџРѕРєР°Р·Р°РЅС‹ РџРљ РґР»СЏ С‚Р°СЂРёС„Р°: {_bookingZoneName}.");
    }

    private void ToggleTimePicker_Click(object sender, RoutedEventArgs e)
    {
        TimePickerPanel.Visibility = TimePickerPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void BookingHour_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var hour))
        {
            return;
        }

        _bookingHour = hour;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Р’СЂРµРјСЏ РёР·РјРµРЅРµРЅРѕ", $"РќР°С‡Р°Р»Рѕ Р±СЂРѕРЅРё: {GetBookingStartTime()}.");
    }

    private void BookingMinute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var minute))
        {
            return;
        }

        if (!IsMinuteAllowedForCurrentPackage(minute))
        {
            ShowStatus("РњРёРЅСѓС‚С‹ РЅРµРґРѕСЃС‚СѓРїРЅС‹", "РџР°РєРµС‚РЅС‹Рµ С‚Р°СЂРёС„С‹ СЃС‚Р°СЂС‚СѓСЋС‚ СЂРѕРІРЅРѕ РІ РІС‹Р±СЂР°РЅРЅС‹Р№ С‡Р°СЃ.");
            return;
        }

        _bookingMinute = minute;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("РњРёРЅСѓС‚С‹ РёР·РјРµРЅРµРЅС‹", $"РќР°С‡Р°Р»Рѕ Р±СЂРѕРЅРё: {GetBookingStartTime()}.");
    }

    private void DurationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var tag = button.Tag?.ToString();
        switch (tag)
        {
            case "night":
                _bookingPackage = "night";
                _bookingDuration = 8;
                _bookingMinute = 0;
                if (!IsNightPackHour(_bookingHour))
                {
                    _bookingHour = 22;
                }
                break;
            case "morning":
                _bookingPackage = "morning";
                _bookingDuration = 3;
                _bookingMinute = 0;
                if (!IsMorningPackHour(_bookingHour))
                {
                    _bookingHour = 6;
                }
                break;
            default:
                if (!int.TryParse(tag, out var duration))
                {
                    return;
                }
                _bookingPackage = "regular";
                _bookingDuration = duration;
                break;
        }

        SetActiveButton(button, Duration1Button, Duration2Button, Duration3Button, MorningPackButton, Duration8Button);
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("РўР°СЂРёС„ РѕР±РЅРѕРІР»РµРЅ", GetPackageDescription());
    }

    private void BookingSeat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var seat = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(seat))
        {
            return;
        }

        if (!_isCompanyBooking)
        {
            _selectedSeats.Clear();
        }
        else if (!_selectedSeats.Contains(seat) && _selectedSeats.Count >= 5)
        {
            BookingErrorText.Text = "Р“СЂСѓРїРїРѕРІР°СЏ Р±СЂРѕРЅСЊ РѕРіСЂР°РЅРёС‡РµРЅР° 5 РџРљ.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("Р›РёРјРёС‚ Р±СЂРѕРЅРё", "Р”Р»СЏ РєРѕРјРїР°РЅРёРё РјРѕР¶РЅРѕ РІС‹Р±СЂР°С‚СЊ РјР°РєСЃРёРјСѓРј 5 РџРљ.");
            return;
        }

        if (!_selectedSeats.Add(seat))
        {
            _selectedSeats.Remove(seat);
        }

        BookingErrorText.Visibility = Visibility.Collapsed;
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Р’С‹Р±РѕСЂ РџРљ РѕР±РЅРѕРІР»РµРЅ", _selectedSeats.Count == 0 ? "РџРљ РїРѕРєР° РЅРµ РІС‹Р±СЂР°РЅ." : $"Р’С‹Р±СЂР°РЅРѕ: {string.Join(", ", _selectedSeats)}.");
    }

    private void ConfirmBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSeats.Count == 0)
        {
            BookingErrorText.Text = "Р’С‹Р±РµСЂРёС‚Рµ С…РѕС‚СЏ Р±С‹ РѕРґРёРЅ СЃРІРѕР±РѕРґРЅС‹Р№ РџРљ РїРµСЂРµРґ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёРµРј.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("РќСѓР¶РµРЅ РџРљ", "Р’С‹Р±РµСЂРёС‚Рµ С…РѕС‚СЏ Р±С‹ РѕРґРёРЅ СЃРІРѕР±РѕРґРЅС‹Р№ РџРљ РїРµСЂРµРґ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёРµРј.");
            return;
        }

        if (!_isCompanyBooking && _selectedSeats.Count > 1)
        {
            BookingErrorText.Text = "РћРґРёРЅРѕС‡РЅР°СЏ Р±СЂРѕРЅСЊ РјРѕР¶РµС‚ СЃРѕРґРµСЂР¶Р°С‚СЊ С‚РѕР»СЊРєРѕ 1 РџРљ.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("Р›РёРјРёС‚ Р±СЂРѕРЅРё", "РџРµСЂРµРєР»СЋС‡РёС‚РµСЃСЊ РЅР° РіСЂСѓРїРїРѕРІСѓСЋ Р±СЂРѕРЅСЊ, РµСЃР»Рё РЅСѓР¶РЅРѕ РЅРµСЃРєРѕР»СЊРєРѕ РџРљ.");
            return;
        }

        if (_isCompanyBooking && _selectedSeats.Count > 5)
        {
            BookingErrorText.Text = "Р“СЂСѓРїРїРѕРІР°СЏ Р±СЂРѕРЅСЊ РѕРіСЂР°РЅРёС‡РµРЅР° 5 РџРљ.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("Р›РёРјРёС‚ Р±СЂРѕРЅРё", "РЈР±РµСЂРёС‚Рµ Р»РёС€РЅРёРµ РџРљ РїРµСЂРµРґ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёРµРј.");
            return;
        }

        BookingErrorText.Visibility = Visibility.Collapsed;

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        if (start <= DateTime.Now.AddMinutes(-15) || end <= start)
        {
            BookingErrorText.Text = "РќРµР»СЊР·СЏ СЃРѕР·РґР°С‚СЊ Р±СЂРѕРЅСЊ РЅР° РїСЂРѕС€РµРґС€РµРµ РёР»Рё РЅРµРєРѕСЂСЂРµРєС‚РЅРѕРµ РІСЂРµРјСЏ.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("РќРµРєРѕСЂСЂРµРєС‚РЅРѕРµ РІСЂРµРјСЏ", "Р’С‹Р±РµСЂРёС‚Рµ Р°РєС‚СѓР°Р»СЊРЅРѕРµ РІСЂРµРјСЏ РЅР°С‡Р°Р»Р° Рё РґР»РёС‚РµР»СЊРЅРѕСЃС‚СЊ Р±СЂРѕРЅРё.");
            return;
        }

        BookingConfirmText.Text =
            $"РџРљ: {SummarySeatsText.Text}\n" +
            $"Р—РѕРЅР°: {SummaryZoneText.Text}\n" +
            $"Р”Р°С‚Р°: {SummaryDateText.Text}\n" +
            $"Р’СЂРµРјСЏ: {SummaryTimeText.Text}\n" +
            $"РўР°СЂРёС„: {SummaryTariffText.Text}\n" +
            $"РС‚РѕРіРѕ: {SummaryTotalText.Text}";

        if (!SaveBookingSelectionToDatabase())
        {
            return;
        }

        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshAdminUx();
        BookingConfirmOverlay.Visibility = Visibility.Visible;
        ShowImportantStatus("Р‘СЂРѕРЅСЊ РїРѕРґС‚РІРµСЂР¶РґРµРЅР°", $"{SummarySeatsText.Text}, {SummaryDateText.Text}, {SummaryTimeText.Text}. РС‚РѕРі: {SummaryTotalText.Text}.");
    }

    private bool SaveBookingSelectionToDatabase()
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            BookingErrorText.Text = "Р’РѕР№РґРёС‚Рµ РІ СЃРёСЃС‚РµРјСѓ РїРµСЂРµРґ Р±СЂРѕРЅРёСЂРѕРІР°РЅРёРµРј.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        var seats = _selectedSeats.Order().ToArray();

        if (!_isCompanyBooking && seats.Length > 1)
        {
            BookingErrorText.Text = "РћРґРёРЅРѕС‡РЅР°СЏ Р±СЂРѕРЅСЊ РјРѕР¶РµС‚ СЃРѕРґРµСЂР¶Р°С‚СЊ С‚РѕР»СЊРєРѕ 1 РџРљ.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        if (_isCompanyBooking && seats.Length > 5)
        {
            BookingErrorText.Text = "Р“СЂСѓРїРїРѕРІР°СЏ Р±СЂРѕРЅСЊ РѕРіСЂР°РЅРёС‡РµРЅР° 5 РџРљ.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        if (end <= start)
        {
            BookingErrorText.Text = "РћРєРѕРЅС‡Р°РЅРёРµ Р±СЂРѕРЅРё РґРѕР»Р¶РЅРѕ Р±С‹С‚СЊ РїРѕР·Р¶Рµ РµС‘ РЅР°С‡Р°Р»Р°.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        if (start < DateTime.Now.AddMinutes(-15))
        {
            BookingErrorText.Text = "РќРµР»СЊР·СЏ Р±СЂРѕРЅРёСЂРѕРІР°С‚СЊ РЅР° РїСЂРѕС€РµРґС€РµРµ РІСЂРµРјСЏ. Р’С‹Р±РµСЂРёС‚Рµ Р±Р»РёР¶Р°Р№С€РёР№ СЃРІРѕР±РѕРґРЅС‹Р№ С‡Р°СЃ.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();

            var resolvedComputers = new Dictionary<string, Computer>(StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                var computer = unitOfWork.Computers.GetByName(seat);
                if (computer is null)
                {
                    BookingErrorText.Text = $"РџРљ {seat} РЅРµ РЅР°Р№РґРµРЅ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                if (NormalizePcStatus(computer.Status) == PcStatuses.Service)
                {
                    BookingErrorText.Text = $"РџРљ {seat} РЅР°С…РѕРґРёС‚СЃСЏ РІ РѕР±СЃР»СѓР¶РёРІР°РЅРёРё. Р’С‹Р±РµСЂРёС‚Рµ РґСЂСѓРіРѕРµ РјРµСЃС‚Рѕ.";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                var hasConflict = unitOfWork.Bookings.HasTimeConflict(computer.Id, start, end);
                if (hasConflict)
                {
                    BookingErrorText.Text = $"РџРљ {seat} СѓР¶Рµ Р·Р°РЅСЏС‚ РЅР° СЌС‚Рѕ РІСЂРµРјСЏ. Р’С‹Р±РµСЂРёС‚Рµ РґСЂСѓРіРѕР№ РёРЅС‚РµСЂРІР°Р» РёР»Рё РјРµСЃС‚Рѕ.";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                var hasSessionConflict = unitOfWork.GameSessions.HasTimeConflict(computer.Id, start, end);
                if (hasSessionConflict)
                {
                    BookingErrorText.Text = $"РџРљ {seat} СѓР¶Рµ Р·Р°РЅСЏС‚ РёРіСЂРѕРІРѕР№ СЃРµСЃСЃРёРµР№ РЅР° СЌС‚Рѕ РІСЂРµРјСЏ. Р’С‹Р±РµСЂРёС‚Рµ РґСЂСѓРіРѕР№ РёРЅС‚РµСЂРІР°Р» РёР»Рё РјРµСЃС‚Рѕ.";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                resolvedComputers[seat] = computer;
            }

            var nextBookingId = unitOfWork.Bookings.GetNextId(booking => booking.Id);
            var isImminent = start.Date == DateTime.Today && start <= DateTime.Now.AddMinutes(15);

            foreach (var seat in seats)
            {
                var computer = resolvedComputers[seat];

                unitOfWork.Bookings.Add(new Booking
                {
                    Id = nextBookingId++,
                    UserId = _currentUserId,
                    ComputerId = computer.Id,
                    StartTime = start,
                    EndTime = end,
                    Status = BookingStatuses.PendingPayment,
                    Package = _bookingPackage,
                    TotalPrice = Math.Round(computer.HourPrice * (decimal)_bookingDuration * GetDiscountFactor(), 2),
                    CreatedAt = DateTime.Now
                });

                if (isImminent)
                {
                    computer.Status = PcStatuses.Reserved;
                }
            }

            unitOfWork.SaveChanges();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ Р±СЂРѕРЅРё", ex);
            BookingErrorText.Text = "РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ Р±СЂРѕРЅСЊ: РїСЂРѕРІРµСЂСЊС‚Рµ РїРѕРґРєР»СЋС‡РµРЅРёРµ Рє SQL Server.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        LoadDatabaseState();
        return true;
    }

    private void CloseBookingConfirm_Click(object sender, RoutedEventArgs e)
    {
        BookingConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClearBooking_Click(object sender, RoutedEventArgs e)
    {
        _selectedSeats.Clear();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Р’С‹Р±РѕСЂ РѕС‡РёС‰РµРЅ", "РњРѕР¶РЅРѕ СЃРѕР±СЂР°С‚СЊ Р±СЂРѕРЅСЊ Р·Р°РЅРѕРІРѕ.");
    }

    private void UpdateBookingSummary()
    {
        if (!IsLoaded)
        {
            return;
        }

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        var seatsCount = Math.Max(_selectedSeats.Count, 1);
        var total = _bookingTariff * _bookingDuration * seatsCount * GetDiscountFactor();
        var baseTotal = _bookingTariff * _bookingDuration * seatsCount;
        var discount = baseTotal - total;

        SummarySeatsText.Text = _selectedSeats.Count == 0 ? "вЂ”" : string.Join(", ", _selectedSeats.Order());
        SummaryZoneText.Text = _bookingZoneName;
        SummaryDateText.Text = _bookingDate.ToString("yyyy-MM-dd");
        SummaryTimeText.Text = $"{start:HH:mm}-{end:HH:mm}";
        SummaryDurationText.Text = $"{_bookingDuration} С‡";
        SummaryTariffText.Text = $"{_bookingTariff} BYN/С‡Р°СЃ В· {GetTariffLabel()}";
        SummaryBaseTotalText.Text = _selectedSeats.Count == 0 ? "0 BYN" : $"{baseTotal:0.##} BYN";
        SummaryDiscountText.Text = _selectedSeats.Count == 0 ? GetTariffLabel() : $"{GetTariffLabel()} В· в€’{discount:0.##} BYN";
        SummaryTotalText.Text = _selectedSeats.Count == 0 ? "0 BYN" : $"{total:0.##} BYN";
        TimePickerToggleButton.Content = $"Р’С‹Р±СЂР°С‚СЊ РІСЂРµРјСЏ: {GetBookingStartTime()}";
        PackageHintText.Text = GetPackageDescription();
        BookingWarningText.Visibility = end.Date > start.Date ? Visibility.Visible : Visibility.Collapsed;
        BookingWarningText.Text = $"Р‘СЂРѕРЅСЊ Р·Р°РєРѕРЅС‡РёС‚СЃСЏ РЅР° СЃР»РµРґСѓСЋС‰РёР№ РґРµРЅСЊ: {end:dd.MM HH:mm}.";
        BookingErrorText.Visibility = Visibility.Collapsed;
        SyncBookingViewModel();
    }

    private void RebuildBookingSeatGrid()
    {
        if (!IsLoaded)
        {
            return;
        }

        BookingSeatGrid.Children.Clear();
        BookingSeatGrid.Columns = _bookingZoneKey switch
        {
            "VIP" => 4,
            "Bootcamp" => 5,
            "Royal VIP" => 5,
            _ => 7
        };

        foreach (var seat in GetSeatsForZone(_bookingZoneKey))
        {
            var button = new Button
            {
                Content = seat.IsAvailable ? seat.Name : $"{seat.Name}\n{GetStatusText(seat.Status)}",
                Tag = seat.Name,
                Style = (Style)FindResource("PcButtonStyle"),
                Margin = new Thickness(0, 0, 8, 8),
                IsEnabled = seat.IsAvailable,
                ToolTip = seat.IsAvailable ? T("Status.AvailableTooltip") : $"{T("Status.UnavailableTooltip")}: {GetStatusText(seat.Status)}"
            };
            if (!seat.IsAvailable)
            {
                button.Style = (Style)FindResource("UnavailablePcButtonStyle");
            }
            button.Click += BookingSeat_Click;
            BookingSeatGrid.Children.Add(button);
        }

        UpdateBookingSeatButtons();
    }

    private void RebuildBookingTimePicker()
    {
        BookingHourGrid.Children.Clear();
        for (var hour = 0; hour < 24; hour++)
        {
            var button = new Button
            {
                Content = $"{hour:00}",
                Tag = hour,
                Style = (Style)FindResource(hour == _bookingHour ? "SelectedTimeButtonStyle" : "TimeButtonStyle"),
                Margin = new Thickness(0, 0, 8, 8),
                IsEnabled = IsHourAllowedForCurrentPackage(hour)
            };
            if (!button.IsEnabled)
            {
                button.Style = (Style)FindResource("UnavailablePcButtonStyle");
            }
            button.Click += BookingHour_Click;
            BookingHourGrid.Children.Add(button);
        }

        BookingMinuteGrid.Children.Clear();
        foreach (var minute in new[] { 0, 15, 30, 45 })
        {
            var button = new Button
            {
                Content = $"{minute:00}",
                Tag = minute,
                Style = (Style)FindResource(minute == _bookingMinute ? "SelectedTimeButtonStyle" : "TimeButtonStyle"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += BookingMinute_Click;
            BookingMinuteGrid.Children.Add(button);
        }
    }

    private void UpdateBookingTimeButtons()
    {
        foreach (var child in BookingHourGrid.Children)
        {
            if (child is Button button)
            {
                var hour = int.TryParse(button.Tag?.ToString(), out var parsedHour) ? parsedHour : -1;
                button.IsEnabled = IsHourAllowedForCurrentPackage(hour);
                button.Style = (Style)FindResource(!button.IsEnabled
                    ? "UnavailablePcButtonStyle"
                    : hour == _bookingHour ? "SelectedTimeButtonStyle" : "TimeButtonStyle");
            }
        }

        foreach (var child in BookingMinuteGrid.Children)
        {
            if (child is Button button)
            {
                var minute = int.TryParse(button.Tag?.ToString(), out var parsedMinute) ? parsedMinute : -1;
                button.IsEnabled = IsMinuteAllowedForCurrentPackage(minute);
                button.Style = (Style)FindResource(!button.IsEnabled
                    ? "UnavailablePcButtonStyle"
                    : minute == _bookingMinute ? "SelectedTimeButtonStyle" : "TimeButtonStyle");
            }
        }
    }

    private SeatInfo[] GetSeatsForZone(string zone)
    {
        var databaseSeats = _computers
            .Where(computer => computer.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .OrderBy(computer => computer.Id)
            .Select(computer => new SeatInfo(computer.Name, NormalizePcStatus(computer.Status)))
            .ToArray();

        return databaseSeats
            .Select(seat => seat with { Status = GetPcStatus(seat.Name, seat.Status) })
            .ToArray();
    }

    private string GetPcStatus(string pc, string fallback)
    {
        return _pcStatusOverrides.TryGetValue(pc, out var status) ? NormalizePcStatus(status) : NormalizePcStatus(fallback);
    }

    private void SetPcStatus(string pc, string status)
    {
        status = NormalizePcStatus(status);
        _pcStatusOverrides[pc] = status;
        SaveComputerStatus(pc, status);
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();

        if (_selectedMapPc == pc)
        {
            _selectedMapStatus = status;
        }
    }

    private void SaveComputerStatus(string pc, string status)
    {
        var localComputer = _computers.FirstOrDefault(computer => computer.Name == pc);
        if (localComputer is not null)
        {
            localComputer.Status = status;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var computer = unitOfWork.Computers.GetByName(pc);
            if (computer is null)
            {
                return;
            }

            computer.Status = status;
            unitOfWork.SaveChanges();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ СЃС‚Р°С‚СѓСЃР° РџРљ", ex);
        }
    }

    private static string NormalizePcStatus(string status)
    {
        return PcStatusNormalizer.Normalize(status);
    }

    private void SetActiveButton(Button activeButton, params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            button.Style = (Style)FindResource(ReferenceEquals(button, activeButton) ? "PrimaryButtonStyle" : "GhostButtonStyle");
        }
    }

    private string GetBookingStartTime()
    {
        return $"{_bookingHour:00}:{_bookingMinute:00}";
    }

    private bool IsHourAllowedForCurrentPackage(int hour)
    {
        return _bookingPackage switch
        {
            "night" => IsNightPackHour(hour),
            "morning" => IsMorningPackHour(hour),
            _ => true
        };
    }

    private bool IsMinuteAllowedForCurrentPackage(int minute)
    {
        return _bookingPackage == "regular" || minute == 0;
    }

    private static bool IsNightPackHour(int hour)
    {
        return hour is 22 or 23 or 0;
    }

    private static bool IsMorningPackHour(int hour)
    {
        return hour is 6 or 7 or 8;
    }

    private decimal GetDiscountFactor()
    {
        return _bookingPackage switch
        {
            "night" => 0.75m,
            "morning" => 0.8m,
            _ => 0.9m
        };
    }

    private string GetTariffLabel()
    {
        return _bookingPackage switch
        {
            "night" => "Night Pack -25%",
            "morning" => "Morning Pack -20%",
            _ => "Gold -10%"
        };
    }

    private string GetPackageDescription()
    {
        return _bookingPackage switch
        {
            "night" => "Night Pack: 8 С‡Р°СЃРѕРІ, СЃС‚Р°СЂС‚ С‚РѕР»СЊРєРѕ 22:00, 23:00 РёР»Рё 00:00, СЃРєРёРґРєР° 25%.",
            "morning" => "Morning Pack: 3 С‡Р°СЃР°, СЃС‚Р°СЂС‚ С‚РѕР»СЊРєРѕ 06:00, 07:00 РёР»Рё 08:00, СЃРєРёРґРєР° 20%.",
            _ => $"РћР±С‹С‡РЅС‹Р№ С‚Р°СЂРёС„: {_bookingDuration} С‡, СЃРєРёРґРєР° Gold 10%."
        };
    }

    private void UpdateBookingSeatButtons()
    {
        foreach (var child in BookingSeatGrid.Children)
        {
            if (child is Button button)
            {
                if (!button.IsEnabled)
                {
                    button.Style = (Style)FindResource("UnavailablePcButtonStyle");
                    continue;
                }

                var isSelected = _selectedSeats.Contains(button.Tag?.ToString() ?? string.Empty);
                button.Style = (Style)FindResource(isSelected ? "PrimaryButtonStyle" : "PcButtonStyle");
            }
        }
    }

    private void ApplyZoneFromMap(string zone)
    {
        Button button = zone switch
        {
            "VIP" => ZoneVipButton,
            "Bootcamp" => ZoneBootcampButton,
            "Royal VIP" => ZoneRoyalButton,
            _ => ZoneStandardButton
        };

        if (button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var tariff))
        {
            return;
        }

        _bookingZoneKey = parts[0];
        _bookingZoneName = parts[1];
        _bookingTariff = GetTariffPrice(parts[0], tariff);
        SetActiveButton(button, ZoneStandardButton, ZoneVipButton, ZoneBootcampButton, ZoneRoyalButton);
    }

    private void ApplyMapPcButtonStatuses()
    {
        if (!IsLoaded || MapView is null)
        {
            return;
        }

        MapView.ApplyTemplate();
        MapView.UpdateLayout();

        foreach (var button in FindVisualChildren<Button>(MapView))
        {
            if (button.Tag is not string raw)
            {
                continue;
            }

            var parts = raw.Split('|');
            if (parts.Length != 3)
            {
                continue;
            }

            var pc = parts[0];
            var status = GetPcStatus(pc, parts[2]);
            var seat = new SeatInfo(pc, status);
            button.ClearValue(Control.BorderBrushProperty);
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(UIElement.OpacityProperty);
            button.Content = seat.IsAvailable ? pc : $"{pc}\n{GetStatusText(seat.Status)}";
            button.Style = (Style)FindResource(seat.IsAvailable ? "PcButtonStyle" : "UnavailablePcButtonStyle");
            button.FocusVisualStyle = null;
            button.ToolTip = seat.IsAvailable ? T("Status.AvailableTooltip") : $"{T("Status.UnavailableTooltip")}: {GetStatusText(seat.Status)}";
            button.IsEnabled = true;
            if (!seat.IsAvailable)
            {
                button.BorderBrush = (Brush)FindResource(seat.Status == PcStatuses.Busy ? "DangerBrush" : seat.Status == PcStatuses.Reserved ? "WaitBrush" : "LineSoftBrush");
                button.Background = (Brush)FindResource("SurfaceBrush");
                button.Opacity = seat.Status == PcStatuses.Busy ? 0.82 : 0.62;
            }

            if (_selectedMapPc == pc)
            {
                _selectedMapStatus = seat.Status;
                PcDetailSubtitle.Text = $"{parts[1]}: СЃС‚Р°С‚СѓСЃ вЂ” {GetStatusText(seat.Status, true)}.";
                PcIntervalsText.Text = seat.IsAvailable
                    ? "РЎРІРѕР±РѕРґРЅРѕ СЃРµРіРѕРґРЅСЏ: 18:00-20:00, 21:00-23:00."
                    : "Р‘Р»РёР¶Р°Р№С€РёР№ СЃРІРѕР±РѕРґРЅС‹Р№ РёРЅС‚РµСЂРІР°Р» РїРѕСЏРІРёС‚СЃСЏ РїРѕСЃР»Рµ Р·Р°РІРµСЂС€РµРЅРёСЏ С‚РµРєСѓС‰РµРіРѕ СЃС‚Р°С‚СѓСЃР°.";
                UpdateSelectedMapPcBookingButton(seat.Status);
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            yield break;
        }

        if (parent is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        if (parent is ContentControl { Content: DependencyObject content })
        {
            if (content is T typedContent)
            {
                yield return typedContent;
            }

            foreach (var descendant in FindVisualChildren<T>(content))
            {
                yield return descendant;
            }
        }

        foreach (var logicalChild in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (ReferenceEquals(logicalChild, parent))
            {
                continue;
            }

            if (logicalChild is T typedLogicalChild)
            {
                yield return typedLogicalChild;
            }

            if (logicalChild is Visual or System.Windows.Media.Media3D.Visual3D or ContentControl)
            {
                foreach (var descendant in FindVisualChildren<T>(logicalChild))
                {
                    yield return descendant;
                }
            }
        }
    }

    private string GetZoneDetails(string zone)
    {
        return zone switch
        {
            "Standard" => "Standard: 18 СЃРІРѕР±РѕРґРЅС‹С… РџРљ, С‚Р°СЂРёС„ 8 BYN/С‡Р°СЃ.",
            "VIP" => "VIP: 4 СЃРІРѕР±РѕРґРЅС‹С… РџРљ, С‚Р°СЂРёС„ 14 BYN/С‡Р°СЃ.",
            "Bootcamp" => "Bootcamp: 1 СЃРІРѕР±РѕРґРЅР°СЏ РєРѕРјРЅР°С‚Р°, С‚Р°СЂРёС„ 50 BYN/С‡Р°СЃ.",
            "Royal VIP" => "Royal VIP: 3 СЃРІРѕР±РѕРґРЅС‹С… РџРљ, С‚Р°СЂРёС„ 24 BYN/С‡Р°СЃ.",
            _ => "Р—РѕРЅР° РІС‹Р±СЂР°РЅР°."
        };
    }

    private static PcSpecs GetPcSpecs(string zone)
    {
        return zone switch
        {
            "Standard" => new PcSpecs("Intel Core i5-13400F", "GeForce RTX 4060", "16 GB DDR4", "24\" 144 Hz"),
            "VIP" => new PcSpecs("AMD Ryzen 5 7600X", "GeForce RTX 4070 Super", "32 GB DDR5", "27\" 180 Hz"),
            "Bootcamp" => new PcSpecs("Intel Core i7-13700KF", "GeForce RTX 4070 Ti", "32 GB DDR5", "27\" 240 Hz"),
            "Royal VIP" => new PcSpecs("AMD Ryzen 7 7800X3D", "GeForce RTX 4080 Super", "64 GB DDR5", "32\" 240 Hz"),
            _ => new PcSpecs("Intel Core i5", "GeForce RTX", "16 GB", "144 Hz")
        };
    }

}
