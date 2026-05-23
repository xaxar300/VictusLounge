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
    private bool SaveGuestSession(string computerName, decimal amount)
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            return false;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return false;
            }

            var now = DateTime.Now;
            var hasComputerSessionConflict = dbContext.GameSessions.Any(session =>
                session.ComputerId == computer.Id
                && session.Status != SessionStatuses.Closed
                && (session.EndTime == null || session.EndTime > now));
            if (hasComputerSessionConflict)
            {
                ShowStatus("ПК занят", $"{computerName}: уже есть незакрытая сессия на этом ПК.");
                return false;
            }

            var currentUser = dbContext.Users.FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null
                && NormalizeRole(currentUser.Role) == "client"
                && HasActiveIndividualSession(dbContext, _currentUserId, out var activeSessionComputer))
            {
                ShowStatus("Сессия уже активна", $"У клиента уже есть активная сессия на {activeSessionComputer}. Сначала завершите ее.");
                return false;
            }

            dbContext.GameSessions.Add(new GameSession
            {
                Id = GetNextId(dbContext.GameSessions, session => session.Id),
                UserId = _currentUserId,
                ComputerId = computer.Id,
                StartTime = now,
                EndTime = null,
                TotalPrice = amount,
                Status = SessionStatuses.Active
            });

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = now,
                Comment = $"Guest session: {computerName}"
            });

            computer.Status = PcStatuses.Busy;
            dbContext.SaveChanges();
            LoadDatabaseState();
            var refreshedUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (refreshedUser is not null)
            {
                RefreshClientUx(dbContext, refreshedUser);
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения сессии", ex);
            return false;
        }
    }

    private void SavePaymentConfirmation(string computerName, decimal amount)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is not null)
            {
                session.Status = SessionStatuses.Active;
                session.TotalPrice += amount;
            }

            var booking = dbContext.Bookings
                .Where(item => item.ComputerId == computer.Id && item.Status == BookingStatuses.PendingPayment)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();
            if (booking is not null)
            {
                booking.Status = BookingStatuses.Confirmed;
            }

            var pendingPayment = dbContext.Payments
                .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending) && item.Amount == amount)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            if (pendingPayment is not null)
            {
                pendingPayment.PaymentType = PaymentTypes.Cash;
                pendingPayment.Comment = $"Payment confirmed: {computerName}";
            }
            else
            {
                var paymentUserId = session?.UserId ?? booking?.UserId ?? ResolveCurrentOrAdminUserId(dbContext);
                if (paymentUserId is null)
                {
                    ShowStatus("Войдите в систему", "Оплата не сохранена: не найден пользователь для записи платежа.");
                    return;
                }

                dbContext.Payments.Add(new Payment
                {
                    Id = GetNextId(dbContext.Payments, payment => payment.Id),
                    UserId = paymentUserId.Value,
                    Amount = amount,
                    PaymentType = PaymentTypes.Cash,
                    CreatedAt = DateTime.Now,
                    Comment = $"Payment confirmed: {computerName}"
                });
            }

            dbContext.SaveChanges();
            LoadDatabaseState();

            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка подтверждения оплаты", ex);
        }
    }

    private void SaveAllPendingPaymentsAsCash(decimal amountPerPayment)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var pendingBookings = dbContext.Bookings
                .Where(item => item.Status == BookingStatuses.PendingPayment
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var booking in pendingBookings)
            {
                booking.Status = BookingStatuses.Confirmed;
            }

            var pendingSessions = dbContext.GameSessions
                .Where(item => item.Status == SessionStatuses.AwaitingPayment
                    && item.StartTime >= today
                    && item.StartTime < tomorrow)
                .ToList();
            foreach (var session in pendingSessions)
            {
                session.Status = SessionStatuses.Active;
            }

            var pendingPayments = dbContext.Payments
                .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending)
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var payment in pendingPayments)
            {
                if (payment.Amount > 0
                    && payment.Comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase)
                    && dbContext.Users.Find(payment.UserId) is { } paymentUser)
                {
                    paymentUser.Balance += payment.Amount;
                    paymentUser.LoyaltyTier = BetterTier(paymentUser.LoyaltyTier, GetClientTier(paymentUser.Balance));
                }

                payment.PaymentType = PaymentTypes.Cash;
                payment.Comment = $"{payment.Comment}; confirmed by admin";
            }

            dbContext.SaveChanges();
            LoadDatabaseState();

            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка закрытия оплат", ex);
        }
    }

    private void SaveSessionClosed(string computerName)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is not null)
            {
                session.EndTime = DateTime.Now;
                session.Status = SessionStatuses.Closed;
            }

            computer.Status = PcStatuses.Free;
            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка закрытия сессии", ex);
        }
    }

    private void SaveSessionExtension(string computerName, decimal amount)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is null)
            {
                ShowStatus("Сессия не найдена", $"{computerName}: нет открытой сессии для продления.");
                return;
            }

            var paymentUserId = session.UserId;
            session.TotalPrice += amount;

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = paymentUserId,
                Amount = amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = $"Session extension: {computerName}"
            });

            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка продления сессии", ex);
        }
    }

    private void SaveShiftState(bool closeShift)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var shift = dbContext.Shifts
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item => item.EndTime == null)
                ?? dbContext.Shifts.OrderByDescending(item => item.StartTime).FirstOrDefault();

            if (shift is null)
            {
                shift = new Shift
                {
                    Id = GetNextId(dbContext.Shifts, item => item.Id),
                    EmployeeName = _currentUserFullName,
                    StartTime = DateTime.Now,
                    CashTotal = _shiftCash
                };
                dbContext.Shifts.Add(shift);
            }

            shift.EmployeeName = _currentUserFullName;
            shift.CashTotal = _shiftCash;
            shift.EndTime = closeShift ? DateTime.Now : null;
            dbContext.SaveChanges();
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
            using var dbContext = new AppDbContext();
            var paymentUserId = ResolveCurrentOrAdminUserId(dbContext);
            if (paymentUserId is null)
            {
                ShowStatus("Войдите в систему", "Расход смены не сохранен: не найден пользователь для записи платежа.");
                return;
            }

            // Demo finance model: negative Payment.Amount marks cash expenses.
            // Income/expense separation is documented in README as a production improvement.
            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = paymentUserId.Value,
                Amount = -amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = DateTime.Now,
                Comment = comment
            });

            var shift = dbContext.Shifts
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item => item.EndTime == null);
            if (shift is not null)
            {
                shift.CashTotal = Math.Max(0, shift.CashTotal - amount);
            }

            dbContext.SaveChanges();
            LoadDatabaseState();
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
            using var dbContext = new AppDbContext();
            var tariff = dbContext.Tariffs.FirstOrDefault(item => item.Name.Contains(namePart));
            if (tariff is not null)
            {
                tariff.PricePerHour = price;
            }

            var zone = namePart switch
            {
                "Standard" => "Standard",
                "VIP" => "VIP",
                "Royal" => "Royal VIP",
                "Bootcamp" => "Bootcamp",
                _ => namePart
            };

            foreach (var computer in dbContext.Computers.Where(item => item.Zone == zone))
            {
                computer.HourPrice = price;
            }

            dbContext.SaveChanges();
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
                            new Button
                            {
                                Content = "OK",
                                IsDefault = true,
                                MinWidth = 86,
                                Margin = new Thickness(0, 0, 8, 0),
                                Style = (Style)FindResource("PrimaryButtonStyle")
                            },
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

        if (dialog.Content is StackPanel panel
            && panel.Children.OfType<StackPanel>().LastOrDefault() is { } buttons)
        {
            foreach (var button in buttons.Children.OfType<Button>())
            {
                if (button.IsDefault)
                {
                    button.Click += (_, _) => dialog.DialogResult = true;
                }
            }
        }

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
            using var dbContext = new AppDbContext();
            return dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.Status == SessionStatuses.AwaitingPayment
                    && (session.EndTime == null || session.EndTime > DateTime.Now))
                .OrderBy(session => session.StartTime)
                .Select(session => dbContext.Computers
                    .Where(computer => computer.Id == session.ComputerId)
                    .Select(computer => computer.Name)
                    .FirstOrDefault())
                .FirstOrDefault();
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
            using var dbContext = new AppDbContext();
            var now = DateTime.Now;
            var shift = dbContext.Shifts
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item => item.EmployeeName == employeeName && item.EndTime == null);

            if (shift is null)
            {
                shift = new Shift
                {
                    Id = GetNextId(dbContext.Shifts, item => item.Id),
                    EmployeeName = employeeName,
                    StartTime = now,
                    CashTotal = _shiftCash
                };
                dbContext.Shifts.Add(shift);
            }
            else
            {
                shift.CashTotal = _shiftCash;
            }

            dbContext.SaveChanges();
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

    private void RescheduleBookingManually()
    {
        var bookingIdText = PromptText("Перенести бронь", "Введите ID брони:", GetLatestActiveBookingId().ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!int.TryParse(bookingIdText, out var bookingId))
        {
            ShowStatus("Бронь не изменена", "Введите числовой ID брони.");
            return;
        }

        var startText = PromptText("Перенести бронь", "Новое начало в формате yyyy-MM-dd HH:mm:", DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:00"));
        if (!DateTime.TryParse(startText, out var newStart))
        {
            ShowStatus("Бронь не изменена", "Не удалось распознать дату и время начала.");
            return;
        }

        var durationText = PromptText("Перенести бронь", "Длительность в часах:", "2");
        if (!double.TryParse(
                durationText?.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var durationHours)
            || durationHours <= 0)
        {
            ShowStatus("Бронь не изменена", "Введите положительную длительность в часах.");
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var booking = dbContext.Bookings.FirstOrDefault(item => item.Id == bookingId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                ShowStatus("Бронь не найдена", $"Бронь #{bookingId} отсутствует или уже отменена.");
                return;
            }

            var newEnd = newStart.AddHours(durationHours);
            var hasConflict = dbContext.Bookings.Any(item =>
                    item.Id != booking.Id
                    && item.ComputerId == booking.ComputerId
                    && item.Status != BookingStatuses.Cancelled
                    && item.StartTime < newEnd
                    && item.EndTime > newStart)
                || dbContext.GameSessions.Any(session =>
                    session.ComputerId == booking.ComputerId
                    && session.Status != SessionStatuses.Closed
                    && session.StartTime < newEnd
                    && (session.EndTime == null || session.EndTime > newStart));

            if (hasConflict)
            {
                ShowStatus("Конфликт расписания", "На выбранное время уже есть бронь или сессия на этом ПК.");
                return;
            }

            booking.StartTime = newStart;
            booking.EndTime = newEnd;
            booking.CreatedAt = DateTime.Now;
            dbContext.SaveChanges();
            LoadDatabaseState();
            AddAdminLog($"Booking #{bookingId} rescheduled");
            ShowStatus("Бронь перенесена", $"Бронь #{bookingId}: {newStart:dd.MM HH:mm}-{newEnd:HH:mm}.");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка переноса брони", ex);
        }
    }

    private void CancelBookingManually()
    {
        var bookingIdText = PromptText("Отменить бронь", "Введите ID брони:", GetLatestActiveBookingId().ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!int.TryParse(bookingIdText, out var bookingId))
        {
            ShowStatus("Бронь не отменена", "Введите числовой ID брони.");
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var booking = dbContext.Bookings.FirstOrDefault(item => item.Id == bookingId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                ShowStatus("Бронь не найдена", $"Бронь #{bookingId} отсутствует или уже отменена.");
                return;
            }

            booking.Status = BookingStatuses.Cancelled;
            dbContext.SaveChanges();
            LoadDatabaseState();
            AddAdminLog($"Booking #{bookingId} cancelled");
            ShowStatus("Бронь отменена", $"Бронь #{bookingId} отменена администратором.");
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
            using var dbContext = new AppDbContext();
            return dbContext.Bookings
                .AsNoTracking()
                .Where(booking => booking.Status != BookingStatuses.Cancelled)
                .OrderByDescending(booking => booking.CreatedAt)
                .Select(booking => booking.Id)
                .FirstOrDefault();
        }
        catch
        {
            return 0;
        }
    }

    private void AdminAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var action = element.Tag?.ToString() ?? "admin-action";
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

                if (SaveGuestSession(sessionComputer, sessionAmount))
                {
                    _shiftCash += sessionAmount;
                    SetPcStatus(sessionComputer, PcStatuses.Busy);
                    RefreshAdminUx();
                    AddAdminLog($"{sessionComputer} started as guest session");
                    ShowStatus("Новая сессия", $"Запущена гостевая сессия на {sessionComputer}. Карта и бронь обновлены.");
                }
                break;

            case "admin-payment":
            case "admin-pay-std10":
                var paymentComputer = PromptText("Отметить оплату", "Введите ПК ожидающей оплаты:", GetFirstPendingPaymentComputerName() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(paymentComputer))
                {
                    PayAdminSession(paymentComputer);
                }
                break;

            case "admin-settle-all":
                if (!PromptMoney("Закрыть все оплаты", "Сумма по умолчанию для платежей без цены:", "18", out var settlementAmount))
                {
                    break;
                }

                SaveAllPendingPaymentsAsCash(settlementAmount);
                _adminPaymentQueue = 0;
                RefreshAdminUx();
                AddAdminLog("All pending payments settled");
                ShowStatus("Оплаты закрыты", "Все ожидающие платежи отмечены как оплаченные, касса пересчитана.");
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
                    SetPcStatus(serviceComputer, PcStatuses.Service);
                    LoadDatabaseState();
                    RefreshAdminUx();
                    AddAdminLog($"{serviceComputer} moved to service");
                    ShowStatus("Сервис", $"{serviceComputer} переведен в обслуживание. Карта и выбор брони обновлены.");
                }
                break;

            case "admin-clear-service":
                var clearServiceComputer = PromptText("Снять сервис с ПК", "Введите имя ПК:", GetFirstServiceComputerName());
                if (!string.IsNullOrWhiteSpace(clearServiceComputer))
                {
                    SetPcStatus(clearServiceComputer, PcStatuses.Free);
                    LoadDatabaseState();
                    RefreshAdminUx();
                    AddAdminLog($"Service released {clearServiceComputer}");
                    ShowStatus("Сервис снят", $"{clearServiceComputer} вернулся из обслуживания и доступен для брони.");
                }
                break;

            case "shift-close":
                _shiftClosed = !_shiftClosed;
                SaveShiftState(_shiftClosed);
                RefreshAdminUx();
                AddAdminLog(_shiftClosed ? "Shift closed" : "Shift reopened");
                ShowStatus(_shiftClosed ? "Смена закрыта" : "Смена снова активна", _shiftClosed ? "Касса заблокирована для новых расходов, отчет готов." : "Операции смены снова доступны.");
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
                RefreshAdminUx();
                AddAdminLog($"Expense added: -{expenseAmount:0.##} BYN");
                ShowStatus("Расход внесен", $"В кассу добавлен расход: -{expenseAmount:0.##} BYN.");
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
                RefreshAdminUx();
                AddAdminLog("Incident added to shift journal");
                ShowStatus("Инцидент добавлен", "Запись появилась в журнале, очередь поддержки увеличена.");
                break;

            case "owner-peak":
                _ownerDemandMode = _ownerDemandMode == "peak" ? "normal" : "peak";
                if (_ownerDemandMode == "peak")
                {
                    _vipRate = Math.Max(_vipRate, 16);
                    _royalRate = Math.Max(_royalRate, 28);
                    SaveTariffRate("VIP", _vipRate);
                    SaveTariffRate("Royal", _royalRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Режим спроса", $"Активный режим: {_ownerDemandMode}. Метрики пересчитаны из тарифов и загрузки.");
                break;

            case "owner-night":
                _ownerDemandMode = _ownerDemandMode == "night" ? "normal" : "night";
                if (_ownerDemandMode == "night")
                {
                    _standardRate = 7;
                    SaveTariffRate("Standard", _standardRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Режим спроса", $"Активный режим: {_ownerDemandMode}. Метрики пересчитаны без ручного накручивания.");
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
    private void ShiftTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        var done = checkBox.IsChecked == true;
        ShowStatus(done ? "Задача выполнена" : "Задача возвращена", checkBox.Content?.ToString() ?? "Задача смены");
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
            case "admin-close-session":
                CloseAdminSession(parts[1]);
                return true;

            case "admin-pay-session":
                PayAdminSession(parts[1]);
                return true;

            case "admin-extend-session":
                ExtendAdminSession(parts[1]);
                return true;

            default:
                return false;
        }
    }

    private void CloseAdminSession(string computerName)
    {
        _adminActiveSessions = Math.Max(0, _adminActiveSessions - 1);
        _adminFreePcs++;
        SaveSessionClosed(computerName);
        SetPcStatus(computerName, PcStatuses.Free);
        RefreshAdminUx();
        AddAdminLog($"{computerName} closed and released");
        ShowStatus("Сессия закрыта", $"{computerName} освобожден и стал доступен на карте клуба.");
    }

    private void PayAdminSession(string computerName)
    {
        var amount = GetOpenSessionAmount(computerName) ?? 0m;
        if (amount <= 0)
        {
            ShowStatus("Оплата не найдена", $"{computerName}: нет ожидающей оплаты в активных сессиях.");
            return;
        }

        _adminPaymentQueue = Math.Max(0, _adminPaymentQueue - 1);
        _shiftCash += amount;
        SavePaymentConfirmation(computerName, amount);
        RefreshAdminUx();
        AddAdminLog($"{computerName} payment confirmed");
        ShowStatus("Оплата принята", $"{computerName}: касса +{amount:0.##} BYN.");
    }

    private void PayFirstPendingAdminSession()
    {
        try
        {
            using var dbContext = new AppDbContext();
            var session = dbContext.GameSessions
                .AsNoTracking()
                .Where(item => item.EndTime == null
                    && item.Status == SessionStatuses.AwaitingPayment
                    && dbContext.Computers.Any(computer => computer.Id == item.ComputerId && computer.Name == "STD-10"))
                .OrderBy(item => item.StartTime)
                .FirstOrDefault();
            if (session is null)
            {
                ShowStatus("Оплата не найдена", "STD-10 не ожидает оплату.");
                return;
            }

            var computerName = dbContext.Computers
                .AsNoTracking()
                .Where(item => item.Id == session.ComputerId)
                .Select(item => item.Name)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(computerName))
            {
                ShowStatus("Оплата не найдена", "ПК для ожидающей оплаты не найден в БД.");
                return;
            }

            PayAdminSession(computerName);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка поиска оплаты", ex);
        }
    }

    private void ExtendAdminSession(string computerName)
    {
        const decimal extensionPrice = 36m;
        _shiftOnline += extensionPrice;
        SaveSessionExtension(computerName, extensionPrice);
        RefreshAdminUx();
        AddAdminLog($"{computerName} extended");
        ShowStatus("Сессия продлена", $"{computerName}: онлайн +{extensionPrice:0.##} BYN.");
    }

    private decimal? GetOpenSessionAmount(string computerName)
    {
        try
        {
            using var dbContext = new AppDbContext();
            return dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.EndTime == null
                    && dbContext.Computers.Any(computer => computer.Id == session.ComputerId && computer.Name == computerName))
                .OrderByDescending(session => session.StartTime)
                .Select(session => (decimal?)session.TotalPrice)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка чтения сессии", ex);
            return null;
        }
    }

    private void RefreshAdminUx()
    {
        RecalculateOwnerMetrics();
        SyncAdminViewModel();
        SyncOwnerViewModel();
        RebuildAdminOperationLogList();
        RebuildAdminSessionsGrid();

        AdminActiveSessionsValue.Text = _adminActiveSessions.ToString();
        AdminPaymentQueueValue.Text = _adminPaymentQueue.ToString();
        AdminFreePcsValue.Text = _adminFreePcs.ToString();
        AdminFreePcsHintText.Text = $"из {_computers.Count} рабочих мест";
        AdminSupportValue.Text = _adminSupportQueue.ToString();
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
        OwnerPeakModeButton.Style = (Style)FindResource(_ownerDemandMode == "peak" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        OwnerNightModeButton.Style = (Style)FindResource(_ownerDemandMode == "night" ? "PrimaryButtonStyle" : "GhostButtonStyle");
    }

    private void RebuildAdminSessionsGrid()
    {
        if (!IsLoaded || AdminSessionsGrid is null)
        {
            return;
        }

        while (AdminSessionsGrid.Children.Count > 4)
        {
            AdminSessionsGrid.Children.RemoveAt(4);
        }

        while (AdminSessionsGrid.RowDefinitions.Count > 1)
        {
            AdminSessionsGrid.RowDefinitions.RemoveAt(AdminSessionsGrid.RowDefinitions.Count - 1);
        }

        try
        {
            using var dbContext = new AppDbContext();
            var sessions = dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.Status != SessionStatuses.Closed
                    && session.StartTime <= DateTime.Now
                    && (session.EndTime == null || session.EndTime > DateTime.Now))
                .OrderBy(session => session.StartTime)
                .Take(5)
                .ToList();
            var computers = dbContext.Computers.AsNoTracking().ToDictionary(computer => computer.Id);
            var users = dbContext.Users.AsNoTracking().ToDictionary(user => user.Id);

            if (sessions.Count == 0)
            {
                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var emptyText = new TextBlock
                {
                    Text = "Нет активных сессий в базе данных.",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    Margin = new Thickness(0, 14, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(emptyText, 1);
                Grid.SetColumnSpan(emptyText, 5);
                AdminSessionsGrid.Children.Add(emptyText);
                return;
            }

            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var row = i + 1;
                computers.TryGetValue(session.ComputerId, out var computer);
                users.TryGetValue(session.UserId, out var user);

                var computerName = computer?.Name ?? $"ПК-{session.ComputerId}";
                var clientName = user?.FullName ?? $"User #{session.UserId}";
                var endText = session.EndTime?.ToString("HH:mm") ?? "открыта";
                var statusText = FormatAdminSessionStatus(session.Status);
                var statusBrush = ResolveAdminSessionStatusBrush(session.Status);

                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddAdminSessionCell(row, 0, computerName, "TextBrush", FontWeights.Bold);
                AddAdminSessionCell(row, 1, clientName, "MutedBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 2, endText, "TextBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 3, statusText, statusBrush, FontWeights.Bold);
                AddAdminSessionButton(row, computerName, session.Status);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки сессий", ex);
        }
    }

    private void AddAdminSessionCell(int row, int column, string text, string brushKey, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource(brushKey),
            FontWeight = weight,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        AdminSessionsGrid.Children.Add(block);
    }

    private void AddAdminSessionButton(int row, string computerName, string status)
    {
        var isAwaitingPayment = string.Equals(status, SessionStatuses.AwaitingPayment, StringComparison.OrdinalIgnoreCase);
        var isTeamSession = string.Equals(status, SessionStatuses.Team, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            Content = isAwaitingPayment ? "Оплата" : isTeamSession ? "Продлить" : "Закрыть",
            Style = (Style)FindResource(isAwaitingPayment ? "PrimaryButtonStyle" : "GhostButtonStyle"),
            Tag = isAwaitingPayment
                ? $"admin-pay-session|{computerName}"
                : isTeamSession
                    ? $"admin-extend-session|{computerName}"
                    : $"admin-close-session|{computerName}",
            MinHeight = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(10, 8, 0, 0)
        };
        button.Click += AdminAction_Click;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, 4);
        AdminSessionsGrid.Children.Add(button);
    }

    private static string FormatAdminSessionStatus(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "Ожидает",
            SessionStatuses.Team => "Команда",
            SessionStatuses.Active => "Оплачено",
            _ => status
        };
    }

    private static string ResolveAdminSessionStatusBrush(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "WaitBrush",
            SessionStatuses.Team => "GoldLightBrush",
            SessionStatuses.Active => "OkBrush",
            _ => "MutedBrush"
        };
    }

    private void RecalculateOwnerMetrics()
    {
        var totalPcs = Math.Max(1, _computers.Count);
        var occupiedPcs = Math.Clamp(totalPcs - _adminFreePcs - _adminSupportQueue, 0, totalPcs);
        var demandMultiplier = _ownerDemandMode switch
        {
            "peak" => 1.18m,
            "night" => 0.88m,
            "loyalty" => 1.05m,
            _ => 1m
        };
        var loadBonus = _ownerDemandMode switch
        {
            "peak" => 7,
            "night" => 3,
            "loyalty" => 2,
            _ => 0
        };

        var standardRevenue = 14 * 3.2m * _standardRate;
        var vipRevenue = 8 * 2.8m * _vipRate;
        var royalRevenue = 5 * 2.4m * _royalRate;
        var bootcampRevenue = _bootcampRate * 0.75m;
        var packageRevenue = _shiftOnline * 0.35m;
        var pendingPenalty = _adminPaymentQueue * 12m;
        var servicePenalty = _adminSupportQueue * 18m;

        _ownerRevenue = (int)Math.Round((standardRevenue + vipRevenue + royalRevenue + bootcampRevenue + packageRevenue + _shiftCash - pendingPenalty - servicePenalty) * demandMultiplier);
        _ownerLoad = Math.Clamp((int)Math.Round(occupiedPcs * 100m / totalPcs) + loadBonus, 0, 100);

        var paidSessions = Math.Max(1, _adminActiveSessions - _adminPaymentQueue);
        _ownerAverageCheck = Math.Max(0, (int)Math.Round(_ownerRevenue / (decimal)paidSessions));
        _ownerRepeatRate = Math.Clamp(58 + (_ownerDemandMode == "loyalty" ? 6 : 0) + (_ownerDemandMode == "night" ? 3 : 0) - Math.Max(0, _adminSupportQueue - 3), 0, 99);
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
            using var dbContext = new AppDbContext();
            var logs = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.PaymentType == PaymentTypes.AdminLog)
                .OrderByDescending(payment => payment.CreatedAt)
                .Take(6)
                .ToList();

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
            using var dbContext = new AppDbContext();
            var userId = ResolveCurrentOrAdminUserId(dbContext);
            if (userId is null)
            {
                return;
            }

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = userId.Value,
                Amount = 0,
                PaymentType = PaymentTypes.AdminLog,
                CreatedAt = DateTime.Now,
                Comment = $"Admin log: {text}"
            });
            dbContext.SaveChanges();
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

    private static int ToggleRate(int current, int normal, int raised)
    {
        return current == normal ? raised : normal;
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

