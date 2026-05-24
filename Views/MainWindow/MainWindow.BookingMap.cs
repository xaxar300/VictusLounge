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
using VictusLounge.Services;

namespace VictusLounge;

public partial class MainWindow
{
    private void ConfigureBookingCommands()
    {
        _viewModel.ClubMap.ConfigureActions(SelectMapPc, BookSelectedMapPc);
        _viewModel.Booking.ConfigureActions(
            SelectBookingMode,
            SelectBookingDate,
            SelectBookingZone,
            SelectBookingDuration,
            SelectBookingHour,
            SelectBookingMinute,
            SelectBookingSeat,
            ToggleBookingTimePicker,
            ConfirmBooking,
            ClearBookingSelection,
            CloseBookingConfirmation);
    }

    private void SelectDashboardZone(string zone)
    {
        if (string.IsNullOrWhiteSpace(zone))
        {
            return;
        }

        ShowStatus($"Выбрана зона {zone}", GetZoneDetails(zone));
    }

    private void SelectMapPc(string raw)
    {
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
        var specs = GetPcSpecs(zone);

        _viewModel.ClubMap.SelectedPc = pc;
        _viewModel.ClubMap.SelectedZone = zone;
        _viewModel.ClubMap.SelectedStatus = status;
        _viewModel.ClubMap.DetailTitle = pc;
        _viewModel.ClubMap.DetailSubtitle = $"{zone}: статус — {statusText}.";
        _viewModel.ClubMap.PhotoCaption = $"{pc} · {zone}";
        _viewModel.ClubMap.Cpu = specs.Cpu;
        _viewModel.ClubMap.Gpu = specs.Gpu;
        _viewModel.ClubMap.Ram = specs.Ram;
        _viewModel.ClubMap.Monitor = specs.Monitor;
        _viewModel.ClubMap.Intervals = status == PcStatuses.Free
            ? "Свободно сегодня: 18:00-20:00, 21:00-23:00."
            : "Ближайший свободный интервал появится после завершения текущего статуса.";
        UpdateSelectedMapPcBookingButton(status);

        ShowStatus($"Выбран {pc}", $"{zone}, статус: {statusText}.");
    }

    private void UpdateSelectedMapPcBookingButton(string status)
    {
        _viewModel.ClubMap.CanBookSelectedPc = status == PcStatuses.Free;
        _viewModel.ClubMap.BookButtonText = status == PcStatuses.Free
            ? T("Map.BookSelected")
            : T("Map.PcUnavailable");
    }

    private void BookSelectedMapPc()
    {
        if (string.IsNullOrWhiteSpace(_selectedMapPc) || string.IsNullOrWhiteSpace(_selectedMapZone))
        {
            ShowStatus("ПК не выбран", "Сначала выберите место на схеме клуба.");
            return;
        }

        if (_selectedMapStatus != PcStatuses.Free)
        {
            ShowStatus("ПК недоступен", "Этот ПК сейчас нельзя забронировать: он занят, в брони или на обслуживании.");
            return;
        }

        ApplyZoneFromMap(_selectedMapZone);
        _selectedSeats.Clear();
        _selectedSeats.Add(_selectedMapPc);
        RebuildBookingSeatGrid();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        NavigateTo("booking");
        ShowStatus("ПК перенесен в бронь", $"{_selectedMapPc} уже выбран в форме бронирования.");
    }

