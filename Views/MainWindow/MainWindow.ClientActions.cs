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
    private void ExecuteQuickAction(string action)
    {
        switch (action)
        {
            case "tournament":
                NavigateTo("events");
                RegisterEventApplication("Dota 2 Weekend Cup", "Dota 2", "22:00", null);
                break;
            case "package":
                NavigateTo("balance");
                ShowStatus("Оплата пакета", "Открыта страница баланса с пакетами времени.");
                break;
            case "profile":
                NavigateTo("cabinet");
                ShowStatus("Профиль клиента", $"Открыт кабинет {_currentUserFullName}.");
                break;
            default:
                NavigateTo(string.IsNullOrWhiteSpace(action) ? "dashboard" : action);
                break;
        }
    }

    private void ExecuteCabinetAction(string action)
    {
        switch (action)
        {
            case "topup":
                NavigateTo("balance");
                OpenTopupOverlay();
                ShowStatus("Пополнение баланса", "Открыта страница баланса и форма оплаты.");
                break;
            case "package":
                NavigateTo("balance");
                ShowStatus("Оплата брони", "На странице баланса доступен только счет по активной брони.");
                break;
            case "events":
                NavigateTo("events");
                break;
            default:
                NavigateTo("cabinet");
                break;
        }
    }

    private void ApplyEventFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        EventFilterAllButton.Style = (Style)FindResource(filter == "all" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterDotaButton.Style = (Style)FindResource(filter == "dota" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterCsButton.Style = (Style)FindResource(filter == "cs2" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterLanButton.Style = (Style)FindResource(filter == "lan" ? "PrimaryButtonStyle" : "GhostButtonStyle");

        DotaEventCard.Visibility = filter is "all" or "dota" ? Visibility.Visible : Visibility.Collapsed;
        CsEventCard.Visibility = filter is "all" or "cs2" ? Visibility.Visible : Visibility.Collapsed;
        LanEventCard.Visibility = filter is "all" or "lan" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void JoinEvent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var parts = raw.Split('|');
        var eventName = parts.ElementAtOrDefault(0) ?? "Событие";
        var category = parts.ElementAtOrDefault(1) ?? "Event";
        var time = parts.ElementAtOrDefault(2) ?? "--:--";

        RegisterEventApplication(eventName, category, time, null);
    }

    private void RegisterEventApplication(string eventName, string category, string time, Button? sourceButton)
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var existing = unitOfWork.Payments.Any(payment =>
                payment.UserId == _currentUserId
                && payment.PaymentType == PaymentTypes.EventRegistration
                && payment.Comment.Contains(eventName));
            if (!existing)
            {
                unitOfWork.Payments.Add(new Payment
                {
                    Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                    UserId = _currentUserId,
                    Amount = 0,
                    PaymentType = PaymentTypes.EventRegistration,
                    CreatedAt = DateTime.Now,
                    Comment = $"Event registration: {eventName}; category {category}; time {time}"
                });
                unitOfWork.SaveChanges();
            }

            if (sourceButton is not null)
            {
                sourceButton.Content = existing ? "Уже записан" : "Заявка отправлена";
                sourceButton.IsEnabled = false;
                sourceButton.Opacity = 0.65;
            }

            EventApplicationsText.Text = $"{category}: {eventName} · {time}";
            LoadDatabaseState();
            if (existing)
            {
                ShowStatus("Заявка уже есть", $"{eventName}: запись уже сохранена в истории клиента.");
            }
            else
            {
                ShowImportantStatus("Заявка отправлена", $"{eventName}: запись сохранена в истории клиента.");
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка записи на событие", ex);
        }
    }

    private string ExportBalanceHistory()
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
            var payments = unitOfWork.Payments.QueryNoTracking()
                .Where(payment => payment.UserId == _currentUserId)
                .OrderByDescending(payment => payment.CreatedAt)
                .ToList();

            var exportDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "Exports");
            System.IO.Directory.CreateDirectory(exportDirectory);
            var login = string.IsNullOrWhiteSpace(user?.Login) ? $"user-{_currentUserId}" : user.Login;
            var path = System.IO.Path.Combine(exportDirectory, $"balance-{login}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var lines = new List<string> { "Date;Type;Amount;Comment" };
            lines.AddRange(payments.Select(payment =>
                $"{payment.CreatedAt:yyyy-MM-dd HH:mm};{EscapeCsv(FormatPaymentMethod(payment))};{payment.Amount:0.##};{EscapeCsv(FormatPaymentOperation(payment))}"));
            System.IO.File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            return path;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка экспорта баланса", ex);
            return "экспорт не выполнен";
        }
    }

    private static string EscapeCsv(string value)
    {
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private void HandleBalancePackagePurchase(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var parts = raw.Split('|');
        var packageName = parts.ElementAtOrDefault(0) ?? T("Balance.Package");
        if (packageName.Equals("booking", StringComparison.OrdinalIgnoreCase))
        {
            NavigateTo("booking");
            ShowStatus("Бронь не выбрана", "Сначала создайте бронь, затем вернитесь к оплате.");
            return;
        }

        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        if (!TryParseMoney(price, out var amount))
        {
            ShowStatus("Оплата пакета", "Не удалось определить стоимость пакета.");
            return;
        }

        var confirmation = MessageBox.Show(
            "Оплата активной брони сразу запускает игровую сессию на выбранном ПК. Продолжить?",
            "Подтверждение оплаты",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            ShowStatus("Оплата отменена", "Бронь осталась активной и ожидает оплаты.");
            return;
        }

        if (SavePackagePurchase(packageName, amount, out var resultMessage))
        {
            UpdateCurrentBalanceText();
            LoadDatabaseState();
            RefreshLiveViewsAfterDatabaseChange();
            Dispatcher.InvokeAsync(() =>
            {
                LoadDatabaseState();
                RefreshLiveViewsAfterDatabaseChange();
            }, DispatcherPriority.Background);
            RefreshAdminUx();
            ShowImportantStatus("Пакет оплачен", resultMessage);
            return;
        }

        ShowStatus("Оплата не выполнена", resultMessage);
        if (resultMessage.Contains("Недостаточно", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.Topup.AmountText = Math.Max(1, amount - _balanceAmount).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            OpenTopupOverlay();
        }
    }

    private bool SavePackagePurchase(string packageName, decimal amount, out string message)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.GetById(_currentUserId);
            if (user is null)
            {
                message = "Пользователь не найден в базе данных.";
                return false;
            }

            var booking = unitOfWork.Bookings.GetPendingForUser(user.Id, DateTime.Now.AddMinutes(-15));
            if (booking is null)
            {
                message = "Сначала забронируй ПК, потом оплачивай пакет.";
                return false;
            }

            var computer = unitOfWork.Computers.GetById(booking.ComputerId);
            if (computer is null)
            {
                message = "ПК из брони не найден в базе данных.";
                return false;
            }

            if (HasActiveIndividualSession(unitOfWork, user.Id, out var activeSessionComputer))
            {
                message = $"У клиента уже есть активная сессия на {activeSessionComputer}. Сначала завершите текущую сессию.";
                return false;
            }

            var bookingAmount = CalculateBookingTotal(booking, computer);
            var bookingLabel = GetBookingPackageLabel(booking, GetStoredClientTier(user.Id));
            amount = ApplyBookingPromo(bookingAmount);
            packageName = bookingLabel;
            if (user.Balance < amount)
            {
                message = $"Недостаточно средств: нужно {amount:0.##} BYN, доступно {user.Balance:0.##} BYN.";
                return false;
            }

            var now = DateTime.Now;
            var duration = booking.EndTime - booking.StartTime;
            var startTime = now;
            var endTime = startTime.Add(duration);
            user.Balance -= amount;

            booking.Status = BookingStatuses.Confirmed;
            computer.Status = PcStatuses.Busy;

            unitOfWork.GameSessions.Add(new GameSession
            {
                Id = unitOfWork.GameSessions.GetNextId(session => session.Id),
                UserId = user.Id,
                ComputerId = computer.Id,
                StartTime = startTime,
                EndTime = endTime,
                TotalPrice = amount,
                Status = SessionStatuses.Active
            });

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = user.Id,
                Amount = -amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = IsPromoApplied()
                    ? $"Package purchase: {packageName}; promo {_appliedPromoCode}; session started on {computer.Name}"
                    : $"Package purchase: {packageName}; session started on {computer.Name}"
            });

            unitOfWork.SaveChanges();
            _balanceAmount = user.Balance;
            message = $"{packageName}: списано {amount:0.##} BYN, начата сессия на {computer.Name} до {endTime:HH:mm}.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Не удалось сохранить оплату пакета в базе данных.";
            ShowDatabaseError("Ошибка оплаты пакета", ex);
            return false;
        }
    }

    private void ShowBalancePackageStatus(string raw)
    {
        if (raw.Equals("booking", StringComparison.OrdinalIgnoreCase))
        {
            NavigateTo("booking");
            ShowStatus("Бронь не выбрана", "Сначала создайте бронь, затем вернитесь к оплате.");
            return;
        }

        var parts = raw.Split('|');
        var packageName = parts.ElementAtOrDefault(0) ?? T("Balance.Package");
        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        var message = string.IsNullOrWhiteSpace(price)
            ? string.Format(T("Balance.PackageToast"), packageName)
            : string.Format(T("Balance.PackageToastWithPrice"), packageName, price);
        ShowStatus(T("Balance.PackageSelected"), message);
    }

}
