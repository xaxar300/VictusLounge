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
using VictusLounge.Services.Facades;

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
        SetChoiceButtonStyles(method,
            ("card", TopupCardMethodButton),
            ("erip", TopupEripMethodButton),
            ("cash", TopupCashMethodButton));
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
        var summary = _topupFacade.BuildSummary(new TopupSummaryRequest(
            _currentUserId,
            _balanceAmount,
            _viewModel.Topup.AmountText,
            _appliedPromoCode));
        _viewModel.Topup.SummaryText = summary.SummaryText;
        _viewModel.Topup.BonusText = summary.BonusText;
    }

    private void ConfirmTopup()
    {
        var result = _topupFacade.ConfirmTopup(new TopupFacadeRequest(
            _currentUserId,
            _viewModel.Topup.AmountText,
            _topupMethod,
            _viewModel.Topup.CardNumber,
            _appliedPromoCode));

        if (!result.Success)
        {
            if (result.Exception is not null)
            {
                ShowDatabaseError("Ошибка пополнения баланса", result.Exception);
            }

            _viewModel.Topup.ShowError(result.ErrorMessage ?? "Не удалось выполнить пополнение.");
            return;
        }

        if (result.Operation is TopupOperation.Erip or TopupOperation.Cash)
        {
            var requestType = result.Operation == TopupOperation.Erip ? "ЕРИП" : "наличными";
            CloseTopupOverlay();
            LoadDatabaseState();
            ShowImportantStatus("Заявка создана", $"Пополнение {requestType} на {result.Amount:0.##} BYN ожидает подтверждения. Баланс пока не изменен.");
            return;
        }

        _balanceAmount = result.NewBalance ?? _balanceAmount;
        UpdateCurrentBalanceText();
        LoadDatabaseState();
        RefreshAdminUx();
        SyncCurrentUserViewModel();
        CloseTopupOverlay();
        ShowImportantStatus("Баланс пополнен", $"Картой зачислено {result.Amount:0.##} BYN. Бонусы: +{result.Bonus:0.##}. Новый баланс: {_balanceAmount:0.##} BYN.");
    }

}