    private void SelectBookingMode(string mode)
    {
        _isCompanyBooking = mode == "company";
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
            _isCompanyBooking ? "Бронь для компании" : "Одиночная бронь",
            _isCompanyBooking ? "Можно выбрать несколько ПК." : "Активен выбор одного ПК.");
    }

    private void SelectBookingDate(string raw)
    {
        if (!DateTime.TryParse(raw, out var date))
        {
            return;
        }

        _bookingDate = date;
        var activeButton = new[]
        {
            DateTodayButton,
            DateTomorrowButton,
            DateThirdButton,
            DateCustomButton
        }.FirstOrDefault(button => string.Equals(button.Tag?.ToString(), raw, StringComparison.OrdinalIgnoreCase));
        if (activeButton is not null)
        {
            SetActiveButton(activeButton, DateTodayButton, DateTomorrowButton, DateThirdButton, DateCustomButton);
        }

        UpdateBookingSummary();
        ShowStatus("Дата изменена", $"Бронь перенесена на {_bookingDate:yyyy-MM-dd}.");
    }

    private void SelectBookingZone(string raw)
    {
        var parts = raw.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var tariff))
        {
            return;
        }

        _bookingZoneKey = parts[0];
        _bookingZoneName = parts[1];
        _bookingTariff = GetTariffPrice(parts[0], tariff);
        _selectedSeats.Clear();
        var activeButton = new[]
        {
            ZoneStandardButton,
            ZoneVipButton,
            ZoneBootcampButton,
            ZoneRoyalButton
        }.FirstOrDefault(button => string.Equals(button.Tag?.ToString(), raw, StringComparison.OrdinalIgnoreCase));
        if (activeButton is not null)
        {
            SetActiveButton(activeButton, ZoneStandardButton, ZoneVipButton, ZoneBootcampButton, ZoneRoyalButton);
        }

        RebuildBookingSeatGrid();
        UpdateBookingSummary();
        ShowStatus("Зона изменена", $"Показаны ПК для тарифа: {_bookingZoneName}.");
    }

    private void ToggleBookingTimePicker()
    {
        TimePickerPanel.Visibility = TimePickerPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SelectBookingHour(int hour)
    {
        _bookingHour = hour;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Время изменено", $"Начало брони: {GetBookingStartTime()}.");
    }

    private void SelectBookingMinute(int minute)
    {
        if (!BookingRules.IsMinuteAllowed(_bookingPackage, minute))
        {
            ShowStatus("Минуты недоступны", "Пакетные тарифы стартуют ровно в выбранный час.");
            return;
        }

        _bookingMinute = minute;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Минуты изменены", $"Начало брони: {GetBookingStartTime()}.");
    }

    private void SelectBookingDuration(string tag)
    {
        switch (tag)
        {
            case "night":
                _bookingPackage = "night";
                _bookingDuration = 8;
                _bookingMinute = 0;
                if (!BookingRules.IsNightPackHour(_bookingHour))
                {
                    _bookingHour = 22;
                }
                break;
            case "morning":
                _bookingPackage = "morning";
                _bookingDuration = 3;
                _bookingMinute = 0;
                if (!BookingRules.IsMorningPackHour(_bookingHour))
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

        var activeButton = new[]
        {
            Duration1Button,
            Duration2Button,
            Duration3Button,
            MorningPackButton,
            Duration8Button
        }.FirstOrDefault(button => string.Equals(button.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
        if (activeButton is not null)
        {
            SetActiveButton(activeButton, Duration1Button, Duration2Button, Duration3Button, MorningPackButton, Duration8Button);
        }

        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Тариф обновлен", GetPackageDescription());
    }

    private void SelectBookingSeat(string seat)
    {
        if (string.IsNullOrWhiteSpace(seat))
        {
            return;
        }

        if (!_isCompanyBooking)
        {
            _selectedSeats.Clear();
        }
        else if (!_selectedSeats.Contains(seat) && _selectedSeats.Count >= BookingRules.MaxCompanySeats)
        {
            _viewModel.Booking.ShowError("Групповая бронь ограничена 5 ПК.");
            ShowStatus("Лимит брони", "Для компании можно выбрать максимум 5 ПК.");
            return;
        }

        if (!_selectedSeats.Add(seat))
        {
            _selectedSeats.Remove(seat);
        }

        _viewModel.Booking.ClearError();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Выбор ПК обновлен", _selectedSeats.Count == 0 ? "ПК пока не выбран." : $"Выбрано: {string.Join(", ", _selectedSeats)}.");
    }

    private void ConfirmBooking()
    {
        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        var validation = BookingRules.Validate(
            _selectedSeats.ToArray(),
            _isCompanyBooking,
            start,
            end,
            _bookingPackage,
            _bookingDuration,
            DateTime.Now);
        if (!validation.Success)
        {
            var message = validation.ErrorMessage ?? "Бронь не прошла проверку правил.";
            _viewModel.Booking.ShowError(message);
            ShowStatus("Проверьте бронь", message);
            return;
        }

        _viewModel.Booking.ClearError();

        _viewModel.Booking.RefreshConfirmationText();

        if (!SaveBookingSelectionToDatabase())
        {
            return;
        }

        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshAdminUx();
        BookingConfirmOverlay.Visibility = Visibility.Visible;
        ShowImportantStatus("Бронь подтверждена", $"{_viewModel.Booking.SeatsText}, {_viewModel.Booking.Date:yyyy-MM-dd}, {_viewModel.Booking.TimeText}. Итого: {_viewModel.Booking.TotalText}.");
    }

    private bool SaveBookingSelectionToDatabase()
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            _viewModel.Booking.ShowError("Войдите в систему перед бронированием.");
            return false;
        }

        var bookingService = new BookingService();
        var result = bookingService.CreateBooking(new BookingCreateRequest(
            _currentUserId,
            _selectedSeats.ToArray(),
            _isCompanyBooking,
            _bookingDate,
            _bookingHour,
            _bookingMinute,
            _bookingDuration,
            _bookingPackage));

        if (!result.Success)
        {
            if (result.Exception is not null)
            {
                ShowDatabaseError("Ошибка сохранения брони", result.Exception);
            }

            _viewModel.Booking.ShowError(result.ErrorMessage ?? "Не удалось сохранить бронь.");
            return false;
        }

        LoadDatabaseState();
        return true;
    }

    private void CloseBookingConfirmation()
    {
        BookingConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClearBookingSelection()
    {
        _selectedSeats.Clear();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Выбор очищен", "Можно собрать бронь заново.");
    }

    private void UpdateBookingSummary()
    {
        if (!IsLoaded)
        {
            return;
        }

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
            button.Command = _viewModel.Booking.SelectSeatCommand;
            button.CommandParameter = seat.Name;
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
                IsEnabled = BookingRules.IsHourAllowed(_bookingPackage, hour)
            };
            if (!button.IsEnabled)
            {
                button.Style = (Style)FindResource("UnavailablePcButtonStyle");
            }
            button.Command = _viewModel.Booking.SelectHourCommand;
            button.CommandParameter = hour;
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
            button.Command = _viewModel.Booking.SelectMinuteCommand;
            button.CommandParameter = minute;
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
                button.IsEnabled = BookingRules.IsHourAllowed(_bookingPackage, hour);
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
                button.IsEnabled = BookingRules.IsMinuteAllowed(_bookingPackage, minute);
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
            ShowDatabaseError("Ошибка сохранения статуса ПК", ex);
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

    private string GetPackageDescription()
    {
        return BookingRules.GetPackageDescription(_bookingPackage, _bookingDuration);
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
                _viewModel.ClubMap.SelectedStatus = seat.Status;
                _viewModel.ClubMap.DetailSubtitle = $"{parts[1]}: статус — {GetStatusText(seat.Status, true)}.";
                _viewModel.ClubMap.Intervals = seat.IsAvailable
                    ? "Свободно сегодня: 18:00-20:00, 21:00-23:00."
                    : "Ближайший свободный интервал появится после завершения текущего статуса.";
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
            "Standard" => "Standard: 18 свободных ПК, тариф 8 BYN/час.",
            "VIP" => "VIP: 4 свободных ПК, тариф 14 BYN/час.",
            "Bootcamp" => "Bootcamp: 1 свободная комната, тариф 50 BYN/час.",
            "Royal VIP" => "Royal VIP: 3 свободных ПК, тариф 24 BYN/час.",
            _ => "Зона выбрана."
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
