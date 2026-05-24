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
    private void ConfigureTopupCommands()
    {
        _viewModel.Topup.ConfigureActions(
            CloseTopupOverlay,
            ConfirmTopup,
            SelectTopupMethod,
            SelectTopupAmountPreset,
            UpdateTopupSummary);
    }

    private void OpenTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Visible;
        ShellRoot.Effect = new BlurEffect { Radius = 7 };
        SelectTopupMethod("card");
        UpdateTopupSummary();
        TopupAmountBox.Focus();
        TopupAmountBox.SelectAll();
    }

    private void CloseTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Collapsed;
        ShellRoot.Effect = null;
        _viewModel.Topup.ClearError();
    }

    private void SelectTopupMethod(string method)
    {
        _topupMethod = method;
        _viewModel.Topup.Method = method;
        TopupCardMethodButton.Style = (Style)FindResource(method == "card" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupEripMethodButton.Style = (Style)FindResource(method == "erip" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupCashMethodButton.Style = (Style)FindResource(method == "cash" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        _viewModel.Topup.MethodHint = method switch
        {
            "erip" => "ЕРИП: будет создан код оплаты. Баланс пополнится после внешнего подтверждения.",
            "cash" => "Наличные: создается заявка для администратора кассы без мгновенного зачисления.",
            _ => "Карта: платеж проверяется по номеру карты и сразу зачисляется на баланс."
        };
        _viewModel.Topup.ConfirmText = method == "card" ? "Пополнить" : "Создать заявку";
        _viewModel.Topup.ClearError();
        UpdateTopupSummary();
    }

    private void SelectTopupAmountPreset(string amount)
    {
        _viewModel.Topup.AmountText = amount;
        UpdateTopupSummary();
    }

    private void UpdateTopupSummary()
    {
        if (!TryReadTopupAmount(out var amount))
        {
            _viewModel.Topup.SummaryText = "0 BYN";
            _viewModel.Topup.BonusText = "Введите сумму больше 0";
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        var tier = GetCurrentClientTier();
        _viewModel.Topup.SummaryText = $"{amount:0.##} BYN";
        _viewModel.Topup.BonusText = bonus > 0
            ? IsPromoApplied()
                ? $"+{bonus:0.##} бонусов по промокоду"
                : $"+{bonus:0.##} бонусов по статусу {tier}"
            : "Бонусы начисляются от 50 BYN";
    }

    private bool TryReadTopupAmount(out decimal amount)
    {
        return decimal.TryParse(
            _viewModel.Topup.AmountText.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private bool SaveBalanceTopup(decimal amount, decimal bonus)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.FirstOrDefault(item => item.Id == _currentUserId);
            if (user is null)
            {
                return false;
            }

            var bonusSource = IsPromoApplied() ? $"promo {_appliedPromoCode}" : $"tier {GetClientTier(user)}";
            user.Balance += amount + bonus;
            user.LoyaltyTier = BetterTier(user.LoyaltyTier, GetClientTier(user.Balance));
            var nextPaymentId = unitOfWork.Payments.GetNextId(payment => payment.Id);
            unitOfWork.Payments.Add(new Payment
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
                unitOfWork.Payments.Add(new Payment
                {
                    Id = nextPaymentId,
                    UserId = user.Id,
                    Amount = bonus,
                    PaymentType = PaymentTypes.Bonus,
                    CreatedAt = DateTime.Now,
                    Comment = $"Top-up bonus from {bonusSource}: {amount:0.##} BYN"
                });
            }

            unitOfWork.SaveChanges();
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
            using var unitOfWork = new UnitOfWork();
            if (!unitOfWork.Users.Any(user => user.Id == _currentUserId))
            {
                return false;
            }

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = method == "erip" ? PaymentTypes.PendingErip : PaymentTypes.PendingCash,
                CreatedAt = DateTime.Now,
                Comment = "Pending balance top-up request"
            });

            unitOfWork.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка заявки на пополнение", ex);
            return false;
        }
    }

    private void ConfirmTopup()
    {
        if (!TryReadTopupAmount(out var amount))
        {
            _viewModel.Topup.ShowError("Введите корректную сумму пополнения.");
            return;
        }

        if (_topupMethod != "card")
        {
            var requestType = _topupMethod == "erip" ? "ЕРИП" : "наличными";
            if (!SavePendingTopupRequest(amount, _topupMethod))
            {
                _viewModel.Topup.ShowError("Не удалось сохранить заявку в базе данных.");
                return;
            }

            CloseTopupOverlay();
            LoadDatabaseState();
            ShowImportantStatus("Заявка создана", $"Пополнение {requestType} на {amount:0.##} BYN ожидает подтверждения. Баланс пока не изменен.");
            return;
        }

        if (!IsValidPaymentCardNumber(_viewModel.Topup.CardNumber))
        {
            _viewModel.Topup.ShowError("Введите корректный номер карты.");
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        if (!SaveBalanceTopup(amount, bonus))
        {
            _viewModel.Topup.ShowError("Не удалось обновить баланс в базе данных.");
            return;
        }

        UpdateCurrentBalanceText();
        LoadDatabaseState();
        RefreshAdminUx();
        SyncCurrentUserViewModel();
        CloseTopupOverlay();
        ShowImportantStatus("Баланс пополнен", $"Картой зачислено {amount:0.##} BYN. Бонусы: +{bonus:0.##}. Новый баланс: {_balanceAmount:0.##} BYN.");
    }

}

