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
    private void OpenTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Visible;
        ShellRoot.Effect = new BlurEffect { Radius = 7 };
        SelectTopupMethod("card");
        UpdateTopupSummary();
        TopupAmountBox.Focus();
        TopupAmountBox.SelectAll();
    }

    private void CloseTopupOverlay_Click(object sender, RoutedEventArgs e)
    {
        CloseTopupOverlay();
    }

    private void CloseTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Collapsed;
        ShellRoot.Effect = null;
        TopupErrorText.Visibility = Visibility.Collapsed;
    }

    private void TopupMethod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string method)
        {
            return;
        }

        SelectTopupMethod(method);
    }

    private void SelectTopupMethod(string method)
    {
        _topupMethod = method;
        TopupCardMethodButton.Style = (Style)FindResource(method == "card" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupEripMethodButton.Style = (Style)FindResource(method == "erip" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupCashMethodButton.Style = (Style)FindResource(method == "cash" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupCardFields.Visibility = method == "card" ? Visibility.Visible : Visibility.Collapsed;
        TopupMethodHintText.Text = method switch
        {
            "erip" => "ЕРИП: будет создан код оплаты. Баланс пополнится после внешнего подтверждения.",
            "cash" => "Наличные: создается заявка для администратора кассы без мгновенного зачисления.",
            _ => "Карта: платеж проверяется по номеру карты и сразу зачисляется на баланс."
        };
        ConfirmTopupButton.Content = method == "card" ? "Пополнить" : "Создать заявку";
        TopupErrorText.Visibility = Visibility.Collapsed;
        UpdateTopupSummary();
    }

    private void TopupAmountPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string amount)
        {
            TopupAmountBox.Text = amount;
            UpdateTopupSummary();
        }
    }

    private void TopupAmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TopupSummaryText is null)
        {
            return;
        }

        UpdateTopupSummary();
    }

    private void UpdateTopupSummary()
    {
        if (TopupSummaryText is null)
        {
            return;
        }

        if (!TryReadTopupAmount(out var amount))
        {
            TopupSummaryText.Text = "0 BYN";
            TopupBonusText.Text = "Введите сумму больше 0";
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        var tier = GetCurrentClientTier();
        TopupSummaryText.Text = $"{amount:0.##} BYN";
        TopupBonusText.Text = bonus > 0
            ? IsPromoApplied()
                ? $"+{bonus:0.##} бонусов по промокоду"
                : $"+{bonus:0.##} бонусов по статусу {tier}"
            : "Бонусы начисляются от 50 BYN";
    }

    private bool TryReadTopupAmount(out decimal amount)
    {
        return decimal.TryParse(
            TopupAmountBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private bool SaveBalanceTopup(decimal amount, decimal bonus)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.FirstOrDefault(item => item.Id == _currentUserId);
            if (user is null)
            {
                return false;
            }

            var bonusSource = IsPromoApplied() ? $"promo {_appliedPromoCode}" : $"tier {GetClientTier(user)}";
            user.Balance += amount + bonus;
            user.LoyaltyTier = BetterTier(user.LoyaltyTier, GetClientTier(user.Balance));
            var nextPaymentId = GetNextId(dbContext.Payments, payment => payment.Id);
            dbContext.Payments.Add(new Payment
            {
                Id = nextPaymentId++,
                UserId = user.Id,
                Amount = amount,
                PaymentType = PaymentTypes.Card,
                CreatedAt = DateTime.Now,
                Comment = bonus > 0
                    ? $"Balance top-up. Bonus added to balance: {bonus:0.##} BYN via {bonusSource}"
                    : "Balance top-up"
            });

            if (bonus > 0)
            {
                dbContext.Payments.Add(new Payment
                {
                    Id = nextPaymentId,
                    UserId = user.Id,
                    Amount = bonus,
                    PaymentType = PaymentTypes.Bonus,
                    CreatedAt = DateTime.Now,
                    Comment = $"Top-up bonus from {bonusSource}: {amount:0.##} BYN"
                });
            }

            dbContext.SaveChanges();
            _balanceAmount = user.Balance;
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка пополнения баланса", ex);
            return false;
        }
    }

    private bool SavePendingTopupRequest(decimal amount, string method)
    {
        try
        {
            using var dbContext = new AppDbContext();
            if (!dbContext.Users.Any(user => user.Id == _currentUserId))
            {
                return false;
            }

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = method == "erip" ? PaymentTypes.PendingErip : PaymentTypes.PendingCash,
                CreatedAt = DateTime.Now,
                Comment = "Pending balance top-up request"
            });

            dbContext.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка заявки на пополнение", ex);
            return false;
        }
    }

    private void ConfirmTopup_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTopupAmount(out var amount))
        {
            TopupErrorText.Text = "Введите корректную сумму пополнения.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (_topupMethod != "card")
        {
            var requestType = _topupMethod == "erip" ? "ЕРИП" : "наличными";
            if (!SavePendingTopupRequest(amount, _topupMethod))
            {
                TopupErrorText.Text = "Не удалось сохранить заявку в базе данных.";
                TopupErrorText.Visibility = Visibility.Visible;
                return;
            }

            CloseTopupOverlay();
            LoadDatabaseState();
            ShowImportantStatus("Заявка создана", $"Пополнение {requestType} на {amount:0.##} BYN ожидает подтверждения. Баланс пока не изменен.");
            return;
        }

        if (!IsValidPaymentCardNumber(TopupCardNumberBox.Text))
        {
            TopupErrorText.Text = "Введите корректный номер карты.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        if (!SaveBalanceTopup(amount, bonus))
        {
            TopupErrorText.Text = "Не удалось обновить баланс в базе данных.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        BalanceAmountText.Text = $"{_balanceAmount:0.##} BYN";
        LoadDatabaseState();
        RefreshAdminUx();
        SyncCurrentUserViewModel();
        CloseTopupOverlay();
        ShowImportantStatus("Баланс пополнен", $"Картой зачислено {amount:0.##} BYN. Бонусы: +{bonus:0.##}. Новый баланс: {_balanceAmount:0.##} BYN.");
    }

}

