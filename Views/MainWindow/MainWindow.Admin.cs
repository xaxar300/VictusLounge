using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;
using VictusLounge.Services;
using VictusLounge.ViewModels;

namespace VictusLounge;

public partial class MainWindow
{
    private void SaveShiftState(bool closeShift)
    {
        try
        {
            _adminOperationsService.SaveShiftState(closeShift, _currentUserFullName, _shiftCash);
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения смены", ex);
        }
    }

    private void SaveShiftExpense(decimal amount, string comment)
    {
        try
        {
            ApplyAdminOperationResult(
                _adminOperationsService.SaveShiftExpense(amount, comment, _currentUserId),
                "Расход не сохранен");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения расхода", ex);
        }
    }

    private void SaveTariffRate(string namePart, decimal price)
    {
        try
        {
            _adminOperationsService.SaveTariffRate(namePart, price);
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения тарифа", ex);
        }
    }

    private string? PromptText(string title, string prompt, string defaultValue = "")
    {
        var inputBox = new TextBox
        {
            Text = defaultValue,
            MinWidth = 320,
            Margin = new Thickness(0, 8, 0, 14),
            Foreground = (Brush)FindResource("TextBrush"),
            Background = (Brush)FindResource("SurfaceBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var okButton = new Button
        {
            Content = "OK",
            IsDefault = true,
            MinWidth = 86,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("PrimaryButtonStyle")
        };

        var dialog = new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("PanelBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    new TextBlock
                    {
                        Text = prompt,
                        Foreground = (Brush)FindResource("TextBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 420
                    },
                    inputBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            okButton,
                            new Button
                            {
                                Content = "Отмена",
                                IsCancel = true,
                                MinWidth = 86,
                                Style = (Style)FindResource("GhostButtonStyle")
                            }
                        }
                    }
                }
            }
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;

        inputBox.SelectAll();
        return dialog.ShowDialog() == true ? inputBox.Text.Trim() : null;
    }

    private bool PromptMoney(string title, string prompt, string defaultValue, out decimal amount)
    {
        amount = 0m;
        var raw = PromptText(title, prompt, defaultValue);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (TryParseMoney(raw, out amount))
        {
            return true;
        }

        ShowStatus("Некорректная сумма", "Введите положительное число, например 18 или 18,50.");
        return false;
    }

    private string GetFirstFreeComputerName()
    {
        return _computers.FirstOrDefault(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free)?.Name ?? "STD-01";
    }

    private string GetFirstServiceComputerName()
    {
        return _computers.FirstOrDefault(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service)?.Name ?? "STD-07";
    }

    private string? GetFirstPendingPaymentComputerName()
    {
        try
        {
            return _adminOperationsService.GetFirstPendingPaymentComputerName();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка поиска оплаты", ex);
            return null;
        }
    }

    private void ChangeTariffManually(string tariffName, int currentRate)
    {
        if (!PromptMoney($"{tariffName}: тариф", "Введите новую цену BYN/час:", currentRate.ToString(System.Globalization.CultureInfo.InvariantCulture), out var price))
        {
            return;
        }

        var roundedPrice = Math.Round(price, 0);
        SaveTariffRate(tariffName, roundedPrice);
        RefreshAdminUx();
        AddAdminLog($"{tariffName} rate changed to {roundedPrice:0} BYN/h");
        ShowStatus($"{tariffName} обновлен", $"Новый тариф {tariffName}: {roundedPrice:0} BYN/час. Метрики пересчитаны.");
    }

    private void UpsertManualShift(string employeeName)
    {
        try
        {
            _adminOperationsService.UpsertManualShift(employeeName, _shiftCash);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения расписания", ex);
        }
    }

