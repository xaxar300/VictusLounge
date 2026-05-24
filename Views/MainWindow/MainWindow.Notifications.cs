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
    private void ToggleNotificationCenter()
    {
        _isNotificationCenterOpen = !_isNotificationCenterOpen;
        NotificationCenter.Visibility = _isNotificationCenterOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MarkNotificationsRead()
    {
        _unreadNotifications = 0;
        UpdateNotificationBadge();

        foreach (var dot in FindVisualChildren<Border>(NotificationList).Where(border => Equals(border.Tag, "notification-dot")))
        {
            dot.Visibility = Visibility.Collapsed;
        }

        NotificationEmptyText.Visibility = NotificationList.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowStatus("Уведомления прочитаны", "Новых уведомлений нет, список остался как история действий.");
    }
    private void HandleShellPreviewMouseDown(object? parameter)
    {
        if (parameter is not MouseButtonEventArgs e)
        {
            return;
        }

        if (!_isNotificationCenterOpen || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsDescendantOf(source, NotificationCenter) || IsDescendantOf(source, NotificationButton))
        {
            return;
        }

        CloseNotificationCenter();
    }

    private void HandleShellPreviewKeyDown(object? parameter)
    {
        if (parameter is not KeyEventArgs e)
        {
            return;
        }

        if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            ShowStatus("Командная палитра", "Команды: бронь, карта, админ, смена, настройки, светлая тема, компактный, PC-04.");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && GlobalSearchBox.IsKeyboardFocusWithin)
        {
            ExecuteGlobalSearch(_viewModel.Shell.SearchQuery);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && TopupOverlay.Visibility == Visibility.Visible)
        {
            CloseTopupOverlay();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape || !_isNotificationCenterOpen)
        {
            return;
        }

        CloseNotificationCenter();
        e.Handled = true;
    }

    private void CloseNotificationCenter()
    {
        _isNotificationCenterOpen = false;
        NotificationCenter.Visibility = Visibility.Collapsed;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject parent)
    {
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
    private void ExecuteGlobalSearch(string query)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (query.Length < 2)
        {
            return;
        }

        if (TryExecuteCommandPalette(query))
        {
            return;
        }

        var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
        var result = string.Empty;
        try
        {
            using var unitOfWork = new UnitOfWork();
            var now = DateTime.Now;
            var normalizedQuery = query.ToLowerInvariant();
            var matchingUsers = unitOfWork.Users
                .QueryNoTracking()
                .Count(user => user.FullName.ToLower().Contains(normalizedQuery)
                    || user.Login.ToLower().Contains(normalizedQuery));
            var matchingComputers = unitOfWork.Computers
                .QueryNoTracking()
                .Count(computer => computer.Name.ToLower().Contains(normalizedQuery)
                    || computer.Zone.ToLower().Contains(normalizedQuery));
            var activeBookings = unitOfWork.Bookings.Count(booking =>
                booking.Status != BookingStatuses.Cancelled
                && booking.EndTime > now);
            var activeSessions = unitOfWork.GameSessions.Count(session =>
                session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now));
            var pendingPayments = unitOfWork.Bookings.Count(booking =>
                    booking.Status == BookingStatuses.PendingPayment
                    && booking.EndTime > now)
                + unitOfWork.GameSessions.Count(session =>
                    session.Status == SessionStatuses.AwaitingPayment
                    && (session.EndTime == null || session.EndTime > now));

            result = normalizedQuery switch
            {
                var text when text.Contains("pc") || text.Contains("пк") || text.Contains("vip") || matchingComputers > 0 =>
                    $"Найдено ПК/зон: {matchingComputers}. Свободных ПК сейчас: {freePcs}.",
                var text when text.Contains("брон") || text.Contains("booking") =>
                    $"Активных будущих броней: {activeBookings}. Ожидают оплату: {pendingPayments}.",
                var text when text.Contains("сесс") || text.Contains("session") =>
                    $"Активных сессий сейчас: {activeSessions}. Ожидают оплату: {pendingPayments}.",
                var text when text.Contains("клиент") || text.Contains("client") || matchingUsers > 0 =>
                    $"Найдено клиентов: {matchingUsers}. Активных сессий сейчас: {activeSessions}.",
                var text when text.Contains("плат") || text.Contains("баланс") || text.Contains("payment") =>
                    $"Ожидают оплату: {pendingPayments}. Активных броней: {activeBookings}.",
                _ => $"Найдено клиентов: {matchingUsers}, ПК/зон: {matchingComputers}, активных броней: {activeBookings}, сессий: {activeSessions}."
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search database lookup failed: {ex}");
            result = $"Поиск по локальному состоянию: свободных ПК {freePcs}, очередь оплат {_adminPaymentQueue}.";
        }

        ShowStatus("Результат поиска", result);
    }

    private bool TryExecuteCommandPalette(string query)
    {
        var command = query.Trim().ToLowerInvariant();
        var view = command switch
        {
            "dashboard" or "main" or "home" or "главная" => "dashboard",
            "map" or "карта" or "схема" => "map",
            "booking" or "бронь" or "бронирование" => "booking",
            "cabinet" or "кабинет" => "cabinet",
            "balance" or "баланс" => "balance",
            "events" or "события" => "events",
            "admin" or "админ" => "admin",
            "shift" or "смена" => "shift",
            "owner" or "владелец" => "owner",
            "settings" or "настройки" => "settings",
            _ => null
        };

        if (view is not null)
        {
            NavigateTo(view);
            ShowStatus("Команда выполнена", $"Открыт раздел: {_viewModel.Navigation.CurrentTitle}.");
            return true;
        }

        switch (command)
        {
            case "light":
            case "светлая":
            case "светлая тема":
                ApplyTheme("Light");
                return true;
            case "dark":
            case "темная":
            case "темная тема":
                ApplyTheme("BlackGold");
                return true;
            case "graphite":
            case "графит":
                ApplyTheme("Graphite");
                return true;
            case "compact":
            case "компактный":
                ApplyInterfaceSize("compact");
                return true;
            case "normal":
            case "обычный":
                ApplyInterfaceSize("normal");
                return true;
            case "large":
            case "крупный":
                ApplyInterfaceSize("large");
                return true;
            case "ru":
            case "русский":
                ApplyLanguage("ru");
                return true;
            case "en":
            case "english":
                ApplyLanguage("en");
                return true;
        }

        var computer = _computers.FirstOrDefault(item => item.Name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase));
        if (computer is null)
        {
            return false;
        }

        NavigateTo("map");
        SelectMapPc($"{computer.Name}|{computer.Zone}|{computer.Status}");
        ShowStatus("ПК найден", $"{computer.Name}: {computer.Zone}, {GetStatusText(computer.Status, true)}.");
        return true;
    }

    private void ShowStatus(string title, string body)
    {
        ShowToast(title, body);
    }

    private void ShowImportantStatus(string title, string body)
    {
        ShowToast(title, body);
        AddNotification(title, body);
    }

    private void ShowToast(string title, string body)
    {
        StatusTitleText.Text = title;
        StatusBodyText.Text = body;
        StatusToast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ShowUndoSnackbar(string title, string body, Action undoAction)
    {
        _pendingUndoAction = undoAction;
        StatusToast.Visibility = Visibility.Collapsed;
        _toastTimer.Stop();
        UndoSnackbarTitleText.Text = title;
        UndoSnackbarBodyText.Text = body;
        UndoSnackbar.Visibility = Visibility.Visible;
        _undoSnackbarTimer.Stop();
        _undoSnackbarTimer.Start();
    }

    private void ExecutePendingUndo()
    {
        var undoAction = _pendingUndoAction;
        if (undoAction is null)
        {
            HideUndoSnackbar();
            return;
        }

        HideUndoSnackbar();
        undoAction();
    }

    private void HideUndoSnackbar()
    {
        _pendingUndoAction = null;
        _undoSnackbarTimer.Stop();
        UndoSnackbar.Visibility = Visibility.Collapsed;
    }

    private void AddNotification(string title, string body)
    {
        if (!IsLoaded)
        {
            return;
        }

        NotificationEmptyText.Visibility = Visibility.Collapsed;

        var dot = new Border
        {
            Tag = "notification-dot",
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)FindResource("GoldLightBrush"),
            Margin = new Thickness(0, 7, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        var content = new StackPanel { Margin = new Thickness(0) };
        content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = (Brush)FindResource("GoldLightBrush"),
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = body,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(content, 1);
        grid.Children.Add(dot);
        grid.Children.Add(content);

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xD4, 0xAF, 0x37)),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 9),
            Child = grid
        };

        NotificationList.Children.Insert(0, card);
        while (NotificationList.Children.Count > 12)
        {
            NotificationList.Children.RemoveAt(NotificationList.Children.Count - 1);
        }

        _unreadNotifications = Math.Min(99, _unreadNotifications + 1);
        UpdateNotificationBadge();
    }

    private void UpdateNotificationBadge()
    {
        NotificationCountText.Text = _unreadNotifications.ToString();
        NotificationBadge.Visibility = _unreadNotifications > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartAnnouncementMarquee()
    {
        AnnouncementTextA.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        AnnouncementTextB.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ResetAnnouncementMarquee();
        _lastAnnouncementTick = DateTime.Now;
        _announcementTimer.Start();
    }

    private void ResetAnnouncementMarquee()
    {
        var textWidth = GetAnnouncementTextWidth();
        Canvas.SetLeft(AnnouncementTextA, 0);
        Canvas.SetLeft(AnnouncementTextB, textWidth + AnnouncementGap);
    }

    private void AnnouncementTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var elapsedSeconds = (now - _lastAnnouncementTick).TotalSeconds;
        _lastAnnouncementTick = now;

        var distance = AnnouncementSpeed * elapsedSeconds;
        MoveAnnouncementText(AnnouncementTextA, distance);
        MoveAnnouncementText(AnnouncementTextB, distance);
    }

    private void MoveAnnouncementText(TextBlock textBlock, double distance)
    {
        var textWidth = GetAnnouncementTextWidth();
        var left = Canvas.GetLeft(textBlock);
        if (double.IsNaN(left))
        {
            left = 0;
        }

        left -= distance;
        if (left <= -(textWidth + AnnouncementGap))
        {
            var otherLeft = ReferenceEquals(textBlock, AnnouncementTextA)
                ? Canvas.GetLeft(AnnouncementTextB)
                : Canvas.GetLeft(AnnouncementTextA);
            left = otherLeft + textWidth + AnnouncementGap;
        }

        Canvas.SetLeft(textBlock, left);
    }

    private double GetAnnouncementTextWidth()
    {
        AnnouncementTextA.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(AnnouncementTextA.DesiredSize.Width, 1);
    }

}

