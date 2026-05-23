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
                ShowStatus("РћРїР»Р°С‚Р° РїР°РєРµС‚Р°", "РћС‚РєСЂС‹С‚Р° СЃС‚СЂР°РЅРёС†Р° Р±Р°Р»Р°РЅСЃР° СЃ РїР°РєРµС‚Р°РјРё РІСЂРµРјРµРЅРё.");
                break;
            case "profile":
                NavigateTo("cabinet");
                ShowStatus("РџСЂРѕС„РёР»СЊ РєР»РёРµРЅС‚Р°", $"РћС‚РєСЂС‹С‚ РєР°Р±РёРЅРµС‚ {_currentUserFullName}.");
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
                ShowStatus("РџРѕРїРѕР»РЅРµРЅРёРµ Р±Р°Р»Р°РЅСЃР°", "РћС‚РєСЂС‹С‚Р° СЃС‚СЂР°РЅРёС†Р° Р±Р°Р»Р°РЅСЃР° Рё С„РѕСЂРјР° РѕРїР»Р°С‚С‹.");
                break;
            case "package":
                NavigateTo("balance");
                ShowStatus("РћРїР»Р°С‚Р° Р±СЂРѕРЅРё", "РќР° СЃС‚СЂР°РЅРёС†Рµ Р±Р°Р»Р°РЅСЃР° РґРѕСЃС‚СѓРїРµРЅ С‚РѕР»СЊРєРѕ СЃС‡РµС‚ РїРѕ Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё.");
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
        var eventName = parts.ElementAtOrDefault(0) ?? "РЎРѕР±С‹С‚РёРµ";
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
                sourceButton.Content = existing ? "РЈР¶Рµ Р·Р°РїРёСЃР°РЅ" : "Р—Р°СЏРІРєР° РѕС‚РїСЂР°РІР»РµРЅР°";
                sourceButton.IsEnabled = false;
                sourceButton.Opacity = 0.65;
            }

            EventApplicationsText.Text = $"{category}: {eventName} В· {time}";
            LoadDatabaseState();
            if (existing)
            {
                ShowStatus("Р—Р°СЏРІРєР° СѓР¶Рµ РµСЃС‚СЊ", $"{eventName}: Р·Р°РїРёСЃСЊ СѓР¶Рµ СЃРѕС…СЂР°РЅРµРЅР° РІ РёСЃС‚РѕСЂРёРё РєР»РёРµРЅС‚Р°.");
            }
            else
            {
                ShowImportantStatus("Р—Р°СЏРІРєР° РѕС‚РїСЂР°РІР»РµРЅР°", $"{eventName}: Р·Р°РїРёСЃСЊ СЃРѕС…СЂР°РЅРµРЅР° РІ РёСЃС‚РѕСЂРёРё РєР»РёРµРЅС‚Р°.");
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РїРёСЃРё РЅР° СЃРѕР±С‹С‚РёРµ", ex);
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
                    ShowStatus("РџСЂРѕРјРѕРєРѕРґ СЃРЅСЏС‚", "РЎРєРёРґРєР° Рє РѕРїР»Р°С‚Рµ Р±СЂРѕРЅРё Рё Р±РѕРЅСѓСЃ Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РѕС‚РєР»СЋС‡РµРЅС‹.");
                    break;
                }

                using (var unitOfWork = new UnitOfWork())
                {
                    var promoCode = unitOfWork.PromoCodes.GetActiveByCode(promo);
                    if (promoCode is not null)
                    {
                        _appliedPromoCode = promoCode.Code;
                        UpdateTopupSummary();
                        LoadDatabaseState();
                        ShowStatus("РџСЂРѕРјРѕРєРѕРґ РїСЂРёРјРµРЅРµРЅ", $"{promoCode.Code}: в€’{promoCode.BookingDiscountRate * 100:0}% Рє РѕРїР»Р°С‚Рµ Р±СЂРѕРЅРё РёР»Рё +{promoCode.TopupBonusRate * 100:0}% Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РѕС‚ {promoCode.MinTopupAmount:0.##} BYN.");
                        break;
                    }
                }

                _appliedPromoCode = null;
                UpdateTopupSummary();
                LoadDatabaseState();
                ShowStatus("РџСЂРѕРјРѕРєРѕРґ РЅРµ РЅР°Р№РґРµРЅ", "РџСЂРѕРІРµСЂСЊС‚Рµ РєРѕРґ РёР»Рё Р°РєС‚РёРІРЅРѕСЃС‚СЊ РїСЂРѕРјРѕРєРѕРґР° РІ Р±Р°Р·Рµ.");
                break;
            case "promo-clear":
                _appliedPromoCode = null;
                PromoCodeBox.Text = string.Empty;
                UpdateTopupSummary();
                LoadDatabaseState();
                ShowStatus("РџСЂРѕРјРѕРєРѕРґ СЃРЅСЏС‚", "РЎРєРёРґРєР° Рє РѕРїР»Р°С‚Рµ Р±СЂРѕРЅРё Рё Р±РѕРЅСѓСЃ Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РѕС‚РєР»СЋС‡РµРЅС‹.");
                break;
            case "offer":
                ShowStatus("РџРµСЂСЃРѕРЅР°Р»СЊРЅР°СЏ Р°РєС†РёСЏ", "Р‘РѕРЅСѓСЃ СЃС‚Р°С‚СѓСЃР° РїСЂРёРјРµРЅСЏРµС‚СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РїСЂРё РїРѕРїРѕР»РЅРµРЅРёРё Рё РЅРµ С‚СЂРµР±СѓРµС‚ Р°РєС‚РёРІР°С†РёРё.");
                break;
            case "export":
                var exportPath = ExportBalanceHistory();
                ShowImportantStatus(T("Balance.Export"), $"РСЃС‚РѕСЂРёСЏ РѕРїРµСЂР°С†РёР№ РІС‹РіСЂСѓР¶РµРЅР°: {exportPath}");
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
            ShowDatabaseError("РћС€РёР±РєР° СЌРєСЃРїРѕСЂС‚Р° Р±Р°Р»Р°РЅСЃР°", ex);
            return "СЌРєСЃРїРѕСЂС‚ РЅРµ РІС‹РїРѕР»РЅРµРЅ";
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
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РІС‹Р±СЂР°РЅР°", "РЎРЅР°С‡Р°Р»Р° СЃРѕР·РґР°Р№С‚Рµ Р±СЂРѕРЅСЊ, Р·Р°С‚РµРј РІРµСЂРЅРёС‚РµСЃСЊ Рє РѕРїР»Р°С‚Рµ.");
            return;
        }

        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        if (!TryParseMoney(price, out var amount))
        {
            ShowStatus("РћРїР»Р°С‚Р° РїР°РєРµС‚Р°", "РќРµ СѓРґР°Р»РѕСЃСЊ РѕРїСЂРµРґРµР»РёС‚СЊ СЃС‚РѕРёРјРѕСЃС‚СЊ РїР°РєРµС‚Р°.");
            return;
        }

        var confirmation = MessageBox.Show(
            "РћРїР»Р°С‚Р° Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё СЃСЂР°Р·Сѓ Р·Р°РїСѓСЃРєР°РµС‚ РёРіСЂРѕРІСѓСЋ СЃРµСЃСЃРёСЋ РЅР° РІС‹Р±СЂР°РЅРЅРѕРј РџРљ. РџСЂРѕРґРѕР»Р¶РёС‚СЊ?",
            "РџРѕРґС‚РІРµСЂР¶РґРµРЅРёРµ РѕРїР»Р°С‚С‹",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            ShowStatus("РћРїР»Р°С‚Р° РѕС‚РјРµРЅРµРЅР°", "Р‘СЂРѕРЅСЊ РѕСЃС‚Р°Р»Р°СЃСЊ Р°РєС‚РёРІРЅРѕР№ Рё РѕР¶РёРґР°РµС‚ РѕРїР»Р°С‚С‹.");
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
            ShowImportantStatus("РџР°РєРµС‚ РѕРїР»Р°С‡РµРЅ", resultMessage);
            return;
        }

        ShowStatus("РћРїР»Р°С‚Р° РЅРµ РІС‹РїРѕР»РЅРµРЅР°", resultMessage);
        if (resultMessage.Contains("РќРµРґРѕСЃС‚Р°С‚РѕС‡РЅРѕ", StringComparison.OrdinalIgnoreCase))
        {
            TopupAmountBox.Text = Math.Max(1, amount - _balanceAmount).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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
                message = "РџРѕР»СЊР·РѕРІР°С‚РµР»СЊ РЅРµ РЅР°Р№РґРµРЅ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
                return false;
            }

            var booking = unitOfWork.Bookings.GetPendingForUser(user.Id, DateTime.Now.AddMinutes(-15));
            if (booking is null)
            {
                message = "РЎРЅР°С‡Р°Р»Р° Р·Р°Р±СЂРѕРЅРёСЂСѓР№ РџРљ, РїРѕС‚РѕРј РѕРїР»Р°С‡РёРІР°Р№ РїР°РєРµС‚.";
                return false;
            }

            var computer = unitOfWork.Computers.GetById(booking.ComputerId);
            if (computer is null)
            {
                message = "РџРљ РёР· Р±СЂРѕРЅРё РЅРµ РЅР°Р№РґРµРЅ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
                return false;
            }

            if (HasActiveIndividualSession(unitOfWork, user.Id, out var activeSessionComputer))
            {
                message = $"РЈ РєР»РёРµРЅС‚Р° СѓР¶Рµ РµСЃС‚СЊ Р°РєС‚РёРІРЅР°СЏ СЃРµСЃСЃРёСЏ РЅР° {activeSessionComputer}. РЎРЅР°С‡Р°Р»Р° Р·Р°РІРµСЂС€РёС‚Рµ С‚РµРєСѓС‰СѓСЋ СЃРµСЃСЃРёСЋ.";
                return false;
            }

            var bookingAmount = CalculateBookingTotal(booking, computer);
            var bookingLabel = GetBookingPackageLabel(booking);
            amount = ApplyBookingPromo(bookingAmount);
            packageName = bookingLabel;
            if (user.Balance < amount)
            {
                message = $"РќРµРґРѕСЃС‚Р°С‚РѕС‡РЅРѕ СЃСЂРµРґСЃС‚РІ: РЅСѓР¶РЅРѕ {amount:0.##} BYN, РґРѕСЃС‚СѓРїРЅРѕ {user.Balance:0.##} BYN.";
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
            message = $"{packageName}: СЃРїРёСЃР°РЅРѕ {amount:0.##} BYN, РЅР°С‡Р°С‚Р° СЃРµСЃСЃРёСЏ РЅР° {computer.Name} РґРѕ {endTime:HH:mm}.";
            return true;
        }
        catch (Exception ex)
        {
            message = "РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РѕРїР»Р°С‚Сѓ РїР°РєРµС‚Р° РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
            ShowDatabaseError("РћС€РёР±РєР° РѕРїР»Р°С‚С‹ РїР°РєРµС‚Р°", ex);
            return false;
        }
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
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РІС‹Р±СЂР°РЅР°", "РЎРЅР°С‡Р°Р»Р° СЃРѕР·РґР°Р№С‚Рµ Р±СЂРѕРЅСЊ, Р·Р°С‚РµРј РІРµСЂРЅРёС‚РµСЃСЊ Рє РѕРїР»Р°С‚Рµ.");
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