    private string SaveShiftReport()
    {
        var reportsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Reports");
        System.IO.Directory.CreateDirectory(reportsDir);
        var path = System.IO.Path.Combine(reportsDir, $"shift-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new[]
        {
            $"Shift report: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Admin: {_currentUserFullName}",
            $"Cash: {_shiftCash:0.##} BYN",
            $"Online: {_shiftOnline:0.##} BYN",
            $"Active sessions: {_adminActiveSessions}",
            $"Pending payments: {_adminPaymentQueue}",
            $"Support queue: {_adminSupportQueue}"
        };
        System.IO.File.WriteAllLines(path, lines);
        return path;
    }

    private string SaveOwnerReport()
    {
        var reportsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Reports");
        System.IO.Directory.CreateDirectory(reportsDir);
        var path = System.IO.Path.Combine(reportsDir, $"owner-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new[]
        {
            $"Owner report: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Revenue: {_ownerRevenue} BYN",
            $"Load: {_ownerLoad}%",
            $"Average check: {_ownerAverageCheck} BYN",
            $"Repeat rate: {_ownerRepeatRate}%",
            $"Rates: Standard {_standardRate}, VIP {_vipRate}, Bootcamp {_bootcampRate}, Royal {_royalRate} BYN/h"
        };
        System.IO.File.WriteAllLines(path, lines);
        return path;
    }

    private T? PromptSelection<T>(string title, string prompt, IReadOnlyList<T> items, string actionText, string emptyMessage)
        where T : class
    {
        if (items.Count == 0)
        {
            ShowStatus(title, emptyMessage);
            return null;
        }

        var list = new ListBox
        {
            ItemsSource = items,
            DisplayMemberPath = "Summary",
            SelectedIndex = 0,
            MinWidth = 560,
            MaxHeight = 320,
            Margin = new Thickness(0, 10, 0, 14),
            Foreground = (Brush)FindResource("TextBrush"),
            Background = (Brush)FindResource("SurfaceBrush"),
            BorderBrush = (Brush)FindResource("LineBrush")
        };

        var okButton = new Button
        {
            Content = actionText,
            IsDefault = true,
            MinWidth = 120,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("PrimaryButtonStyle")
        };

        var dialog = new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("PanelBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    new TextBlock
                    {
                        Text = prompt,
                        Foreground = (Brush)FindResource("TextBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 620
                    },
                    list,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            okButton,
                            new Button
                            {
                                Content = "Отмена",
                                IsCancel = true,
                                MinWidth = 86,
                                Style = (Style)FindResource("GhostButtonStyle")
                            }
                        }
                    }
                }
            }
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        list.MouseDoubleClick += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true ? list.SelectedItem as T : null;
    }

    private void ShowPendingPaymentsDialog()
    {
        try
        {
            var payment = PromptSelection(
                "Оплата",
                "Выберите бронь или сессию, которая ожидает оплату:",
                _adminOperationsService.GetPendingPayments(DateTime.Now),
                "Принять оплату",
                "Нет броней или сессий, ожидающих оплату.");
            if (payment is null)
            {
                return;
            }

            ExecuteAdminAction(payment.ActionCommandParameter);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки оплат", ex);
        }
    }

