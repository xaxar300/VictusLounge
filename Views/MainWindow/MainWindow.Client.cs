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
using VictusLounge.ViewModels;

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
        CabinetTierText.Text = $"{GetClientTier(user)} · {user.Login}";
        CabinetProgressText.Text = $"{progress}% · бонусов: {bonus:0.##}";
        CabinetBalanceText.Text = $"{user.Balance:0.##} BYN";
        CabinetBonusText.Text = $"{bonus:0.##}";
        _viewModel.Balance.BonusAmount = bonus;
        CabinetPlayedText.Text = $"{playedHours:0.#} ч";
        CabinetFavoriteZoneText.Text = favoriteZone;
        _balanceAmount = user.Balance;
        UpdateCurrentBalanceText();
        UpdateBalancePersonalOffer(user);

        if (activeBooking is not null && computers.TryGetValue(activeBooking.ComputerId, out var bookingComputer))
        {
            var price = CalculateBookingTotal(activeBooking, bookingComputer);
            var label = GetBookingPackageLabel(activeBooking);
            var payablePrice = ApplyBookingPromo(price);
            var promoSuffix = payablePrice < price ? $" · промокод -{price - payablePrice:0.##} BYN" : string.Empty;
            CabinetActiveBookingText.Text = $"{bookingComputer.Name} · {activeBooking.StartTime:dd.MM HH:mm}–{activeBooking.EndTime:HH:mm}";
            CabinetActiveBookingPriceText.Text = $"{payablePrice:0.##} BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Visible;
            _activeCabinetBookingId = activeBooking.Id;
            CabinetBookingCardPcText.Text = bookingComputer.Name;
            CabinetBookingCardTimeText.Text = $"{activeBooking.StartTime:dd.MM HH:mm}–{activeBooking.EndTime:HH:mm}";
            CabinetBookingCardPriceText.Text = $"{bookingComputer.Zone} · {label} · {payablePrice:0.##} BYN{promoSuffix}";
            UpdateBalanceBookingOffer(activeBooking, bookingComputer, price, label);
        }
        else
        {
            CabinetActiveBookingText.Text = "Нет активной брони";
            CabinetActiveBookingPriceText.Text = "0 BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Collapsed;
            _activeCabinetBookingId = null;
            CabinetBookingCardPcText.Text = "Нет брони";
            CabinetBookingCardTimeText.Text = string.Empty;
            CabinetBookingCardPriceText.Text = string.Empty;
            UpdateBalanceBookingOffer(null, null, 0m, string.Empty);
        }

        RebuildCabinetSessionsGrid(userSessions, computers);
        RebuildBalanceHistoryGrid(userPayments);
    }

    private void UpdateBalanceBookingOffer(Booking? booking, Computer? computer, decimal total, string packageLabel)
    {
        if (booking is null || computer is null)
        {
            _viewModel.Balance.ShowBookingRequiredOffer();
            return;
        }

        var duration = Math.Max(1, (booking.EndTime - booking.StartTime).TotalHours);
        var payableTotal = ApplyBookingPromo(total);
        var promoSuffix = payableTotal < total ? $" · промокод -{total - payableTotal:0.##} BYN" : string.Empty;
        var tag = $"{packageLabel}|{payableTotal:0.##} BYN";
        _viewModel.Balance.ShowActiveBookingOffer(
            "Оплата активной брони",
            $"{computer.Name} · {computer.Zone}",
            $"{packageLabel} · {duration:0.#} ч · {payableTotal:0.##} BYN{promoSuffix}",
            $"Оплатить {payableTotal:0.##} BYN",
            tag);
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
            ? "Промокод активен: +20% бонусов к пополнению от 50 BYN и −10% к оплате брони. Персональный бонус статуса не суммируется."
            : rate > 0
                ? $"{tier}: +{rate * 100:0}% бонусов к пополнению от 50 BYN. Если применить промокод, он заменит этот бонус."
                : $"{tier}: бонусов к пополнению пока нет. Silver откроет +5% от 50 BYN.";

        _viewModel.Balance.PersonalOfferText = promoText;
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

    private void CancelCabinetBooking()
    {
        if (_activeCabinetBookingId is null)
        {
            ShowStatus("Бронь не выбрана", "В кабинете нет активной брони для отмены.");
            return;
        }

        if (CancelBooking(_activeCabinetBookingId.Value))
        {
            LoadDatabaseState();
            ApplyMapPcButtonStatuses();
            RebuildBookingSeatGrid();
            RefreshAdminUx();
            ShowImportantStatus("Бронь отменена", "Статус брони обновлен в базе данных.");
            return;
        }

        ShowStatus("Бронь не отменена", "Не удалось обновить статус брони в базе данных.");
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
            ShowDatabaseError("Ошибка отмены брони", ex);
            return false;
        }
    }

    private void EndCabinetSession()
    {
        if (_activeCabinetSessionId is null)
        {
            ShowStatus("Сессия не выбрана", "В кабинете нет индивидуальной сессии для завершения.");
            return;
        }

        if (EndCurrentClientSession(_activeCabinetSessionId.Value, out var computerName))
        {
            LoadDatabaseState();
            ApplyMapPcButtonStatuses();
            RebuildBookingSeatGrid();
            RefreshAdminUx();
            ShowImportantStatus("Сессия завершена", $"{computerName} освобожден, сессия закрыта в базе данных.");
            return;
        }

        ShowStatus("Сессия не завершена", "Не удалось закрыть текущую индивидуальную сессию.");
    }

    private bool EndCurrentClientSession(int sessionId, out string computerName)
    {
        computerName = "ПК";

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
            ShowDatabaseError("Ошибка завершения сессии", ex);
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
            AddCabinetSessionRow(0, "Статус", "Нет текущей сессии", true);
            AddCabinetSessionRow(1, "Действие", "Оплатите активную бронь или начните сессию у администратора.", false);
            return;
        }

        computers.TryGetValue(currentSession.ComputerId, out var computer);
        var finishText = currentSession.EndTime is null ? "открытая сессия" : currentSession.EndTime.Value.ToString("dd.MM HH:mm");
        var durationEnd = currentSession.EndTime ?? now;
        var duration = Math.Max(0, (durationEnd - currentSession.StartTime).TotalHours);

        AddCabinetSessionRow(0, "Статус", "Активна", true);
        AddCabinetSessionRow(1, "ПК", computer?.Name ?? "-", false);
        AddCabinetSessionRow(2, "Зона", computer?.Zone ?? "-", false);
        AddCabinetSessionRow(3, "Начало", currentSession.StartTime.ToString("dd.MM HH:mm"), false);
        AddCabinetSessionRow(4, "Окончание", finishText, false);
        AddCabinetSessionRow(5, "Длительность", $"{duration:0.#} ч", false);
        AddCabinetSessionRow(6, "Сумма", $"{currentSession.TotalPrice:0.##} BYN", false);
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
            ShowDatabaseError("Ошибка истории баланса", ex);
        }
    }

    private void RebuildBalanceHistoryGrid(IReadOnlyList<Payment> payments)
    {
        if (payments.Count == 0)
        {
            _viewModel.Balance.SetHistory(new[]
            {
                new BalanceHistoryItemViewModel
                {
                    Date = string.Empty,
                    Operation = "Пока нет операций по балансу.",
                    Method = string.Empty,
                    Amount = string.Empty,
                    Status = string.Empty,
                    AmountBrush = (Brush)FindResource("MutedBrush"),
                    StatusBrush = (Brush)FindResource("MutedBrush")
                }
            });
            return;
        }

        var rows = payments.Take(8).Select(payment =>
        {
            var status = FormatPaymentStatus(payment);
            var (amountBrush, statusBrush) = ResolveBalanceHistoryBrushes(payment, status);
            return new BalanceHistoryItemViewModel
            {
                Date = payment.CreatedAt.ToString("dd.MM"),
                Operation = FormatPaymentOperation(payment),
                Method = FormatPaymentMethod(payment),
                Amount = FormatPaymentAmount(payment),
                Status = status,
                AmountBrush = (Brush)FindResource(amountBrush),
                StatusBrush = (Brush)FindResource(statusBrush)
            };
        }).ToList();
        _viewModel.Balance.SetHistory(rows);
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
            "Ожидает" => "WaitBrush",
            "Начислено" => "GoldLightBrush",
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
                ? "Бонус"
                : "Операция";
        }
        if (comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "Ожидание пополнения";
        }
        if (comment.Contains("Balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "Пополнение баланса";
        }
        if (comment.StartsWith("Package purchase", StringComparison.OrdinalIgnoreCase))
        {
            var separator = comment.IndexOf(';');
            var head = separator > 0 ? comment[..separator] : comment;
            return head.Replace("Package purchase", "Покупка пакета", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Guest session", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Guest session", "Гостевая сессия", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Session extension", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Session extension", "Продление сессии", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Payment confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Payment confirmed", "Оплата сессии", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Shift expense", StringComparison.OrdinalIgnoreCase))
        {
            return "Расход смены";
        }
        if (comment.StartsWith("Bulk payment", StringComparison.OrdinalIgnoreCase))
        {
            return "Подтверждение очереди оплат";
        }
        if (comment.StartsWith("Event registration", StringComparison.OrdinalIgnoreCase))
        {
            var separator = comment.IndexOf(';');
            var head = separator > 0 ? comment[..separator] : comment;
            return head.Replace("Event registration", "Запись на событие", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Admin log", StringComparison.OrdinalIgnoreCase))
        {
            return "Журнал администратора";
        }
        return comment.Length > 60 ? comment[..60] + "…" : comment;
    }

    private static string FormatPaymentMethod(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        return paymentType switch
        {
            "Card" => "Карта",
            "Cash" => "Наличные",
            "Online" => "Онлайн",
            "Bonus" => "Бонусы",
            "EventRegistration" => "Событие",
            "AdminLog" => "Журнал",
            "PendingErip" => "ЕРИП",
            "PendingCash" => "Наличные",
            _ when paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase) => "Ожидание",
            _ => paymentType
        };
    }

    private static string FormatPaymentStatus(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        if (paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "Ожидает";
        }
        if (string.Equals(paymentType, "Bonus", StringComparison.OrdinalIgnoreCase))
        {
            return "Начислено";
        }
        return IsDebitPayment(payment) ? "Списано" : "Успешно";
    }

    private static bool IsDebitPayment(Payment payment)
    {
        return payment.Amount < 0
            || (payment.Comment ?? string.Empty).StartsWith("Package purchase", StringComparison.OrdinalIgnoreCase)
            || (payment.Comment ?? string.Empty).StartsWith("Shift expense", StringComparison.OrdinalIgnoreCase);
    }

}

