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
            "erip" => "Р•Р РРџ: Р±СѓРґРµС‚ СЃРѕР·РґР°РЅ РєРѕРґ РѕРїР»Р°С‚С‹. Р‘Р°Р»Р°РЅСЃ РїРѕРїРѕР»РЅРёС‚СЃСЏ РїРѕСЃР»Рµ РІРЅРµС€РЅРµРіРѕ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёСЏ.",
            "cash" => "РќР°Р»РёС‡РЅС‹Рµ: СЃРѕР·РґР°РµС‚СЃСЏ Р·Р°СЏРІРєР° РґР»СЏ Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР° РєР°СЃСЃС‹ Р±РµР· РјРіРЅРѕРІРµРЅРЅРѕРіРѕ Р·Р°С‡РёСЃР»РµРЅРёСЏ.",
            _ => "РљР°СЂС‚Р°: РїР»Р°С‚РµР¶ РїСЂРѕРІРµСЂСЏРµС‚СЃСЏ РїРѕ РЅРѕРјРµСЂСѓ РєР°СЂС‚С‹ Рё СЃСЂР°Р·Сѓ Р·Р°С‡РёСЃР»СЏРµС‚СЃСЏ РЅР° Р±Р°Р»Р°РЅСЃ."
        };
        ConfirmTopupButton.Content = method == "card" ? "РџРѕРїРѕР»РЅРёС‚СЊ" : "РЎРѕР·РґР°С‚СЊ Р·Р°СЏРІРєСѓ";
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
            TopupBonusText.Text = "Р’РІРµРґРёС‚Рµ СЃСѓРјРјСѓ Р±РѕР»СЊС€Рµ 0";
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        var tier = GetCurrentClientTier();
        TopupSummaryText.Text = $"{amount:0.##} BYN";
        TopupBonusText.Text = bonus > 0
            ? IsPromoApplied()
                ? $"+{bonus:0.##} Р±РѕРЅСѓСЃРѕРІ РїРѕ РїСЂРѕРјРѕРєРѕРґСѓ"
                : $"+{bonus:0.##} Р±РѕРЅСѓСЃРѕРІ РїРѕ СЃС‚Р°С‚СѓСЃСѓ {tier}"
            : "Р‘РѕРЅСѓСЃС‹ РЅР°С‡РёСЃР»СЏСЋС‚СЃСЏ РѕС‚ 50 BYN";
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
            ShowDatabaseError("РћС€РёР±РєР° РїРѕРїРѕР»РЅРµРЅРёСЏ Р±Р°Р»Р°РЅСЃР°", ex);
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
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°СЏРІРєРё РЅР° РїРѕРїРѕР»РЅРµРЅРёРµ", ex);
            return false;
        }
    }

    private void ConfirmTopup_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTopupAmount(out var amount))
        {
            TopupErrorText.Text = "Р’РІРµРґРёС‚Рµ РєРѕСЂСЂРµРєС‚РЅСѓСЋ СЃСѓРјРјСѓ РїРѕРїРѕР»РЅРµРЅРёСЏ.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (_topupMethod != "card")
        {
            var requestType = _topupMethod == "erip" ? "Р•Р РРџ" : "РЅР°Р»РёС‡РЅС‹РјРё";
            if (!SavePendingTopupRequest(amount, _topupMethod))
            {
                TopupErrorText.Text = "РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ Р·Р°СЏРІРєСѓ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
                TopupErrorText.Visibility = Visibility.Visible;
                return;
            }

            CloseTopupOverlay();
            LoadDatabaseState();
            ShowImportantStatus("Р—Р°СЏРІРєР° СЃРѕР·РґР°РЅР°", $"РџРѕРїРѕР»РЅРµРЅРёРµ {requestType} РЅР° {amount:0.##} BYN РѕР¶РёРґР°РµС‚ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёСЏ. Р‘Р°Р»Р°РЅСЃ РїРѕРєР° РЅРµ РёР·РјРµРЅРµРЅ.");
            return;
        }

        if (!IsValidPaymentCardNumber(TopupCardNumberBox.Text))
        {
            TopupErrorText.Text = "Р’РІРµРґРёС‚Рµ РєРѕСЂСЂРµРєС‚РЅС‹Р№ РЅРѕРјРµСЂ РєР°СЂС‚С‹.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        if (!SaveBalanceTopup(amount, bonus))
        {
            TopupErrorText.Text = "РќРµ СѓРґР°Р»РѕСЃСЊ РѕР±РЅРѕРІРёС‚СЊ Р±Р°Р»Р°РЅСЃ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        UpdateCurrentBalanceText();
        LoadDatabaseState();
        RefreshAdminUx();
        SyncCurrentUserViewModel();
        CloseTopupOverlay();
        ShowImportantStatus("Р‘Р°Р»Р°РЅСЃ РїРѕРїРѕР»РЅРµРЅ", $"РљР°СЂС‚РѕР№ Р·Р°С‡РёСЃР»РµРЅРѕ {amount:0.##} BYN. Р‘РѕРЅСѓСЃС‹: +{bonus:0.##}. РќРѕРІС‹Р№ Р±Р°Р»Р°РЅСЃ: {_balanceAmount:0.##} BYN.");
    }

}

