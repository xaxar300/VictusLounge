using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;
using VictusLounge.Services;
using VictusLounge.Services.Facades;

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
        UpdatePcPhoto(zone);
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
        RebuildBookingSeatGrid();
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

        RebuildBookingSeatGrid();
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
        RebuildBookingSeatGrid();
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
        RebuildBookingSeatGrid();
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
        RebuildBookingSeatGrid();
        UpdateBookingSummary();
        ShowStatus("Тариф обновлен", GetPackageDescription());
    }

    private void SelectBookingSeat(string seat)
    {
        var selection = _bookingFacade.SelectSeat(new BookingSeatSelectionRequest(
            _selectedSeats.ToArray(),
            seat,
            _isCompanyBooking));
        if (!selection.Success)
        {
            var message = selection.ErrorMessage ?? "Не удалось выбрать ПК.";
            _viewModel.Booking.ShowError(message);
            ShowStatus("Лимит брони", message);
            return;
        }

        _selectedSeats.Clear();
        foreach (var selectedSeat in selection.Seats)
        {
            _selectedSeats.Add(selectedSeat);
        }

        _viewModel.Booking.ClearError();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Выбор ПК обновлен", _selectedSeats.Count == 0 ? "ПК пока не выбран." : $"Выбрано: {string.Join(", ", _selectedSeats)}.");
    }

    private async void ConfirmBooking()
    {
        var result = await _bookingFacade.ConfirmBookingAsync(new BookingFacadeRequest(
            _currentUserId,
            _currentUserId > 0,
            _selectedSeats.ToArray(),
            _isCompanyBooking,
            _bookingDate,
            _bookingHour,
            _bookingMinute,
            _bookingDuration,
            _bookingPackage));

        if (!result.Success)
        {
            HandleBookingFacadeError(result);
            return;
        }

        _viewModel.Booking.ClearError();
        _viewModel.Booking.RefreshConfirmationText();
        LoadDatabaseState();
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshAdminUx();
        BookingConfirmOverlay.Visibility = Visibility.Visible;
        ShowImportantStatus("Бронь подтверждена", $"{_viewModel.Booking.SeatsText}, {_viewModel.Booking.Date:yyyy-MM-dd}, {_viewModel.Booking.TimeText}. Итого: {_viewModel.Booking.TotalText}.");
    }

    private void HandleBookingFacadeError(BookingFacadeResult result)
    {
        var message = result.ErrorMessage ?? "Не удалось подтвердить бронь.";
        if (result.Exception is not null)
        {
            ShowDatabaseError("Ошибка сохранения брони", result.Exception);
        }

        _viewModel.Booking.ShowError(message);
        ShowStatus(GetBookingFacadeErrorTitle(result.Failure), message);
    }

    private static string GetBookingFacadeErrorTitle(BookingFacadeFailure failure)
    {
        return failure switch
        {
            BookingFacadeFailure.AuthRequired => "Нужен вход",
            BookingFacadeFailure.Persistence => "Ошибка сохранения",
            _ => "Проверьте бронь"
        };
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
        BookingSeatGrid.RowDefinitions.Clear();
        BookingSeatGrid.ColumnDefinitions.Clear();

        var seats = GetSeatsForZone(_bookingZoneKey);
        var timelineStarts = GetTimelineStarts();
        var seatNames = seats.Select(seat => seat.Name).ToArray();
        var selectedSlotBlocks = GetSelectedSlotBlocks(seats.Select(seat => seat.Name));
        var slotBlocksByStart = timelineStarts.ToDictionary(
            slotStart => slotStart,
            slotStart => GetSlotBlocks(seatNames, slotStart, slotStart.AddHours(_bookingDuration)));
        var bookableSeats = new HashSet<string>(StringComparer.Ordinal);

        BookingSeatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        foreach (var _ in timelineStarts)
        {
            BookingSeatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        }

        BookingSeatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddTimelineText("ПК", 0, 0, FontWeights.Black, (Brush)FindResource("MutedBrush"));
        for (var column = 0; column < timelineStarts.Length; column++)
        {
            AddTimelineText(GetTimelineSlotLabel(timelineStarts[column]), 0, column + 1, FontWeights.Bold, (Brush)FindResource("MutedBrush"));
        }

        for (var row = 0; row < seats.Length; row++)
        {
            var seat = seats[row];
            BookingSeatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddSeatTimelineLabel(seat, row + 1);

            var selectedHasSlotBlock = selectedSlotBlocks.ContainsKey(seat.Name);
            if (!selectedHasSlotBlock && seat.Status != PcStatuses.Service)
            {
                bookableSeats.Add(seat.Name);
            }

            for (var column = 0; column < timelineStarts.Length; column++)
            {
                var slotStart = timelineStarts[column];
                var slotBlocks = slotBlocksByStart[slotStart];
                var slotBlock = slotBlocks.TryGetValue(seat.Name, out var block) ? block : null;
                var cell = BuildSeatTimelineCell(seat, slotStart, slotBlock);
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, column + 1);
                BookingSeatGrid.Children.Add(cell);
            }
        }

        _selectedSeats.RemoveWhere(seat => !bookableSeats.Contains(seat));
        UpdateBookingSeatButtons();
    }

    private DateTime[] GetTimelineStarts()
    {
        var firstSlot = GetBookingStartDateTime();
        return Enumerable.Range(0, 6)
            .Select(offset => firstSlot.AddHours(offset))
            .ToArray();
    }

    private static string GetTimelineSlotLabel(DateTime start)
    {
        return $"{start:HH:mm}\n{start.AddHours(1):HH:mm}";
    }

    private void AddTimelineText(string text, int row, int column, FontWeight weight, Brush foreground)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            Foreground = foreground,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 8)
        };
        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, column);
        BookingSeatGrid.Children.Add(textBlock);
    }

    private void AddSeatTimelineLabel(SeatInfo seat, int row)
    {
        var statusText = seat.Status == PcStatuses.Free
            ? "свободен сейчас"
            : $"сейчас {GetStatusText(seat.Status)}";

        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 8)
        };
        panel.Children.Add(new TextBlock
        {
            Text = seat.Name,
            FontWeight = FontWeights.Black,
            Foreground = (Brush)FindResource("TextBrush")
        });
        panel.Children.Add(new TextBlock
        {
            Text = statusText,
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, 0);
        BookingSeatGrid.Children.Add(panel);
    }

    private Button BuildSeatTimelineCell(SeatInfo seat, DateTime slotStart, string? slotBlock)
    {
        var isService = seat.Status == PcStatuses.Service;
        var isBookable = !isService && string.IsNullOrWhiteSpace(slotBlock);
        var isSelected = _selectedSeats.Contains(seat.Name) && slotStart == GetBookingStartDateTime();
        var button = new Button
        {
            Content = GetTimelineCellContent(seat, slotStart, slotBlock),
            Tag = seat.Name,
            DataContext = slotStart,
            Uid = string.IsNullOrWhiteSpace(slotBlock) ? seat.Status : PcStatuses.Reserved,
            Style = (Style)FindResource(isSelected ? "PrimaryButtonStyle" : "PcButtonStyle"),
            Margin = new Thickness(0, 0, 8, 8),
            MinHeight = 58,
            Padding = new Thickness(8, 6, 8, 6),
            IsEnabled = isBookable,
            ToolTip = GetBookingSeatTooltip(seat, slotBlock)
        };

        if (!isBookable)
        {
            button.Style = (Style)FindResource("UnavailablePcButtonStyle");
        }

        ApplyPcStatusVisual(button, string.IsNullOrWhiteSpace(slotBlock) ? seat.Status : PcStatuses.Reserved, isBookable);
        button.Click += (_, _) => SelectBookingTimelineSlot(seat.Name, slotStart);
        return button;
    }

    private string GetTimelineCellContent(SeatInfo seat, DateTime slotStart, string? slotBlock)
    {
        if (!string.IsNullOrWhiteSpace(slotBlock))
        {
            return "занято";
        }

        if (seat.Status == PcStatuses.Service)
        {
            return "сервис";
        }

        var selectedStart = GetBookingStartDateTime();
        return slotStart == selectedStart
            ? "выбрать"
            : $"{slotStart:HH:mm}";
    }

    private void SelectBookingTimelineSlot(string seatName, DateTime slotStart)
    {
        _bookingDate = slotStart.Date;
        _bookingHour = slotStart.Hour;
        _bookingMinute = slotStart.Minute;
        UpdateBookingTimeButtons();
        SelectBookingSeat(seatName);
        RebuildBookingSeatGrid();
        UpdateBookingSummary();
    }

    private Dictionary<string, string> GetSelectedSlotBlocks(IEnumerable<string> seatNames)
    {
        var start = GetBookingStartDateTime();
        return GetSlotBlocks(seatNames, start, start.AddHours(_bookingDuration));
    }

    private Dictionary<string, string> GetSlotBlocks(IEnumerable<string> seatNames, DateTime start, DateTime end)
    {
        var seatNameSet = seatNames.ToHashSet(StringComparer.Ordinal);
        if (seatNameSet.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var blocks = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            using var unitOfWork = new UnitOfWork();
            var computersById = unitOfWork.Computers
                .QueryNoTracking()
                .Where(computer => seatNameSet.Contains(computer.Name))
                .ToDictionary(computer => computer.Id);
            var computerIds = computersById.Keys.ToArray();

            var bookedComputerIds = unitOfWork.Bookings
                .QueryNoTracking()
                .Where(booking => computerIds.Contains(booking.ComputerId)
                    && booking.Status != BookingStatuses.Cancelled
                    && booking.StartTime < end
                    && booking.EndTime > start)
                .Select(booking => booking.ComputerId)
                .Distinct()
                .ToArray();

            foreach (var computerId in bookedComputerIds)
            {
                if (computersById.TryGetValue(computerId, out var computer))
                {
                    blocks[computer.Name] = "бронь на выбранное время";
                }
            }

            var sessionComputerIds = unitOfWork.GameSessions
                .QueryNoTracking()
                .Where(session => computerIds.Contains(session.ComputerId)
                    && session.Status != SessionStatuses.Closed
                    && session.StartTime < end
                    && ((session.EndTime != null && session.EndTime > start)
                        || (session.EndTime == null && start.Date == DateTime.Today)))
                .Select(session => session.ComputerId)
                .Distinct()
                .ToArray();

            foreach (var computerId in sessionComputerIds)
            {
                if (computersById.TryGetValue(computerId, out var computer))
                {
                    blocks[computer.Name] = "сессия на выбранное время";
                }
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка проверки доступности ПК", ex);
        }

        return blocks;
    }

    private string GetBookingSeatContent(SeatInfo seat, string? slotBlock)
    {
        if (!string.IsNullOrWhiteSpace(slotBlock))
        {
            return $"{seat.Name}\nзанят в слот";
        }

        return seat.Status switch
        {
            PcStatuses.Free => seat.Name,
            PcStatuses.Service => $"{seat.Name}\nсервис",
            _ => $"{seat.Name}\nсейчас {GetStatusText(seat.Status)}"
        };
    }

    private string GetBookingSeatTooltip(SeatInfo seat, string? slotBlock)
    {
        if (!string.IsNullOrWhiteSpace(slotBlock))
        {
            return $"{T("Status.UnavailableTooltip")}: {slotBlock}.";
        }

        return seat.Status switch
        {
            PcStatuses.Free => T("Status.AvailableTooltip"),
            PcStatuses.Service => $"{T("Status.UnavailableTooltip")}: {GetStatusText(seat.Status)}",
            _ => $"ПК сейчас {GetStatusText(seat.Status)}, но доступен для выбранной даты и времени."
        };
    }

    private void RebuildBookingTimePicker()
    {
        BookingHourGrid.Children.Clear();
        for (var hour = 0; hour < 24; hour++)
        {
            BookingHourGrid.Children.Add(CreateTimeButton(
                hour,
                hour == _bookingHour,
                BookingRules.IsHourAllowed(_bookingPackage, hour),
                _viewModel.Booking.SelectHourCommand,
                new Thickness(0, 0, 8, 8)));
        }

        BookingMinuteGrid.Children.Clear();
        foreach (var minute in new[] { 0, 15, 30, 45 })
        {
            BookingMinuteGrid.Children.Add(CreateTimeButton(
                minute,
                minute == _bookingMinute,
                isEnabled: true,
                _viewModel.Booking.SelectMinuteCommand,
                new Thickness(0, 0, 0, 8)));
        }
    }

    private void UpdateBookingTimeButtons()
    {
        UpdateTimeButtonStyles(BookingHourGrid, _bookingHour, hour => BookingRules.IsHourAllowed(_bookingPackage, hour));
        UpdateTimeButtonStyles(BookingMinuteGrid, _bookingMinute, minute => BookingRules.IsMinuteAllowed(_bookingPackage, minute));
    }

    private Button CreateTimeButton(int value, bool isSelected, bool isEnabled, ICommand command, Thickness margin)
    {
        var button = new Button
        {
            Content = $"{value:00}",
            Tag = value,
            IsEnabled = isEnabled,
            Style = GetTimeButtonStyle(isSelected, isEnabled),
            Margin = margin,
            Command = command,
            CommandParameter = value
        };

        return button;
    }

    private void UpdateTimeButtonStyles(Panel panel, int selectedValue, Func<int, bool> isAllowed)
    {
        foreach (var child in panel.Children.OfType<Button>())
        {
            var value = int.TryParse(child.Tag?.ToString(), out var parsedValue) ? parsedValue : -1;
            child.IsEnabled = isAllowed(value);
            child.Style = GetTimeButtonStyle(value == selectedValue, child.IsEnabled);
        }
    }

    private Style GetTimeButtonStyle(bool isSelected, bool isEnabled)
    {
        return (Style)FindResource(!isEnabled
            ? "UnavailablePcButtonStyle"
            : isSelected ? "SelectedTimeButtonStyle" : "TimeButtonStyle");
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

    private DateTime GetBookingStartDateTime()
    {
        return _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
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
                    ApplyPcStatusVisual(button, button.Uid, isAvailable: false);
                    continue;
                }

                var isSelected = _selectedSeats.Contains(button.Tag?.ToString() ?? string.Empty)
                    && button.DataContext is DateTime slotStart
                    && slotStart == GetBookingStartDateTime();
                button.Style = (Style)FindResource(isSelected ? "PrimaryButtonStyle" : "PcButtonStyle");
                if (!isSelected)
                {
                    ApplyPcStatusVisual(button, button.Uid, isAvailable: true);
                }
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
            ApplyPcStatusVisual(button, seat.Status, seat.IsAvailable);

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

    private void ApplyPcStatusVisual(Button button, string status, bool isAvailable)
    {
        var normalizedStatus = NormalizePcStatus(status);
        button.Opacity = 1;

        if (isAvailable)
        {
            button.Background = (Brush)FindResource("PcFreeBackgroundBrush");
            button.BorderBrush = (Brush)FindResource("OkBrush");
            button.Foreground = (Brush)FindResource("OkBrush");
            return;
        }

        switch (normalizedStatus)
        {
            case PcStatuses.Busy:
                button.Background = (Brush)FindResource("PcBusyBackgroundBrush");
                button.BorderBrush = (Brush)FindResource("DangerBrush");
                button.Foreground = (Brush)FindResource("DangerBrush");
                break;
            case PcStatuses.Reserved:
                button.Background = (Brush)FindResource("PcReservedBackgroundBrush");
                button.BorderBrush = (Brush)FindResource("WaitBrush");
                button.Foreground = (Brush)FindResource("WaitBrush");
                break;
            default:
                button.Background = (Brush)FindResource("PcServiceBackgroundBrush");
                button.BorderBrush = (Brush)FindResource("LineSoftBrush");
                button.Foreground = (Brush)FindResource("MutedBrush");
                button.Opacity = 0.74;
                break;
        }
    }

    private void UpdatePcPhoto(string zone)
    {
        PcPhotoFrame.Background = new ImageBrush
        {
            ImageSource = new BitmapImage(new Uri(GetZonePhotoPath(zone), UriKind.Absolute)),
            Stretch = Stretch.UniformToFill
        };
    }

    private static string GetZonePhotoPath(string zone)
    {
        return zone switch
        {
            "VIP" => "pack://application:,,,/Assets/VIP.png",
            "Bootcamp" => "pack://application:,,,/Assets/Bootcamp.png",
            "Royal VIP" => "pack://application:,,,/Assets/RoyalVip.png",
            _ => "pack://application:,,,/Assets/Standard.png"
        };
    }

}
