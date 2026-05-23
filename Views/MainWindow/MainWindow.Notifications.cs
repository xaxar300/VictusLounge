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
    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        _isNotificationCenterOpen = !_isNotificationCenterOpen;
        NotificationCenter.Visibility = _isNotificationCenterOpen ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void MarkNotifications_Click(object sender, RoutedEventArgs e)
    {
        _unreadNotifications = 0;
        UpdateNotificationBadge();

        foreach (var dot in FindVisualChildren<Border>(NotificationList).Where(border => Equals(border.Tag, "notification-dot")))
        {
            dot.Visibility = Visibility.Collapsed;
        }

        NotificationEmptyText.Visibility = NotificationList.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowStatus("РЈРІРµРґРѕРјР»РµРЅРёСЏ РїСЂРѕС‡РёС‚Р°РЅС‹", "РќРѕРІС‹С… СѓРІРµРґРѕРјР»РµРЅРёР№ РЅРµС‚, СЃРїРёСЃРѕРє РѕСЃС‚Р°Р»СЃСЏ РєР°Рє РёСЃС‚РѕСЂРёСЏ РґРµР№СЃС‚РІРёР№.");
        e.Handled = true;
    }
    private void RootGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
    private void GlobalSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (GlobalSearchBox.Text != SearchPlaceholder)
        {
            return;
        }

        GlobalSearchBox.Text = string.Empty;
        GlobalSearchBox.Foreground = (Brush)FindResource("TextBrush");
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || GlobalSearchBox.Text == SearchPlaceholder)
        {
            return;
        }

        var query = GlobalSearchBox.Text.Trim();
        if (query.Length < 2)
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
                var text when text.Contains("pc") || text.Contains("РїРє") || text.Contains("vip") || matchingComputers > 0 =>
                    $"РќР°Р№РґРµРЅРѕ РџРљ/Р·РѕРЅ: {matchingComputers}. РЎРІРѕР±РѕРґРЅС‹С… РџРљ СЃРµР№С‡Р°СЃ: {freePcs}.",
                var text when text.Contains("Р±СЂРѕРЅ") || text.Contains("booking") =>
                    $"РђРєС‚РёРІРЅС‹С… Р±СѓРґСѓС‰РёС… Р±СЂРѕРЅРµР№: {activeBookings}. РћР¶РёРґР°СЋС‚ РѕРїР»Р°С‚Сѓ: {pendingPayments}.",
                var text when text.Contains("СЃРµСЃСЃ") || text.Contains("session") =>
                    $"РђРєС‚РёРІРЅС‹С… СЃРµСЃСЃРёР№ СЃРµР№С‡Р°СЃ: {activeSessions}. РћР¶РёРґР°СЋС‚ РѕРїР»Р°С‚Сѓ: {pendingPayments}.",
                var text when text.Contains("РєР»РёРµРЅС‚") || text.Contains("client") || matchingUsers > 0 =>
                    $"РќР°Р№РґРµРЅРѕ РєР»РёРµРЅС‚РѕРІ: {matchingUsers}. РђРєС‚РёРІРЅС‹С… СЃРµСЃСЃРёР№ СЃРµР№С‡Р°СЃ: {activeSessions}.",
                var text when text.Contains("РїР»Р°С‚") || text.Contains("Р±Р°Р»Р°РЅСЃ") || text.Contains("payment") =>
                    $"РћР¶РёРґР°СЋС‚ РѕРїР»Р°С‚Сѓ: {pendingPayments}. РђРєС‚РёРІРЅС‹С… Р±СЂРѕРЅРµР№: {activeBookings}.",
                _ => $"РќР°Р№РґРµРЅРѕ РєР»РёРµРЅС‚РѕРІ: {matchingUsers}, РџРљ/Р·РѕРЅ: {matchingComputers}, Р°РєС‚РёРІРЅС‹С… Р±СЂРѕРЅРµР№: {activeBookings}, СЃРµСЃСЃРёР№: {activeSessions}."
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search database lookup failed: {ex}");
            result = $"РџРѕРёСЃРє РїРѕ Р»РѕРєР°Р»СЊРЅРѕРјСѓ СЃРѕСЃС‚РѕСЏРЅРёСЋ: СЃРІРѕР±РѕРґРЅС‹С… РџРљ {freePcs}, РѕС‡РµСЂРµРґСЊ РѕРїР»Р°С‚ {_adminPaymentQueue}.";
        }

        ShowStatus("Р РµР·СѓР»СЊС‚Р°С‚ РїРѕРёСЃРєР°", result);
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

