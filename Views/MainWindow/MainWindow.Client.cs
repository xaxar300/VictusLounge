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
    private void RefreshClientUx(IUnitOfWork unitOfWork, User user)
    {
        if (!IsLoaded)
        {
            return;
        }

        var userSessions = unitOfWork.GameSessions
            .QueryNoTracking()
            .Where(session => session.UserId == user.Id)
            .ToList();
        var computers = unitOfWork.Computers.GetDictionaryNoTracking();
        var userPayments = unitOfWork.Payments
            .QueryNoTracking()
            .Where(payment => payment.UserId == user.Id)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToList();
        var payableBookingCutoff = DateTime.Now.AddMinutes(-15);
        var activeBooking = unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.UserId == user.Id
                && booking.Status == BookingStatuses.PendingPayment
                && booking.StartTime >= payableBookingCutoff)
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenByDescending(booking => booking.Id)
            .FirstOrDefault();

        var playedHours = userSessions
            .Where(session => session.EndTime is not null)
            .Sum(session => Math.Max(0, (session.EndTime!.Value - session.StartTime).TotalHours));
        var bonus = userPayments
            .Where(payment => payment.PaymentType.Equals(PaymentTypes.Bonus, StringComparison.OrdinalIgnoreCase))
            .Sum(payment => payment.Amount);
        var favoriteZone = userSessions
            .Where(session => computers.ContainsKey(session.ComputerId))
            .GroupBy(session => computers[session.ComputerId].Zone)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "-";
        var progress = Math.Clamp((int)Math.Round(user.Balance / 150m * 100), 0, 100);

        CabinetUserNameText.Text = user.FullName;
        CabinetTierText.Text = $"{GetClientTier(user)} В· {user.Login}";
        CabinetProgressText.Text = $"{progress}% В· Р±РѕРЅСѓСЃРѕРІ: {bonus:0.##}";
        CabinetBalanceText.Text = $"{user.Balance:0.##} BYN";
        CabinetBonusText.Text = $"{bonus:0.##}";
        CabinetPlayedText.Text = $"{playedHours:0.#} С‡";
        CabinetFavoriteZoneText.Text = favoriteZone;
        _balanceAmount = user.Balance;
        UpdateCurrentBalanceText();
        BalanceBonusText.Text = $"РџРѕР»СѓС‡РµРЅРѕ Р±РѕРЅСѓСЃРѕРІ: {bonus:0.##}";
        UpdateBalancePersonalOffer(user);

        if (activeBooking is not null && computers.TryGetValue(activeBooking.ComputerId, out var bookingComputer))
        {
            var price = CalculateBookingTotal(activeBooking, bookingComputer);
            var label = GetBookingPackageLabel(activeBooking);
            var payablePrice = ApplyBookingPromo(price);
            var promoSuffix = payablePrice < price ? $" В· РїСЂРѕРјРѕРєРѕРґ -{price - payablePrice:0.##} BYN" : string.Empty;
            CabinetActiveBookingText.Text = $"{bookingComputer.Name} В· {activeBooking.StartTime:dd.MM HH:mm}вЂ“{activeBooking.EndTime:HH:mm}";
            CabinetActiveBookingPriceText.Text = $"{payablePrice:0.##} BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Visible;
            _activeCabinetBookingId = activeBooking.Id;
            CabinetBookingCardPcText.Text = bookingComputer.Name;
            CabinetBookingCardTimeText.Text = $"{activeBooking.StartTime:dd.MM HH:mm}вЂ“{activeBooking.EndTime:HH:mm}";
            CabinetBookingCardPriceText.Text = $"{bookingComputer.Zone} В· {label} В· {payablePrice:0.##} BYN{promoSuffix}";
            UpdateBalanceBookingOffer(activeBooking, bookingComputer, price, label);
        }
        else
        {
            CabinetActiveBookingText.Text = "РќРµС‚ Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё";
            CabinetActiveBookingPriceText.Text = "0 BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Collapsed;
            _activeCabinetBookingId = null;
            CabinetBookingCardPcText.Text = "РќРµС‚ Р±СЂРѕРЅРё";
            CabinetBookingCardTimeText.Text = string.Empty;
            CabinetBookingCardPriceText.Text = string.Empty;
            UpdateBalanceBookingOffer(null, null, 0m, string.Empty);
        }

        RebuildCabinetSessionsGrid(userSessions, computers);
        RebuildBalanceHistoryGrid(userPayments);
    }

    private void UpdateBalanceBookingOffer(Booking? booking, Computer? computer, decimal total, string packageLabel)
    {
        QuickGamePackageCard.Visibility = Visibility.Visible;
        EveningPackageCard.Visibility = Visibility.Collapsed;
        NightPackageCard.Visibility = Visibility.Collapsed;
        BootcampPackageCard.Visibility = Visibility.Collapsed;
        WeekendPackageCard.Visibility = Visibility.Collapsed;

        if (booking is null || computer is null)
        {
            BalancePackagesTitleText.Text = "РћРїР»Р°С‚Р° Р±СЂРѕРЅРё";
            QuickGameTitleText.Text = "РќРµС‚ Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё";
            QuickGamePackageText.Text = "РЎРЅР°С‡Р°Р»Р° Р·Р°Р±СЂРѕРЅРёСЂСѓР№С‚Рµ РџРљ";
            QuickGameBuyButton.Content = "РџРµСЂРµР№С‚Рё Рє Р±СЂРѕРЅРё";
            QuickGameBuyButton.Tag = "booking";
            QuickGamePackageCard.Tag = "booking";
            return;
        }

        var duration = Math.Max(1, (booking.EndTime - booking.StartTime).TotalHours);
        var payableTotal = ApplyBookingPromo(total);
        var promoSuffix = payableTotal < total ? $" В· РїСЂРѕРјРѕРєРѕРґ -{total - payableTotal:0.##} BYN" : string.Empty;
        var tag = $"{packageLabel}|{payableTotal:0.##} BYN";
        BalancePackagesTitleText.Text = "РћРїР»Р°С‚Р° Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё";
        QuickGameTitleText.Text = $"{computer.Name} В· {computer.Zone}";
        QuickGamePackageText.Text = $"{packageLabel} В· {duration:0.#} С‡ В· {payableTotal:0.##} BYN{promoSuffix}";
        QuickGameBuyButton.Content = $"РћРїР»Р°С‚РёС‚СЊ {payableTotal:0.##} BYN";
        QuickGameBuyButton.Tag = tag;
        QuickGamePackageCard.Tag = tag;
    }

    private bool IsPromoApplied()
    {
        return GetAppliedPromoCode() is not null;
    }

    private decimal ApplyBookingPromo(decimal total)
    {
        var promoCode = GetAppliedPromoCode();
        return promoCode is null ? total : Math.Round(total * (1 - promoCode.BookingDiscountRate), 2);
    }

    private PromoCode? GetAppliedPromoCode()
    {
        if (string.IsNullOrWhiteSpace(_appliedPromoCode))
        {
            return null;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.PromoCodes.GetActiveByCode(_appliedPromoCode);
        }
        catch
        {
            return null;
        }
    }

    private void UpdateBalancePersonalOffer(User user)
    {
        var tier = GetClientTier(user);
        var rate = GetTierTopupBonusRate(tier);
        var promoText = IsPromoApplied()
            ? "РџСЂРѕРјРѕРєРѕРґ Р°РєС‚РёРІРµРЅ: +20% Р±РѕРЅСѓСЃРѕРІ Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РѕС‚ 50 BYN Рё в€’10% Рє РѕРїР»Р°С‚Рµ Р±СЂРѕРЅРё. РџРµСЂСЃРѕРЅР°Р»СЊРЅС‹Р№ Р±РѕРЅСѓСЃ СЃС‚Р°С‚СѓСЃР° РЅРµ СЃСѓРјРјРёСЂСѓРµС‚СЃСЏ."
            : rate > 0
                ? $"{tier}: +{rate * 100:0}% Р±РѕРЅСѓСЃРѕРІ Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РѕС‚ 50 BYN. Р•СЃР»Рё РїСЂРёРјРµРЅРёС‚СЊ РїСЂРѕРјРѕРєРѕРґ, РѕРЅ Р·Р°РјРµРЅРёС‚ СЌС‚РѕС‚ Р±РѕРЅСѓСЃ."
                : $"{tier}: Р±РѕРЅСѓСЃРѕРІ Рє РїРѕРїРѕР»РЅРµРЅРёСЋ РїРѕРєР° РЅРµС‚. Silver РѕС‚РєСЂРѕРµС‚ +5% РѕС‚ 50 BYN.";

        BalancePersonalOfferText.Text = promoText;
        BalanceOfferButton.Visibility = Visibility.Collapsed;
    }

    private decimal CalculateBookingTotal(Booking booking, Computer computer)
    {
        if (booking.TotalPrice > 0)
        {
            return booking.TotalPrice;
        }

        var duration = Math.Max(1m, (decimal)(booking.EndTime - booking.StartTime).TotalHours);
        var baseTotal = computer.HourPrice * duration;
        return Math.Round(baseTotal * GetBookingDiscountFactor(booking), 2);
    }

    private static decimal GetBookingDiscountFactor(Booking booking)
    {
        return booking.Package switch
        {
            "night" => 0.75m,
            "morning" => 0.8m,
            _ => 0.9m
        };
    }

    private static string GetBookingPackageLabel(Booking booking)
    {
        return booking.Package switch
        {
            "night" => "Night Pack -25%",
            "morning" => "Morning Pack -20%",
            _ => "Gold -10%"
        };
    }

    private void CabinetCancelBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_activeCabinetBookingId is null)
        {
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РІС‹Р±СЂР°РЅР°", "Р’ РєР°Р±РёРЅРµС‚Рµ РЅРµС‚ Р°РєС‚РёРІРЅРѕР№ Р±СЂРѕРЅРё РґР»СЏ РѕС‚РјРµРЅС‹.");
            return;
        }

        if (CancelBooking(_activeCabinetBookingId.Value))
        {
            LoadDatabaseState();
            ApplyMapPcButtonStatuses();
            RebuildBookingSeatGrid();
            RefreshAdminUx();
            ShowImportantStatus("Р‘СЂРѕРЅСЊ РѕС‚РјРµРЅРµРЅР°", "РЎС‚Р°С‚СѓСЃ Р±СЂРѕРЅРё РѕР±РЅРѕРІР»РµРЅ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….");
            return;
        }

        ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РѕС‚РјРµРЅРµРЅР°", "РќРµ СѓРґР°Р»РѕСЃСЊ РѕР±РЅРѕРІРёС‚СЊ СЃС‚Р°С‚СѓСЃ Р±СЂРѕРЅРё РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….");
    }

    private bool CancelBooking(int bookingId)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var booking = unitOfWork.Bookings.FirstOrDefault(item => item.Id == bookingId && item.UserId == _currentUserId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                return false;
            }

            booking.Status = BookingStatuses.Cancelled;

            var now = DateTime.Now;
            var hasOtherImminentBooking = unitOfWork.Bookings.HasImminentBooking(booking.ComputerId, now, booking.Id);
            var hasOpenSession = unitOfWork.GameSessions.HasOpenSession(booking.ComputerId, now);

            var computer = unitOfWork.Computers.GetById(booking.ComputerId);
            if (computer is not null && NormalizePcStatus(computer.Status) != PcStatuses.Service)
            {
                computer.Status = hasOpenSession
                    ? PcStatuses.Busy
                    : hasOtherImminentBooking
                        ? PcStatuses.Reserved
                        : PcStatuses.Free;
            }

            unitOfWork.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РѕС‚РјРµРЅС‹ Р±СЂРѕРЅРё", ex);
            return false;
        }
    }

    private void CabinetEndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeCabinetSessionId is null)
        {
            ShowStatus("РЎРµСЃСЃРёСЏ РЅРµ РІС‹Р±СЂР°РЅР°", "Р’ РєР°Р±РёРЅРµС‚Рµ РЅРµС‚ РёРЅРґРёРІРёРґСѓР°Р»СЊРЅРѕР№ СЃРµСЃСЃРёРё РґР»СЏ Р·Р°РІРµСЂС€РµРЅРёСЏ.");
            return;
        }

        if (EndCurrentClientSession(_activeCabinetSessionId.Value, out var computerName))
        {
            LoadDatabaseState();
            ApplyMapPcButtonStatuses();
            RebuildBookingSeatGrid();
            RefreshAdminUx();
            ShowImportantStatus("РЎРµСЃСЃРёСЏ Р·Р°РІРµСЂС€РµРЅР°", $"{computerName} РѕСЃРІРѕР±РѕР¶РґРµРЅ, СЃРµСЃСЃРёСЏ Р·Р°РєСЂС‹С‚Р° РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….");
            return;
        }

        ShowStatus("РЎРµСЃСЃРёСЏ РЅРµ Р·Р°РІРµСЂС€РµРЅР°", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РєСЂС‹С‚СЊ С‚РµРєСѓС‰СѓСЋ РёРЅРґРёРІРёРґСѓР°Р»СЊРЅСѓСЋ СЃРµСЃСЃРёСЋ.");
    }

    private bool EndCurrentClientSession(int sessionId, out string computerName)
    {
        computerName = "РџРљ";

        try
        {
            using var unitOfWork = new UnitOfWork();
            var now = DateTime.Now;
            var session = unitOfWork.GameSessions.FirstOrDefault(item =>
                item.Id == sessionId
                && item.UserId == _currentUserId
                && item.Status != SessionStatuses.Closed
                && item.Status != SessionStatuses.Team
                && item.StartTime <= now
                && (item.EndTime == null || item.EndTime > now));

            if (session is null)
            {
                return false;
            }

            session.EndTime = now;
            session.Status = SessionStatuses.Closed;

            var computer = unitOfWork.Computers.GetById(session.ComputerId);
            if (computer is not null)
            {
                computerName = computer.Name;
                var hasOtherOpenSession = unitOfWork.GameSessions.HasOpenSession(session.ComputerId, now, session.Id);
                var hasImminentBooking = unitOfWork.Bookings.HasImminentBooking(session.ComputerId, now);

                if (NormalizePcStatus(computer.Status) != PcStatuses.Service)
                {
                    computer.Status = hasOtherOpenSession
                        ? PcStatuses.Busy
                        : hasImminentBooking
                            ? PcStatuses.Reserved
                            : PcStatuses.Free;
                }
            }

            unitOfWork.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РІРµСЂС€РµРЅРёСЏ СЃРµСЃСЃРёРё", ex);
            return false;
        }
    }

    private static bool HasActiveIndividualSession(IUnitOfWork unitOfWork, int userId, out string computerName)
    {
        return unitOfWork.GameSessions.TryGetActiveIndividualSession(userId, out computerName);
    }

    private void RebuildCabinetSessionsGrid(IReadOnlyCollection<GameSession> sessions, IReadOnlyDictionary<int, Computer> computers)
    {
        CabinetSessionsGrid.Children.Clear();
        CabinetSessionsGrid.ColumnDefinitions.Clear();
        CabinetSessionsGrid.RowDefinitions.Clear();

        CabinetSessionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) });
        CabinetSessionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });

        var now = DateTime.Now;
        var currentSession = sessions
            .Where(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime is null || session.EndTime > now))
            .OrderBy(session => string.Equals(session.Status, SessionStatuses.Team, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(session => session.StartTime)
            .FirstOrDefault();

        if (currentSession is null)
        {
            _activeCabinetSessionId = null;
            CabinetEndSessionButton.Visibility = Visibility.Collapsed;
            AddCabinetSessionRow(0, "РЎС‚Р°С‚СѓСЃ", "РќРµС‚ С‚РµРєСѓС‰РµР№ СЃРµСЃСЃРёРё", true);
            AddCabinetSessionRow(1, "Р”РµР№СЃС‚РІРёРµ", "РћРїР»Р°С‚РёС‚Рµ Р°РєС‚РёРІРЅСѓСЋ Р±СЂРѕРЅСЊ РёР»Рё РЅР°С‡РЅРёС‚Рµ СЃРµСЃСЃРёСЋ Сѓ Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР°.", false);
            return;
        }

        computers.TryGetValue(currentSession.ComputerId, out var computer);
        var finishText = currentSession.EndTime is null ? "РѕС‚РєСЂС‹С‚Р°СЏ СЃРµСЃСЃРёСЏ" : currentSession.EndTime.Value.ToString("dd.MM HH:mm");
        var durationEnd = currentSession.EndTime ?? now;
        var duration = Math.Max(0, (durationEnd - currentSession.StartTime).TotalHours);

        AddCabinetSessionRow(0, "РЎС‚Р°С‚СѓСЃ", "РђРєС‚РёРІРЅР°", true);
        AddCabinetSessionRow(1, "РџРљ", computer?.Name ?? "-", false);
        AddCabinetSessionRow(2, "Р—РѕРЅР°", computer?.Zone ?? "-", false);
        AddCabinetSessionRow(3, "РќР°С‡Р°Р»Рѕ", currentSession.StartTime.ToString("dd.MM HH:mm"), false);
        AddCabinetSessionRow(4, "РћРєРѕРЅС‡Р°РЅРёРµ", finishText, false);
        AddCabinetSessionRow(5, "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ", $"{duration:0.#} С‡", false);
        AddCabinetSessionRow(6, "РЎСѓРјРјР°", $"{currentSession.TotalPrice:0.##} BYN", false);
        _activeCabinetSessionId = currentSession.Id;
        CabinetEndSessionButton.Visibility = string.Equals(currentSession.Status, SessionStatuses.Team, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AddCabinetSessionRow(int row, string label, string value, bool isPrimary)
    {
        CabinetSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCabinetSessionCell(row, 0, label, isPrimary, false);
        AddCabinetSessionCell(row, 1, value, isPrimary, true);
    }

    private void AddCabinetSessionCell(int row, int column, string text, bool isPrimary, bool isValue)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontWeight = isPrimary || isValue ? FontWeights.Bold : FontWeights.Normal,
            Foreground = (Brush)FindResource(isPrimary && isValue ? "GoldLightBrush" : isValue ? "TextBrush" : "MutedBrush"),
            Margin = row == 0 ? new Thickness(0) : new Thickness(0, 12, 0, 0),
            HorizontalAlignment = isValue ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            TextAlignment = isValue ? TextAlignment.Right : TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap
        };

        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, column);
        CabinetSessionsGrid.Children.Add(textBlock);
    }

    private void RefreshBalanceHistoryFromDatabase()
    {
        if (BalanceHistoryGrid is null)
        {
            return;
        }

        if (_currentUserId <= 0)
        {
            RebuildBalanceHistoryGrid(Array.Empty<Payment>());
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var payments = unitOfWork.Payments.GetRecentForUser(_currentUserId, 8);
            RebuildBalanceHistoryGrid(payments);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РёСЃС‚РѕСЂРёРё Р±Р°Р»Р°РЅСЃР°", ex);
        }
    }

    private void RebuildBalanceHistoryGrid(IReadOnlyList<Payment> payments)
    {
        if (BalanceHistoryGrid is null)
        {
            return;
        }

        BalanceHistoryGrid.Children.Clear();
        BalanceHistoryGrid.ColumnDefinitions.Clear();
        BalanceHistoryGrid.RowDefinitions.Clear();

        foreach (var width in new[] { "0.7*", "1.6*", "1.1*", "0.9*", "0.8*" })
        {
            BalanceHistoryGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = (GridLength)new GridLengthConverter().ConvertFromString(width)!
            });
        }

        BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddBalanceHistoryCell(0, 0, "Р”Р°С‚Р°", "GoldLightBrush", FontWeights.Bold);
        AddBalanceHistoryCell(0, 1, "РћРїРµСЂР°С†РёСЏ", "GoldLightBrush", FontWeights.Bold);
        AddBalanceHistoryCell(0, 2, "РњРµС‚РѕРґ", "GoldLightBrush", FontWeights.Bold);
        AddBalanceHistoryCell(0, 3, "РЎСѓРјРјР°", "GoldLightBrush", FontWeights.Bold);
        AddBalanceHistoryCell(0, 4, "РЎС‚Р°С‚СѓСЃ", "GoldLightBrush", FontWeights.Bold, alignRight: true);

        if (payments.Count == 0)
        {
            BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var emptyText = new TextBlock
            {
                Text = "РџРѕРєР° РЅРµС‚ РѕРїРµСЂР°С†РёР№ РїРѕ Р±Р°Р»Р°РЅСЃСѓ.",
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 13, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(emptyText, 1);
            Grid.SetColumn(emptyText, 0);
            Grid.SetColumnSpan(emptyText, 5);
            BalanceHistoryGrid.Children.Add(emptyText);
            return;
        }

        var visible = payments.Take(8).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var payment = visible[i];
            var rowIndex = i + 1;
            BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var status = FormatPaymentStatus(payment);
            var (amountBrush, statusBrush) = ResolveBalanceHistoryBrushes(payment, status);

            AddBalanceHistoryCell(rowIndex, 0, payment.CreatedAt.ToString("dd.MM"), "MutedBrush", FontWeights.Normal);
            AddBalanceHistoryCell(rowIndex, 1, FormatPaymentOperation(payment), "TextBrush", FontWeights.Bold);
            AddBalanceHistoryCell(rowIndex, 2, FormatPaymentMethod(payment), "MutedBrush", FontWeights.Normal);
            AddBalanceHistoryCell(rowIndex, 3, FormatPaymentAmount(payment), amountBrush, FontWeights.Bold);
            AddBalanceHistoryCell(rowIndex, 4, status, statusBrush, FontWeights.Bold, alignRight: true);
        }
    }

    private void AddBalanceHistoryCell(int row, int column, string text, string brushKey, FontWeight weight, bool alignRight = false)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource(brushKey),
            FontWeight = weight,
            Margin = new Thickness(0, 13, 0, 0),
            HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        BalanceHistoryGrid.Children.Add(block);
    }

    private static string FormatPaymentAmount(Payment payment)
    {
        if (IsDebitPayment(payment))
        {
            return $"-{Math.Abs(payment.Amount):0.##} BYN";
        }

        if (payment.Amount > 0)
        {
            return $"+{payment.Amount:0.##} BYN";
        }
        if (payment.Amount < 0)
        {
            return $"{payment.Amount:0.##} BYN";
        }
        return "0 BYN";
    }

    private static (string AmountBrush, string StatusBrush) ResolveBalanceHistoryBrushes(Payment payment, string status)
    {
        var amountBrush = string.Equals(payment.PaymentType, "Bonus", StringComparison.OrdinalIgnoreCase)
            ? "GoldLightBrush"
            : IsDebitPayment(payment)
                ? "DangerBrush"
                : "OkBrush";
        var statusBrush = status switch
        {
            "РћР¶РёРґР°РµС‚" => "WaitBrush",
            "РќР°С‡РёСЃР»РµРЅРѕ" => "GoldLightBrush",
            _ => "OkBrush"
        };
        return (amountBrush, statusBrush);
    }

    private static string FormatPaymentOperation(Payment payment)
    {
        var comment = payment.Comment ?? string.Empty;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Equals(payment.PaymentType, "Bonus", StringComparison.OrdinalIgnoreCase)
                ? "Р‘РѕРЅСѓСЃ"
                : "РћРїРµСЂР°С†РёСЏ";
        }
        if (comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "РћР¶РёРґР°РЅРёРµ РїРѕРїРѕР»РЅРµРЅРёСЏ";
        }
        if (comment.Contains("Balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "РџРѕРїРѕР»РЅРµРЅРёРµ Р±Р°Р»Р°РЅСЃР°";
        }
        if (comment.StartsWith("Package purchase", StringComparison.OrdinalIgnoreCase))
        {
            var separator = comment.IndexOf(';');
            var head = separator > 0 ? comment[..separator] : comment;
            return head.Replace("Package purchase", "РџРѕРєСѓРїРєР° РїР°РєРµС‚Р°", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Guest session", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Guest session", "Р“РѕСЃС‚РµРІР°СЏ СЃРµСЃСЃРёСЏ", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Session extension", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Session extension", "РџСЂРѕРґР»РµРЅРёРµ СЃРµСЃСЃРёРё", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Payment confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Payment confirmed", "РћРїР»Р°С‚Р° СЃРµСЃСЃРёРё", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Shift expense", StringComparison.OrdinalIgnoreCase))
        {
            return "Р Р°СЃС…РѕРґ СЃРјРµРЅС‹";
        }
        if (comment.StartsWith("Bulk payment", StringComparison.OrdinalIgnoreCase))
        {
            return "РџРѕРґС‚РІРµСЂР¶РґРµРЅРёРµ РѕС‡РµСЂРµРґРё РѕРїР»Р°С‚";
        }
        if (comment.StartsWith("Event registration", StringComparison.OrdinalIgnoreCase))
        {
            var separator = comment.IndexOf(';');
            var head = separator > 0 ? comment[..separator] : comment;
            return head.Replace("Event registration", "Р—Р°РїРёСЃСЊ РЅР° СЃРѕР±С‹С‚РёРµ", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Admin log", StringComparison.OrdinalIgnoreCase))
        {
            return "Р–СѓСЂРЅР°Р» Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР°";
        }
        return comment.Length > 60 ? comment[..60] + "вЂ¦" : comment;
    }

    private static string FormatPaymentMethod(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        return paymentType switch
        {
            "Card" => "РљР°СЂС‚Р°",
            "Cash" => "РќР°Р»РёС‡РЅС‹Рµ",
            "Online" => "РћРЅР»Р°Р№РЅ",
            "Bonus" => "Р‘РѕРЅСѓСЃС‹",
            "EventRegistration" => "РЎРѕР±С‹С‚РёРµ",
            "AdminLog" => "Р–СѓСЂРЅР°Р»",
            "PendingErip" => "Р•Р РРџ",
            "PendingCash" => "РќР°Р»РёС‡РЅС‹Рµ",
            _ when paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase) => "РћР¶РёРґР°РЅРёРµ",
            _ => paymentType
        };
    }

    private static string FormatPaymentStatus(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        if (paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "РћР¶РёРґР°РµС‚";
        }
        if (string.Equals(paymentType, "Bonus", StringComparison.OrdinalIgnoreCase))
        {
            return "РќР°С‡РёСЃР»РµРЅРѕ";
        }
        return IsDebitPayment(payment) ? "РЎРїРёСЃР°РЅРѕ" : "РЈСЃРїРµС€РЅРѕ";
    }

    private static bool IsDebitPayment(Payment payment)
    {
        return payment.Amount < 0
            || (payment.Comment ?? string.Empty).StartsWith("Package purchase", StringComparison.OrdinalIgnoreCase)
            || (payment.Comment ?? string.Empty).StartsWith("Shift expense", StringComparison.OrdinalIgnoreCase);
    }

}