    private void RescheduleBookingManually()
    {
        try
        {
            var bookings = _adminOperationsService.GetActiveBookings(DateTime.Now);
            var computers = _adminOperationsService.GetComputerNames();
            var bookingBox = new ComboBox
            {
                ItemsSource = bookings,
                DisplayMemberPath = "Summary",
                SelectedIndex = bookings.Count > 0 ? 0 : -1,
                MinWidth = 620,
                Margin = new Thickness(0, 8, 0, 12),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };
            var computerBox = new ComboBox
            {
                ItemsSource = computers,
                MinWidth = 260,
                Margin = new Thickness(0, 8, 0, 12),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };
            var startBox = new TextBox
            {
                Text = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:00"),
                MinWidth = 260,
                Margin = new Thickness(0, 8, 12, 12),
                Padding = new Thickness(10, 6, 10, 6),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };
            var durationBox = new TextBox
            {
                Text = "2",
                MinWidth = 120,
                Margin = new Thickness(0, 8, 0, 12),
                Padding = new Thickness(10, 6, 10, 6),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };

            if (bookings.Count == 0)
            {
                ShowStatus("Перенос брони", "Нет активных броней для переноса.");
                return;
            }

            bookingBox.SelectionChanged += (_, _) =>
            {
                if (bookingBox.SelectedItem is AdminBookingInfo selected)
                {
                    computerBox.SelectedItem = selected.ComputerName;
                    startBox.Text = selected.StartTime.ToString("yyyy-MM-dd HH:mm");
                    durationBox.Text = Math.Max(1, (selected.EndTime - selected.StartTime).TotalHours).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                }
            };
            if (bookingBox.SelectedItem is AdminBookingInfo initial)
            {
                computerBox.SelectedItem = initial.ComputerName;
                startBox.Text = initial.StartTime.ToString("yyyy-MM-dd HH:mm");
                durationBox.Text = Math.Max(1, (initial.EndTime - initial.StartTime).TotalHours).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }

            var okButton = new Button { Content = "Перенести", IsDefault = true, MinWidth = 120, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("PrimaryButtonStyle") };
            var dialog = new Window
            {
                Title = "Перенести бронь",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("PanelBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                Content = new StackPanel
                {
                    Margin = new Thickness(18),
                    Children =
                    {
                        new TextBlock { Text = "Выберите бронь и новый слот:", FontWeight = FontWeights.Bold },
                        bookingBox,
                        new TextBlock { Text = "Новый ПК" },
                        computerBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new StackPanel { Children = { new TextBlock { Text = "Старт" }, startBox } },
                                new StackPanel { Children = { new TextBlock { Text = "Часы" }, durationBox } }
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children =
                            {
                                okButton,
                                new Button { Content = "Отмена", IsCancel = true, MinWidth = 86, Style = (Style)FindResource("GhostButtonStyle") }
                            }
                        }
                    }
                }
            };
            okButton.Click += (_, _) => dialog.DialogResult = true;
            if (dialog.ShowDialog() != true || bookingBox.SelectedItem is not AdminBookingInfo booking)
            {
                return;
            }

            if (!DateTime.TryParse(startBox.Text, out var newStart)
                || !double.TryParse(durationBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var durationHours)
                || durationHours <= 0)
            {
                ShowStatus("Бронь не перенесена", "Проверьте дату старта и длительность.");
                return;
            }

            var result = _adminOperationsService.RescheduleBooking(booking.Id, newStart, durationHours, computerBox.SelectedItem?.ToString());
            if (!ApplyAdminOperationResult(result, "Бронь не перенесена"))
            {
                return;
            }

            AddAdminLog($"Booking #{booking.Id} rescheduled");
            ShowStatus("Бронь перенесена", $"#{booking.Id}: {computerBox.SelectedItem}, {newStart:dd.MM HH:mm}-{newStart.AddHours(durationHours):HH:mm}.");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка переноса брони", ex);
        }
    }

    private void CancelBookingManually()
    {
        try
        {
            var booking = PromptSelection(
                "Отменить бронь",
                "Выберите активную бронь для отмены:",
                _adminOperationsService.GetActiveBookings(DateTime.Now),
                "Отменить бронь",
                "Нет активных броней для отмены.");
            if (booking is null)
            {
                return;
            }

            var result = _adminOperationsService.CancelBooking(booking.Id);
            if (!ApplyAdminOperationResult(result, "Бронь не отменена"))
            {
                return;
            }

            AddAdminLog($"Booking #{booking.Id} cancelled");
            ShowStatus("Бронь отменена", $"Бронь #{booking.Id} отменена администратором.");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка отмены брони", ex);
        }
    }
    private int GetLatestActiveBookingId()
    {
        try
        {
            return _adminOperationsService.GetLatestActiveBookingId();
        }
        catch
        {
            return 0;
        }
    }

