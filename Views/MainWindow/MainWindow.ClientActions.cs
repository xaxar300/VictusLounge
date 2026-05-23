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
    private void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var action = button.Tag?.ToString();
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
                NavigateTo(action ?? "dashboard");
                break;
        }
    }

    private void CabinetAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag?.ToString())
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

    private void EventFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string filter)
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

    private void EventJoin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        var eventName = parts.ElementAtOrDefault(0) ?? "Событие";
        var category = parts.ElementAtOrDefault(1) ?? "Event";
        var time = parts.ElementAtOrDefault(2) ?? "--:--";

        RegisterEventApplication(eventName, category, time, button);
    }

    private void RegisterEventApplication(string eventName, string category, string time, Button? sourceButton)
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var existing = dbContext.Payments.Any(payment =>
                payment.UserId == _currentUserId
                && payment.PaymentType == PaymentTypes.EventRegistration
                && payment.Comment.Contains(eventName));
            if (!existing)
            {
                dbContext.Payments.Add(new Payment
                {
                    Id = dbContext.Payments.GetNextId(payment => payment.Id),
                    UserId = _currentUserId,
                    Amount = 0,
                    PaymentType = PaymentTypes.EventRegistration,
                    CreatedAt = DateTime.Now,
                    Comment = $"Event registration: {eventName}; category {category}; time {time}"
                });
                dbContext.SaveChanges();
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

    private void BalanceAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag?.ToString())
        {
            case "topup":
                OpenTopupOverlay();
                break;
            case "promo":
                var promo = PromoCodeBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(promo))
                {
                    _appliedPromoCode = null;
                    UpdateTopupSummary();
                    LoadDatabaseState();
                    ShowStatus("Промокод снят", "Скидка к оплате брони и бонус к пополнению отключены.");
                    break;
                }

                using (var dbContext = new AppDbContext())
                {
                    var promoCode = dbContext.PromoCodes
                        .AsNoTracking()
                        .FirstOrDefault(item => item.IsActive && item.Code == promo);
                    if (promoCode is not null)
                    {
                        _appliedPromoCode = promoCode.Code;
                        UpdateTopupSummary();
                        LoadDatabaseState();
                        ShowStatus("Промокод применен", $"{promoCode.Code}: −{promoCode.BookingDiscountRate * 100:0}% к оплате брони или +{promoCode.TopupBonusRate * 100:0}% к пополнению от {promoCode.MinTopupAmount:0.##} BYN.");
                        break;
                    }
                }

                _appliedPromoCode = null;
                UpdateTopupSummary();
                LoadDatabaseState();
                ShowStatus("Промокод не найден", "Проверьте код или активность промокода в базе.");
                break;
            case "promo-clear":
                _appliedPromoCode = null;
                PromoCodeBox.Text = string.Empty;
                UpdateTopupSummary();
                LoadDatabaseState();
                ShowStatus("Промокод снят", "Скидка к оплате брони и бонус к пополнению отключены.");
                break;
            case "offer":
                ShowStatus("Персональная акция", "Бонус статуса применяется автоматически при пополнении и не требует активации.");
                break;
            case "export":
                var exportPath = ExportBalanceHistory();
                ShowImportantStatus(T("Balance.Export"), $"История операций выгружена: {exportPath}");
                break;
            default:
                LoadDatabaseState();
                break;
        }
    }

    private string ExportBalanceHistory()
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.AsNoTracking().FirstOrDefault(item => item.Id == _currentUserId);
            var payments = dbContext.Payments
                .AsNoTracking()
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

    private void BalancePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
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
            TopupAmountBox.Text = Math.Max(1, amount - _balanceAmount).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            OpenTopupOverlay();
        }
    }

    private bool SavePackagePurchase(string packageName, decimal amount, out string message)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.FirstOrDefault(item => item.Id == _currentUserId);
            if (user is null)
            {
                message = "Пользователь не найден в базе данных.";
                return false;
            }

            var booking = dbContext.Bookings
                .Where(item => item.UserId == user.Id
                    && item.Status == BookingStatuses.PendingPayment
                    && item.StartTime >= DateTime.Now.AddMinutes(-15))
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            if (booking is null)
            {
                message = "Сначала забронируй ПК, потом оплачивай пакет.";
                return false;
            }

            var computer = dbContext.Computers.FirstOrDefault(item => item.Id == booking.ComputerId);
            if (computer is null)
            {
                message = "ПК из брони не найден в базе данных.";
                return false;
            }

            if (HasActiveIndividualSession(dbContext, user.Id, out var activeSessionComputer))
            {
                message = $"У клиента уже есть активная сессия на {activeSessionComputer}. Сначала завершите текущую сессию.";
                return false;
            }

            var bookingAmount = CalculateBookingTotal(booking, computer);
            var bookingLabel = GetBookingPackageLabel(booking);
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

            dbContext.GameSessions.Add(new GameSession
            {
                Id = dbContext.GameSessions.GetNextId(session => session.Id),
                UserId = user.Id,
                ComputerId = computer.Id,
                StartTime = startTime,
                EndTime = endTime,
                TotalPrice = amount,
                Status = SessionStatuses.Active
            });

            dbContext.Payments.Add(new Payment
            {
                Id = dbContext.Payments.GetNextId(payment => payment.Id),
                UserId = user.Id,
                Amount = -amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = IsPromoApplied()
                    ? $"Package purchase: {packageName}; promo {_appliedPromoCode}; session started on {computer.Name}"
                    : $"Package purchase: {packageName}; session started on {computer.Name}"
            });

            dbContext.SaveChanges();
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

    private static double GetPackageDurationHours(string packageName, Booking booking)
    {
        if (packageName.Contains("Quick", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (packageName.Contains("Evening", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (packageName.Contains("Night", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (packageName.Contains("Weekend", StringComparison.OrdinalIgnoreCase))
        {
            return 12;
        }

        return Math.Max(1, (booking.EndTime - booking.StartTime).TotalHours);
    }

    private void BalancePackageCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.Tag is not string raw)
        {
            return;
        }

        if (raw.Equals("booking", StringComparison.OrdinalIgnoreCase))
        {
            NavigateTo("booking");
            ShowStatus("Бронь не выбрана", "Сначала создайте бронь, затем вернитесь к оплате.");
            return;
        }

        ShowBalancePackageStatus(raw);
    }

    private void ShowBalancePackageStatus(string raw)
    {
        var parts = raw.Split('|');
        var packageName = parts.ElementAtOrDefault(0) ?? T("Balance.Package");
        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        var message = string.IsNullOrWhiteSpace(price)
            ? string.Format(T("Balance.PackageToast"), packageName)
            : string.Format(T("Balance.PackageToastWithPrice"), packageName, price);
        ShowStatus(T("Balance.PackageSelected"), message);
    }

}