    private void ExecuteAdminAction(string action)
    {
        action = string.IsNullOrWhiteSpace(action) ? "admin-action" : action;
        if (HandleAdminSessionAction(action))
        {
            return;
        }

        switch (action)
        {
            case "admin-new-session":
                var sessionComputer = PromptText("Новая сессия", "Введите имя ПК для запуска сессии:", GetFirstFreeComputerName());
                if (string.IsNullOrWhiteSpace(sessionComputer))
                {
                    break;
                }

                if (!PromptMoney("Новая сессия", "Введите сумму оплаты:", "8", out var sessionAmount))
                {
                    break;
                }

                if (ApplyAdminOperationResult(
                    _adminOperationsService.CreateGuestSession(sessionComputer, sessionAmount, _currentUserId),
                    "Сессия не сохранена"))
                {
                    _shiftCash += sessionAmount;
                    CompleteAdminAction(
                        $"{sessionComputer} started as guest session",
                        "Новая сессия",
                        $"Запущена гостевая сессия на {sessionComputer}. Карта и бронь обновлены.");
                }
                break;

            case "admin-payment":
            case "admin-pay-std10":
                ShowPendingPaymentsDialog();
                break;

            case "admin-reschedule-booking":
                RescheduleBookingManually();
                break;

            case "admin-cancel-booking":
                CancelBookingManually();
                break;

            case "admin-service":
                var serviceComputer = PromptText("Поставить ПК в сервис", "Введите имя ПК:", _selectedMapPc ?? GetFirstFreeComputerName());
                if (!string.IsNullOrWhiteSpace(serviceComputer))
                {
                    var previousStatus = GetPcStatus(serviceComputer, _computers.FirstOrDefault(computer => computer.Name.Equals(serviceComputer, StringComparison.OrdinalIgnoreCase))?.Status ?? PcStatuses.Free);
                    if (!TrySetPcStatus(serviceComputer, PcStatuses.Service))
                    {
                        break;
                    }
                    LoadDatabaseState();
                    CompleteAdminAction($"{serviceComputer} moved to service", "Сервис", $"{serviceComputer} переведен в обслуживание. Карта и выбор брони обновлены.");
                    ShowUndoSnackbar("Можно отменить", $"{serviceComputer}: вернуть предыдущий статус.", () =>
                    {
                        TrySetPcStatus(serviceComputer, previousStatus);
                        LoadDatabaseState();
                        RefreshAdminUx();
                        AddAdminLog($"{serviceComputer} service action undone");
                        ShowStatus("Действие отменено", $"{serviceComputer}: статус восстановлен.");
                    });
                }
                break;

            case "admin-clear-service":
                var clearServiceComputer = PromptText("Снять сервис с ПК", "Введите имя ПК:", GetFirstServiceComputerName());
                if (!string.IsNullOrWhiteSpace(clearServiceComputer))
                {
                    var previousStatus = GetPcStatus(clearServiceComputer, _computers.FirstOrDefault(computer => computer.Name.Equals(clearServiceComputer, StringComparison.OrdinalIgnoreCase))?.Status ?? PcStatuses.Service);
                    if (!TrySetPcStatus(clearServiceComputer, PcStatuses.Free))
                    {
                        break;
                    }
                    LoadDatabaseState();
                    CompleteAdminAction($"Service released {clearServiceComputer}", "Сервис снят", $"{clearServiceComputer} вернулся из обслуживания и доступен для брони.");
                    ShowUndoSnackbar("Можно отменить", $"{clearServiceComputer}: вернуть предыдущий статус.", () =>
                    {
                        TrySetPcStatus(clearServiceComputer, previousStatus);
                        LoadDatabaseState();
                        RefreshAdminUx();
                        AddAdminLog($"{clearServiceComputer} service release undone");
                        ShowStatus("Действие отменено", $"{clearServiceComputer}: статус восстановлен.");
                    });
                }
                break;

            case "shift-close":
                var previousShiftClosed = _shiftClosed;
                _shiftClosed = !_shiftClosed;
                SaveShiftState(_shiftClosed);
                CompleteAdminAction(
                    _shiftClosed ? "Shift closed" : "Shift reopened",
                    _shiftClosed ? "Смена закрыта" : "Смена снова активна",
                    _shiftClosed ? "Касса заблокирована для новых расходов, отчет готов." : "Операции смены снова доступны.");
                ShowUndoSnackbar("Можно отменить", "Вернуть предыдущее состояние смены.", () =>
                {
                    _shiftClosed = previousShiftClosed;
                    SaveShiftState(_shiftClosed);
                    RefreshAdminUx();
                    AddAdminLog(_shiftClosed ? "Shift close restored" : "Shift reopen undone");
                    ShowStatus("Действие отменено", _shiftClosed ? "Смена снова закрыта." : "Смена снова активна.");
                });
                break;

            case "shift-expense":
                if (_shiftClosed)
                {
                    ShowStatus("Смена закрыта", "Нельзя внести расход после закрытия смены.");
                    break;
                }
                if (!PromptMoney("Внести расход", "Введите сумму расхода:", "35", out var expenseAmount))
                {
                    break;
                }

                var expenseComment = PromptText("Внести расход", "Комментарий к расходу:", "Shift expense: расходники");
                if (string.IsNullOrWhiteSpace(expenseComment))
                {
                    break;
                }

                _shiftCash = Math.Max(0, _shiftCash - expenseAmount);
                SaveShiftExpense(expenseAmount, expenseComment);
                CompleteAdminAction($"Expense added: -{expenseAmount:0.##} BYN", "Расход внесен", $"В кассу добавлен расход: -{expenseAmount:0.##} BYN.");
                break;

            case "shift-report":
                var shiftReportPath = SaveShiftReport();
                AddAdminLog("Shift report generated");
                ShowStatus("Отчет смены", $"Касса: {_shiftCash:0} BYN, онлайн: {_shiftOnline:0} BYN. Файл: {shiftReportPath}");
                break;

            case "shift-incident":
                var incidentText = PromptText("Добавить инцидент", "Введите текст записи:", "Ручная запись смены добавлена администратором");
                if (string.IsNullOrWhiteSpace(incidentText))
                {
                    break;
                }

                AddIncident($"{DateTime.Now:HH:mm} · {incidentText}");
                _adminSupportQueue++;
                CompleteAdminAction("Incident added to shift journal", "Инцидент добавлен", "Запись появилась в журнале, очередь поддержки увеличена.");
                break;

            case "owner-peak":
                _ownerDemandMode = _ownerDemandMode == "peak" ? "normal" : "peak";
                CompleteAdminAction($"Owner DB slice applied: {_ownerDemandMode}", "Режим спроса", $"Показан срез из БД: {_ownerDemandMode}.");
                break;

            case "owner-night":
                _ownerDemandMode = _ownerDemandMode == "night" ? "normal" : "night";
                CompleteAdminAction($"Owner DB slice applied: {_ownerDemandMode}", "Режим спроса", $"Показан срез из БД: {_ownerDemandMode}.");
                break;

            case "owner-export":
                var ownerReportPath = SaveOwnerReport();
                AddAdminLog("Owner report exported");
                ShowStatus("Отчет владельца", $"Сводка: выручка {_ownerRevenue} BYN, загрузка {_ownerLoad}%. Файл: {ownerReportPath}");
                break;

            case "owner-schedule":
                var employeeName = PromptText("Расписание смен", "Введите сотрудника для новой/обновленной смены:", _currentUserFullName);
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    break;
                }

                UpsertManualShift(employeeName);
                _ownerDemandMode = "loyalty";
                LoadDatabaseState();
                RefreshAdminUx();
                AddAdminLog($"Staff schedule updated for {employeeName}");
                ShowStatus("Расписание обновлено", $"Смена для {employeeName} сохранена в базе данных.");
                break;

            case "owner-standard":
                ChangeTariffManually("Standard", _standardRate);
                break;

            case "owner-vip":
                ChangeTariffManually("VIP", _vipRate);
                break;

            case "owner-bootcamp":
                ChangeTariffManually("Bootcamp", _bootcampRate);
                break;

            case "owner-royal":
                ChangeTariffManually("Royal", _royalRate);
                break;

            default:
                ShowStatus("Команда не выполнена", $"Команда интерфейса не распознана: {action}.");
                break;
        }
    }
    private void ExecuteShiftTask(string taskKey)
    {
        var (done, text) = taskKey switch
        {
            "vip" => (_viewModel.Admin.IsVipTaskDone, "Проверить VIP-зону после 18:00"),
            "bootcamp" => (_viewModel.Admin.IsBootcampTaskDone, "Подготовить Bootcamp к тренировке Team Alpha"),
            "payment" => (_viewModel.Admin.IsPaymentTaskDone, "Проверить оплату ожидающих броней"),
            _ => (false, "Задача смены")
        };

        ShowStatus(done ? "Задача выполнена" : "Задача возвращена", text);
    }

    private bool HandleAdminSessionAction(string action)
    {
        var parts = action.Split('|', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        switch (parts[0])
        {
            case "admin-pay-session":
                PayAdminSession(parts[1]);
                return true;

            case "admin-payment":
                PayAdminSession(parts[1]);
                return true;

            case "admin-extend-session":
                ExtendAdminSession(parts[1]);
                return true;

            case "admin-clear-service":
                ReleaseServiceComputerDirectly(parts[1]);
                return true;

            default:
                return false;
        }
    }

    private void ReleaseServiceComputerDirectly(string? computerName)
    {
        if (string.IsNullOrWhiteSpace(computerName))
        {
            return;
        }

        var previousStatus = GetPcStatus(
            computerName,
            _computers.FirstOrDefault(computer => computer.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase))?.Status ?? PcStatuses.Service);
        SetPcStatus(computerName, PcStatuses.Free);
        LoadDatabaseState();
        CompleteAdminAction(
            $"Service released {computerName}",
            "Сервис снят",
            $"{computerName} вернулся из обслуживания и доступен для брони.");
        ShowUndoSnackbar("Можно отменить", $"{computerName}: вернуть предыдущий статус.", () =>
        {
            SetPcStatus(computerName, previousStatus);
            LoadDatabaseState();
            RefreshAdminUx();
            AddAdminLog($"{computerName} service release undone");
            ShowStatus("Действие отменено", $"{computerName}: статус восстановлен.");
        });
    }

    private void CloseAdminSession(string computerName)
    {
        _adminActiveSessions = Math.Max(0, _adminActiveSessions - 1);
        _adminFreePcs++;
        if (!ApplyAdminOperationResult(
            _adminOperationsService.CloseSession(computerName),
            "Session was not closed"))
        {
            return;
        }
        CompleteAdminAction($"{computerName} closed and released", "Сессия закрыта", $"{computerName} освобожден и стал доступен на карте клуба.");
    }

    private void PayAdminSession(string computerName)
    {
        var amount = _adminOperationsService.GetPendingPaymentAmount(computerName) ?? 0m;
        if (amount <= 0)
        {
            ShowStatus("Оплата не найдена", $"{computerName}: нет ожидающей оплаты в активных сессиях.");
            return;
        }

        _adminPaymentQueue = Math.Max(0, _adminPaymentQueue - 1);
        _shiftCash += amount;
        if (!ApplyAdminOperationResult(
            _adminOperationsService.ConfirmPayment(computerName, amount, _currentUserId),
            "Payment was not saved"))
        {
            return;
        }
        CompleteAdminAction($"{computerName} payment confirmed", "Оплата принята", $"{computerName}: касса +{amount:0.##} BYN.");
    }

    private void ExtendAdminSession(string computerName)
    {
        const decimal extensionPrice = 36m;
        _shiftOnline += extensionPrice;
        if (!ApplyAdminOperationResult(
            _adminOperationsService.ExtendSession(computerName, extensionPrice),
            "Session was not extended"))
        {
            return;
        }
        CompleteAdminAction($"{computerName} extended", "Сессия продлена", $"{computerName}: онлайн +{extensionPrice:0.##} BYN.");
    }

    private void CompleteAdminAction(string logEntry, string statusTitle, string statusBody)
    {
        RefreshAdminUx();
        AddAdminLog(logEntry);
        ShowStatus(statusTitle, statusBody);
    }

    private void RefreshAdminUx()
    {
        RecalculateOwnerMetrics();
        SyncAdminViewModel();
        SyncOwnerViewModel();
        RebuildAdminOperationLogList();
        RebuildAdminTaskQueue();
        RebuildAdminSessionsGrid();

        SetText(AdminActiveSessionsValue, _adminActiveSessions);
        SetText(AdminPaymentQueueValue, _adminPaymentQueue);
        SetText(AdminFreePcsValue, _adminFreePcs);
        AdminFreePcsHintText.Text = $"из {_computers.Count} рабочих мест";
        SetText(AdminSupportValue, _adminSupportQueue);
        ShiftCashValue.Text = $"{_shiftCash:0} BYN";
        ShiftOnlineValue.Text = $"{_shiftOnline:0} BYN";
        OwnerRevenueValue.Text = $"{_ownerRevenue:N0} BYN".Replace(',', ' ');
        OwnerLoadValue.Text = $"{_ownerLoad}%";
        OwnerLoadBar.Value = _ownerLoad;
        OwnerAverageValue.Text = $"{_ownerAverageCheck} BYN";
        OwnerRepeatValue.Text = $"{_ownerRepeatRate}%";
        OwnerStandardPriceText.Text = $"{_standardRate} BYN/час · 14 ПК";
        OwnerVipPriceText.Text = $"{_vipRate} BYN/час · 8 ПК";
        OwnerBootcampPriceText.Text = $"{_bootcampRate} BYN/час · 5 ПК";
        OwnerRoyalPriceText.Text = $"{_royalRate} BYN/час · 5 ПК";
        SetChoiceButtonStyles(_ownerDemandMode,
            ("peak", OwnerPeakModeButton),
            ("night", OwnerNightModeButton));
    }

    private static void SetText(TextBlock target, object value)
    {
        target.Text = value.ToString();
    }

    private bool ApplyAdminOperationResult(AdminOperationResult result, string failureTitle)
    {
        if (!result.Success)
        {
            ShowStatus(failureTitle, result.ErrorMessage ?? "Операция не выполнена.");
            return false;
        }

        LoadDatabaseState();
        RefreshCurrentClientAfterAdminOperation();
        return true;
    }

    private void RefreshCurrentClientAfterAdminOperation()
    {
        if (_currentUserId <= 0)
        {
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var currentUser = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(unitOfWork, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка обновления клиента", ex);
        }
    }

    private void RebuildAdminTaskQueue()
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.Admin.TaskQueue.Clear();
        try
        {
            foreach (var item in _adminOperationsService.GetTaskQueue(DateTime.Now))
            {
                _viewModel.Admin.TaskQueue.Add(new AdminTaskQueueItemViewModel
                {
                    Title = item.Title,
                    Details = item.Details,
                    Kind = item.Kind == AdminTaskType.Payment ? AdminTaskKind.Payment : AdminTaskKind.Service,
                    ActionText = item.ActionText,
                    ActionCommandParameter = item.ActionCommandParameter,
                    IsPrimaryAction = item.IsPrimaryAction
                });
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки очереди задач", ex);
        }

        if (_viewModel.Admin.TaskQueue.Count == 0)
        {
            _viewModel.Admin.TaskQueue.Add(new AdminTaskQueueItemViewModel
            {
                Title = "Очередь чистая",
                Details = "Ожидающих оплат и сервисных задач нет.",
                Kind = AdminTaskKind.Payment,
                ActionText = string.Empty,
                ActionCommandParameter = string.Empty,
                IsPrimaryAction = false
            });
        }
    }

    private void RebuildAdminSessionsGrid()
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.Admin.Sessions.Clear();
        try
        {
            foreach (var session in _adminOperationsService.GetActiveSessions(DateTime.Now))
            {
                _viewModel.Admin.Sessions.Add(new AdminSessionRowViewModel
                {
                    ComputerName = session.ComputerName,
                    ClientName = session.ClientName,
                    EndText = session.EndText,
                    Status = session.Status,
                    StatusText = session.StatusText,
                    ActionText = session.ActionText,
                    ActionCommandParameter = session.ActionCommandParameter,
                    IsPrimaryAction = session.IsPrimaryAction
                });
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки сессий", ex);
        }
    }

    private void RecalculateOwnerMetrics()
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var now = DateTime.Now;
            var totalPcs = Math.Max(1, unitOfWork.Computers.Count(_ => true));

            bool InSelectedSlice(DateTime value)
            {
                return _ownerDemandMode switch
                {
                    "peak" => value.Hour is >= 18 and < 23,
                    "night" => value.Hour >= 22 || value.Hour < 6,
                    _ => value >= today && value < tomorrow
                };
            }

            var payments = unitOfWork.Payments
                .QueryNoTracking()
                .Where(payment => payment.Amount > 0
                    && payment.PaymentType != PaymentTypes.AdminLog
                    && payment.PaymentType != PaymentTypes.EventRegistration
                    && !payment.PaymentType.StartsWith(PaymentTypes.Pending)
                    && payment.CreatedAt >= today
                    && payment.CreatedAt < tomorrow)
                .ToList()
                .Where(payment => InSelectedSlice(payment.CreatedAt))
                .ToList();

            var sessionsToday = unitOfWork.GameSessions
                .QueryNoTracking()
                .Where(session => session.StartTime < tomorrow && (session.EndTime == null || session.EndTime >= today))
                .ToList();
            var sessionsInSlice = sessionsToday.Where(session => InSelectedSlice(session.StartTime)).ToList();
            var occupiedNow = unitOfWork.GameSessions.Count(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now));
            var serviceNow = unitOfWork.Computers.Count(computer => computer.Status == PcStatuses.Service);

            _ownerRevenue = (int)Math.Round(payments.Sum(payment => payment.Amount));
            _ownerLoad = _ownerDemandMode == "normal"
                ? Math.Clamp((int)Math.Round((occupiedNow + serviceNow) * 100m / totalPcs), 0, 100)
                : Math.Clamp((int)Math.Round(sessionsInSlice.Select(session => session.ComputerId).Distinct().Count() * 100m / totalPcs), 0, 100);
            _ownerAverageCheck = payments.Count == 0 ? 0 : (int)Math.Round(payments.Average(payment => payment.Amount));

            var paidUserIds = payments.Select(payment => payment.UserId).Distinct().ToHashSet();
            var repeatUsers = unitOfWork.Payments.QueryNoTracking()
                .Where(payment => paidUserIds.Contains(payment.UserId)
                    && payment.Amount > 0
                    && payment.CreatedAt < today)
                .Select(payment => payment.UserId)
                .Distinct()
                .Count();
            _ownerRepeatRate = paidUserIds.Count == 0 ? 0 : Math.Clamp((int)Math.Round(repeatUsers * 100m / paidUserIds.Count), 0, 100);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка расчета метрик владельца", ex);
        }
    }
    private void AddIncident(string text)
    {
        var row = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        ShiftIncidentList.Children.Insert(Math.Min(1, ShiftIncidentList.Children.Count), row);
    }

    private void AddAdminLog(string text)
    {
        if (!IsLoaded)
        {
            return;
        }

        var row = new TextBlock
        {
            Text = $"{DateTime.Now:HH:mm} · {text}",
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

        AdminOperationLogList.Children.Insert(0, row);
        while (AdminOperationLogList.Children.Count > 6)
        {
            AdminOperationLogList.Children.RemoveAt(AdminOperationLogList.Children.Count - 1);
        }

        SaveAdminLogEntry(text);
    }

    private void RebuildAdminOperationLogList()
    {
        if (!IsLoaded || AdminOperationLogList is null)
        {
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var logs = unitOfWork.Payments.GetAdminLogs(6);

            AdminOperationLogList.Children.Clear();
            if (logs.Count == 0)
            {
                AdminOperationLogList.Children.Add(new TextBlock
                {
                    Text = "Журнал операций пуст",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var log in logs)
            {
                AdminOperationLogList.Children.Add(new TextBlock
                {
                    Text = $"{log.CreatedAt:HH:mm} · {FormatAdminLogComment(log.Comment)}",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Admin log load failed: {ex}");
        }
    }

    private void SaveAdminLogEntry(string text)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var userId = ResolveCurrentOrAdminUserId(unitOfWork);
            if (userId is null)
            {
                return;
            }

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = userId.Value,
                Amount = 0,
                PaymentType = PaymentTypes.AdminLog,
                CreatedAt = DateTime.Now,
                Comment = $"Admin log: {text}"
            });
            unitOfWork.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Admin log save failed: {ex}");
        }
    }

    private static string FormatAdminLogComment(string comment)
    {
        return comment.StartsWith("Admin log:", StringComparison.OrdinalIgnoreCase)
            ? comment["Admin log:".Length..].Trim()
            : comment;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

}
